package com.dezigwer.nothingmusic

import android.content.Context
import org.json.JSONObject
import kotlin.math.roundToInt

/**
 * Настройки звука и оформления. Аналог AudioSettings.cs из десктопной версии.
 * Хранятся в SharedPreferences в виде JSON.
 */
data class Settings(
    var speed: Float = 1f,
    var preservePitch: Boolean = true,
    var bass: Float = 0f,
    var mid: Float = 0f,
    var treble: Float = 0f,
    var underwater: Boolean = false,
    var underwaterDepth: Float = 0.6f,
    var reverb: Float = 0f,
    var desktopSite: Boolean = false
) {

    fun sanitized(): Settings = copy(
        speed = speed.coerceIn(0.5f, 2f),
        bass = bass.coerceIn(-12f, 12f),
        mid = mid.coerceIn(-12f, 12f),
        treble = treble.coerceIn(-12f, 12f),
        underwaterDepth = underwaterDepth.coerceIn(0f, 1f),
        reverb = reverb.coerceIn(0f, 1f)
    )

    private fun round(value: Float): Double = (value * 100f).roundToInt() / 100.0

    /** Объект для страницы: имена полей совпадают с audio-effects.js. */
    fun toJs(): String = JSONObject().apply {
        put("speed", round(speed))
        put("preservePitch", preservePitch)
        put("bass", round(bass))
        put("mid", round(mid))
        put("treble", round(treble))
        put("underwater", underwater)
        put("underwaterDepth", round(underwaterDepth))
        put("reverb", round(reverb))
    }.toString()

    fun toJson(): JSONObject = JSONObject().apply {
        put("speed", round(speed))
        put("preservePitch", preservePitch)
        put("bass", round(bass))
        put("mid", round(mid))
        put("treble", round(treble))
        put("underwater", underwater)
        put("underwaterDepth", round(underwaterDepth))
        put("reverb", round(reverb))
        put("desktopSite", desktopSite)
    }

    companion object {
        private const val PREFS = "nothingmusic"
        private const val KEY = "settings"

        fun load(context: Context): Settings {
            val raw = context
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .getString(KEY, null) ?: return Settings()

            return try {
                val o = JSONObject(raw)
                Settings(
                    speed = o.optDouble("speed", 1.0).toFloat(),
                    preservePitch = o.optBoolean("preservePitch", true),
                    bass = o.optDouble("bass", 0.0).toFloat(),
                    mid = o.optDouble("mid", 0.0).toFloat(),
                    treble = o.optDouble("treble", 0.0).toFloat(),
                    underwater = o.optBoolean("underwater", false),
                    underwaterDepth = o.optDouble("underwaterDepth", 0.6).toFloat(),
                    reverb = o.optDouble("reverb", 0.0).toFloat(),
                    desktopSite = o.optBoolean("desktopSite", false)
                ).sanitized()
            } catch (e: Exception) {
                Settings()
            }
        }

        fun save(context: Context, settings: Settings) {
            context
                .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString(KEY, settings.toJson().toString())
                .apply()
        }
    }
}
