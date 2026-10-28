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

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const string AppMutexName = "NothingMusicApp_SingleInstance";

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, AppMutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    // Заголовок окна настраивается пользователем, поэтому ищем процесс, а не текст
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
            Process current = Process.GetCurrentProcess();

            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id == current.Id)
                    continue;

                IntPtr handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    continue;

                ShowWindow(handle, IsIconic(handle) ? SW_RESTORE : SW_SHOW);
                SetForegroundWindow(handle);
                break;
            }
        }
    }
}
