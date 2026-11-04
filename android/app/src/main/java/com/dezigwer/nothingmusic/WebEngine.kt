package com.dezigwer.nothingmusic

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Bitmap
import android.graphics.Color
import android.webkit.CookieManager
import android.webkit.WebChromeClient
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import org.json.JSONObject
import org.json.JSONTokener

data class PlayerState(
    val playing: Boolean,
    val position: Double,
    val duration: Double,
    val title: String,
    val artist: String,
    val album: String,
    val artwork: String
)

/**
 * Один WebView на всё приложение: он живёт дольше Activity, поэтому музыка
 * не прерывается при сворачивании и повороте экрана.
 */
object WebEngine {

    const val HOME = "https://music.youtube.com/"

    private const val MOBILE_UA =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36"

    private const val DESKTOP_UA =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"

    @Volatile
    private var web: WebView? = null

    private var appContext: Context? = null
    private var settings: Settings = Settings()
    private var audioScript: String? = null
    private var playerScript: String? = null

    /** Текущие настройки (используются экраном настроек). */
    fun currentSettings(context: Context): Settings {
        if (appContext == null) {
            appContext = context.applicationContext
            settings = Settings.load(context.applicationContext)
        }
        return settings.copy()
    }

    fun get(context: Context): WebView {
        val app = context.applicationContext
        appContext = app

        val existing = web
        if (existing != null) return existing

        synchronized(this) {
            web?.let { return it }

            settings = Settings.load(app)
            audioScript = readAsset(app, "audio-effects.js")
            playerScript = readAsset(app, "player-bridge.js")

            val created = create(app)
            web = created
            return created
        }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun create(context: Context): WebView {
        val view = WebView(context)
        view.setBackgroundColor(Color.BLACK)

        view.settings.apply {
            javaScriptEnabled = true
            domStorageEnabled = true
            databaseEnabled = true
            mediaPlaybackRequiresUserGesture = false
            useWideViewPort = true
            loadWithOverviewMode = true
            setSupportMultipleWindows(false)
            cacheMode = WebSettings.LOAD_DEFAULT
            userAgentString = if (settings.desktopSite) DESKTOP_UA else MOBILE_UA
        }

        CookieManager.getInstance().apply {
            setAcceptCookie(true)
            setAcceptThirdPartyCookies(view, true)
        }

        view.webViewClient = object : WebViewClient() {
            override fun onPageStarted(v: WebView, url: String?, favicon: Bitmap?) {
                super.onPageStarted(v, url, favicon)
                inject(v)
            }

            override fun onPageFinished(v: WebView, url: String?) {
                super.onPageFinished(v, url)
                inject(v)
                pushSettings()
            }
        }
        view.webChromeClient = WebChromeClient()

        view.loadUrl(HOME)
        return view
    }

    /** Скрипты идемпотентны (внутри стоят guard'ы), поэтому вставляем их дважды. */
    private fun inject(view: WebView) {
        audioScript?.let { view.evaluateJavascript(it, null) }
        playerScript?.let { view.evaluateJavascript(it, null) }
    }

    /** Уже созданный WebView, без создания нового. */
    fun peek(): WebView? = web

    fun reload() {
        web?.reload()
    }

    fun saveSettings(context: Context, updated: Settings) {
        val sanitized = updated.sanitized()
        val desktopChanged = sanitized.desktopSite != settings.desktopSite

        settings = sanitized
        appContext = context.applicationContext
        Settings.save(context.applicationContext, sanitized)

        val view = web
        if (view != null && desktopChanged) {
            view.settings.userAgentString = if (sanitized.desktopSite) DESKTOP_UA else MOBILE_UA
            view.reload()
        }
        pushSettings()
    }

    fun pushSettings() {
        val view = web ?: return
        val json = settings.toJs()
        evaluate(view, "window.__nothingAudio && window.__nothingAudio.set($json);")
    }

    fun player(action: String, value: Double? = null) {
        val view = web ?: return
        val call = if (value != null) {
            "window.__nothingPlayer && window.__nothingPlayer.$action($value)"
        } else {
            "window.__nothingPlayer && window.__nothingPlayer.$action()"
        }
        evaluate(view, call)
    }

    fun seek(seconds: Double) = player("seek", seconds)

    fun state(callback: (PlayerState?) -> Unit) {
        val view = web ?: run { callback(null); return }

        evaluate(
            view,
            "window.__nothingPlayer ? JSON.stringify(window.__nothingPlayer.state()) : null"
        ) { raw ->
            val json = unquote(raw)
            if (json.isNullOrEmpty() || json == "null") {
                callback(null)
            } else {
                callback(
                    try {
                        parse(JSONObject(json))
                    } catch (e: Exception) {
                        null
                    }
                )
            }
        }
    }

    fun destroy() {
        synchronized(this) {
            web?.let { view ->
                (view.parent as? android.view.ViewGroup)?.removeView(view)
                view.destroy()
            }
            web = null
        }
    }

    private fun parse(o: JSONObject) = PlayerState(
        playing = o.optBoolean("playing", false),
        position = o.optDouble("position", 0.0),
        duration = o.optDouble("duration", 0.0),
        title = o.optString("title", ""),
        artist = o.optString("artist", ""),
        album = o.optString("album", ""),
        artwork = o.optString("artwork", "")
    )

    /**
     * evaluateJavascript отдаёт результат уже в виде JSON-строки,
     * поэтому строку нужно «распаковать» через JSONTokener.
     */
    private fun unquote(raw: String?): String? {
        if (raw == null || raw == "null") return null
        return try {
            JSONTokener(raw).nextValue() as? String
        } catch (e: Exception) {
            null
        }
    }

    private fun evaluate(view: WebView, js: String, callback: ((String?) -> Unit)? = null) {
        try {
            view.evaluateJavascript(js) { result -> callback?.invoke(result) }
        } catch (e: Exception) {
            callback?.invoke(null)
        }
    }

    private fun readAsset(context: Context, name: String): String? = try {
        context.assets.open(name).bufferedReader().use { it.readText() }
    } catch (e: Exception) {
        null
    }
}
