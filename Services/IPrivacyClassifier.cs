using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IPrivacyClassifier
    {
        ClassificationResult Analyze(string rawText, PrivacyMaskingSettings? settings = null);
    }
}
