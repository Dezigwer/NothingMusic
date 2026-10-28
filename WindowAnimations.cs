#nullable enable
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NothingMusic
{
    /// <summary>
    /// Проверяет, включены ли анимации в самой Windows (спец. возможности).
    /// </summary>
    internal static class WindowsEffects
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct AnimationInfo
        {
            public uint cbSize;
            public int iMinAnimate;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref AnimationInfo pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        private const uint SPI_GETANIMATION = 0x0048;
        private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

        private static bool? _enabled;

        /// <summary>false, если пользователь отключил анимации в Windows.</summary>
        public static bool AnimationsEnabled => _enabled ??= Detect();

        private static bool Detect()
        {
            try
            {
                int clientArea = 1;
                if (SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref clientArea, 0) && clientArea == 0)
                    return false;

                var info = new AnimationInfo { cbSize = (uint)Marshal.SizeOf<AnimationInfo>() };
                if (SystemParametersInfo(SPI_GETANIMATION, info.cbSize, ref info, 0) && info.iMinAnimate == 0)
                    return false;
            }
            catch
            {
                // не смогли прочитать настройки — считаем, что анимации включены
            }

            return true;
        }
    }

    /// <summary>
    /// Плавная анимация окна: прозрачность + смещение (ease-out).
    /// </summary>
    internal sealed class SmoothWindow : IDisposable
    {
        private readonly Form _form;
        private readonly Timer _timer = new() { Interval = 15 };
        private readonly Stopwatch _watch = new();

        private double _fromOpacity;
        private double _toOpacity;
        private Point _fromLocation;
        private Point _toLocation;
        private int _duration;
        private Action? _completed;

        public SmoothWindow(Form form)
        {
            _form = form;
            _timer.Tick += OnTick;
        }

        public bool Busy => _timer.Enabled;

        public void Animate(double opacity, Point location, int durationMs, Action? completed = null)
        {
            _fromOpacity = _form.Opacity;
            _toOpacity = Math.Clamp(opacity, 0, 1);
            _fromLocation = _form.Location;
            _toLocation = location;
            _duration = Math.Max(1, durationMs);
            _completed = completed;

            _watch.Restart();
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            _completed = null;
            _form.Opacity = 1;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            double t = Math.Clamp(_watch.Elapsed.TotalMilliseconds / _duration, 0, 1);
            double eased = 1 - Math.Pow(1 - t, 3);

            _form.Opacity = _fromOpacity + (_toOpacity - _fromOpacity) * eased;
            _form.Location = new Point(
                (int)Math.Round(_fromLocation.X + (_toLocation.X - _fromLocation.X) * eased),
                (int)Math.Round(_fromLocation.Y + (_toLocation.Y - _fromLocation.Y) * eased));

            if (t < 1) return;

            _timer.Stop();
            _form.Opacity = _toOpacity;
            _form.Location = _toLocation;

            var completed = _completed;
            _completed = null;
            completed?.Invoke();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
