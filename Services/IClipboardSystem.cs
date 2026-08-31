using System;
using System.Threading.Tasks;

namespace ClipboardManager.Services
{
    public enum ClipboardSnapshotStatus
    {
        Success,
        NoText,
        ReadFailed
    }

    public sealed class ClipboardChangedEventArgs : EventArgs
    {
        public string? Text { get; }
        public ClipboardSnapshotStatus Status { get; }

        public ClipboardChangedEventArgs(string? text, ClipboardSnapshotStatus status)
        {
            Text = text;
            Status = status;
        }
    }

    public sealed record ClipboardHistoryMatch(string Id, string Text);

    public interface IClipboardSystem
    {
        event EventHandler<ClipboardChangedEventArgs>? ContentChanged;
        event EventHandler? HistoryChanged;

        void Start();
        void Stop();
        bool IsHistoryEnabled();
        Task<ClipboardHistoryMatch?> TryGetLatestHistoryIdAsync();
    }
}
