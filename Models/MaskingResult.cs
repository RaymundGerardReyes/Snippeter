namespace ClipboardManager.Models
{
    public sealed class MaskingResult
    {
        public string MaskedText { get; init; } = string.Empty;
        public int MaskedCharacterCount { get; init; }
        public int TotalSensitiveCharacterCount { get; init; }
    }
}
