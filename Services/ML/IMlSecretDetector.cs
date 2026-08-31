using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services.Ml
{
    public interface IMlSecretDetector
    {
        bool IsAvailable { get; }
        Task<IReadOnlyList<PrivacyFinding>> DetectAsync(string input, TimeSpan timeBudget, CancellationToken cancellationToken);
    }
}
