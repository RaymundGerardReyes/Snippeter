using Microsoft.UI.Xaml.Controls;
using ClipboardManager.ViewModels;

namespace ClipboardManager
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;
        }
    }
}
