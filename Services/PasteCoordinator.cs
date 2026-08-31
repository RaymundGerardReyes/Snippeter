using System;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public enum PasteResult
    {
        Success,
        ClipboardUnavailable,
        ProtectionFailure,
        Expired,
        ItemNotFound
    }

    public class PasteCoordinator : IPasteCoordinator
    {
        private readonly IClipboardWriter _writer;
        private readonly IClipboardHistoryRestorer _restorer;

        public PasteCoordinator(IClipboardWriter writer, IClipboardHistoryRestorer restorer)
        {
            _writer = writer;
            _restorer = restorer;
        }

        public async Task<PasteResult> PasteAsync(ClipboardItem item)
        {
            // 1. Authoritative Expiration Check
            if (item.ProtectionState == ClipboardProtectionState.Expired || 
               (item.ExpiresAt.HasValue && item.ExpiresAt <= DateTimeOffset.UtcNow))
            {
                return PasteResult.Expired;
            }

            // 2. Reject Failed Replacements (Original secret may still be in OS)
            if (item.ProtectionState == ClipboardProtectionState.ReplacementFailed)
            {
                return PasteResult.ProtectionFailure;
            }

            // 3. Protected Route: Push SafeText as a net-new OS package
            if (item.ProtectionState == ClipboardProtectionState.Protected)
            {
                var outcome = _writer.WriteMaskedText(item.SafeText);
                return outcome.Result == ClipboardWriteResult.Success 
                    ? PasteResult.Success 
                    : PasteResult.ClipboardUnavailable;
            }

            // 4. Normal Route: Safely restore native history
            if (item.ProtectionState == ClipboardProtectionState.Normal)
            {
                if (string.IsNullOrWhiteSpace(item.WindowsId))
                {
                    return PasteResult.ItemNotFound;
                }

                var restoreResult = await _restorer.RestoreAsync(item.WindowsId);
                return restoreResult == ClipboardWriteResult.Success 
                    ? PasteResult.Success 
                    : PasteResult.ClipboardUnavailable;
            }

            return PasteResult.ProtectionFailure;
        }
    }
}
