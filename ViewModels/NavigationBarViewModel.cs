using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Messages;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Bear_Browser.ViewModels
{
    public partial class NavigationBarViewModel : ObservableRecipient
    {
        private readonly IBookmarkService _bookmarkService;
        private readonly IDownloadService _downloadService;
        private readonly IExtensionService _extensionService;
        private readonly ILocalizationService _localizationService;
        private readonly IProfileService _profileService;

        [ObservableProperty]
        private string _currentUrl = "";

        [ObservableProperty]
        private string _currentTitle = "Yeni Sekme";

        [ObservableProperty]
        private bool _canGoBack;

        [ObservableProperty]
        private bool _canGoForward;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isBookmarked;

        [ObservableProperty]
        private double _zoomFactor = 1.0;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private bool _isDownloadCompleted;

        public ObservableCollection<DownloadItem> RecentDownloads { get; } = new();
        public ObservableCollection<ExtensionInfo> InstalledExtensions => _extensionService.InstalledExtensions;
        public UserProfile CurrentProfile => _profileService.CurrentProfile;

        public NavigationBarViewModel(
            IBookmarkService bookmarkService,
            IDownloadService downloadService,
            IExtensionService extensionService,
            ILocalizationService localizationService,
            IProfileService profileService,
            IMessenger messenger) : base(messenger)
        {
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _extensionService = extensionService ?? throw new ArgumentNullException(nameof(extensionService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));

            RefreshRecentDownloads();

            _downloadService.DownloadsUpdated += (s, e) => RefreshRecentDownloads();
            _bookmarkService.BookmarksUpdated += (s, e) => UpdateBookmarkState();
            _profileService.ProfileUpdated += (s, p) => OnPropertyChanged(nameof(CurrentProfile));
        }

        public void RefreshRecentDownloads()
        {
            RecentDownloads.Clear();
            var list = _downloadService.Downloads.Take(6);
            foreach (var item in list)
            {
                RecentDownloads.Add(item);
            }
        }

        public void UpdateUrlAndTitle(string url, string? title = null)
        {
            CurrentUrl = url;
            if (!string.IsNullOrWhiteSpace(title))
            {
                CurrentTitle = title;
            }
            else if (!url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                CurrentTitle = url;
            }
            UpdateBookmarkState();
        }

        public void UpdateBookmarkState()
        {
            if (string.IsNullOrWhiteSpace(CurrentUrl) || CurrentUrl.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                IsBookmarked = false;
                return;
            }

            IsBookmarked = _bookmarkService.IsBookmarked(CurrentUrl);
        }

        public async Task AddOrUpdateBookmarkAsync(string name, string folder)
        {
            if (string.IsNullOrWhiteSpace(CurrentUrl)) return;
            string title = string.IsNullOrWhiteSpace(name) ? CurrentTitle : name;
            await _bookmarkService.AddOrUpdateBookmarkAsync(CurrentUrl, title, folder);
            UpdateBookmarkState();
        }

        public async Task RemoveCurrentBookmarkAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentUrl)) return;
            await _bookmarkService.RemoveBookmarkByUrlAsync(CurrentUrl);
            UpdateBookmarkState();
        }

        public BookmarkItem? GetCurrentBookmark()
        {
            if (string.IsNullOrWhiteSpace(CurrentUrl)) return null;
            return _bookmarkService.GetBookmark(CurrentUrl);
        }

        public async Task RemoveDownloadAsync(Guid id)
        {
            await _downloadService.RemoveDownloadAsync(id);
            RefreshRecentDownloads();
        }

        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Messenger.Send(new NavigateRequestedMessage(url));
        }

        public void GoBack()
        {
            Messenger.Send(new GoBackRequestedMessage());
        }

        public void GoForward()
        {
            Messenger.Send(new GoForwardRequestedMessage());
        }

        public void RefreshOrStop()
        {
            if (IsLoading)
            {
                Messenger.Send(new StopRequestedMessage());
            }
            else
            {
                Messenger.Send(new RefreshRequestedMessage());
            }
        }

        public void NewTab()
        {
            Messenger.Send(new NewTabRequestedMessage());
        }

        public void ToggleFullScreen()
        {
            Messenger.Send(new ToggleFullScreenMessage());
        }
    }
}
