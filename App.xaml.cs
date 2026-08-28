using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClipboardManager
{
    public partial class App : Application
    {
        private Window? m_window;
        private readonly string _logPath;

        public App()
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager");
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, "crash.log");

            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) => LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
            Microsoft.UI.Xaml.Application.Current.UnhandledException += (s, e) => 
            {
                LogCrash("WinUI.UnhandledException", e.Exception);
                e.Handled = true;
            };

            File.AppendAllText(_logPath, $"\n\n[{DateTime.Now}] Application Starting...");

            this.InitializeComponent();
        }

        private void LogCrash(string source, Exception? ex)
        {
            try
            {
                string log = $"\n[{DateTime.Now}] CRASH ({source}): {ex?.Message}\n{ex?.StackTrace}\n";
                if (ex?.InnerException != null)
                {
                    log += $"Inner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";
                }
                File.AppendAllText(_logPath, log);
            }
            catch { }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] OnLaunched triggered. Creating MainWindow...");
                m_window = new MainWindow();
                m_window.Activate();
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] MainWindow activated successfully.");
            }
            catch (Exception ex)
            {
                LogCrash("OnLaunched", ex);
            }
        }
    }
}
