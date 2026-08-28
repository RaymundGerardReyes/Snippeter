namespace ClipboardManager.Models
{
    public sealed class ClipboardRecord
    {
        public ClipboardItem Item { get; init; } = null!;
        public SearchProjection Projection { get; init; } = null!;
    }
}
