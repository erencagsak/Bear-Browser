using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.ViewModels
{
    public partial class ExtensionsViewModel : ObservableRecipient
    {
        private readonly IExtensionService _extensionService;
        private readonly ICrashReportingService _crashReportingService;

        public ObservableCollection<ExtensionInfo> DisplayedExtensions { get; } = new();

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private bool _isDevModeEnabled;

        public string CountBadgeText => $"{_extensionService.InstalledExtensions.Count} eklenti";
        public bool IsEmpty => _extensionService.InstalledExtensions.Count == 0;

        public ExtensionsViewModel(
            IExtensionService extensionService,
            ICrashReportingService crashReportingService,
            IMessenger messenger) : base(messenger)
        {
            _extensionService = extensionService ?? throw new ArgumentNullException(nameof(extensionService));
            _crashReportingService = crashReportingService ?? throw new ArgumentNullException(nameof(crashReportingService));

            RefreshList();
            _extensionService.InstalledExtensions.CollectionChanged += (s, e) => RefreshList();
        }

        public void RefreshList()
        {
            DisplayedExtensions.Clear();
            string query = SearchQuery?.Trim().ToLowerInvariant() ?? "";
            var source = _extensionService.InstalledExtensions;

            var filtered = string.IsNullOrEmpty(query)
                ? source
                : source.Where(e => (e.Name != null && e.Name.ToLowerInvariant().Contains(query)) ||
                                    (e.Description != null && e.Description.ToLowerInvariant().Contains(query)) ||
                                    (e.Id != null && e.Id.ToLowerInvariant().Contains(query)));

            foreach (var item in filtered)
            {
                DisplayedExtensions.Add(item);
            }

            OnPropertyChanged(nameof(CountBadgeText));
            OnPropertyChanged(nameof(IsEmpty));
        }

        partial void OnSearchQueryChanged(string value) => RefreshList();

        public async Task SyncInstalledExtensionsAsync(CoreWebView2Profile? profile)
        {
            if (profile != null)
            {
                await _extensionService.SyncInstalledExtensionsAsync(profile);
                RefreshList();
            }
        }

        public async Task InstallFromFolderAsync(string folderPath, CoreWebView2Profile? profile)
        {
            try
            {
                await _extensionService.InstallFromFolderAsync(folderPath, profile);
                RefreshList();
            }
            catch (Exception ex)
            {
                _crashReportingService.LogCustomError("Load Unpacked Extension Failed", ex.Message);
            }
        }

        public async Task SetExtensionEnabledAsync(string extId, bool isEnabled, CoreWebView2Profile? profile)
        {
            await _extensionService.SetExtensionEnabledAsync(extId, isEnabled, profile);
        }

        public async Task RemoveExtensionAsync(string extId, CoreWebView2Profile? profile)
        {
            await _extensionService.RemoveExtensionAsync(extId, profile);
            RefreshList();
        }
    }
}
