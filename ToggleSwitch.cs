#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NothingMusic
{
    /// <summary>
    /// Переключатель-«таблетка» с плавным ходом (pill switch), тёмная тема.
    /// </summary>
    internal sealed class ToggleSwitch : CheckBox
    {
        private readonly Timer _animTimer = new() { Interval = 15 };
        private double _position;
        private double _target;

        private static readonly Color OffColor = Color.FromArgb(51, 51, 51);
        private static readonly Color KnobOffColor = Color.FromArgb(143, 143, 143);

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);

            AutoSize = false;
            BackColor = Color.FromArgb(16, 16, 16);
            ForeColor = Color.FromArgb(255, 255, 255);
            Font = new Font("Segoe UI", 9f);
            Height = 24;
            Cursor = Cursors.Hand;

            _animTimer.Tick += OnAnimTick;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // полностью перекрываем прошлый кадр (иначе остаются следы ползунка)
            pevent.Graphics.Clear(BackColor);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = Color.FromArgb(255, 255, 255);

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);

            _target = Checked ? 1 : 0;

            if (!WindowsEffects.AnimationsEnabled)
            {
                _position = _target;
                Invalidate();
                return;
            }

            _animTimer.Start();
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            double diff = _target - _position;
            _position += diff * 0.35;

            if (Math.Abs(diff) < 0.01)
            {
                _position = _target;
                _animTimer.Stop();
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // CheckBox может не закрашивать фон сам — без этого из общего буфера
            // двойной буферизации «просвечивает» кадр соседнего контрола
            g.Clear(BackColor);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int pillWidth = 36;
            const int pillHeight = 18;
            int pillY = (Height - pillHeight) / 2;
            var pill = new Rectangle(Width - pillWidth - 2, pillY, pillWidth, pillHeight);

            float position = (float)Math.Clamp(_position, 0, 1);

            using (var path = RoundRect(pill, pillHeight / 2f))
            using (var brush = new SolidBrush(Blend(OffColor, AccentColor, position)))
                g.FillPath(brush, path);

            int knobSize = pillHeight - 4;
            int travel = pillWidth - knobSize - 4;
            int knobX = pill.X + 2 + (int)Math.Round(travel * position);

            using (var knobBrush = new SolidBrush(Blend(KnobOffColor, Color.FromArgb(16, 16, 16), position)))
                g.FillEllipse(knobBrush, knobX, pill.Y + 2, knobSize, knobSize);

            var textRect = new Rectangle(0, 0, Math.Max(1, Width - pillWidth - 12), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : Color.FromArgb(110, 110, 125),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        private static Color Blend(Color from, Color to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * t),
                (int)Math.Round(from.G + (to.G - from.G) * t),
                (int)Math.Round(from.B + (to.B - from.B) * t));
        }

        private static GraphicsPath RoundRect(Rectangle rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _animTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}
