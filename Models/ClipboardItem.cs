using System;
using System.Collections.Generic;

namespace ClipboardManager.Models
{
    public enum ClipboardProtectionState
    {
        Normal,
        Protected,
        ReplacementFailed,
        Expired
    }

    public sealed class ClipboardItem
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        
        // WindowsId is nullable because programmatically replaced items 
        // may not correspond to an OS-managed history entry.
        public string? WindowsId { get; init; }

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public string ContentType { get; init; } = "Text";

        // Authoritative Security State
        public ClipboardProtectionState ProtectionState { get; init; } = ClipboardProtectionState.Normal;

        // The authoritative content: original text (if Normal) or masked text (if Protected)
        public string SafeText { get; init; } = string.Empty;

        public PrivacyCategory PrimaryCategory { get; init; } = PrivacyCategory.Normal;
        public bool IsPinned { get; set; }
        public DateTimeOffset? ExpiresAt { get; init; }

        public IReadOnlyList<PrivacyFinding> Findings { get; init; } = Array.Empty<PrivacyFinding>();

        // Computed Properties & UI Bindings
        public bool IsProtected => ProtectionState == ClipboardProtectionState.Protected;
        public string TextPreview => SafeText;
        public Microsoft.UI.Xaml.Visibility HasText => ContentType == "Image" ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        public Microsoft.UI.Xaml.Visibility HasImage => ContentType == "Image" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public string FormatBadge => ContentType;
        public DateTimeOffset Timestamp => CreatedAt;
        public string TimestampString => Timestamp.ToLocalTime().ToString("g");
        public Microsoft.UI.Xaml.Visibility PinnedVisibility => IsPinned ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? ImagePreview { get; set; }
    }
}
