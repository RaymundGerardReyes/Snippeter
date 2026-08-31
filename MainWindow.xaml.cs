using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private readonly IGlobalHotkeyService _hotkeyService;

        public MainWindow(ClipboardViewModel viewModel, IGlobalHotkeyService hotkeyService)
        {
            this.InitializeComponent();
            
            ViewModel = viewModel;
            this.RootGrid.DataContext = ViewModel;
            
            _hotkeyService = hotkeyService;
            _hotkeyService.HotkeyPressed += OnGlobalHotkeyPressed;

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            _hotkeyService.Register(hwnd);
        }

        public MainWindow() : this(new ClipboardViewModel(), new Win32GlobalHotkeyService())
        {
        }

        private void OnGlobalHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
        {
            this.AppWindow.Show();
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

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _hotkeyService.Unregister();
        }
    }
}
