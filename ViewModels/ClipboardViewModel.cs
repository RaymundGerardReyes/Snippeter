using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ClipboardManager.Models;
using ClipboardManager.Services;

namespace ClipboardManager.ViewModels
{
    public class ClipboardViewModel : INotifyPropertyChanged
    {
        private readonly ClipboardService _clipboardService;
        private string _searchQuery = string.Empty;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiThreadDispatcher;

        public ObservableCollection<ClipboardItem> HistoryItems { get; set; } = new();
        
        public ICommand DeleteCommand { get; }
        public ICommand PinCommand { get; }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                _ = RefreshHistoryAsync();
            }
        }

        public ClipboardViewModel()
        {
            _uiThreadDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            _clipboardService = new ClipboardService();
            _clipboardService.ClipboardUpdated += OnClipboardUpdated;
            
            DeleteCommand = new RelayCommand<ClipboardItem>(DeleteItem);
            PinCommand = new RelayCommand<ClipboardItem>(PinItem);

            _clipboardService.StartMonitoring();
            _ = RefreshHistoryAsync();
        }

        private void OnClipboardUpdated(object? sender, EventArgs e)
        {
            // The clipboard event fires on a background thread! We must marshal to the UI thread.
            _uiThreadDispatcher?.TryEnqueue(async () => 
            {
                await RefreshHistoryAsync();
            });
        }

        public async Task RefreshHistoryAsync()
        {
            var allItems = await _clipboardService.GetHistoryAsync();
            HistoryItems.Clear();

            var filteredItems = string.IsNullOrWhiteSpace(SearchQuery) 
                ? allItems 
                : allItems.Where(i => i.TextPreview.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filteredItems.OrderByDescending(i => i.IsPinned).ThenByDescending(i => i.Timestamp))
            {
                HistoryItems.Add(item);
            }
        }

        public void PasteItem(ClipboardItem item)
        {
            _clipboardService.SetActiveClipboardItem(item);
        }

        private void DeleteItem(ClipboardItem? item)
        {
            if (item == null) return;
            _clipboardService.DeleteHistoryItem(item);
            HistoryItems.Remove(item);
        }

        private void PinItem(ClipboardItem? item)
        {
            if (item == null) return;
            item.IsPinned = !item.IsPinned;
            _ = RefreshHistoryAsync();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
