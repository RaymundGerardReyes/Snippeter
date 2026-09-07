using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using ClipboardManager.Models;
using ClipboardManager.Services;
using ClipboardManager.ViewModels;
using ClipboardManager.Helpers;

namespace ClipboardManager
{
    public sealed partial class MainWindow : Window
    {
        public ClipboardViewModel ViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        private readonly IGlobalHotkeyService _hotkeyService;
        private ScrollViewer? _historyScrollViewer;
        private NativeMethods.SUBCLASSPROC? _mouseWheelSubclassDelegate;
        private readonly System.Collections.Generic.List<IntPtr> _subclassedHwnds = new();
        private double _targetHistoryOffset = 0;
        private long _lastScrollTimestamp = 0;
        private long _lastXamlWheelTick = 0;

        public MainWindow(ClipboardViewModel viewModel, SettingsViewModel settingsViewModel, IGlobalHotkeyService hotkeyService)
        {
            this.InitializeComponent();
            
            ViewModel = viewModel;
            SettingsViewModel = settingsViewModel;
            this.RootGrid.DataContext = ViewModel;

            // Set window size (width 420, height 600)
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(420, 600));
            
            _hotkeyService = hotkeyService;
            _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _hotkeyService.Register(hwnd);

            // Subclass top-level HWND and child HWNDs (e.g. DesktopChildSiteBridge) for Win32 WM_MOUSEWHEEL
            SubclassWindowHierarchy(hwnd);

            this.Closed += Window_Closed;

            HistoryListView.Loaded += (s, e) =>
            {
                _historyScrollViewer = FindScrollViewer(HistoryListView);
                // Re-scan child windows after layout is loaded
                NativeMethods.EnumChildWindows(hwnd, (childHwnd, lParam) =>
                {
                    if (!_subclassedHwnds.Contains(childHwnd))
                    {
                        if (NativeMethods.SetWindowSubclass(childHwnd, _mouseWheelSubclassDelegate!, (IntPtr)2, 0))
                        {
                            _subclassedHwnds.Add(childHwnd);
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            };

            // Centralized mouse wheel routing on root container for 100% window coverage without duplicate events
            this.RootGrid.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), handledEventsToo: true);
        }

        public MainWindow(ClipboardViewModel viewModel, IGlobalHotkeyService hotkeyService) 
            : this(viewModel, new SettingsViewModel(new PrivacyMaskingSettingsProvider()), hotkeyService)
        {
        }

        public MainWindow() 
            : this(new ClipboardViewModel(), new SettingsViewModel(new PrivacyMaskingSettingsProvider()), new Win32GlobalHotkeyService())
        {
        }

        private void OnGlobalHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        {
            ShowHistoryView();
            this.AppWindow.Show();
            this.Activate();
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            NativeMethods.SetForegroundWindow(hwnd);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsView();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowHistoryView();
        }

        public void ShowSettingsView()
        {
            if (SettingsFrame.Content == null)
            {
                SettingsFrame.Content = new SettingsPage(SettingsViewModel);
            }
            HistoryView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
        }

        public void ShowHistoryView()
        {
            SettingsView.Visibility = Visibility.Collapsed;
            HistoryView.Visibility = Visibility.Visible;
            _ = ViewModel.RefreshHistoryAsync();
        }

        private async void OnClipboardItemClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ClipboardItem selectedItem)
            {
                this.AppWindow.Hide();
                await ViewModel.PasteItemAsync(selectedItem);
            }
        }

        private void PinItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is ClipboardItem clipboardItem)
            {
                if (ViewModel.PinCommand.CanExecute(clipboardItem))
                {
                    ViewModel.PinCommand.Execute(clipboardItem);
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.DataContext is ClipboardItem clipboardItem)
            {
                if (ViewModel.DeleteCommand.CanExecute(clipboardItem))
                {
                    ViewModel.DeleteCommand.Execute(clipboardItem);
                }
            }
        }

        private void SubclassWindowHierarchy(IntPtr topHwnd)
        {
            _mouseWheelSubclassDelegate = new NativeMethods.SUBCLASSPROC(WindowMouseWheelSubClass);
            if (NativeMethods.SetWindowSubclass(topHwnd, _mouseWheelSubclassDelegate, (IntPtr)2, 0))
            {
                _subclassedHwnds.Add(topHwnd);
            }

            NativeMethods.EnumChildWindows(topHwnd, (childHwnd, lParam) =>
            {
                if (NativeMethods.SetWindowSubclass(childHwnd, _mouseWheelSubclassDelegate, (IntPtr)2, 0))
                {
                    _subclassedHwnds.Add(childHwnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(null).Properties;
            int delta = properties.MouseWheelDelta;
            if (delta != 0)
            {
                _lastXamlWheelTick = Environment.TickCount64;
                PerformScroll(delta);
                e.Handled = true;
            }
        }

        public void PerformScroll(int wheelDelta)
        {
            if (HistoryView.Visibility == Visibility.Visible)
            {
                var sv = _historyScrollViewer ??= FindScrollViewer(HistoryListView);
                if (sv != null)
                {
                    double maxScroll = Math.Max(sv.ScrollableHeight, sv.ExtentHeight - sv.ViewportHeight);
                    if (maxScroll > 0)
                    {
                        long now = Environment.TickCount64;
                        if (now - _lastScrollTimestamp > 200 || Math.Abs(_targetHistoryOffset - sv.VerticalOffset) > 200)
                        {
                            _targetHistoryOffset = sv.VerticalOffset;
                        }
                        _lastScrollTimestamp = now;

                        _targetHistoryOffset = Math.Clamp(_targetHistoryOffset - wheelDelta, 0, maxScroll);
                        sv.ChangeView(null, _targetHistoryOffset, null, disableAnimation: true);
                    }
                }
            }
            else if (SettingsView.Visibility == Visibility.Visible)
            {
                if (SettingsFrame.Content is SettingsPage sp)
                {
                    sp.ScrollByWheel(wheelDelta);
                }
            }
        }

        private IntPtr WindowMouseWheelSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData)
        {
            if (uMsg == NativeMethods.WM_MOUSEWHEEL)
            {
                short delta = unchecked((short)((long)wParam >> 16));
                long now = Environment.TickCount64;
                // Fallback: only execute via Win32 if XAML routed events did not process within 200ms
                if (now - _lastXamlWheelTick > 200 && now - _lastScrollTimestamp > 200)
                {
                    _lastScrollTimestamp = now;
                    this.DispatcherQueue.TryEnqueue(() => PerformScroll(delta));
                    return IntPtr.Zero;
                }
            }
            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private static ScrollViewer? FindScrollViewer(DependencyObject? root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _hotkeyService.Unregister();
            if (_mouseWheelSubclassDelegate != null)
            {
                foreach (var hwnd in _subclassedHwnds)
                {
                    NativeMethods.RemoveWindowSubclass(hwnd, _mouseWheelSubclassDelegate, (IntPtr)2);
                }
                _subclassedHwnds.Clear();
            }
        }
    }
}
