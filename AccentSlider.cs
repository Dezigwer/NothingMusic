#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NothingMusic
{
    /// <summary>
    /// Ползунок с градиентной заливкой (тёмная тема).
    /// </summary>
    internal sealed class AccentSlider : Control
    {
        private double _minimum;
        private double _maximum = 1;
        private double _value;
        private double _step;
        private bool _dragging;
        private bool _hover;

        private const int SidePadding = 9;

        public AccentSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);

            BackColor = Color.FromArgb(16, 16, 16);
            Height = 26;
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(255, 255, 255);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // всегда полностью заливаем фон — иначе старые кадры «просвечивают»
            e.Graphics.Clear(BackColor);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor2 { get; set; } = Color.FromArgb(208, 208, 208);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TrackColor { get; set; } = Color.FromArgb(42, 42, 42);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color ThumbColor { get; set; } = Color.FromArgb(16, 16, 16);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum < _minimum) _maximum = _minimum;
                SetValue(_value, false);
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_minimum > _maximum) _minimum = _maximum;
                SetValue(_value, false);
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Step
        {
            get => _step;
            set => _step = Math.Max(0, value);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Value
        {
            get => _value;
            set => SetValue(value, true);
        }

        public event EventHandler? ValueChanged;

        private bool SetValue(double value, bool raiseEvent)
        {
            double v = Math.Clamp(value, _minimum, _maximum);
            if (_step > 0)
                v = _minimum + Math.Round((v - _minimum) / _step) * _step;
            v = Math.Clamp(v, _minimum, _maximum);

            if (Math.Abs(v - _value) < 1e-9)
                return false;

            _value = v;
            Invalidate();

            if (raiseEvent)
                ValueChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // всегда заливаем фон: иначе видны остатки предыдущего кадра из буфера
            g.Clear(BackColor);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackHeight = 6;
            int cy = Height / 2;
            var trackRect = new Rectangle(SidePadding, cy - trackHeight / 2,
                Math.Max(trackHeight, Width - SidePadding * 2), trackHeight);

            using (var trackPath = RoundRect(trackRect, trackHeight / 2f))
            using (var trackBrush = new SolidBrush(TrackColor))
                g.FillPath(trackBrush, trackPath);

            double ratio = _maximum > _minimum ? (_value - _minimum) / (_maximum - _minimum) : 0;
            ratio = Math.Clamp(ratio, 0, 1);

            int fillWidth = (int)Math.Round(trackRect.Width * ratio);
            if (fillWidth > 0)
            {
                var fillRect = new Rectangle(trackRect.X, trackRect.Y, Math.Max(trackHeight, fillWidth), trackHeight);
                using var fillPath = RoundRect(fillRect, trackHeight / 2f);
                using var gradient = new LinearGradientBrush(
                    new Rectangle(trackRect.X, trackRect.Y, Math.Max(1, trackRect.Width), trackHeight),
                    AccentColor, AccentColor2, LinearGradientMode.Horizontal);
                g.FillPath(gradient, fillPath);
            }

            int thumbRadius = 7;
            int glowRadius = (_dragging || _hover) ? 12 : 10;
            int thumbX = trackRect.X + (int)Math.Round(trackRect.Width * ratio);

            using (var glow = new SolidBrush(Color.FromArgb(_dragging || _hover ? 80 : 40, AccentColor)))
                g.FillEllipse(glow, thumbX - glowRadius, cy - glowRadius, glowRadius * 2, glowRadius * 2);

            var thumbRect = new Rectangle(thumbX - thumbRadius, cy - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            using (var thumb = new SolidBrush(ThumbColor))
                g.FillEllipse(thumb, thumbRect);
            using (var ring = new Pen(AccentColor, 2f))
                g.DrawEllipse(ring, thumbRect);
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

        private void SetFromX(int x)
        {
            int width = Math.Max(1, Width - SidePadding * 2);
            double ratio = Math.Clamp((x - SidePadding) / (double)width, 0, 1);
            Value = _minimum + ratio * (_maximum - _minimum);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            Capture = true;
            SetFromX(e.X);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragging)
            {
                SetFromX(e.X);
                return;
            }

            bool inside = ClientRectangle.Contains(e.Location);
            if (inside != _hover)
            {
                _hover = inside;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            if (!_dragging) Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            double step = _step > 0 ? _step : (_maximum - _minimum) / 20.0;
            Value = _value + Math.Sign(e.Delta) * step;
        }
    }
}
