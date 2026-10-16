using System;
using Bear_Browser.Models;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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

        public ExtensionsViewModel ViewModel { get; }
        private CoreWebView2Profile? _profile;

        public ExtensionsView()
        {
            this.InitializeComponent();
            ViewModel = App.Services.GetRequiredService<ExtensionsViewModel>();

            ExtensionsItemsControl.ItemsSource = ViewModel.DisplayedExtensions;
            UpdateStatusUI();

            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.CountBadgeText) ||
                    e.PropertyName == nameof(ViewModel.IsEmpty))
                {
                    UpdateStatusUI();
                }
            };
        }

        private void UpdateStatusUI()
        {
            if (ExtensionsCountBadge != null)
            {
                ExtensionsCountBadge.Text = ViewModel.CountBadgeText;
            }

            if (EmptyStateBorder != null)
            {
                EmptyStateBorder.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void SetProfile(CoreWebView2Profile profile)
        {
            _profile = profile;
            _ = ViewModel.SyncInstalledExtensionsAsync(_profile);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox?.Text?.Trim() ?? "";
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
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            IntPtr hwnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                await ViewModel.InstallFromFolderAsync(folder.Path, _profile);
            }
        }

        private async void ExtensionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && toggle.Tag is string extId)
            {
                await ViewModel.SetExtensionEnabledAsync(extId, toggle.IsOn, _profile);
            }
        }

        private async void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string extId)
            {
                await ViewModel.RemoveExtensionAsync(extId, _profile);
            }
        }
    }
}