using System;
using System.Linq;
using System.Text;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public static class MaskingPolicy
    {
        public static MaskingResult GenerateSafePreview(string rawText, ClassificationResult classification)
        {
            if (!classification.IsSensitive || classification.Findings.Count == 0)
            {
                return new MaskingResult
                {
                    MaskedText = rawText,
                    MaskedCharacterCount = 0,
                    TotalSensitiveCharacterCount = 0
                };
            }

            int totalSensitiveCharacters = classification.Findings.Sum(f => f.Length);
            int maskedCharacters = 0;

            var sortedFindings = classification.Findings.OrderByDescending(f => f.StartIndex).ToList();
            var builder = new StringBuilder(rawText);

            foreach (var finding in sortedFindings)
            {
                // Enforce the invariant: >= 70% of sensitive material must be concealed.
                // For short strings, mask 100%. Otherwise, calculate 75% to safely clear the 70% bar.
                int charsToMask = finding.Length <= 4 ? finding.Length : (int)Math.Ceiling(finding.Length * 0.75);
                int suffixLength = finding.Length - charsToMask;
                
                string suffix = suffixLength > 0 ? rawText.Substring(finding.StartIndex + charsToMask, suffixLength) : string.Empty;
                string mask = new string('*', charsToMask);
                string maskedSection = mask + suffix;

                builder.Remove(finding.StartIndex, finding.Length);
                builder.Insert(finding.StartIndex, maskedSection);
                
                maskedCharacters += charsToMask;
            }

            return new MaskingResult
            {
                MaskedText = builder.ToString(),
                MaskedCharacterCount = maskedCharacters,
                TotalSensitiveCharacterCount = totalSensitiveCharacters
            };
        }
    }
}
