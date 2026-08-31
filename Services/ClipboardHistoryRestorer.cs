using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ClipboardManager.Services
{
    public class ClipboardHistoryRestorer : IClipboardHistoryRestorer
    {
        public virtual async Task<ClipboardWriteResult> RestoreAsync(string windowsId)
        {
            try
            {
                var history = await Clipboard.GetHistoryItemsAsync();
                var nativeItem = history.Items.FirstOrDefault(x => x.Id == windowsId);
                
                if (nativeItem != null)
                {
                    var result = Clipboard.SetHistoryItemAsContent(nativeItem);
                    return result == SetHistoryItemAsContentStatus.Success 
                        ? ClipboardWriteResult.Success 
                        : ClipboardWriteResult.Failed;
                }
                return ClipboardWriteResult.Unavailable;
            }
            catch
            {
                return ClipboardWriteResult.Failed;
            }
        }
    }
}
