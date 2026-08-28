using System;
using System.Collections.Generic;

namespace ClipboardManager.Models
{
    public enum PrivacyCategory
    {
        Normal,
        Email,
        Phone,
        PasswordLike,
        SecretLike,
        TokenLike,
        PublicIp,
        PrivateIp,
        SensitiveUrl
    }

    public class PrivacyFinding
    {
        public PrivacyCategory Category { get; init; }
        public int StartIndex { get; init; }
        public int Length { get; init; }
        public float Confidence { get; init; }
    }

    public class ClassificationResult
    {
        public bool IsSensitive { get; init; }
        public float OverallConfidence { get; init; }
        public IReadOnlyList<PrivacyFinding> Findings { get; init; } = Array.Empty<PrivacyFinding>();
    }
}
