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
            if (string.IsNullOrEmpty(rawText) || classification == null || !classification.IsSensitive || classification.Findings == null || classification.Findings.Count == 0)
            {
                return new MaskingResult
                {
                    Success = true,
                    SafeText = rawText ?? string.Empty,
                    MaskedText = rawText ?? string.Empty,
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
                if (finding.StartIndex < 0 || finding.Length <= 0 || finding.StartIndex >= rawText.Length || finding.StartIndex + finding.Length > rawText.Length)
                    continue;

                int actualLength = finding.Length;
                int charsToMask = actualLength <= 4 ? actualLength : (int)Math.Ceiling(actualLength * 0.75);
                int suffixLength = actualLength - charsToMask;
                
                string suffix = suffixLength > 0 ? rawText.Substring(finding.StartIndex + charsToMask, suffixLength) : string.Empty;
                string mask = new string('*', charsToMask);
                string maskedSection = mask + suffix;

                builder.Remove(finding.StartIndex, actualLength);
                builder.Insert(finding.StartIndex, maskedSection);
                
                maskedCharacters += charsToMask;
            }

            string maskedText = builder.ToString();

            return new MaskingResult
            {
                Success = true,
                SafeText = maskedText,
                MaskedText = maskedText,
                MaskedCharacterCount = maskedCharacters,
                TotalSensitiveCharacterCount = totalSensitiveCharacters
            };
        }
    }
}
