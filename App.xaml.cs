using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;
using ClipboardManager.Data;
using ClipboardManager.Services;
using ClipboardManager.ViewModels;
using ClipboardManager.Helpers;

namespace ClipboardManager
{
    public partial class App : Application
    {
        private Window? m_window;
        private readonly string _logPath;
        private ExpirationCleanupService? _cleanupService;
        private ClipboardMonitor? _clipboardMonitor;

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
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] OnLaunched triggered. Initializing services...");

                // 1. Initialize SQLite Database
                string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager", "clipboard_history.db");
                new DatabaseInitializer(dbPath).Initialize();
                var repository = new ClipboardRepository(dbPath);

                // 2. Start Background Expiration Sweeper (Step 10)
                _cleanupService = new ExpirationCleanupService(repository);
                _cleanupService.Start(TimeSpan.FromMinutes(1)); // Sweeps every 1 minute

                // 3. Initialize Secure Pipeline Services
                var mlModelsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager", "Models", "ml");
                IMlModelLoader mlModelLoader = new MlModelLoader(mlModelsPath);

                var writer = new ClipboardWriter();
                var restorer = new ClipboardHistoryRestorer();
                var pasteCoordinator = new PasteCoordinator(writer, restorer);
                var tracker = new ClipboardReentrancyTracker();
                
                var ingestor = new ClipboardIngestor(
                    repository,
                    new PrivacyClassifier(),
                    new MaskingService(),
                    writer,
                    tracker
                );

                var clipboardSystem = new WindowsClipboardSystem();
                _clipboardMonitor = new ClipboardMonitor(ingestor, tracker, clipboardSystem);

                // 4. Initialize UI
                var pasteAction = new Win32PasteAction();
                var viewModel = new ClipboardViewModel(pasteCoordinator, repository, pasteAction);
                _clipboardMonitor.ClipboardUpdated += (s, e) => viewModel.NotifyClipboardChanged();

                _clipboardMonitor.StartMonitoring();

                var hotkeyService = new Win32GlobalHotkeyService();

                m_window = new MainWindow(viewModel, hotkeyService);
                
                // 5. Safely teardown background services on close
                m_window.Closed += (s, e) => 
                {
                    _clipboardMonitor.Dispose();
                    _cleanupService.Dispose();
                };

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
