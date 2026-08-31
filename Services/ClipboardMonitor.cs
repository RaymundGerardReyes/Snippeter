using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ClipboardManager.Services
{
    public class ClipboardMonitor : IDisposable
    {
        public event EventHandler? ClipboardUpdated;

        private readonly SemaphoreSlim _processingLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly IClipboardIngestor _ingestor;
        private readonly IReentrancyTracker _tracker;
        private readonly IClipboardSystem _clipboardSystem;

        public ClipboardMonitor(IClipboardIngestor ingestor, IReentrancyTracker tracker, IClipboardSystem clipboardSystem)
        {
            _ingestor = ingestor;
            _tracker = tracker;
            _clipboardSystem = clipboardSystem;
        }

        public void StartMonitoring()
        {
            _clipboardSystem.Start();

            _clipboardSystem.ContentChanged -= OnClipboardContentChanged;
            _clipboardSystem.ContentChanged += OnClipboardContentChanged;

            if (_clipboardSystem.IsHistoryEnabled())
            {
                _clipboardSystem.HistoryChanged -= OnClipboardHistoryChanged;
                _clipboardSystem.HistoryChanged += OnClipboardHistoryChanged;
            }
        }

        public void StopMonitoring()
        {
            _clipboardSystem.Stop();
            _clipboardSystem.ContentChanged -= OnClipboardContentChanged;
            _clipboardSystem.HistoryChanged -= OnClipboardHistoryChanged;
            
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private async void OnClipboardContentChanged(object? sender, ClipboardChangedEventArgs e)
        {
            try
            {
                await _processingLock.WaitAsync(_cts.Token);
                try
                {
                    await ProcessContentChangedAsync(e);
                }
                finally
                {
                    _processingLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Clipboard monitoring was canceled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical failure in clipboard monitor: {ex.Message}");
            }
        }

        private async Task ProcessContentChangedAsync(ClipboardChangedEventArgs e)
        {
            if (e.Status != ClipboardSnapshotStatus.Success || string.IsNullOrWhiteSpace(e.Text) || _tracker.ConsumeExpectedWrite(e.Text))
                return;

            var outcome = await _ingestor.ProcessNewContentAsync(e.Text, async () => 
            {
                var match = await _clipboardSystem.TryGetLatestHistoryIdAsync();
                return match != null && string.Equals(match.Text, e.Text, StringComparison.Ordinal) ? match.Id : null;
            });

            switch (outcome.Result)
            {
                case IngestionResult.Success:
                case IngestionResult.Ignored:
                    break;
                case IngestionResult.MaskingFailed:
                case IngestionResult.ReplacementFailed:
                case IngestionResult.PersistenceFailed:
                    Debug.WriteLine($"Clipboard Ingestion Issue: {outcome.Result}");
                    break;
            }
        }

        private void OnClipboardHistoryChanged(object? sender, EventArgs e)
        {
            ClipboardUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            StopMonitoring();
            _cts.Dispose();
            _processingLock.Dispose();
        }
    }
}
