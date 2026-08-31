using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ClipboardManager.Helpers;
using ClipboardManager.Models;
using ClipboardManager.Services;
using ClipboardManager.Data;
using ClipboardManager.Search;

namespace ClipboardManager.ViewModels
{
    public class ClipboardViewModel : INotifyPropertyChanged
    {
        private readonly IPasteCoordinator? _pasteCoordinator;
        private readonly IClipboardRepository? _repository;
        private readonly IPasteAction? _pasteAction;
        private readonly SearchQueryParser _searchParser = new();
        private string _searchQuery = string.Empty;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue? _uiThreadDispatcher;

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

        public ClipboardViewModel(IPasteCoordinator pasteCoordinator, IClipboardRepository repository, IPasteAction pasteAction)
        {
            _pasteCoordinator = pasteCoordinator;
            _repository = repository;
            _pasteAction = pasteAction;

            try
            {
                _uiThreadDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                _uiThreadDispatcher = null;
            }

            DeleteCommand = new RelayCommand<ClipboardItem>(DeleteItem);
            PinCommand = new RelayCommand<ClipboardItem>(PinItem);
            
            _ = RefreshHistoryAsync();
        }

        public ClipboardViewModel(IPasteCoordinator pasteCoordinator, IClipboardRepository repository) 
            : this(pasteCoordinator, repository, null!)
        {
        }

        public ClipboardViewModel(IPasteCoordinator pasteCoordinator) 
            : this(pasteCoordinator, null!, null!)
        {
        }

        public ClipboardViewModel()
        {
            try
            {
                _uiThreadDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                _uiThreadDispatcher = null;
            }

            DeleteCommand = new RelayCommand<ClipboardItem>(DeleteItem);
            PinCommand = new RelayCommand<ClipboardItem>(PinItem);
        }

        public async Task<PasteResult> PasteItemAsync(ClipboardItem item)
        {
            if (_pasteCoordinator == null)
            {
                return PasteResult.Success;
            }

            var result = await _pasteCoordinator.PasteAsync(item);
            if (result == PasteResult.Success)
            {
                _pasteAction?.SimulatePaste();
            }

            return result;
        }

        // Call this from MainWindow or App when the monitor raises ClipboardUpdated
        public void NotifyClipboardChanged()
        {
            _uiThreadDispatcher?.TryEnqueue(async () => 
            {
                await RefreshHistoryAsync();
            });
        }

        public async Task RefreshHistoryAsync()
        {
            HistoryItems.Clear();

            if (_repository == null) return;

            var items = string.IsNullOrWhiteSpace(SearchQuery) 
                ? await _repository.GetRecentAsync() 
                : await _repository.SearchAsync(_searchParser.Parse(SearchQuery));

            foreach (var item in items)
            {
                HistoryItems.Add(item);
            }
        }

        private async void DeleteItem(ClipboardItem? item)
        {
            if (item == null) return;
            if (_repository != null)
            {
                await _repository.DeleteAsync(item.Id);
            }
            HistoryItems.Remove(item);
        }

        private async void PinItem(ClipboardItem? item)
        {
            if (item == null) return;
            item.IsPinned = !item.IsPinned;
            if (_repository != null)
            {
                await _repository.SetPinnedAsync(item.Id, item.IsPinned);
            }
            await RefreshHistoryAsync();
        }

        public void PasteItem(ClipboardItem item)
        {
            _ = PasteItemAsync(item);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
