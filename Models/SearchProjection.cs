namespace ClipboardManager.Models
{
    public sealed class SearchProjection
    {
        public string SearchText { get; init; } = string.Empty;
        public bool ContainsSensitiveMaterial { get; init; }
    }
}
