#nullable enable
using System;
using System.IO;

namespace NothingMusic
{
    /// <summary>
    /// Единые пути приложения. Папку старого имени переименовываем один раз,
    /// чтобы настройки и кэш WebView2 не потерялись.
    /// </summary>
    internal static class AppPaths
    {
        public const string FolderName = "NothingMusic";
        private const string LegacyFolderName = "YouTubeMusic";

        private static string? _root;

        public static string Root
        {
            get
            {
                if (_root != null)
                    return _root;

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string current = Path.Combine(local, FolderName);
                string legacy = Path.Combine(local, LegacyFolderName);

                try
                {
                    if (!Directory.Exists(current) && Directory.Exists(legacy))
                        Directory.Move(legacy, current);
                }
                catch
                {
                    // не смогли перенести — работаем с новой папкой
                }

                _root = current;
                return _root;
            }
        }
    }
}
