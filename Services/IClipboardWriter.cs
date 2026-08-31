using System.Threading.Tasks;

namespace ClipboardManager.Services
{
    public interface IClipboardWriter
    {
        ClipboardWriteOutcome WriteMaskedText(string maskedText);
    }
}
