using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        public void StartMonitoring()
        {
            if (Clipboard.IsHistoryEnabled())
            {
                Clipboard.HistoryChanged += OnClipboardHistoryChanged;
            }
        }

        public void StopMonitoring()
        {
            Clipboard.HistoryChanged -= OnClipboardHistoryChanged;
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
                        MaskedPreview = textPreview,
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

        public async void SetActiveClipboardItem(ClipboardItem item)
        {
            var history = await Clipboard.GetHistoryItemsAsync();
            var native = history.Items.FirstOrDefault(x => x.Id == item.WindowsId);
            if (native != null) Clipboard.SetHistoryItemAsContent(native);
        }

        public async void DeleteHistoryItem(ClipboardItem item)
        {
            var history = await Clipboard.GetHistoryItemsAsync();
            var native = history.Items.FirstOrDefault(x => x.Id == item.WindowsId);
            if (native != null) Clipboard.DeleteItemFromHistory(native);
        }
    }
}
