#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NothingMusic
{
    /// <summary>
    /// Окно настроек звука: серая тема, плавное появление и скрытие.
    /// </summary>
    internal sealed class AudioSettingsForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static readonly Color Bg = Color.FromArgb(16, 16, 16);
        private static readonly Color HeaderBg = Color.FromArgb(22, 22, 22);
        private static readonly Color Card = Color.FromArgb(30, 30, 30);
        private static readonly Color Border = Color.FromArgb(48, 48, 48);
        private static readonly Color TextColor = Color.FromArgb(255, 255, 255);
        private static readonly Color Dim = Color.FromArgb(156, 156, 156);
        private static readonly Color Accent = Color.FromArgb(255, 255, 255);
        private static readonly Color Accent2 = Color.FromArgb(208, 208, 208);

        private const int ContentWidth = 300;
        private const int LeftMargin = 16;
        private const int HeaderHeight = 48;
        private const int AnimFrames = 14;

        private readonly AudioSettings _settings;
        private readonly Panel _content;
        private readonly List<(AccentSlider Slider, Func<double> Get)> _sliderSync = new();
        private readonly List<(ToggleSwitch Box, Func<bool> Get)> _checkSync = new();
        private readonly List<(TextBox Box, Func<string> Get)> _textSync = new();
        private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 15 };

        private bool _suppress;
        private bool _hiding;
        private int _animStep;
        private int _y;
        private Point _slideTarget;

        public event Action<AudioSettings>? SettingsChanged;

        public AudioSettingsForm(AudioSettings settings)
        {
            _settings = settings;

            Text = "Настройки звука";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Bg;
            ClientSize = new Size(360, 700);
            KeyPreview = true;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f);

            BuildHeader();

            _content = new BufferedPanel
            {
                Location = new Point(0, HeaderHeight),
                Size = new Size(ClientSize.Width, ClientSize.Height - HeaderHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Bg,
                AutoScroll = false
            };
            Controls.Add(_content);

            BuildContent();
            SyncFromSettings();

            _animTimer.Tick += AnimTick;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) HideAnimated();
            };
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    HideAnimated();
                }
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _animTimer.Dispose();

            base.Dispose(disposing);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int value = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch
            {
                // старая Windows — просто прямоугольное окно
            }
        }

        /* ---------- анимация ---------- */

        public void ShowAnimated(Point target)
        {
            _slideTarget = target;
            _hiding = false;
            _animStep = 0;

            if (!WindowsEffects.AnimationsEnabled)
            {
                Location = target;
                Show();
                return;
            }

            // только сдвиг: без Opacity (слоёное окно ломает перерисовку дочерних контролов)
            Location = new Point(target.X + 16, target.Y + 6);
            Show();
            _animTimer.Start();
        }

        public void HideAnimated()
        {
            if (!Visible || _hiding) return;

            if (!WindowsEffects.AnimationsEnabled)
            {
                Hide();
                return;
            }

            _hiding = true;
            _animStep = 0;
            _animTimer.Start();
        }

        private void AnimTick(object? sender, EventArgs e)
        {
            _animStep++;
            double t = Math.Clamp(_animStep / (double)AnimFrames, 0, 1);
            double eased = _hiding
                ? t * t
                : 1 - Math.Pow(1 - t, 3);

            Location = new Point(
                _slideTarget.X + (int)Math.Round(16 * (1 - eased)),
                _slideTarget.Y + (int)Math.Round(6 * (1 - eased)));

            if (t < 1) return;

            _animTimer.Stop();
            Location = _slideTarget;

            if (_hiding)
            {
                Hide();
                _hiding = false;
            }
            else
            {
                Invalidate(true);
                Update();
            }
        }

        /* ---------- интерфейс ---------- */

        private void BuildHeader()
        {
            var header = new BufferedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, HeaderHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = HeaderBg
            };
            header.MouseDown += DragWindow;
            header.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(40, 40, 40));
                e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var title = new Label
            {
                Text = "НАСТРОЙКИ ЗВУКА",
                ForeColor = TextColor,
                Font = AppFont.Dot(18f),
                AutoSize = true,
                BackColor = HeaderBg,
                Location = new Point(LeftMargin, 13)
            };
            title.MouseDown += DragWindow;

            var close = new Button
            {
                Text = "×",
                Size = new Size(32, 30),
                Location = new Point(ClientSize.Width - 44, 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = HeaderBg,
                ForeColor = Dim,
                Font = new Font("Segoe UI", 12f),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 42, 42);
            close.FlatAppearance.MouseDownBackColor = Color.FromArgb(56, 56, 56);
            close.MouseEnter += (s, e) => close.ForeColor = Color.White;
            close.MouseLeave += (s, e) => close.ForeColor = Dim;
            close.Click += (s, e) => HideAnimated();

            header.Controls.Add(title);
            header.Controls.Add(close);
            Controls.Add(header);
        }

        private void DragWindow(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private void BuildContent()
        {
            _y = 16;

            AddSection("СКОРОСТЬ");

            AddSlider("", 0.5, 2.0, 0.01,
                v => v.ToString("0.00") + "x",
                v => _settings.Speed = v,
                () => _settings.Speed);

            AddCheckBox("Сохранять питч", _settings.PreservePitch,
                v => _settings.PreservePitch = v,
                () => _settings.PreservePitch);

            _y += 14;
            AddSection("ЭКВАЛАЙЗЕР");

            AddSlider("Низкие (бас)", -12, 12, 0.5,
                v => (v > 0 ? "+" : "") + v.ToString("0.0") + " дБ",
                v => _settings.Bass = v,
                () => _settings.Bass);

            AddSlider("Средние", -12, 12, 0.5,
                v => (v > 0 ? "+" : "") + v.ToString("0.0") + " дБ",
                v => _settings.Mid = v,
                () => _settings.Mid);

            AddSlider("Высокие", -12, 12, 0.5,
                v => (v > 0 ? "+" : "") + v.ToString("0.0") + " дБ",
                v => _settings.Treble = v,
                () => _settings.Treble);

            _y += 14;
            AddSection("ЭФФЕКТЫ");

            AddCheckBox("Под водой", _settings.Underwater,
                v => _settings.Underwater = v,
                () => _settings.Underwater);

            AddSlider("Глубина воды", 0, 1, 0.05,
                v => Math.Round(v * 100) + "%",
                v => _settings.UnderwaterDepth = v,
                () => _settings.UnderwaterDepth);

            AddSlider("Реверберация (эхо)", 0, 1, 0.05,
                v => Math.Round(v * 100) + "%",
                v => _settings.Reverb = v,
                () => _settings.Reverb);

            _y += 14;
            AddSection("ОКНО");

            AddCheckBox("Системный заголовок", _settings.NativeTitleBar,
                v => _settings.NativeTitleBar = v,
                () => _settings.NativeTitleBar);

            AddCheckBox("Тёмный заголовок", _settings.DarkTitleBar,
                v => _settings.DarkTitleBar = v,
                () => _settings.DarkTitleBar);

            AddTextBox("Название окна", _settings.WindowTitle,
                v => _settings.WindowTitle = v,
                () => _settings.WindowTitle);
        }

        private void AddTextBox(string caption, string initial, Action<string> setter, Func<string> getter)
        {
            var title = new Label
            {
                Text = caption,
                ForeColor = Dim,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                Width = 200,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(LeftMargin, _y),
                BackColor = Bg
            };

            var box = new TextBox
            {
                Text = initial ?? string.Empty,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 9f),
                Width = ContentWidth,
                Location = new Point(LeftMargin, _y + 20),
                PlaceholderText = "пусто — без надписи"
            };

            box.TextChanged += (s, e) =>
            {
                if (_suppress) return;
                setter(box.Text);
                RaiseChanged();
            };

            _content.Controls.Add(title);
            _content.Controls.Add(box);
            _textSync.Add((box, getter));

            _y += 20 + 26 + 6;
        }

        private void AddSection(string text)
        {
            var label = new Label
            {
                Text = text,
                ForeColor = Accent,
                Font = AppFont.Dot(14f),
                AutoSize = false,
                Width = ContentWidth,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(LeftMargin, _y),
                BackColor = Bg
            };
            _content.Controls.Add(label);
            _y += 22;
        }

        private void AddSlider(string caption, double min, double max, double step,
            Func<double, string> format, Action<double> setter, Func<double> getter)
        {
            var title = new Label
            {
                Text = caption,
                ForeColor = Dim,
                Font = new Font("Segoe UI", 8.5f),
                AutoSize = false,
                Width = 200,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(LeftMargin, _y),
                BackColor = Bg
            };

            var value = new Label
            {
                Text = format(min),
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = false,
                Width = 100,
                Height = 18,
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(LeftMargin + ContentWidth - 100, _y),
                BackColor = Bg
            };

            var slider = new AccentSlider
            {
                Minimum = min,
                Maximum = max,
                Step = step,
                Value = min,
                Width = ContentWidth,
                Location = new Point(LeftMargin, _y + 20),
                AccentColor = Accent,
                AccentColor2 = Accent2,
                BackColor = Bg
            };

            value.Text = format(slider.Value);

            slider.ValueChanged += (s, e) =>
            {
                value.Text = format(slider.Value);
                if (_suppress) return;
                setter(slider.Value);
                RaiseChanged();
            };

            if (!string.IsNullOrEmpty(caption))
                _content.Controls.Add(title);
            _content.Controls.Add(value);
            _content.Controls.Add(slider);
            _sliderSync.Add((slider, getter));

            _y += 20 + 26 + 6;
        }

        private void AddCheckBox(string text, bool initial, Action<bool> setter, Func<bool> getter)
        {
            var box = new ToggleSwitch
            {
                Text = text,
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 9f),
                BackColor = Bg,
                Checked = initial,
                Size = new Size(ContentWidth, 24),
                Location = new Point(LeftMargin, _y),
                AccentColor = Accent
            };

            box.CheckedChanged += (s, e) =>
            {
                if (_suppress) return;
                setter(box.Checked);
                RaiseChanged();
            };

            _content.Controls.Add(box);
            _checkSync.Add((box, getter));

            _y += 32;
        }

        private void SyncFromSettings()
        {
            _suppress = true;
            foreach (var (slider, get) in _sliderSync)
                slider.Value = get();
            foreach (var (box, get) in _checkSync)
                box.Checked = get();
            foreach (var (box, get) in _textSync)
                box.Text = get() ?? string.Empty;
            _suppress = false;
        }

        private void RaiseChanged()
        {
            _settings.Sanitize();
            SettingsChanged?.Invoke(_settings);
        }
    }
}
