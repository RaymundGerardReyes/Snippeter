using System.Linq;
using System.Text;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IMaskingService
    {
        string GenerateSafePreview(string originalContent, ClassificationResult classification);
    }

    public class MaskingService : IMaskingService
    {
        public string GenerateSafePreview(string originalContent, ClassificationResult classification)
        {
            if (originalContent == null) return null!;
            if (string.IsNullOrEmpty(originalContent)) return originalContent;
            if (classification == null || !classification.IsSensitive || classification.Findings == null || classification.Findings.Count == 0)
                return originalContent;

            // Handle invalid finding boundaries cleanly
            var validFindings = classification.Findings
                .Where(f => f.StartIndex >= 0 && f.StartIndex + f.Length <= originalContent.Length)
                .ToList();

            if (validFindings.Count == 0)
                return originalContent;

            var safeClassification = new ClassificationResult
            {
                IsSensitive = true,
                OverallConfidence = classification.OverallConfidence,
                Findings = validFindings
            };

            return MaskingPolicy.GenerateSafePreview(originalContent, safeClassification).MaskedText;
        }
    }
}
