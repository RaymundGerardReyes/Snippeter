using System;
using System.Threading.Tasks;
using ClipboardManager.Models;

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
        public ClipboardPayload Payload { get; }
        public ClipboardSnapshotStatus Status { get; }

        public ClipboardChangedEventArgs(ClipboardPayload payload, ClipboardSnapshotStatus status)
        {
            Payload = payload;
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
