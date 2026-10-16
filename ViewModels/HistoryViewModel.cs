using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Bear_Browser.ViewModels
{
    public partial class HistoryViewModel : ObservableRecipient
    {
        private readonly IHistoryService _historyService;

        public List<string> Filters { get; } = new() { "Tüm Geçmiş", "Bugün", "Dün", "Daha Eski" };

        public ObservableCollection<HistoryItem> DisplayedHistory { get; } = new();

        [ObservableProperty]
        private string _selectedFilter = "Tüm Geçmiş";

        [ObservableProperty]
        private string _searchQuery = "";

        public HistoryViewModel(IHistoryService historyService, IMessenger messenger) : base(messenger)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            RefreshList();
            _historyService.HistoryUpdated += (s, e) => RefreshList();
        }

        public void RefreshList()
        {
            DisplayedHistory.Clear();
            var items = _historyService.History.AsEnumerable();

            DateTime today = DateTime.Today;
            if (SelectedFilter == "Bugün")
            {
                items = items.Where(h => h.VisitedAt.Date == today);
            }
            else if (SelectedFilter == "Dün")
            {
                items = items.Where(h => h.VisitedAt.Date == today.AddDays(-1));
            }
            else if (SelectedFilter == "Daha Eski")
            {
                items = items.Where(h => h.VisitedAt.Date < today.AddDays(-1));
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                items = items.Where(h => h.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                         h.Url.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in items)
            {
                DisplayedHistory.Add(item);
            }
        }

        partial void OnSelectedFilterChanged(string value) => RefreshList();
        partial void OnSearchQueryChanged(string value) => RefreshList();

        public async Task RemoveHistoryItemAsync(Guid id)
        {
            await _historyService.RemoveHistoryItemAsync(id);
            RefreshList();
        }

        public async Task ClearHistoryAsync()
        {
            await _historyService.ClearHistoryAsync();
            RefreshList();
        }
    }
}
