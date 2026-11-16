package com.dezigwer.nothingmusic

import android.content.Context
import android.content.Intent
import android.content.res.ColorStateList
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.os.PowerManager
import android.util.TypedValue
import android.view.Gravity
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.SeekBar
import android.widget.Switch
import android.widget.TextView
import java.util.Locale
import kotlin.math.roundToInt

/**
 * Настройки как оверлей поверх WebView, а не отдельная Activity.
 * Так страница остаётся видимой и музыка продолжает играть, пока
 * пользователь крутит эквалайзер или скорость.
 */
class SettingsPanel(
    context: Context,
    private val onClose: () -> Unit,
    private val onQuit: () -> Unit
) : FrameLayout(context) {

    private var settings: Settings = WebEngine.currentSettings(context)
    private val handler = Handler(Looper.getMainLooper())
    private val persist = Runnable { WebEngine.saveSettings(context, settings) }

    private val bg = Color.BLACK
    private val fg = Color.WHITE
    private val muted = Color.parseColor("#8A8A8E")
    private val track = Color.parseColor("#33FFFFFF")
    private val stroke = Color.parseColor("#40FFFFFF")

    private val pixel: Typeface = try {
        Typeface.createFromAsset(context.assets, "fonts/PixelifySans.ttf")
    } catch (e: Exception) {
        Typeface.MONOSPACE
    }

    init {
        setBackgroundColor(bg)
        isClickable = true
        build()
    }

    fun close() {
        handler.removeCallbacks(persist)
        WebEngine.saveSettings(context, settings)
        onClose()
    }

    /* ---------- разметка ---------- */

    private fun build() {
        removeAllViews()

        val scroll = ScrollView(context).apply {
            setBackgroundColor(bg)
            isFillViewport = true
            overScrollMode = View.OVER_SCROLL_NEVER
        }
        val root = LinearLayout(context).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(22), dp(18), dp(22), dp(36))
        }
        scroll.addView(root)
        addView(scroll, LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT))

        root.addView(header())
        root.addView(section(context.getString(R.string.section_speed)))

        root.addView(
            slider(
                context.getString(R.string.speed),
                settings.speed, 0.5f, 2f, 0.05f,
                { value -> String.format(Locale.US, "%.2fx", value) }
            ) { value -> settings.speed = value; commit() }
        )
        root.addView(toggle(context.getString(R.string.preserve_pitch), settings.preservePitch) { on ->
            settings.preservePitch = on
            commitNow()
        })

        root.addView(section(context.getString(R.string.section_eq)))
        root.addView(
            slider(
                context.getString(R.string.bass),
                settings.bass, -12f, 12f, 1f,
                { value -> String.format(Locale.US, "%+.0f дБ", value) }
            ) { value -> settings.bass = value; commit() }
        )
        root.addView(
            slider(
                context.getString(R.string.mid),
                settings.mid, -12f, 12f, 1f,
                { value -> String.format(Locale.US, "%+.0f дБ", value) }
            ) { value -> settings.mid = value; commit() }
        )
        root.addView(
            slider(
                context.getString(R.string.treble),
                settings.treble, -12f, 12f, 1f,
                { value -> String.format(Locale.US, "%+.0f дБ", value) }
            ) { value -> settings.treble = value; commit() }
        )

        root.addView(section(context.getString(R.string.section_effects)))
        root.addView(toggle(context.getString(R.string.underwater), settings.underwater) { on ->
            settings.underwater = on
            commitNow()
        })
        root.addView(
            slider(
                context.getString(R.string.underwater_depth),
                settings.underwaterDepth, 0f, 1f, 0.05f,
                { value -> "${(value * 100).roundToInt()}%" }
            ) { value -> settings.underwaterDepth = value; commit() }
        )
        root.addView(
            slider(
                context.getString(R.string.reverb),
                settings.reverb, 0f, 1f, 0.05f,
                { value -> "${(value * 100).roundToInt()}%" }
            ) { value -> settings.reverb = value; commit() }
        )

        root.addView(section(context.getString(R.string.section_app)))
        root.addView(toggle(context.getString(R.string.desktop_site), settings.desktopSite) { on ->
            settings.desktopSite = on
            commitNow()
        })
        root.addView(batteryRow())

        root.addView(button(context.getString(R.string.reset)) {
            val keepDesktop = settings.desktopSite
            settings = Settings(desktopSite = keepDesktop)
            handler.removeCallbacks(persist)
            WebEngine.saveSettings(context, settings)
            build()
        }, rowParams(top = 18))

        root.addView(button(context.getString(R.string.quit)) { onQuit() }, rowParams(top = 10))

        root.addView(
            TextView(context).apply {
                text = context.getString(R.string.footer_note)
                setTextColor(muted)
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                setLineSpacing(dp(3).toFloat(), 1f)
            },
            rowParams(top = 26)
        )
    }

    /* ---------- сохранение ---------- */

    private fun commit() {
        handler.removeCallbacks(persist)
        handler.postDelayed(persist, 150)
    }

    private fun commitNow() {
        handler.removeCallbacks(persist)
        WebEngine.saveSettings(context, settings)
    }

    /* ---------- элементы ---------- */

    private fun header(): View {
        val row = LinearLayout(context).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        val titles = LinearLayout(context).apply { orientation = LinearLayout.VERTICAL }

        titles.addView(
            TextView(context).apply {
                text = context.getString(R.string.app_name)
                setTextColor(fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 24f)
                letterSpacing = 0.06f
            }
        )
        titles.addView(
            TextView(context).apply {
                text = context.getString(R.string.subtitle)
                setTextColor(muted)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
                letterSpacing = 0.14f
                setPadding(0, dp(4), 0, 0)
            }
        )

        row.addView(titles, LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f))
        row.addView(
            TextView(context).apply {
                text = context.getString(R.string.done)
                setTextColor(fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
                letterSpacing = 0.08f
                setPadding(dp(14), dp(8), dp(14), dp(8))
                background = GradientDrawable().apply {
                    cornerRadius = dp(20).toFloat()
                    setStroke(dp(1), stroke)
                }
                isClickable = true
                setOnClickListener { close() }
            }
        )
        return row
    }

    private fun section(title: String): TextView = TextView(context).apply {
        text = title
        setTextColor(muted)
        typeface = pixel
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 12f)
        letterSpacing = 0.16f
        setPadding(0, dp(26), 0, dp(6))
    }

    private fun slider(
        title: String,
        value: Float,
        min: Float,
        max: Float,
        step: Float,
        format: (Float) -> String,
        onChange: (Float) -> Unit
    ): View {
        val column = LinearLayout(context).apply { orientation = LinearLayout.VERTICAL }

        val head = LinearLayout(context).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
        }

        head.addView(
            TextView(context).apply {
                text = title
                setTextColor(fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            },
            LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        )

        val steps = ((max - min) / step).roundToInt()
        val initial = (((value - min) / step).roundToInt()).coerceIn(0, steps)

        val valueText = TextView(context).apply {
            text = format(value)
            setTextColor(fg)
            typeface = pixel
            setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            gravity = Gravity.END
        }
        head.addView(valueText)
        column.addView(head)

        val bar = SeekBar(context).apply {
            this.max = steps
            progress = initial
            progressTintList = ColorStateList.valueOf(fg)
            progressBackgroundTintList = ColorStateList.valueOf(track)
            thumbTintList = ColorStateList.valueOf(fg)
            splitTrack = false
            setPadding(0, dp(10), 0, dp(10))
        }

        bar.setOnSeekBarChangeListener(object : SeekBar.OnSeekBarChangeListener {
            override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) {
                val current = min + progress * step
                valueText.text = format(current)
                if (fromUser) onChange(current)
            }

            override fun onStartTrackingTouch(seekBar: SeekBar?) = Unit
            override fun onStopTrackingTouch(seekBar: SeekBar?) = Unit
        })

        column.addView(bar)
        column.addView(divider())
        return column
    }

    private fun toggle(title: String, checked: Boolean, onChange: (Boolean) -> Unit): View {
        val row = LinearLayout(context).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(0, dp(4), 0, dp(4))
        }

        row.addView(
            TextView(context).apply {
                text = title
                setTextColor(fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            },
            LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        )

        val switch = Switch(context).apply {
            isChecked = checked
            showText = false
            thumbTintList = ColorStateList.valueOf(fg)
            trackTintList = ColorStateList.valueOf(if (checked) Color.WHITE else track)
            setOnCheckedChangeListener { _, on ->
                trackTintList = ColorStateList.valueOf(if (on) Color.WHITE else track)
                onChange(on)
            }
        }
        row.addView(switch)

        val wrapper = LinearLayout(context).apply { orientation = LinearLayout.VERTICAL }
        wrapper.addView(row)
        wrapper.addView(divider())
        return wrapper
    }

    /** Строка про оптимизацию батареи: без исключения фон на многих прошивках режут. */
    private fun batteryRow(): View {
        val pm = context.getSystemService(Context.POWER_SERVICE) as PowerManager
        val exempt = pm.isIgnoringBatteryOptimizations(context.packageName)

        val row = LinearLayout(context).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(0, dp(12), 0, dp(12))
            isClickable = !exempt
            if (!exempt) {
                setOnClickListener {
                    try {
                        context.startActivity(
                            Intent(
                                android.provider.Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS,
                                Uri.parse("package:${context.packageName}")
                            )
                        )
                    } catch (e: Exception) {
                        // некоторые прошивки не поддерживают этот диалог
                        try {
                            context.startActivity(
                                Intent(android.provider.Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS)
                            )
                        } catch (ignored: Exception) {
                        }
                    }
                }
            }
        }

        row.addView(
            TextView(context).apply {
                text = context.getString(R.string.battery)
                setTextColor(fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 15f)
            },
            LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f)
        )
        row.addView(
            TextView(context).apply {
                text = context.getString(
                    if (exempt) R.string.battery_ok else R.string.battery_allow
                )
                setTextColor(if (exempt) muted else fg)
                typeface = pixel
                setTextSize(TypedValue.COMPLEX_UNIT_SP, 13f)
            }
        )

        val wrapper = LinearLayout(context).apply { orientation = LinearLayout.VERTICAL }
        wrapper.addView(row)
        wrapper.addView(divider())
        return wrapper
    }

    private fun button(text: String, onClick: () -> Unit): TextView = TextView(context).apply {
        this.text = text
        setTextColor(fg)
        typeface = pixel
        setTextSize(TypedValue.COMPLEX_UNIT_SP, 14f)
        letterSpacing = 0.06f
        gravity = Gravity.CENTER
        setPadding(dp(18), dp(13), dp(18), dp(13))
        background = GradientDrawable().apply {
            cornerRadius = dp(22).toFloat()
            setStroke(dp(1), stroke)
        }
        isClickable = true
        setOnClickListener { onClick() }
    }

    private fun divider(): View = View(context).apply {
        setBackgroundColor(Color.parseColor("#1AFFFFFF"))
        layoutParams = LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            dp(1)
        ).apply { topMargin = dp(6) }
    }

    private fun rowParams(top: Int = 0): LinearLayout.LayoutParams =
        LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MATCH_PARENT,
            ViewGroup.LayoutParams.WRAP_CONTENT
        ).apply { topMargin = dp(top) }

    private fun dp(value: Int): Int =
        (value * resources.displayMetrics.density).toInt()
}
