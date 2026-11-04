package com.dezigwer.nothingmusic

import android.Manifest
import android.app.Activity
import android.content.pm.PackageManager
import android.content.res.ColorStateList
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.ImageButton
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat

class MainActivity : Activity() {

    private lateinit var container: FrameLayout
    private var panel: SettingsPanel? = null

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Android 15 рисует приложения под системными панелями — отступаем вручную.
        WindowCompat.setDecorFitsSystemWindows(window, false)
        window.statusBarColor = Color.TRANSPARENT
        window.navigationBarColor = Color.TRANSPARENT

        container = FrameLayout(this).apply { setBackgroundColor(Color.BLACK) }
        setContentView(container)

        ViewCompat.setOnApplyWindowInsetsListener(container) { view, insets ->
            val bars = insets.getInsets(
                WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.ime()
            )
            view.setPadding(bars.left, bars.top, bars.right, bars.bottom)
            WindowInsetsCompat.CONSUMED
        }

        val web = WebEngine.get(applicationContext)
        (web.parent as? ViewGroup)?.removeView(web)
        container.addView(
            web,
            FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            )
        )

        val gear = ImageButton(this).apply {
            setImageResource(R.drawable.ic_settings)
            imageTintList = ColorStateList.valueOf(Color.WHITE)
            background = GradientDrawable().apply {
                shape = GradientDrawable.OVAL
                setColor(Color.parseColor("#CC000000"))
                setStroke(dp(1), Color.parseColor("#33FFFFFF"))
            }
            contentDescription = getString(R.string.settings)
            setPadding(dp(11), dp(11), dp(11), dp(11))
            setOnClickListener { openSettings() }
        }
        // поднимаем над мини-плеером YouTube Music, чтобы не перекрывать кнопки
        val gearParams = FrameLayout.LayoutParams(dp(44), dp(44), Gravity.END or Gravity.BOTTOM)
        gearParams.setMargins(0, 0, dp(12), dp(92))
        container.addView(gear, gearParams)

        // Держим процесс живым и показываем медиа-уведомление, пока играет музыка.
        PlaybackService.start(this)

        if (Build.VERSION.SDK_INT >= 33 &&
            checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 10)
        }
    }

    override fun onResume() {
        super.onResume()
        WebEngine.pushSettings()
    }

    /** Настройки показываем оверлеем: WebView остаётся видимым, музыка не прерывается. */
    private fun openSettings() {
        if (panel != null) return
        val created = SettingsPanel(this, ::closeSettings) { quitApp() }
        created.alpha = 0f
        panel = created
        container.addView(
            created,
            FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            )
        )
        created.animate().alpha(1f).setDuration(150L).start()
    }

    private fun closeSettings() {
        val current = panel ?: return
        panel = null
        current.animate()
            .alpha(0f)
            .setDuration(120L)
            .withEndAction { container.removeView(current) }
            .start()
        WebEngine.pushSettings()
    }

    private fun quitApp() {
        PlaybackService.stop(this)
        WebEngine.destroy()
        finishAffinity()
    }

    @Deprecated("Deprecated in Java")
    override fun onBackPressed() {
        val openPanel = panel
        if (openPanel != null) {
            openPanel.close()
            return
        }

        val web = WebEngine.peek()
        if (web != null && web.canGoBack()) {
            web.goBack()
        } else {
            // как трей на десктопе: не убиваем музыку, просто уходим в фон
            moveTaskToBack(true)
        }
    }

    override fun onDestroy() {
        WebEngine.peek()?.let { web ->
            (web.parent as? ViewGroup)?.removeView(web)
        }
        super.onDestroy()
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()
}
