package com.dezigwer.nothingmusic

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Color
import android.net.wifi.WifiManager
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.os.PowerManager
import android.support.v4.media.MediaMetadataCompat
import android.support.v4.media.session.MediaSessionCompat
import android.support.v4.media.session.PlaybackStateCompat
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.core.graphics.drawable.toBitmap
import java.net.HttpURLConnection
import java.net.URL
import java.util.concurrent.Executors

/**
 * Foreground-сервис: не даёт системе усыпить процесс, держит медиа-сессию
 * и уведомление с кнопками. Сам звук идёт из WebView.
 */
class PlaybackService : Service() {

    companion object {
        private const val CHANNEL_ID = "playback"
        private const val NOTIFICATION_ID = 1
        private const val ACTION_TOGGLE = "com.dezigwer.nothingmusic.TOGGLE"
        private const val ACTION_PREV = "com.dezigwer.nothingmusic.PREV"
        private const val ACTION_NEXT = "com.dezigwer.nothingmusic.NEXT"
        private const val ACTION_STOP = "com.dezigwer.nothingmusic.STOP"

        private const val ACTIONS = PlaybackStateCompat.ACTION_PLAY or
            PlaybackStateCompat.ACTION_PAUSE or
            PlaybackStateCompat.ACTION_PLAY_PAUSE or
            PlaybackStateCompat.ACTION_SKIP_TO_NEXT or
            PlaybackStateCompat.ACTION_SKIP_TO_PREVIOUS or
            PlaybackStateCompat.ACTION_SEEK_TO or
            PlaybackStateCompat.ACTION_STOP

        fun start(context: Context) {
            ContextCompat.startForegroundService(
                context,
                Intent(context, PlaybackService::class.java)
            )
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, PlaybackService::class.java))
        }
    }

    private lateinit var session: MediaSessionCompat
    private val handler = Handler(Looper.getMainLooper())
    private val artExecutor = Executors.newSingleThreadExecutor()

    private var playing = false
    private var title: String? = null
    private var artist: String? = null
    private var positionMs = 0L
    private var durationMs = 0L
    private var artworkUrl: String? = null
    private var artwork: Bitmap? = null
    private var fingerprint: String? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var wifiLock: WifiManager.WifiLock? = null
    private var destroyed = false

    private val poll = object : Runnable {
        override fun run() {
            WebEngine.state { state -> if (state != null) applyState(state) }
            WebEngine.pushSettings()
            handler.postDelayed(this, 1_000L)
        }
    }

    override fun onCreate() {
        super.onCreate()
        createChannel()

        session = MediaSessionCompat(this, "NothingMusic").apply {
            setCallback(object : MediaSessionCompat.Callback() {
                override fun onPlay() = WebEngine.player("play")
                override fun onPause() = WebEngine.player("pause")
                override fun onSkipToNext() = WebEngine.player("next")
                override fun onSkipToPrevious() = WebEngine.player("prev")
                override fun onSeekTo(pos: Long) = WebEngine.seek(pos / 1000.0)
                override fun onStop() = stopSelf()
            })
            isActive = true
        }

        startForegroundCompat(buildNotification())
        handler.post(poll)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_TOGGLE -> WebEngine.player(if (playing) "pause" else "play")
            ACTION_PREV -> WebEngine.player("prev")
            ACTION_NEXT -> WebEngine.player("next")
            ACTION_STOP -> {
                stopSelf()
                return START_NOT_STICKY
            }
        }
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onTaskRemoved(rootIntent: Intent?) {
        // Если музыка на паузе — незачем висеть в памяти после свайпа из недавних.
        if (!playing) stopSelf()
        super.onTaskRemoved(rootIntent)
    }

    override fun onDestroy() {
        destroyed = true
        handler.removeCallbacks(poll)
        updateWakeLock(false)
        artExecutor.shutdownNow()
        session.isActive = false
        session.release()
        if (Build.VERSION.SDK_INT >= 24) stopForeground(STOP_FOREGROUND_REMOVE)
        super.onDestroy()
    }

    /* ---------- состояние ---------- */

    private fun applyState(state: PlayerState) {
        playing = state.playing
        title = state.title.ifBlank { "NothingMusic" }
        artist = state.artist
        positionMs = (state.position * 1000).toLong()
        durationMs = (state.duration * 1000).toLong()

        updateWakeLock(playing)

        session.setMetadata(
            MediaMetadataCompat.Builder()
                .putString(MediaMetadataCompat.METADATA_KEY_TITLE, title)
                .putString(MediaMetadataCompat.METADATA_KEY_ARTIST, artist)
                .putLong(MediaMetadataCompat.METADATA_KEY_DURATION, durationMs)
                .build()
        )
        session.setPlaybackState(
            PlaybackStateCompat.Builder()
                .setActions(ACTIONS)
                .setState(
                    if (playing) PlaybackStateCompat.STATE_PLAYING
                    else PlaybackStateCompat.STATE_PAUSED,
                    positionMs,
                    if (playing) 1f else 0f
                )
                .build()
        )

        if (state.artwork.isNotBlank() && state.artwork != artworkUrl) {
            artworkUrl = state.artwork
            loadArtwork(state.artwork)
        }

        notifyChanged()
    }

    private fun updateWakeLock(enabled: Boolean) {
        if (enabled) {
            if (wakeLock == null) {
                val pm = getSystemService(Context.POWER_SERVICE) as PowerManager
                wakeLock = pm.newWakeLock(
                    PowerManager.PARTIAL_WAKE_LOCK,
                    "NothingMusic:playback"
                ).apply {
                    setReferenceCounted(false)
                    acquire(6 * 60 * 60 * 1000L)
                }
            }
            if (wifiLock == null) {
                val wm = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
                wifiLock = wm.createWifiLock(
                    WifiManager.WIFI_MODE_FULL_HIGH_PERF,
                    "NothingMusic:wifi"
                ).apply {
                    setReferenceCounted(false)
                    acquire()
                }
            }
        } else {
            wakeLock?.let { if (it.isHeld) it.release() }
            wakeLock = null
            wifiLock?.let { if (it.isHeld) it.release() }
            wifiLock = null
        }
    }

    /* ---------- уведомление ---------- */

    private fun createChannel() {
        val manager = getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            getString(R.string.channel_playback),
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = getString(R.string.channel_playback_desc)
            setShowBadge(false)
        }
        manager.createNotificationChannel(channel)
    }

    private fun startForegroundCompat(notification: Notification) {
        if (Build.VERSION.SDK_INT >= 29) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun notifyChanged(force: Boolean = false) {
        if (destroyed) return
        val current = "$title|$artist|$playing|$artworkUrl"
        if (!force && current == fingerprint) return
        fingerprint = current
        getSystemService(NotificationManager::class.java)
            .notify(NOTIFICATION_ID, buildNotification())
    }

    private fun buildNotification(): Notification {
        val builder = NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_music_note)
            .setContentTitle(title ?: "NothingMusic")
            .setContentText(artist ?: "YouTube Music")
            .setContentIntent(openIntent())
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setShowWhen(false)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setColor(Color.WHITE)
            .addAction(R.drawable.ic_prev, getString(R.string.previous), serviceAction(ACTION_PREV, 1))
            .addAction(
                if (playing) R.drawable.ic_pause else R.drawable.ic_play,
                getString(if (playing) R.string.pause else R.string.play),
                serviceAction(ACTION_TOGGLE, 2)
            )
            .addAction(R.drawable.ic_next, getString(R.string.next), serviceAction(ACTION_NEXT, 3))
            .addAction(R.drawable.ic_close, getString(R.string.close), serviceAction(ACTION_STOP, 4))
            .setStyle(
                androidx.media.app.NotificationCompat.MediaStyle()
                    .setMediaSession(session.sessionToken)
                    .setShowActionsInCompactView(0, 1, 2)
            )

        val large = artwork ?: ContextCompat
            .getDrawable(this, R.drawable.ic_notification_large)
            ?.toBitmap(288, 288)
        if (large != null) builder.setLargeIcon(large)

        return builder.build()
    }

    private fun openIntent(): PendingIntent {
        val intent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        return PendingIntent.getActivity(
            this,
            0,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }

    private fun serviceAction(action: String, requestCode: Int): PendingIntent {
        val intent = Intent(this, PlaybackService::class.java).setAction(action)
        return PendingIntent.getService(
            this,
            requestCode,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
    }

    private fun loadArtwork(url: String) {
        artExecutor.execute {
            val bitmap = try {
                val connection = (URL(url).openConnection() as HttpURLConnection).apply {
                    connectTimeout = 5_000
                    readTimeout = 5_000
                }
                connection.inputStream.use { BitmapFactory.decodeStream(it) }
            } catch (e: Exception) {
                null
            }

            if (bitmap != null) {
                handler.post {
                    artwork = bitmap
                    notifyChanged(force = true)
                }
            }
        }
    }
}
