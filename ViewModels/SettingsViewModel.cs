using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bear_Browser.Messages;
using Bear_Browser.Models;
using Bear_Browser.Platform.Interfaces;
using Bear_Browser.Services;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.ViewModels
{
    public partial class SettingsViewModel : ObservableRecipient
    {
        private readonly ISettingsService _settingsService;
        private readonly IProfileService _profileService;
        private readonly IBookmarkService _bookmarkService;
        private readonly IHistoryService _historyService;
        private readonly IDownloadService _downloadService;
        private readonly IPasswordService _passwordService;
        private readonly IExtensionService _extensionService;
        private readonly ILocalizationService _localizationService;
        private readonly ICrashReportingService _crashReportingService;
        private readonly IMemoryOptimizerService _memoryOptimizerService;
        private readonly IDefaultBrowserService _defaultBrowserService;

        public ObservableCollection<ExtensionInfo> DisplayedExtensions { get; } = new();
        public ObservableCollection<LanguageItem> LanguagesList { get; } = new();

        [ObservableProperty]
        private string _versionText = "Sürüm 1.0.0 (64 bit)";

        [ObservableProperty]
        private string _archText = "64 bit";

        [ObservableProperty]
        private string _downloadsPath = "";

        [ObservableProperty]
        private string _ramUsageText = "-- MB";

        [ObservableProperty]
        private string _passwordsCountText = "0 adet kayıtlı parola";

        [ObservableProperty]
        private string _bookmarksCountText = "0 adet yer imi";

        [ObservableProperty]
        private string _historyCountText = "0 adet sayfa kaydı";

        public BrowserSettings Settings => _settingsService.Settings;
        public UserProfile CurrentProfile => _profileService.CurrentProfile;

        public SettingsViewModel(
            ISettingsService settingsService,
            IProfileService profileService,
            IBookmarkService bookmarkService,
            IHistoryService historyService,
            IDownloadService downloadService,
            IPasswordService passwordService,
            IExtensionService extensionService,
            ILocalizationService localizationService,
            ICrashReportingService crashReportingService,
            IMemoryOptimizerService memoryOptimizerService,
            IDefaultBrowserService defaultBrowserService,
            IMessenger messenger) : base(messenger)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _extensionService = extensionService ?? throw new ArgumentNullException(nameof(extensionService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _crashReportingService = crashReportingService ?? throw new ArgumentNullException(nameof(crashReportingService));
            _memoryOptimizerService = memoryOptimizerService ?? throw new ArgumentNullException(nameof(memoryOptimizerService));
            _defaultBrowserService = defaultBrowserService ?? throw new ArgumentNullException(nameof(defaultBrowserService));

            LoadSystemInfo();
            RefreshProfileCounts();
            RefreshExtensions();
            RefreshLanguages();

            _extensionService.InstalledExtensions.CollectionChanged += (s, e) => RefreshExtensions();
            _localizationService.LanguageChanged += (s, e) => RefreshLanguages();
            _profileService.ProfileUpdated += (s, p) => RefreshProfileCounts();
            _bookmarkService.BookmarksUpdated += (s, e) => RefreshProfileCounts();
            _historyService.HistoryUpdated += (s, e) => RefreshProfileCounts();
            _passwordService.PasswordsUpdated += (s, e) => RefreshProfileCounts();
        }

        public void LoadSystemInfo()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                string arch = Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit";
                string versionText = version != null
                    ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                    : "1.0.0.0";

                VersionText = $"Sürüm {versionText} ({arch})";
                ArchText = arch;
                DownloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
            catch
            {
                VersionText = "Sürüm 1.0.0 (64 bit)";
            }
        }

        public void UpdateRamUsage()
        {
            try
            {
                long bytes = Process.GetCurrentProcess().WorkingSet64;
                double mb = bytes / (1024.0 * 1024.0);
                RamUsageText = $"{Math.Round(mb)} MB";
            }
            catch
            {
                RamUsageText = "-- MB";
            }
        }

        public void RefreshProfileCounts()
        {
            OnPropertyChanged(nameof(CurrentProfile));
            PasswordsCountText = $"{_passwordService.Passwords.Count} adet kayıtlı parola";
            BookmarksCountText = $"{_bookmarkService.Bookmarks.Count} adet yer imi";
            HistoryCountText = $"{_historyService.History.Count} adet sayfa kaydı";
        }

        public void RefreshExtensions(string? filter = null)
        {
            string query = filter?.Trim().ToLowerInvariant() ?? "";
            DisplayedExtensions.Clear();

            var source = _extensionService.InstalledExtensions;
            var filtered = string.IsNullOrEmpty(query)
                ? source
                : source.Where(e => e.Name.ToLowerInvariant().Contains(query) ||
                                    e.Description.ToLowerInvariant().Contains(query) ||
                                    e.Id.ToLowerInvariant().Contains(query));

            foreach (var item in filtered)
            {
                DisplayedExtensions.Add(item);
            }
        }

        public void RefreshLanguages()
        {
            LanguagesList.Clear();
            var items = _localizationService.GetAvailableLanguages();
            foreach (var item in items)
            {
                LanguagesList.Add(item);
            }
        }

        public void SetLanguage(string langCode)
        {
            _localizationService.SetLanguage(langCode);
            RefreshLanguages();
        }

        public async Task ClearBrowserDataAsync()
        {
            await _historyService.ClearHistoryAsync();
            await _downloadService.ClearDownloadsAsync();
            RefreshProfileCounts();
        }

        public void MakeDefaultBrowser()
        {
            _ = _defaultBrowserService.OpenDefaultBrowserSettingsAsync();
        }

        public void OpenCrashLogs()
        {
            _crashReportingService.OpenLogsFolder();
        }

        public void TriggerTestCrash()
        {
            _crashReportingService.TriggerTestCrash();
        }

        public void OptimizeMemory()
        {
            _memoryOptimizerService.OptimizeMemoryAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            UpdateRamUsage();
        }

        public async Task SetExtensionEnabledAsync(string extId, bool enable, CoreWebView2Profile? profile)
        {
            await _extensionService.SetExtensionEnabledAsync(extId, enable, profile);
        }

        public async Task RemoveExtensionAsync(string extId, CoreWebView2Profile? profile)
        {
            await _extensionService.RemoveExtensionAsync(extId, profile);
            RefreshExtensions();
        }

        public async Task InstallExtensionFromFolderAsync(string folderPath, CoreWebView2Profile? profile)
        {
            await _extensionService.InstallFromFolderAsync(folderPath, profile);
            RefreshExtensions();
        }

        public async Task SaveSettingsAsync()
        {
            await _settingsService.SaveAsync();
        }
    }
}
