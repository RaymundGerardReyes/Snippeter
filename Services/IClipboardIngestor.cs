using System;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public enum IngestionResult
    {
        Success,
        Ignored,
        MaskingFailed,
        ReplacementFailed,
        PersistenceFailed
    }

    public sealed record IngestionOutcome(IngestionResult Result, ClipboardItem? Item);

    public interface IClipboardIngestor
    {
        Task<IngestionOutcome> ProcessNewContentAsync(string rawText, Func<Task<string?>>? historyIdFetcher = null, System.Threading.CancellationToken cancellationToken = default);
        Task<IngestionOutcome> ProcessNewContentAsync(string rawText, string? windowsId, System.Threading.CancellationToken cancellationToken = default);
    }
}
