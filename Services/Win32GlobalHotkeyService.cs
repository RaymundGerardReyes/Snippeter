using System;
using ClipboardManager.Helpers;

namespace ClipboardManager.Services
{
    public class Win32GlobalHotkeyService : IGlobalHotkeyService, IDisposable
    {
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        
        private IntPtr _hwnd;
        private NativeMethods.SUBCLASSPROC? _subclassDelegate;
        private bool _isRegistered;
        private const int HOTKEY_ID = 9000;

        public void Register(IntPtr hwnd)
        {
            if (!_isRegistered && hwnd != IntPtr.Zero)
            {
                _hwnd = hwnd;
                _subclassDelegate = new NativeMethods.SUBCLASSPROC(WindowSubClass);
                NativeMethods.SetWindowSubclass(_hwnd, _subclassDelegate, (IntPtr)1, 0);
                NativeMethods.RegisterHotKey(_hwnd, HOTKEY_ID, NativeMethods.MOD_CTRL | NativeMethods.MOD_SHIFT, NativeMethods.VK_V);
                _isRegistered = true;
            }
        }

        public void Unregister()
        {
            if (_isRegistered && _hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, HOTKEY_ID);
                _isRegistered = false;
            }
        }

        private IntPtr WindowSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData)
        {
            if (uMsg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs());
                return IntPtr.Zero;
            }
            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        public void Dispose()
        {
            Unregister();
        }
    }
}
