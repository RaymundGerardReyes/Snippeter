using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ClipboardManager.ViewModels;

namespace ClipboardManager
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        private double _targetVerticalOffset = 0;
        private long _lastScrollTimestamp = 0;

        public SettingsPage(SettingsViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            this.DataContext = ViewModel;

            // Only attach to this Page as fallback for standalone hosting without handledEventsToo
            this.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChanged), handledEventsToo: false);
        }

        public SettingsPage() : this(new SettingsViewModel(new Services.PrivacyMaskingSettingsProvider()))
        {
        }

        private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (e.Handled) return;

            var properties = e.GetCurrentPoint(this).Properties;
            int delta = properties.MouseWheelDelta;
            if (delta != 0)
            {
                ScrollByWheel(delta);
                e.Handled = true;
            }
        }

        public void ScrollByWheel(int delta)
        {
            if (SettingsScrollViewer != null)
            {
                double maxScroll = Math.Max(SettingsScrollViewer.ScrollableHeight, SettingsScrollViewer.ExtentHeight - SettingsScrollViewer.ViewportHeight);
                if (maxScroll > 0)
                {
                    long now = Environment.TickCount64;
                    if (now - _lastScrollTimestamp > 200 || Math.Abs(_targetVerticalOffset - SettingsScrollViewer.VerticalOffset) > 200)
                    {
                        _targetVerticalOffset = SettingsScrollViewer.VerticalOffset;
                    }
                    _lastScrollTimestamp = now;

                    _targetVerticalOffset = Math.Clamp(_targetVerticalOffset - delta, 0, maxScroll);
                    SettingsScrollViewer.ChangeView(null, _targetVerticalOffset, null, disableAnimation: true);
                }
            }
        }
    }
}
