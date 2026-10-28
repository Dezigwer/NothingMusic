#nullable enable
using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NothingMusic
{
    /// <summary>
    /// Шрифты интерфейса. Pixelify Sans — открытый пиксельный шрифт (SIL OFL)
    /// с поддержкой кириллицы; если его нет в сборке — Segoe UI.
    /// </summary>
    internal static class AppFont
    {
        private static readonly PrivateFontCollection Fonts = new();
        private static readonly IntPtr FontMemory;
        private static readonly string? DotFamily;

        static AppFont()
        {
            try
            {
                using Stream? stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("NothingMusic.PixelifySans.ttf");

                if (stream != null)
                {
                    byte[] data = new byte[stream.Length];
                    stream.ReadExactly(data);

                    FontMemory = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, FontMemory, data.Length);
                    Fonts.AddMemoryFont(FontMemory, data.Length);

                    if (Fonts.Families.Length > 0)
                    {
                        // у вариативного шрифта берём именованный экземпляр пожирнее:
                        // GDI+ не умеет синтезировать Bold для таких шрифтов
                        DotFamily = Fonts.Families.FirstOrDefault(f => f.Name.Contains("SemiBold"))?.Name
                                    ?? Fonts.Families.FirstOrDefault(f => f.Name.Contains("Medium"))?.Name
                                    ?? Fonts.Families[0].Name;
                    }
                }
            }
            catch
            {
                DotFamily = null;
            }
        }

        /// <summary>Пиксельный шрифт в пикселях; при отсутствии Pixelify Sans — Segoe UI.</summary>
        public static Font Dot(float pixelSize, FontStyle style = FontStyle.Regular)
        {
            return DotFamily != null
                ? new Font(DotFamily, pixelSize, style, GraphicsUnit.Pixel)
                : new Font("Segoe UI", pixelSize * 0.75f, FontStyle.Bold, GraphicsUnit.Point);
        }
    }
}
