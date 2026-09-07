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
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] OnLaunched entered. args={args}");

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] 1. Initialize SQLite Database...");

                // 1. Initialize SQLite Database
                string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager", "clipboard_history.db");
                new DatabaseInitializer(dbPath).Initialize();
                var repository = new ClipboardRepository(dbPath);

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] 2. Start Background Expiration Sweeper...");
                // 2. Start Background Expiration Sweeper (Step 10)
                _cleanupService = new ExpirationCleanupService(repository);
                _cleanupService.Start(TimeSpan.FromMinutes(1)); // Sweeps every 1 minute

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] 3. Initialize Secure Pipeline Services...");
                // 3. Initialize Secure Pipeline Services
                var mlModelsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager", "Models", "ml");
                var mlModelLoader = new ClipboardManager.Services.Ml.MlModelLoader(mlModelsPath);

                _ = Task.Run(async () => await mlModelLoader.LoadAsync(System.Threading.CancellationToken.None));

                string versionDir = Path.Combine(mlModelsPath, "versions", "1.0.0");
                string vocabPath = Path.Combine(versionDir, "vocab.txt");
                string onnxPath = Path.Combine(versionDir, "secret_pii_detector.onnx");

                var tokenizer = new ClipboardManager.Services.Ml.BertTokenizerService(vocabPath);
                var inferenceRunner = new ClipboardManager.Services.Ml.OnnxInferenceRunner(mlModelLoader, onnxPath);
                var mlDetector = new ClipboardManager.Services.Ml.MlSecretDetector(mlModelLoader, tokenizer, inferenceRunner);

                var privacyClassifier = new PrivacyClassifier(mlDetector);
                var writer = new ClipboardWriter();
                var restorer = new ClipboardHistoryRestorer();
                var pasteCoordinator = new PasteCoordinator(writer, restorer);
                var tracker = new ClipboardReentrancyTracker();

                // Initialize settings persistence and provider
                var settingsRepo = new SqliteSettingsRepository($"Data Source={dbPath}");
                var settingsProvider = new PrivacyMaskingSettingsProvider(settingsRepo);
                var settingsViewModel = new SettingsViewModel(settingsProvider);

                var ingestor = new ClipboardIngestor(
                    repository,
                    privacyClassifier,
                    new MaskingService(),
                    writer,
                    tracker,
                    settingsProvider
                );

                var clipboardSystem = new WindowsClipboardSystem();
                _clipboardMonitor = new ClipboardMonitor(ingestor, tracker, clipboardSystem);

                // 4. Initialize UI
                var pasteAction = new Win32PasteAction();
                var viewModel = new ClipboardViewModel(pasteCoordinator, repository, pasteAction);
                _clipboardMonitor.ClipboardUpdated += (s, e) => viewModel.NotifyClipboardChanged();

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] Starting Clipboard Monitor...");
                _clipboardMonitor.StartMonitoring();

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] Initializing hotkey service...");
                var hotkeyService = new Win32GlobalHotkeyService();

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] Creating MainWindow...");
                m_window = new MainWindow(viewModel, settingsViewModel, hotkeyService);
                
                // 5. Safely teardown background services on close
                m_window.Closed += (s, e) => 
                {
                    _clipboardMonitor.Dispose();
                    _cleanupService.Dispose();
                };

                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] Showing AppWindow...");
                m_window.AppWindow.Show();
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] Calling Activate...");
                m_window.Activate();
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] MainWindow activated successfully. Startup completed.");
            }
            catch (Exception ex)
            {
                File.AppendAllText(_logPath, $"\n[{DateTime.Now}] STARTUP FAILURE in OnLaunched: {ex}");
                LogCrash("OnLaunched", ex);
                throw;
            }
        }
    }
}
