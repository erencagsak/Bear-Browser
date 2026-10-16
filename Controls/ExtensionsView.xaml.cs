using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Bear_Browser.Controls
{
    public sealed partial class ExtensionsView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;

        private ObservableCollection<ExtensionInfo> _displayedExtensions = new();
        private CoreWebView2Profile? _profile;

        public ExtensionsView()
        {
            this.InitializeComponent();
            ExtensionsItemsControl.ItemsSource = _displayedExtensions;
            RefreshList();

            ExtensionService.Instance.InstalledExtensions.CollectionChanged += (s, e) => RefreshList();
        }

        public void SetProfile(CoreWebView2Profile profile)
        {
            _profile = profile;
            _ = ExtensionService.Instance.SyncInstalledExtensionsAsync(_profile);
        }

        private void RefreshList()
        {
            string query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
            _displayedExtensions.Clear();

            var source = ExtensionService.Instance.InstalledExtensions;
            var filtered = string.IsNullOrEmpty(query)
                ? source
                : source.Where(e => e.Name.ToLowerInvariant().Contains(query) ||
                                    e.Description.ToLowerInvariant().Contains(query) ||
                                    e.Id.ToLowerInvariant().Contains(query));

            foreach (var item in filtered)
            {
                _displayedExtensions.Add(item);
            }

            int count = source.Count;
            if (ExtensionsCountBadge != null)
            {
                ExtensionsCountBadge.Text = $"{count} eklenti";
            }

            if (EmptyStateBorder != null)
            {
                EmptyStateBorder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
        }

        private void OpenChromeWebStore_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "https://chromewebstore.google.com");
        }

        private void OpenEdgeAddons_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "https://microsoftedge.microsoft.com/addons");
        }

        private void DevModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            LoadUnpackedBtn.Visibility = DevModeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LoadUnpackedBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
                folderPicker.FileTypeFilter.Add("*");

                IntPtr hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await ExtensionService.Instance.InstallFromFolderAsync(folder.Path, _profile);
                    RefreshList();
                }
            }
            catch (Exception ex)
            {
                CrashReportingService.Instance.LogCustomError("Load Unpacked Extension Failed", ex.Message);
            }
        }

        private async void ExtensionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && toggle.Tag is string extId)
            {
                await ExtensionService.Instance.SetExtensionEnabledAsync(extId, toggle.IsOn, _profile);
            }
        }

        private async void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string extId)
            {
                await ExtensionService.Instance.RemoveExtensionAsync(extId, _profile);
                RefreshList();
            }
        }
    }
}
