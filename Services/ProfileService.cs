using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bear_Browser.Data.Implementations;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bear_Browser.Services
{
    public class ProfileService : IProfileService
    {
        private readonly IJsonStorageService _storageService;
        private readonly IBookmarkService _bookmarkService;
        private readonly IHistoryService _historyService;
        private readonly IDownloadService _downloadService;
        private readonly IPasswordService _passwordService;
        private readonly string _profileFile;

        public UserProfile CurrentProfile { get; private set; }

        public event EventHandler<UserProfile>? ProfileUpdated;
        public event EventHandler? BookmarksUpdated;
        public event EventHandler? HistoryUpdated;
        public event EventHandler? DownloadsUpdated;
        public event EventHandler? PasswordsUpdated;

        public List<HistoryItem> History => _historyService.History;
        public List<BookmarkItem> Bookmarks => _bookmarkService.Bookmarks;
        public List<DownloadItem> Downloads => _downloadService.Downloads;
        public List<PasswordItem> Passwords => _passwordService.Passwords;
        public List<string> Folders => _bookmarkService.Folders;

        public ProfileService(
            IJsonStorageService storageService,
            IBookmarkService bookmarkService,
            IHistoryService historyService,
            IDownloadService downloadService,
            IPasswordService passwordService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));

            string baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(baseFolder);
            _profileFile = Path.Combine(baseFolder, "profile.json");

            CurrentProfile = new UserProfile
            {
                ProfileFolderPath = baseFolder
            };

            LoadProfileData();

            // Alt servis olaylarını dışarı aktar
            _bookmarkService.BookmarksUpdated += (s, e) => BookmarksUpdated?.Invoke(this, e);
            _historyService.HistoryUpdated += (s, e) => HistoryUpdated?.Invoke(this, e);
            _downloadService.DownloadsUpdated += (s, e) => DownloadsUpdated?.Invoke(this, e);
            _passwordService.PasswordsUpdated += (s, e) => PasswordsUpdated?.Invoke(this, e);
        }

        public ProfileService() : this(
            new JsonStorageService(),
            new BookmarkService(new JsonStorageService()),
            new HistoryService(new JsonStorageService()),
            new DownloadService(new JsonStorageService()),
            new PasswordService(new JsonStorageService()))
        {
        }

        private void LoadProfileData()
        {
            try
            {
                if (File.Exists(_profileFile))
                {
                    var loadedProfile = _storageService.Load<UserProfile>(_profileFile);
                    if (loadedProfile != null)
                    {
                        CurrentProfile = loadedProfile;
                        CurrentProfile.ProfileFolderPath = Path.GetDirectoryName(_profileFile) ?? "";
                    }
                }
            }
            catch { }
        }

        public async Task UpdateProfileAsync(string newName, string? newSourceImagePath)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            if (!string.IsNullOrWhiteSpace(newSourceImagePath) && File.Exists(newSourceImagePath))
            {
                string extension = Path.GetExtension(newSourceImagePath);
                string newFileName = $"avatar_{DateTime.UtcNow.Ticks}{extension}";
                string destPath = Path.Combine(CurrentProfile.ProfileFolderPath, newFileName);

                try
                {
                    foreach (var old in Directory.GetFiles(CurrentProfile.ProfileFolderPath, "avatar*"))
                    {
                        try { File.Delete(old); } catch { }
                    }
                    File.Copy(newSourceImagePath, destPath, overwrite: true);
                    CurrentProfile.AvatarImagePath = destPath;
                }
                catch { }
            }

            await _storageService.SaveAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        public async Task UpdateProfileAvatarPathAsync(string newName, string avatarPath)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            CurrentProfile.AvatarImagePath = avatarPath;
            await _storageService.SaveAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        public async Task UpdateProfileNameAsync(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            await _storageService.SaveAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        // --- History Delegasyonu ---
        public Task AddHistoryAsync(string title, string url) => _historyService.AddHistoryAsync(title, url);
        public Task RemoveHistoryItemAsync(Guid id) => _historyService.RemoveHistoryItemAsync(id);
        public Task ClearHistoryAsync() => _historyService.ClearHistoryAsync();

        // --- Bookmark Delegasyonu ---
        public Task<BookmarkItem> AddOrUpdateBookmarkAsync(string url, string title, string folder) =>
            _bookmarkService.AddOrUpdateBookmarkAsync(url, title, folder);
        public Task RemoveBookmarkByUrlAsync(string url) => _bookmarkService.RemoveBookmarkByUrlAsync(url);
        public Task RemoveBookmarkAsync(Guid id) => _bookmarkService.RemoveBookmarkAsync(id);
        public Task AddFolderAsync(string folderName) => _bookmarkService.AddFolderAsync(folderName);
        public Task RemoveFolderAsync(string folderName) => _bookmarkService.RemoveFolderAsync(folderName);
        public bool IsBookmarked(string url) => _bookmarkService.IsBookmarked(url);
        public BookmarkItem? GetBookmark(string url) => _bookmarkService.GetBookmark(url);
        public Task ExportBookmarksHtmlAsync(string filePath) => _bookmarkService.ExportBookmarksHtmlAsync(filePath);
        public Task<int> ImportBookmarksHtmlAsync(string filePath) => _bookmarkService.ImportBookmarksHtmlAsync(filePath);

        // --- Download Delegasyonu ---
        public void AddActiveDownload(DownloadItem item) => _downloadService.AddActiveDownload(item);
        public Task CompleteDownloadAsync(DownloadItem item) => _downloadService.CompleteDownloadAsync(item);
        public Task AddDownloadAsync(DownloadItem item) => _downloadService.AddDownloadAsync(item);
        public Task RemoveDownloadAsync(Guid id) => _downloadService.RemoveDownloadAsync(id);
        public Task ClearDownloadsAsync() => _downloadService.ClearDownloadsAsync();

        // --- Password Delegasyonu ---
        public Task AddOrUpdatePasswordAsync(string url, string userName, string password) =>
            _passwordService.AddOrUpdatePasswordAsync(url, userName, password);
        public Task RemovePasswordAsync(Guid id) => _passwordService.RemovePasswordAsync(id);
        public Task ClearPasswordsAsync() => _passwordService.ClearPasswordsAsync();
        public Task ExportPasswordsCsvAsync(string filePath) => _passwordService.ExportPasswordsCsvAsync(filePath);
        public Task<int> ImportPasswordsCsvAsync(string filePath) => _passwordService.ImportPasswordsCsvAsync(filePath);
    }
}