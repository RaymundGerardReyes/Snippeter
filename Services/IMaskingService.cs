using System;
using System.Linq;
using System.Text;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IMaskingService
    {
        MaskingResult Apply(string rawText, ClassificationResult classification);
        string GenerateSafePreview(string originalContent, ClassificationResult classification);
    }

    public class MaskingService : IMaskingService
    {
        public MaskingResult Apply(string rawText, ClassificationResult classification)
        {
            if (rawText == null)
                return MaskingResult.Failed("Raw text is null.");

            // 1. Safe pass-through ONLY if explicitly marked non-sensitive
            if (classification == null || !classification.IsSensitive)
            {
                return MaskingResult.Succeeded(rawText);
            }

            // 2. Fail-Closed Guards
            if (classification.MaskingPlan == null || classification.MaskingPlan.Count == 0)
            {
                return MaskingResult.Failed("Sensitive payload must contain an actionable MaskingPlan.");
            }

            var builder = new StringBuilder();
            int currentIndex = 0;
            var sortedSpans = classification.MaskingPlan.OrderBy(s => s.Start).ToList();

            // 3. Coordinate-Based Transformation
            foreach (var span in sortedSpans)
            {
                if (span.Start < currentIndex || span.Start < 0 || span.Length <= 0 || span.Start + span.Length > rawText.Length)
                {
                    return MaskingResult.Failed("Masking plan contains invalid or unsorted/overlapping spans.");
                }

                // Append preserved text before the secret
                builder.Append(rawText.Substring(currentIndex, span.Start - currentIndex));

                int maskLen = span.Mode == MaskingMode.Full 
                    ? span.Length 
                    : (span.Length <= 4 ? span.Length : (int)Math.Ceiling(span.Length * 0.75));

                builder.Append(new string('*', maskLen));
                
                int exposedLen = span.Length - maskLen;
                if (exposedLen > 0)
                {
                    builder.Append(rawText.Substring(span.Start + maskLen, exposedLen));
                }

                currentIndex = span.Start + span.Length;
            }

            if (currentIndex < rawText.Length)
            {
                builder.Append(rawText.Substring(currentIndex));
            }

            string safeText = builder.ToString();

            // 4. Strict Coordinate Output Validation
            if (safeText.Length != rawText.Length)
            {
                return MaskingResult.Failed("Output validation failed: Length was not preserved.");
            }

            foreach (var span in sortedSpans.Where(s => s.Mode == MaskingMode.Full))
            {
                string maskedSegment = safeText.Substring(span.Start, span.Length);
                if (maskedSegment.Any(c => c != '*'))
                {
                    return MaskingResult.Failed("Output validation failed: Full mask span was not completely redacted.");
                }
            }

            return MaskingResult.Succeeded(safeText);
        }

        public string GenerateSafePreview(string originalContent, ClassificationResult classification)
        {
            if (originalContent == null) return null!;
            if (string.IsNullOrEmpty(originalContent)) return originalContent;
            if (classification == null || !classification.IsSensitive) return originalContent;

            var result = Apply(originalContent, classification);
            if (result.Success && result.SafeText != null)
            {
                return result.SafeText;
            }

            // Fallback for legacy callers without MaskingPlan
            if (classification != null && classification.Findings != null && classification.Findings.Count > 0)
            {
                return MaskingPolicy.GenerateSafePreview(originalContent, classification).MaskedText;
            }

            return originalContent;
        }
    }
}
