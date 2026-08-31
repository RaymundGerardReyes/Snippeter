using System.Threading.Tasks;

namespace ClipboardManager.Services
{
    public interface IClipboardHistoryRestorer
    {
        Task<ClipboardWriteResult> RestoreAsync(string windowsId);
    }
}
