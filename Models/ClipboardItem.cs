using System;
using System.Collections.Generic;

namespace ClipboardManager.Models
{
    public enum StorageState
    {
        WindowsOnly,
        Protected
    }

    public class ClipboardItem
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string WindowsId { get; init; } = string.Empty; 
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
        public string ContentType { get; init; } = "Text";
        public bool IsSensitive { get; init; }
        public bool IsPinned { get; set; }
        public string MaskedPreview { get; init; } = string.Empty;
        public PrivacyCategory PrimaryCategory { get; init; } = PrivacyCategory.Normal;
        public StorageState StorageState { get; init; } = StorageState.WindowsOnly;
        public DateTimeOffset? ExpiresAt { get; init; }
        
        public IReadOnlyList<PrivacyFinding> Findings { get; init; } = Array.Empty<PrivacyFinding>();

        // UI Binding Properties
        public string TextPreview => MaskedPreview;
        public Microsoft.UI.Xaml.Visibility HasText => ContentType == "Image" ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        public Microsoft.UI.Xaml.Visibility HasImage => ContentType == "Image" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public string FormatBadge => ContentType;
        public DateTimeOffset Timestamp => CreatedAt;
        public string TimestampString => Timestamp.ToString("g");
        public Microsoft.UI.Xaml.Visibility PinnedVisibility => IsPinned ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? ImagePreview { get; set; }
    }
}
