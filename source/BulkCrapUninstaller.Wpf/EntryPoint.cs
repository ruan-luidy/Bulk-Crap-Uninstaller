/*
    Copyright (c) 2026 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Klocman;

namespace BulkCrapUninstaller.Wpf
{
    // Same startup as the WinForms EntryPoint: NBug, log, single instance, settings and culture, then the WPF app
    internal static class EntryPoint
    {
        private const string MutexName = @"Global\BCU-singleinstance";
        private static Mutex _mutex;

        public static bool IsRestarting { get; set; }

        [STAThread]
        public static void Main(string[] args)
        {
            NBugConfigurator.SetupNBug();

            using (LogWriter.StartLogging())
            {
                try
                {
                    Directory.SetCurrentDirectory(Program.AssemblyLocation.FullName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                try
                {
                    // The old dialogs still run in this process
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    System.Windows.Forms.Application.EnableVisualStyles();

                    _mutex = new Mutex(true, MutexName, out var createdNew);
                    if (!createdNew)
                    {
                        _mutex.Dispose();
                        _mutex = null;
                        FocusOtherInstance();
                        return;
                    }

                    Program.PrepareSettings();
                    CultureConfigurator.SetupCulture();

                    var app = new App();
                    app.InitializeComponent();
                    app.Run();
                }
                finally
                {
                    Shutdown();
                }
            }
        }

        private static void Shutdown()
        {
            if (_mutex == null) return;

            if (!IsRestarting && !_mutex.SafeWaitHandle.IsClosed)
                _mutex.ReleaseMutex();
            _mutex.Dispose();

            // Portable copies clean their leftovers from the system, same as the WinForms build
            if (!IsRestarting && !Program.IsInstalled && !Program.EnableDebug)
                Program.StartLogCleaner();
        }

        public static void Restart()
        {
            IsRestarting = true;
            _mutex?.ReleaseMutex();
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
        }

        private static void FocusOtherInstance()
        {
            try
            {
                var me = Process.GetCurrentProcess();
                var other = Process.GetProcessesByName(me.ProcessName).FirstOrDefault(x => x.Id != me.Id);
                if (other != null && other.MainWindowHandle != IntPtr.Zero)
                    SetForegroundWindow(other.MainWindowHandle);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
