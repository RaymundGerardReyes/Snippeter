using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models.Ml;

namespace ClipboardManager.Services.Ml
{
    public interface IMlModelLoader
    {
        bool IsModelLoaded { get; }
        ModelLoadStatus Status { get; }
        ModelManifest? Manifest { get; }
        Task LoadAsync(CancellationToken cancellationToken);
    }
}
