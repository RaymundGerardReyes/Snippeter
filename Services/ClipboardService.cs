using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public class ClipboardService
    {
        public event EventHandler? ClipboardUpdated;

        private readonly IPrivacyClassifier _classifier;
        private readonly ClipboardWriter _writer;
        private readonly ClipboardHistoryRestorer _restorer;
        private readonly PasteCoordinator _pasteCoordinator;
        private readonly SemaphoreSlim _clipboardLock = new SemaphoreSlim(1, 1);
        private string? _lastProgrammaticClipboardText;

        public ClipboardService(
            IPrivacyClassifier? classifier = null,
            ClipboardWriter? writer = null,
            ClipboardHistoryRestorer? restorer = null,
            PasteCoordinator? pasteCoordinator = null)
        {
            _classifier = classifier ?? new PrivacyClassifier();
            _writer = writer ?? new ClipboardWriter();
            _restorer = restorer ?? new ClipboardHistoryRestorer();
            _pasteCoordinator = pasteCoordinator ?? new PasteCoordinator(_writer, _restorer);
        }

        public void StartMonitoring()
        {
            if (Clipboard.IsHistoryEnabled())
            {
                Clipboard.HistoryChanged += OnClipboardHistoryChanged;
                Clipboard.ContentChanged += OnClipboardContentChanged;
            }
        }

        public void StopMonitoring()
        {
            Clipboard.HistoryChanged -= OnClipboardHistoryChanged;
            Clipboard.ContentChanged -= OnClipboardContentChanged;
        }

        private async void OnClipboardContentChanged(object? sender, object e)
        {
            await _clipboardLock.WaitAsync();
            try
            {
                await ProcessClipboardChangeAsync();
            }
            finally
            {
                _clipboardLock.Release();
            }
        }

        public async Task ProcessClipboardChangeAsync()
        {
            try
            {
                var dataPackageView = Clipboard.GetContent();

                if (!dataPackageView.Contains(StandardDataFormats.Text))
                    return;

                string rawText = await dataPackageView.GetTextAsync();

                // 1. Deterministic Guard: Exit if OS is notifying us of our own masking action
                if (string.Equals(rawText, _lastProgrammaticClipboardText, StringComparison.Ordinal))
                {
                    _lastProgrammaticClipboardText = null;
                    return;
                }

                // 2. Classification Boundary
                var classification = _classifier.Analyze(rawText);
                if (!classification.IsSensitive)
                {
                    return;
                }

                // 3. Masking Policy Enforcement
                var maskingResult = MaskingPolicy.GenerateSafePreview(rawText, classification);
                string maskedText = maskingResult.MaskedText;

                _lastProgrammaticClipboardText = maskedText;

                // 4. Secure OS Overwrite
                var writeResult = _writer.WriteMaskedText(maskedText);
                if (writeResult.Result != ClipboardWriteResult.Success)
                {
                    Debug.WriteLine($"CRITICAL: OS Clipboard replacement failed with result: {writeResult.Result}");
                    _lastProgrammaticClipboardText = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clipboard processing error: {ex.Message}");
            }
        }

        private void OnClipboardHistoryChanged(object? sender, ClipboardHistoryChangedEventArgs e)
        {
            ClipboardUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task<List<ClipboardItem>> GetHistoryAsync()
        {
            var parsedItems = new List<ClipboardItem>();
            if (!Clipboard.IsHistoryEnabled()) return parsedItems;

            var historyResult = await Clipboard.GetHistoryItemsAsync();
            if (historyResult.Status == ClipboardHistoryItemsResultStatus.Success)
            {
                foreach (var nativeItem in historyResult.Items)
                {
                    string contentType = "Unknown";
                    string textPreview = string.Empty;
                    BitmapImage? imagePreview = null;

                    if (nativeItem.Content.Contains(StandardDataFormats.Bitmap))
                    {
                        contentType = "Image";
                        try
                        {
                            var streamRef = await nativeItem.Content.GetBitmapAsync();
                            using IRandomAccessStream stream = await streamRef.OpenReadAsync();

                            var bitmap = new BitmapImage();
                            await bitmap.SetSourceAsync(stream);
                            imagePreview = bitmap;
                        }
                        catch
                        {
                            textPreview = "[Unreadable Image]";
                        }
                    }
                    else if (nativeItem.Content.Contains(StandardDataFormats.Html))
                    {
                        contentType = "HTML";
                        string rawHtml = await nativeItem.Content.GetHtmlFormatAsync();
                        textPreview = ExtractHtmlFragment(rawHtml);
                    }
                    else if (nativeItem.Content.Contains(StandardDataFormats.Rtf))
                    {
                        contentType = "RTF";
                        if (nativeItem.Content.Contains(StandardDataFormats.Text))
                        {
                            textPreview = await nativeItem.Content.GetTextAsync();
                        }
                        else
                        {
                            textPreview = "[Rich Text Document]";
                        }
                    }
                    else if (nativeItem.Content.Contains(StandardDataFormats.Text))
                    {
                        contentType = "Text";
                        textPreview = await nativeItem.Content.GetTextAsync();
                    }
                    else
                    {
                        contentType = "Unknown";
                        textPreview = "[Unsupported Format]";
                    }

                    var item = new ClipboardItem
                    {
                        WindowsId = nativeItem.Id,
                        CreatedAt = nativeItem.Timestamp,
                        ContentType = contentType,
                        SafeText = textPreview,
                        ImagePreview = imagePreview
                    };

                    parsedItems.Add(item);
                }
            }
            return parsedItems;
        }

        private string ExtractHtmlFragment(string htmlFormat)
        {
            var match = Regex.Match(htmlFormat, @"<!--StartFragment-->(.*?)<!--EndFragment-->", RegexOptions.Singleline);
            if (match.Success)
            {
                string fragment = match.Groups[1].Value;
                return Regex.Replace(fragment, "<.*?>", string.Empty).Trim();
            }
            return htmlFormat;
        }

        public async Task<PasteResult> SetActiveClipboardItemAsync(ClipboardItem item)
        {
            return await _pasteCoordinator.PasteAsync(item);
        }

        public void SetActiveClipboardItem(ClipboardItem item, ClipboardProtectionState? state = null)
        {
            _ = SetActiveClipboardItemAsync(item);
        }

        public async void DeleteHistoryItem(ClipboardItem item)
        {
            if (string.IsNullOrEmpty(item.WindowsId)) return;
            var history = await Clipboard.GetHistoryItemsAsync();
            var native = history.Items.FirstOrDefault(x => x.Id == item.WindowsId);
            if (native != null) Clipboard.DeleteItemFromHistory(native);
        }
    }
}
