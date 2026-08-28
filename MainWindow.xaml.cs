using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using ClipboardManager.Models;
using ClipboardManager.Helpers;

namespace ClipboardManager
{
    public sealed partial class MainWindow : Window
    {
        public ViewModels.ClipboardViewModel ViewModel { get; } = new ViewModels.ClipboardViewModel();

        private readonly IntPtr _hwnd;
        private readonly NativeMethods.SUBCLASSPROC _subclassDelegate;

        public MainWindow()
        {
            this.InitializeComponent();
            this.RootGrid.DataContext = ViewModel;

            _hwnd = WindowNative.GetWindowHandle(this);
            _subclassDelegate = new NativeMethods.SUBCLASSPROC(WindowSubClass);
            NativeMethods.SetWindowSubclass(_hwnd, _subclassDelegate, (IntPtr)1, 0);
            NativeMethods.RegisterHotKey(_hwnd, 9000, NativeMethods.MOD_CTRL | NativeMethods.MOD_SHIFT, NativeMethods.VK_V);
        }

        private IntPtr WindowSubClass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, uint dwRefData)
        {
            if (uMsg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == 9000)
            {
                this.AppWindow.Show();
                return IntPtr.Zero;
            }
            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private async void OnClipboardItemClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ClipboardItem selectedItem)
            {
                ViewModel.PasteItem(selectedItem);
                this.AppWindow.Hide();
                await Task.Delay(100);
                NativeMethods.SimulatePaste();
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

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            NativeMethods.UnregisterHotKey(_hwnd, 9000);
        }
    }
}
