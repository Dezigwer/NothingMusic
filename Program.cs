#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace NothingMusic
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        private const string AppMutexName = "NothingMusicApp_SingleInstance";

        // Повторный запуск просит уже работающее окно показаться из трея этим сообщением
        internal static readonly uint ShowFromTrayMessage = RegisterWindowMessage("NothingMusic_ShowFromTray");

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, AppMutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    ActivateRunningInstance();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }

        private static void ActivateRunningInstance()
        {
            // Окно, скрытое в трей, не имеет MainWindowHandle, поэтому сначала
            // широковещательно просим его показаться.
            if (ShowFromTrayMessage != 0)
                PostMessage(HWND_BROADCAST, ShowFromTrayMessage, IntPtr.Zero, IntPtr.Zero);

            // Запасной вариант: если сообщение не дошло (или окно свёрнуто),
            // показываем главное окно процесса напрямую.
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id == current.Id)
                    continue;

                IntPtr handle = FindLargestWindow(process.Id);
                if (handle == IntPtr.Zero)
                    continue;

                ShowWindow(handle, IsIconic(handle) ? SW_RESTORE : SW_SHOW);
                SetForegroundWindow(handle);
                return;
            }
        }

        // Скрытая форма не отдаётся через MainWindowHandle, поэтому ищем самое
        // большое окно процесса вручную — это и есть главное окно приложения.
        private static IntPtr FindLargestWindow(int processId)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                GetWindowThreadProcessId(hWnd, out int owner);
                if (owner != processId)
                    return true;

                if (!GetWindowRect(hWnd, out RECT rect))
                    return true;

                long area = Math.Max(0, rect.Right - rect.Left) * (long)Math.Max(0, rect.Bottom - rect.Top);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            return best;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
