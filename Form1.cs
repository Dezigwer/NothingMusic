#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace NothingMusic
{
    public partial class Form1 : Form
    {
        private WebView2? webView;
        private Panel? titleBar;
        private Label? titleLabel;
        private PictureBox? logoBox;
        private Button? closeButton;
        private Button? minimizeButton;
        private Button? settingsButton;
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private AudioSettingsForm? settingsForm;
        private AudioBridge? audioBridge;
        private SmoothWindow? smoother;
        private bool _wasMinimized;
        private readonly AudioSettings audioSettings = AudioSettings.Load();
        private System.Windows.Forms.Timer? audioSyncTimer;
        
        // Хранилище для загруженных DLL
        private static Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        
        private static string? _tempFolder;
        private static string? _loaderPath;

        public Form1()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            ExtractWebView2Loader();
            InitializeForm();
            CreateTrayIcon();
            CreateTitleBar();
            ApplyWindowChrome();
            InitializeWebView();
        }
        
        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            if (args.Name == null) return null;
            
            string assemblyName = new AssemblyName(args.Name).Name ?? string.Empty;
            
            if (string.IsNullOrEmpty(assemblyName)) return null;
            
            if (_loadedAssemblies.ContainsKey(assemblyName))
                return _loadedAssemblies[assemblyName];
            
            string resourceName = $"NothingMusic.{assemblyName}.dll";
            
            using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                
                byte[] assemblyData = new byte[stream.Length];
                int offset = 0;
                while (offset < assemblyData.Length)
                {
                    int bytesRead = stream.Read(assemblyData, offset, assemblyData.Length - offset);
                    if (bytesRead <= 0) break;
                    offset += bytesRead;
                }
                
                Assembly assembly = Assembly.Load(assemblyData);
                _loadedAssemblies[assemblyName] = assembly;
                return assembly;
            }
        }
        
        private void ExtractWebView2Loader()
        {
            try
            {
                _tempFolder = Path.Combine(AppPaths.Root, "WebView2Loader");
                
                if (!Directory.Exists(_tempFolder))
                    Directory.CreateDirectory(_tempFolder);
                
                _loaderPath = Path.Combine(_tempFolder, "WebView2Loader.dll");
                
                using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NothingMusic.WebView2Loader.dll"))
                {
                    if (stream == null)
                        return;
                    
                    byte[] data = new byte[stream.Length];
                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int bytesRead = stream.Read(data, offset, data.Length - offset);
                        if (bytesRead <= 0) break;
                        offset += bytesRead;
                    }
                    File.WriteAllBytes(_loaderPath, data);
                }
                
                SetDllDirectory(_tempFolder);
                LoadLibrary(_loaderPath);
            }
            catch
            {
                // Пропускаем ошибки извлечения
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int value = DWMWCP_ROUND;
            DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            ApplyTitleBarTheme();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl+, — открыть настройки (на случай системного заголовка без кнопки)
            if (keyData == (Keys.Control | Keys.Oemcomma))
            {
                ToggleSettings();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Применяет оформление окна из настроек: свой заголовок или системный.</summary>
        private void ApplyWindowChrome()
        {
            bool native = audioSettings.NativeTitleBar;
            string title = audioSettings.WindowTitle ?? string.Empty;

            if (titleBar != null)
                titleBar.Visible = true;

            if (titleLabel != null)
                titleLabel.Text = title;

            this.Text = title;

            var style = native ? FormBorderStyle.Sizable : FormBorderStyle.None;
            if (FormBorderStyle != style)
                FormBorderStyle = style;

            LayoutTitleBar();
            ApplyTitleBarTheme();
        }

        /// <summary>Раскладка кнопок заголовка: в системном режиме остаётся только шестерёнка.</summary>
        private void LayoutTitleBar()
        {
            bool native = audioSettings.NativeTitleBar;
            int right = ClientSize.Width;

            if (closeButton != null)
            {
                closeButton.Visible = !native;
                closeButton.Location = new Point(right - 32, 3);
            }

            if (minimizeButton != null)
            {
                minimizeButton.Visible = !native;
                minimizeButton.Location = new Point(right - 60, 3);
            }

            if (settingsButton != null)
            {
                settingsButton.Visible = true;
                settingsButton.Location = new Point(right - (native ? 36 : 88), 3);
            }

            if (logoBox != null)
                logoBox.Visible = !native;

            if (titleLabel != null)
                titleLabel.Visible = !native;
        }

        /// <summary>Тёмная системная рамка/заголовок средствами DWM.</summary>
        private void ApplyTitleBarTheme()
        {
            if (!IsHandleCreated) return;

            try
            {
                int dark = audioSettings.DarkTitleBar ? 1 : 0;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));

                int caption = ToColorRef(audioSettings.DarkTitleBar
                    ? Color.FromArgb(32, 32, 32)
                    : Color.FromArgb(243, 243, 243));
                int text = ToColorRef(audioSettings.DarkTitleBar
                    ? Color.FromArgb(240, 240, 240)
                    : Color.FromArgb(24, 24, 24));

                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
            catch
            {
                // старая Windows — просто стандартная рамка
            }
        }

        private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

        private void InitializeForm()
        {
            this.Text = "NothingMusic";
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(0, 0, 0);
            this.MinimumSize = new Size(800, 600);
            
            this.FormClosing += Form1_FormClosing;

            smoother = new SmoothWindow(this);
            if (WindowsEffects.AnimationsEnabled)
                this.Opacity = 0;
            
            try
            {
                using var stream = GetEmbeddedResource("ico.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }
            catch { }
        }

        private void CreateTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            
            var showItem = new ToolStripMenuItem("Открыть NothingMusic");
            showItem.Click += (s, e) => ShowFromTray();
            showItem.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            
            var settingsItem = new ToolStripMenuItem("Настройки звука");
            settingsItem.Click += (s, e) =>
            {
                ShowFromTray();
                ShowSettings();
            };
            
            var separator = new ToolStripSeparator();
            
            var exitItem = new ToolStripMenuItem("Закрыть");
            exitItem.Click += (s, e) => ExitApplication();
            
            trayMenu.Items.Add(showItem);
            trayMenu.Items.Add(settingsItem);
            trayMenu.Items.Add(separator);
            trayMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon
            {
                Text = "NothingMusic",
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            try
            {
                // Иконка трея: отдельный tray.ico, если он есть, иначе общая иконка приложения
                using var stream = GetEmbeddedResource("tray.ico") ?? GetEmbeddedResource("ico.ico");
                if (stream != null)
                {
                    trayIcon.Icon = new Icon(stream, SystemInformation.SmallIconSize);
                }
            }
            catch { }

            trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        private void HideToTray()
        {
            if (settingsForm != null && settingsForm.Visible)
                settingsForm.HideAnimated();

            if (smoother != null && WindowsEffects.AnimationsEnabled && this.Visible && this.WindowState == FormWindowState.Normal)
            {
                smoother.Animate(0.0, this.Location, 160, () =>
                {
                    this.Hide();
                    this.Opacity = 1;
                    ShowTrayIcon();
                });
                return;
            }

            this.Hide();
            this.Opacity = 1;
            ShowTrayIcon();
        }

        private void ShowTrayIcon()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = true;
                trayIcon.ShowBalloonTip(3000, "NothingMusic", "Приложение свернуто в трей", ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            bool wasMinimized = this.WindowState == FormWindowState.Minimized;

            if (!this.Visible)
                this.Show();

            if (wasMinimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else if (smoother != null && WindowsEffects.AnimationsEnabled && !smoother.Busy)
            {
                this.Opacity = 0;
                smoother.Animate(1.0, this.Location, 220);
            }

            this.Activate();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (smoother != null && WindowsEffects.AnimationsEnabled)
            {
                this.Opacity = 0;
                smoother.Animate(1.0, this.Location, 280);
            }
        }

        private void MinimizeAnimated()
        {
            if (smoother == null || !WindowsEffects.AnimationsEnabled || this.WindowState != FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Minimized;
                return;
            }

            Point origin = this.Location;

            smoother.Animate(0.0, new Point(origin.X, origin.Y + 48), 170, () =>
            {
                this.WindowState = FormWindowState.Minimized;
                this.Opacity = 1;
                this.Location = origin;
            });
        }

        private void ExitApplication()
        {
            audioSyncTimer?.Stop();
            audioSyncTimer?.Dispose();

            smoother?.Dispose();

            if (settingsForm != null)
            {
                settingsForm.Hide();
                settingsForm.Dispose();
                settingsForm = null;
            }

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            
            this.FormClosing -= Form1_FormClosing;
            Application.Exit();
        }

        private void CreateTitleBar()
        {
            titleBar = new Panel
            {
                Height = 30,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(0, 0, 0)
            };

            titleLabel = new Label
            {
                Text = audioSettings.WindowTitle ?? "",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Location = new Point(12, 6),
                AutoSize = true
            };

            titleLabel.ContextMenuStrip = new ContextMenuStrip();
            
            var openInBrowserItem = new ToolStripMenuItem("Открыть в браузере");
            openInBrowserItem.Click += (s, e) =>
            {
                if (webView?.CoreWebView2 != null)
                {
                    OpenInDefaultBrowser(webView.CoreWebView2.Source);
                }
                else
                {
                    MessageBox.Show("WebView еще не инициализирован", "Информация", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            titleLabel.ContextMenuStrip.Items.Add(openInBrowserItem);
            
            var separatorMenu = new ToolStripSeparator();
            titleLabel.ContextMenuStrip.Items.Add(separatorMenu);
            
            var exitMenuItem = new ToolStripMenuItem("Выйти");
            exitMenuItem.Click += (s, e) => ExitApplication();
            titleLabel.ContextMenuStrip.Items.Add(exitMenuItem);

            // то же меню на полосе заголовка — в системном режиме это единственный видимый путь к настройкам
            var settingsMenuItem = new ToolStripMenuItem("Настройки");
            settingsMenuItem.Click += (s, e) => ToggleSettings();
            titleLabel.ContextMenuStrip.Items.Insert(0, new ToolStripSeparator());
            titleLabel.ContextMenuStrip.Items.Insert(0, settingsMenuItem);

            minimizeButton = CreateImageButton("minimize.png", this.Width - 60, 3, 22, 22);
            minimizeButton.Click += (s, e) => MinimizeAnimated();

            settingsButton = CreateImageButton("settings.png", this.Width - 88, 3, 22, 22, Color.FromArgb(46, 46, 46));
            settingsButton.Click += (s, e) => ToggleSettings();

            closeButton = CreateImageButton("close.png", this.Width - 32, 3, 22, 22);
            closeButton.Click += (s, e) => this.Close();
            closeButton.MouseEnter += (s, e) => closeButton.BackColor = Color.FromArgb(232, 17, 35);
            closeButton.MouseLeave += (s, e) => closeButton.BackColor = Color.Transparent;

            var controls = new List<Control> { titleLabel, minimizeButton, settingsButton, closeButton };

            // Необязательный логотип слева от заголовка
            try
            {
                using var logoStream = GetEmbeddedResource("logo.png");
                if (logoStream != null)
                {
                    var logo = new PictureBox
                    {
                        Image = new Bitmap(new Bitmap(logoStream), new Size(20, 20)),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Size = new Size(20, 20),
                        Location = new Point(8, 5),
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand
                    };
                    logo.Click += (s, e) => ToggleSettings();
                    logoBox = logo;
                    titleLabel.Location = new Point(34, 6);
                    controls.Insert(0, logo);
                }
            }
            catch { }

            titleBar.Controls.AddRange(controls.ToArray());
            titleBar.ContextMenuStrip = titleLabel.ContextMenuStrip;

            titleBar.MouseDown += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
            
            titleLabel.MouseDown += (s, e) => 
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            this.Controls.Add(titleBar);
        }

        private Button CreateImageButton(string resourceName, int x, int y, int width, int height, Color? hoverColor = null)
        {
            Button button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                Size = new Size(width, height),
                Location = new Point(x, y),
                Text = "",
                ImageAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };

            Color hover = hoverColor ?? Color.FromArgb(60, 60, 60);

            try
            {
                using var stream = GetEmbeddedResource(resourceName);
                if (stream != null)
                {
                    // иконки рисуем с полями и сохраняем пропорции: глиф не должен упираться в края
                    const int iconSize = 18;
                    const int inset = 2;
                    int content = iconSize - inset * 2;

                    using var source = new Bitmap(stream);

                    double scale = Math.Min(content / (double)source.Width, content / (double)source.Height);
                    int iconW = Math.Max(1, (int)Math.Round(source.Width * scale));
                    int iconH = Math.Max(1, (int)Math.Round(source.Height * scale));

                    var scaled = new Bitmap(iconSize, iconSize);
                    using (var graphics = Graphics.FromImage(scaled))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.DrawImage(source, new Rectangle((iconSize - iconW) / 2, (iconSize - iconH) / 2, iconW, iconH));
                    }

                    button.Image = scaled;
                }
                else
                {
                    button.Text = GetFallbackSymbol(resourceName);
                    button.ForeColor = Color.White;
                    button.Font = new Font("Segoe UI", 8);
                    button.TextAlign = ContentAlignment.MiddleCenter;
                }
            }
            catch
            {
                button.Text = GetFallbackSymbol(resourceName);
                button.ForeColor = Color.White;
                button.Font = new Font("Segoe UI", 8);
                button.TextAlign = ContentAlignment.MiddleCenter;
            }

            button.MouseEnter += (s, e) => 
            {
                if (button != closeButton)
                    button.BackColor = hover;
            };
            button.MouseLeave += (s, e) => 
            {
                if (button != closeButton)
                    button.BackColor = Color.Transparent;
            };

            return button;
        }

        private Stream? GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"NothingMusic.{name}";
            return assembly.GetManifestResourceStream(resourceName);
        }

        private string GetFallbackSymbol(string resourceName)
        {
            string filename = Path.GetFileNameWithoutExtension(resourceName).ToLower();
            return filename switch
            {
                "minimize" => "─",
                "close" => "✕",
                "settings" => "⚙",
                _ => "●"
            };
        }

        private async void InitializeWebView()
        {
            string userDataFolder = Path.Combine(AppPaths.Root, "WebView2");

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder
                );

                webView = new WebView2
                {
                    Dock = DockStyle.Fill
                };
                
                this.Controls.Add(webView);
                webView.BringToFront();
                
                await webView.EnsureCoreWebView2Async(env);
                
                webView.CoreWebView2.NewWindowRequested += WebView_NewWindowRequested;
                webView.CoreWebView2.NavigationStarting += WebView_NavigationStarting;

                // Звуковые эффекты: скрипт + повторная отправка настроек после каждой навигации
                audioBridge = new AudioBridge(webView.CoreWebView2);
                await audioBridge.InstallAsync();
                await audioBridge.PushAsync(audioSettings);

                webView.CoreWebView2.NavigationCompleted += async (s, e) =>
                {
                    // даём странице чуть-чуть времени создать <video>
                    await Task.Delay(1200);
                    if (audioBridge != null)
                        await audioBridge.PushAsync(audioSettings);
                };

                // подстраховка: раз в 5 секунд напоминаем настройки (SPA-переходы, перезапуск плеера)
                audioSyncTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                audioSyncTimer.Tick += async (s, e) =>
                {
                    if (audioBridge != null)
                        await audioBridge.PushAsync(audioSettings);
                };
                audioSyncTimer.Start();

                webView.CoreWebView2.Navigate("https://music.youtube.com");
            }
            catch (Exception ex)
            {
                var result = MessageBox.Show(
                    $"Для работы программы требуется WebView2 Runtime.\n\nСкачать и установить сейчас?\n\nОшибка: {ex.Message}", 
                    "NothingMusic", 
                    MessageBoxButtons.YesNo, 
                    MessageBoxIcon.Error);
                    
                if (result == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                }
                Application.Exit();
            }
        }

        private void OpenInDefaultBrowser(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось открыть браузер: {ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void WebView_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            OpenInDefaultBrowser(e.Uri);
        }

        private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.Uri.Contains("music.youtube.com") && 
                !e.Uri.Contains("youtube.com") &&
                !e.Uri.Contains("accounts.google.com") &&
                !e.Uri.Contains("google.com"))
            {
                e.Cancel = true;
                OpenInDefaultBrowser(e.Uri);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTitleBar();

            if (this.WindowState == FormWindowState.Minimized)
            {
                _wasMinimized = true;
            }
            else if (this.WindowState == FormWindowState.Normal && _wasMinimized)
            {
                _wasMinimized = false;
                if (smoother != null && WindowsEffects.AnimationsEnabled)
                {
                    Point target = this.Location;
                    smoother.Stop();
                    this.Opacity = 0;
                    smoother.Animate(1.0, target, 240);
                }
            }
        }

        private void ToggleSettings()
        {
            if (settingsForm != null && settingsForm.Visible)
            {
                settingsForm.HideAnimated();
                return;
            }
            ShowSettings();
        }

        private void ShowSettings()
        {
            if (settingsForm == null)
            {
                settingsForm = new AudioSettingsForm(audioSettings);
                settingsForm.SettingsChanged += OnAudioSettingsChanged;
                settingsForm.Owner = this;
            }

            var screen = Screen.FromControl(this).WorkingArea;
            int x = this.WindowState == FormWindowState.Normal
                ? this.Right - settingsForm.Width - 16
                : screen.Right - settingsForm.Width - 24;
            int y = this.WindowState == FormWindowState.Normal
                ? this.Top + 40
                : screen.Top + 60;

            x = Math.Max(screen.Left + 8, Math.Min(x, screen.Right - settingsForm.Width - 8));
            y = Math.Max(screen.Top + 8, Math.Min(y, screen.Bottom - settingsForm.Height - 8));

            if (!WindowsEffects.AnimationsEnabled)
            {
                settingsForm.Location = new Point(x, y);
                settingsForm.Show(this);
                settingsForm.BringToFront();
                return;
            }

            settingsForm.ShowAnimated(new Point(x, y));
            settingsForm.BringToFront();
        }

        private async void OnAudioSettingsChanged(AudioSettings settings)
        {
            settings.Save();
            ApplyWindowChrome();

            if (audioBridge != null)
                await audioBridge.PushAsync(settings);
        }
    }
}
