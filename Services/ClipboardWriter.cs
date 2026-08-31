using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ClipboardManager.Services
{
    public enum ClipboardWriteResult
    {
        Success,
        Unavailable,
        Failed,
        Unsupported
    }

    public sealed record ClipboardWriteOutcome(ClipboardWriteResult Result, string? WrittenText);

    public class ClipboardWriter : IClipboardWriter
    {
        public virtual ClipboardWriteOutcome WriteMaskedText(string maskedText)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(maskedText);

                var options = new ClipboardContentOptions
                {
                    IsAllowedInHistory = false, 
                    IsRoamable = false          
                };

                bool success = Clipboard.SetContentWithOptions(package, options);
                return success 
                    ? new ClipboardWriteOutcome(ClipboardWriteResult.Success, maskedText) 
                    : new ClipboardWriteOutcome(ClipboardWriteResult.Unavailable, null);
            }
            catch
            {
                return new ClipboardWriteOutcome(ClipboardWriteResult.Failed, null);
            }
        }
    }
}
