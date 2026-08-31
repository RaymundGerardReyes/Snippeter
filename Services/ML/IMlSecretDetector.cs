using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services.Ml
{
    public interface IMlSecretDetector
    {
        bool IsModelLoaded { get; }
        string? ModelVersion { get; }
        Task<IReadOnlyList<PrivacyFinding>> DetectAsync(string input, CancellationToken cancellationToken = default);
    }
}
