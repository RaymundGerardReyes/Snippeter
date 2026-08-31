using System;

namespace ClipboardManager.Services
{
    public class HotkeyPressedEventArgs : EventArgs { }

    public interface IGlobalHotkeyService
    {
        event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        void Register(IntPtr hwnd);
        void Unregister();
    }
}
