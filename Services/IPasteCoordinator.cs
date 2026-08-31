using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IPasteCoordinator
    {
        Task<PasteResult> PasteAsync(ClipboardItem item);
    }
}
