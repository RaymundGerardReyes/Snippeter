using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IPrivacyClassifier
    {
        ClassificationResult Analyze(string rawText, PrivacyMaskingSettings? settings = null);
        Task<ClassificationResult> AnalyzeAsync(string rawText, PrivacyMaskingSettings? settings = null, System.Threading.CancellationToken cancellationToken = default);
    }
}
