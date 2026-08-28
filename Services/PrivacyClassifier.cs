using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public class PrivacyClassifier : IPrivacyClassifier
    {
        public ClassificationResult Classify(string rawText) => Analyze(rawText);

        public ClassificationResult Analyze(string rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return new ClassificationResult { IsSensitive = false };

            var findings = new List<PrivacyFinding>();

            var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            findings.AddRange(FindMatches(rawText, emailRegex, PrivacyCategory.Email));

            var phoneRegex = new Regex(@"(?<!-)\b\+?\d{1,3}?[-.\s]?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b");
            findings.AddRange(FindMatches(rawText, phoneRegex, PrivacyCategory.Phone));

            var tokenRegex = new Regex(@"ey[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+");
            findings.AddRange(FindMatches(rawText, tokenRegex, PrivacyCategory.TokenLike));
            
            var privateIpRegex = new Regex(@"\b(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|172\.(1[6-9]|2\d|3[0-1])\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3})\b");
            findings.AddRange(FindMatches(rawText, privateIpRegex, PrivacyCategory.PrivateIp));

            var passwordRegex = new Regex(@"(?i)(?:password|pwd|secret|token|key)\s*[:=]\s*(\S+)|(?i)bearer\s+(\S+)");
            var pwdMatches = passwordRegex.Matches(rawText);
            foreach (Match match in pwdMatches)
            {
                var targetGroup = match.Groups[1].Success ? match.Groups[1] : match.Groups[2];
                if (targetGroup.Success)
                {
                    findings.Add(new PrivacyFinding
                    {
                        Category = PrivacyCategory.PasswordLike,
                        StartIndex = targetGroup.Index,
                        Length = targetGroup.Length,
                        Confidence = 0.95f
                    });
                }
            }

            var distinctFindings = new List<PrivacyFinding>();
            foreach (var f in findings.OrderBy(f => f.StartIndex))
            {
                if (!distinctFindings.Any(df => f.StartIndex < df.StartIndex + df.Length && f.StartIndex + f.Length > df.StartIndex))
                {
                    distinctFindings.Add(f);
                }
            }

            bool isSensitive = distinctFindings.Count > 0;
            return new ClassificationResult
            {
                IsSensitive = isSensitive,
                OverallConfidence = isSensitive ? 1.0f : 0f,
                Findings = distinctFindings
            };
        }

        private IEnumerable<PrivacyFinding> FindMatches(string text, Regex regex, PrivacyCategory category)
        {
            var matches = regex.Matches(text);
            foreach (Match match in matches)
            {
                int length = match.Value.EndsWith(" ") ? match.Length - 1 : match.Length;
                int startIndex = match.Value.StartsWith(" ") ? match.Index + 1 : match.Index;
                if (match.Value.StartsWith(" ")) length--;

                yield return new PrivacyFinding
                {
                    Category = category,
                    StartIndex = startIndex,
                    Length = length,
                    Confidence = 0.9f
                };
            }
        }
    }
}
