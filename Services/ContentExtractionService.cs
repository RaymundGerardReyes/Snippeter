using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public class ContentExtractionService
    {
        public async Task<string> ExtractTextAsync(ClipboardHistoryItem item)
        {
            if (item.Content.Contains(StandardDataFormats.Text))
            {
                return await item.Content.GetTextAsync();
            }
            return string.Empty;
        }
    }
}
