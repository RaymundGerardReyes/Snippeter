using Windows.Storage.Streams;

namespace ClipboardManager.Models
{
    public class ClipboardPayload
    {
        public string? Text { get; set; }
        public string? Rtf { get; set; }
        public string? Html { get; set; }
        public IRandomAccessStreamReference? ImageStream { get; set; }
        public string ContentType { get; set; } = "Text";
    }
}
