#nullable enable
using System;
using System.IO;
using System.Text.Json;

namespace NothingMusic
{
    /// <summary>
    /// Настройки звуковых эффектов. Хранятся в %LOCALAPPDATA%\YouTubeMusic\settings.json
    /// Имена свойств сериализуются в camelCase, чтобы совпадать с JS-объектом настроек.
    /// </summary>
    internal sealed class AudioSettings
    {
        public double Speed { get; set; } = 1.0;          // 0.5 .. 2.0
        public bool PreservePitch { get; set; } = true;   // не делать "бурундука"
        public double Bass { get; set; } = 0;             // дБ, -12 .. 12
        public double Mid { get; set; } = 0;              // дБ, -12 .. 12
        public double Treble { get; set; } = 0;           // дБ, -12 .. 12
        public bool Underwater { get; set; } = false;     // эффект "под водой"
        public double UnderwaterDepth { get; set; } = 0.6; // 0 .. 1
        public double Reverb { get; set; } = 0;           // 0 .. 1

        // --- оформление окна (в страницу не уходит, скрипт лишние поля игнорирует) ---
        public bool NativeTitleBar { get; set; } = false; // системный заголовок Windows
        public bool DarkTitleBar { get; set; } = true;    // тёмная системная рамка
        public string WindowTitle { get; set; } = "";     // своё название окна ("" — без надписи)

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static string FilePath => Path.Combine(AppPaths.Root, "settings.json");

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public AudioSettings Clone() => (AudioSettings)MemberwiseClone();

        public AudioSettings Sanitize()
        {
            Speed = Math.Clamp(Speed, 0.5, 2.0);
            Bass = Math.Clamp(Bass, -12, 12);
            Mid = Math.Clamp(Mid, -12, 12);
            Treble = Math.Clamp(Treble, -12, 12);
            UnderwaterDepth = Math.Clamp(UnderwaterDepth, 0, 1);
            Reverb = Math.Clamp(Reverb, 0, 1);
            WindowTitle = (WindowTitle ?? "").Trim();
            if (WindowTitle.Length > 80) WindowTitle = WindowTitle.Substring(0, 80);
            return this;
        }

        public void CopyFrom(AudioSettings other)
        {
            Speed = other.Speed;
            PreservePitch = other.PreservePitch;
            Bass = other.Bass;
            Mid = other.Mid;
            Treble = other.Treble;
            Underwater = other.Underwater;
            UnderwaterDepth = other.UnderwaterDepth;
            Reverb = other.Reverb;
        }

        public static AudioSettings Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path))
                    return new AudioSettings();

                var loaded = JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(path), JsonOptions);
                return loaded?.Sanitize() ?? new AudioSettings();
            }
            catch
            {
                return new AudioSettings();
            }
        }

        public void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(FilePath, ToJson());
            }
            catch
            {
                // настройки — не критично, молча пропускаем
            }
        }
    }
}
