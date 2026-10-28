#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace NothingMusic
{
    /// <summary>
    /// Мост между C# и Web Audio графом внутри страницы YouTube Music.
    /// </summary>
    internal sealed class AudioBridge
    {
        private readonly CoreWebView2 _core;
        private readonly string _script;

        public AudioBridge(CoreWebView2 core)
        {
            _core = core;
            _script = LoadScript();
        }

        public bool IsReady => _script.Length > 0;

        private static string LoadScript()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (!name.EndsWith("audio-effects.js", StringComparison.OrdinalIgnoreCase))
                        continue;

                    using Stream? stream = assembly.GetManifestResourceStream(name);
                    if (stream == null) continue;

                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                // ниже вернём пустую строку
            }

            return string.Empty;
        }

        /// <summary>Регистрирует скрипт, который выполняется в каждом новом документе.</summary>
        public async Task InstallAsync()
        {
            if (!IsReady) return;

            try
            {
                await _core.AddScriptToExecuteOnDocumentCreatedAsync(_script);
            }
            catch
            {
                // страница могла ещё не быть готовой — повторим позже
            }
        }

        /// <summary>Отправляет текущие настройки в страницу.</summary>
        public async Task PushAsync(AudioSettings settings)
        {
            try
            {
                string json = settings.Sanitize().ToJson();
                await _core.ExecuteScriptAsync(
                    $"window.__nothingAudio && window.__nothingAudio.set({json});");
            }
            catch
            {
                // WebView может быть занят навигацией — не страшно, попробуем в следующий раз
            }
        }
    }
}
