using System;
using System.Collections.Generic;

namespace ClipboardManager.Models
{
    public enum Severity { Low, Medium, High, Critical }

    public enum PrivacyCategory
    {
        Normal, Email, Phone, PasswordLike, SecretLike, TokenLike, PublicIp, PrivateIp, SensitiveUrl,
        Hostname, Domain, Ipv6, DatabaseUrl, DatabaseCredential, ApiKey, AuthHeader, 
        PrivateKey, HashContext, ConnectionString, CloudCredential, JsonSecret, EnvironmentVariable,
        Username,
        Port,           // URI Port number
        DatabaseName,   // URI/ConnectionString Database Name
        HashId          // Standalone SHA256/MD5/UUID hashes
    }

    public enum FindingSource { Regex, MachineLearning, CustomRule }

    public enum MaskingMode { Preserve, Partial, Full }

    public sealed record MaskSpan
    {
        public int Start { get; }
        public int Length { get; }
        public MaskingMode Mode { get; }

        private MaskSpan(int start, int length, MaskingMode mode)
        {
            Start = start;
            Length = length;
            Mode = mode;
        }

        public static MaskSpan? TryCreate(int start, int length, MaskingMode mode, int maxTextLength)
        {
            if (start < 0 || length <= 0 || start + length > maxTextLength) return null;
            return new MaskSpan(start, length, mode);
        }
    }

    public sealed class PrivacyFinding
    {
        public PrivacyCategory Category { get; init; }
        public int StartIndex { get; init; }
        public int Length { get; init; }
        public float Confidence { get; init; }
        public Severity Severity { get; init; } = Severity.Medium;
        public int? ValueStartIndex { get; init; }
        public int? ValueLength { get; init; }
        public FindingSource Source { get; init; } = FindingSource.Regex;
    }

    public sealed class ClassificationResult
    {
        public bool IsSensitive { get; init; }
        public float OverallConfidence { get; init; }
        public Severity EffectiveSeverity { get; init; } = Severity.Low;
        public Severity HighestSeverity => EffectiveSeverity;
        public IReadOnlyList<PrivacyFinding> Findings { get; init; } = Array.Empty<PrivacyFinding>();
        public IReadOnlyList<MaskSpan> MaskingPlan { get; init; } = Array.Empty<MaskSpan>();
    }
}
