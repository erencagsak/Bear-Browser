using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Bear_Browser.ViewModels
{
    public partial class DownloadsViewModel : ObservableRecipient
    {
        private readonly IDownloadService _downloadService;

        public ObservableCollection<DownloadItem> DisplayedDownloads { get; } = new();

        [ObservableProperty]
        private string _searchQuery = "";

        public DownloadsViewModel(IDownloadService downloadService, IMessenger messenger) : base(messenger)
        {
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            RefreshList();
            _downloadService.DownloadsUpdated += (s, e) => RefreshList();
        }

        public void RefreshList()
        {
            DisplayedDownloads.Clear();
            var items = _downloadService.Downloads.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                items = items.Where(d => d.FileName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in items)
            {
                DisplayedDownloads.Add(item);
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            RefreshList();
        }

        public async Task RemoveDownloadAsync(Guid id)
        {
            await _downloadService.RemoveDownloadAsync(id);
            RefreshList();
        }

        public async Task ClearDownloadsAsync()
        {
            await _downloadService.ClearDownloadsAsync();
            RefreshList();
        }
    }
}
