using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace ClipboardManager
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "crash.log");

            try
            {
                File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Program.Main entered.");
                WinRT.ComWrappersSupport.InitializeComWrappers();

                try
                {
                    // Initialize Bootstrap for unpackaged WinUI 3 app
                    Bootstrap.Initialize(0x00010005);
                    File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Bootstrap.Initialize succeeded.");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Bootstrap.Initialize skipped or failed: {ex.Message}");
                }

                Application.Start((p) =>
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] FATAL in Program.Main: {ex}");
            }
        }
    }
}
