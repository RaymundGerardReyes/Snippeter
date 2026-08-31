namespace ClipboardManager.Models
{
    public sealed record MaskingResult
    {
        public bool Success { get; init; } = true;
        public string? SafeText { get; init; }
        public string? Error { get; init; }

        public string MaskedText { get; init; } = string.Empty;
        public int MaskedCharacterCount { get; init; }
        public int TotalSensitiveCharacterCount { get; init; }

        public MaskingResult() { }

        public MaskingResult(bool success, string? safeText, string? error)
        {
            Success = success;
            SafeText = safeText;
            Error = error;
            MaskedText = safeText ?? string.Empty;
        }

        public static MaskingResult Succeeded(string safeText) => 
            new MaskingResult(true, safeText, null);

        public static MaskingResult Failed(string error) => 
            new MaskingResult(false, null, error);
    }
}
