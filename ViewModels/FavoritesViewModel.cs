using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Controls;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Bear_Browser.ViewModels
{
    public partial class FavoritesViewModel : ObservableRecipient
    {
        private readonly IBookmarkService _bookmarkService;

        public ObservableCollection<string> Folders { get; } = new();
        public ObservableCollection<FavoriteDisplayItem> DisplayedItems { get; } = new();

        [ObservableProperty]
        private string _currentFolder = "Sık kullanılanlar çubuğu";

        [ObservableProperty]
        private string _searchQuery = "";

        public bool IsRootFolder => CurrentFolder == "Sık kullanılanlar çubuğu";
        public string BreadcrumbText => IsRootFolder ? "Sık Kullanılanlar" : $"Sık Kullanılanlar > {CurrentFolder}";

        public FavoritesViewModel(IBookmarkService bookmarkService, IMessenger messenger) : base(messenger)
        {
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));

            SyncFolders();
            RefreshList();

            _bookmarkService.BookmarksUpdated += (s, e) =>
            {
                SyncFolders();
                RefreshList();
            };
        }

        public void SyncFolders()
        {
            Folders.Clear();
            foreach (var folder in _bookmarkService.Folders)
            {
                Folders.Add(folder);
            }
        }

        public void RefreshList()
        {
            OnPropertyChanged(nameof(IsRootFolder));
            OnPropertyChanged(nameof(BreadcrumbText));

            DisplayedItems.Clear();
            string query = SearchQuery?.Trim() ?? "";

            if (IsRootFolder && string.IsNullOrEmpty(query))
            {
                var customFolders = _bookmarkService.Folders
                    .Where(f => !f.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var folder in customFolders)
                {
                    int count = _bookmarkService.Bookmarks.Count(b => b.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase));
                    DisplayedItems.Add(new FavoriteDisplayItem
                    {
                        IsFolder = true,
                        Title = folder,
                        Subtitle = $"{count} bağlantı (Klasör)",
                        Glyph = "\uED25",
                        Tag = folder
                    });
                }
            }

            var bookmarks = _bookmarkService.Bookmarks
                .Where(b => b.Folder.Equals(CurrentFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrEmpty(query))
            {
                bookmarks = _bookmarkService.Bookmarks
                    .Where(b => b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                b.Url.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var b in bookmarks)
            {
                DisplayedItems.Add(new FavoriteDisplayItem
                {
                    IsFolder = false,
                    Title = b.Title,
                    Subtitle = b.Url,
                    Glyph = "\uE774",
                    Url = b.Url,
                    Tag = b.Id,
                    FaviconUrl = b.FaviconUrl
                });
            }
        }

        partial void OnCurrentFolderChanged(string value) => RefreshList();
        partial void OnSearchQueryChanged(string value) => RefreshList();

        public void NavigateToRoot()
        {
            CurrentFolder = "Sık kullanılanlar çubuğu";
        }

        public void OpenFolder(string folderName)
        {
            CurrentFolder = folderName;
        }

        public async Task RemoveBookmarkAsync(Guid id)
        {
            await _bookmarkService.RemoveBookmarkAsync(id);
            RefreshList();
        }

        public async Task RemoveFolderAsync(string folderName)
        {
            await _bookmarkService.RemoveFolderAsync(folderName);
            RefreshList();
        }

        public async Task AddFolderAsync(string folderName)
        {
            await _bookmarkService.AddFolderAsync(folderName);
            SyncFolders();
            RefreshList();
        }

        public async Task AddOrUpdateBookmarkAsync(string url, string title, string folder)
        {
            await _bookmarkService.AddOrUpdateBookmarkAsync(url, title, folder);
            RefreshList();
        }

        public async Task<int> ImportBookmarksHtmlAsync(string filePath)
        {
            int count = await _bookmarkService.ImportBookmarksHtmlAsync(filePath);
            SyncFolders();
            RefreshList();
            return count;
        }

        public async Task ExportBookmarksHtmlAsync(string filePath)
        {
            await _bookmarkService.ExportBookmarksHtmlAsync(filePath);
        }
    }
}
