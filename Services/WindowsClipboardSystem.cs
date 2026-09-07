using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public sealed class WindowsClipboardSystem : IClipboardSystem
    {
        public event EventHandler<ClipboardChangedEventArgs>? ContentChanged;
        public event EventHandler? HistoryChanged;

        public void Start()
        {
            Clipboard.ContentChanged += OnNativeContentChanged;
            Clipboard.HistoryChanged += OnNativeHistoryChanged;
        }

        public void Stop()
        {
            Clipboard.ContentChanged -= OnNativeContentChanged;
            Clipboard.HistoryChanged -= OnNativeHistoryChanged;
        }

        public bool IsHistoryEnabled() => Clipboard.IsHistoryEnabled();

        private async void OnNativeContentChanged(object? sender, object e)
        {
            var payload = new ClipboardPayload();
            var status = ClipboardSnapshotStatus.NoText;

            try
            {
                var dataPackageView = Clipboard.GetContent();

                if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                {
                    payload.ImageStream = await dataPackageView.GetBitmapAsync();
                    payload.ContentType = "Image";
                    status = ClipboardSnapshotStatus.Success;
                }
                else if (dataPackageView.Contains(StandardDataFormats.Html))
                {
                    payload.Html = await dataPackageView.GetHtmlFormatAsync();
                    payload.ContentType = "HTML";
                    status = ClipboardSnapshotStatus.Success;
                }
                else if (dataPackageView.Contains(StandardDataFormats.Rtf))
                {
                    payload.Rtf = await dataPackageView.GetRtfAsync();
                    payload.ContentType = "RTF";
                    status = ClipboardSnapshotStatus.Success;
                }

                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    payload.Text = await dataPackageView.GetTextAsync();
                    if (status == ClipboardSnapshotStatus.NoText) 
                    {
                        payload.ContentType = "Text";
                        status = ClipboardSnapshotStatus.Success;
                    }
                }
            }
            catch
            {
                status = ClipboardSnapshotStatus.ReadFailed;
            }

            ContentChanged?.Invoke(this, new ClipboardChangedEventArgs(payload, status));
        }

        private void OnNativeHistoryChanged(object? sender, ClipboardHistoryChangedEventArgs e)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<ClipboardHistoryMatch?> TryGetLatestHistoryIdAsync()
        {
            if (!IsHistoryEnabled()) return null;

            try
            {
                var historyResult = await Clipboard.GetHistoryItemsAsync();
                if (historyResult.Status == ClipboardHistoryItemsResultStatus.Success)
                {
                    var latestItem = historyResult.Items.FirstOrDefault();
                    if (latestItem != null && latestItem.Content.Contains(StandardDataFormats.Text))
                    {
                        string nativeText = await latestItem.Content.GetTextAsync();
                        return new ClipboardHistoryMatch(latestItem.Id, nativeText);
                    }
                }
            }
            catch { /* Best-effort OS query; ignore locked states */ }
            
            return null;
        }
    }
}
