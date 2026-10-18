using System;
using System.IO;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Bear_Browser.Controls
{
    public sealed partial class NavigationBar : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler? BackRequested;
        public event EventHandler? ForwardRequested;
        public event EventHandler? RefreshRequested;
        public event EventHandler? StopRequested;
        public event EventHandler? NewTabRequested;
        public event EventHandler<string>? MenuActionTriggered;
        public event EventHandler? ManageProfileRequested;
        public event EventHandler? ZoomInRequested;
        public event EventHandler? ZoomOutRequested;
        public event EventHandler? FullScreenRequested;

        private readonly IBookmarkService _bookmarkService;
        private readonly IDownloadService _downloadService;
        private readonly ILocalizationService _localizationService;
        private readonly IExtensionService _extensionService;

        private bool _keepMenuOpen;
        private bool _isLoading;
        private TextBlock? _cachedZoomLabel;
        private string _currentPageTitle = "Yeni Sekme";

        public bool IsAddressBarFocused => AddressBar.FocusState != FocusState.Unfocused;
        public bool IsMoreMenuOpen => MoreMenuFlyout.IsOpen;
        public NavigationBarViewModel? ViewModel { get; }

        public NavigationBar()
        {
            ViewModel = App.Services.GetService<NavigationBarViewModel>();
            _bookmarkService = App.Services.GetRequiredService<IBookmarkService>();
            _downloadService = App.Services.GetRequiredService<IDownloadService>();
            _localizationService = App.Services.GetRequiredService<ILocalizationService>();
            _extensionService = App.Services.GetRequiredService<IExtensionService>();

            InitializeComponent();

            _bookmarkService.BookmarksUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(UpdateBookmarkState);
            };

            _downloadService.DownloadsUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(RefreshDownloadsFlyoutList);
            };

            ApplyLocalization();
            _localizationService.LanguageChanged += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(ApplyLocalization);
            };
        }

        public void UpdateUrl(string url, string? title = null)
        {
            AddressBar.Text = url;
            if (!string.IsNullOrWhiteSpace(title))
            {
                _currentPageTitle = title;
            }
            else if (!url.StartsWith("bear://"))
            {
                _currentPageTitle = url;
            }
            UpdateBookmarkState();
        }

        public void UpdateProfile(UserProfile profile)
        {
            NavProfilePic.DisplayName = profile.Name;
            FlyoutProfilePic.DisplayName = profile.Name;
            FlyoutProfileName.Text = profile.Name;

            if (!string.IsNullOrEmpty(profile.AvatarImagePath) && File.Exists(profile.AvatarImagePath))
            {
                var bitmap = new BitmapImage(new Uri(profile.AvatarImagePath));
                NavProfilePic.ProfilePicture = bitmap;
                FlyoutProfilePic.ProfilePicture = bitmap;
            }
            else
            {
                NavProfilePic.ProfilePicture = null;
                FlyoutProfilePic.ProfilePicture = null;
            }
        }

        public void UpdateNavigationState(bool canGoBack, bool canGoForward)
        {
            BackButton.IsEnabled = canGoBack;
            ForwardButton.IsEnabled = canGoForward;
        }

        public void UpdateZoomDisplay(double zoomFactor)
        {
            if (_cachedZoomLabel != null)
            {
                _cachedZoomLabel.Text = $"%{Math.Round(zoomFactor * 100)}";
            }
        }

        public void SetLoading(bool isLoading)
        {
            _isLoading = isLoading;
            if (AddressLoadingBar != null)
            {
                AddressLoadingBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            }

            if (RefreshIcon != null && RefreshButton != null)
            {
                if (isLoading)
                {
                    RefreshIcon.Glyph = "\uE711"; // Durdur / Çarpı simgesi
                    ToolTipService.SetToolTip(RefreshButton, _localizationService["Nav_Stop"]);
                }
                else
                {
                    RefreshIcon.Glyph = "\uE72C"; // Yenile simgesi
                    ToolTipService.SetToolTip(RefreshButton, _localizationService["Nav_Refresh"]);
                }
            }
        }

        public void ApplyLocalization()
        {
            var loc = _localizationService;

            if (BackButton != null) ToolTipService.SetToolTip(BackButton, loc["Nav_Back"]);
            if (ForwardButton != null) ToolTipService.SetToolTip(ForwardButton, loc["Nav_Forward"]);
            if (RefreshButton != null) ToolTipService.SetToolTip(RefreshButton, _isLoading ? loc["Nav_Stop"] : loc["Nav_Refresh"]);
            if (AddressBar != null) AddressBar.PlaceholderText = loc["Nav_AddressPlaceholder"];
            if (GoButton != null) ToolTipService.SetToolTip(GoButton, loc["Nav_Go"]);
            if (BookmarkButton != null) ToolTipService.SetToolTip(BookmarkButton, loc["Nav_Bookmark"]);
            if (DownloadButton != null) ToolTipService.SetToolTip(DownloadButton, loc["Nav_Downloads"]);
            if (ExtensionsButton != null) ToolTipService.SetToolTip(ExtensionsButton, loc["Nav_Extensions"]);
            if (ProfileButton != null) ToolTipService.SetToolTip(ProfileButton, loc["Nav_Profile"]);
            if (MoreMenuButton != null) ToolTipService.SetToolTip(MoreMenuButton, loc["Nav_More"]);

            if (BookmarkTitleText != null) BookmarkTitleText.Text = loc["Bookmark_Title"];
            if (BookmarkNameLabel != null) BookmarkNameLabel.Text = loc["Bookmark_Name"];
            if (BookmarkFolderLabel != null) BookmarkFolderLabel.Text = loc["Bookmark_Folder"];
            if (DoneBookmarkBtn != null) DoneBookmarkBtn.Content = loc["Bookmark_Done"];
            if (RemoveBookmarkBtn != null) RemoveBookmarkBtn.Content = loc["Bookmark_Remove"];

            if (FlyoutProfileOfflineText != null) FlyoutProfileOfflineText.Text = loc["Profile_Offline"];
            if (ManageProfileBtn != null) ManageProfileBtn.Content = loc["Profile_Manage"];

            if (FlyoutExtensionsTitleText != null) FlyoutExtensionsTitleText.Text = loc["Extensions_Title"];
            if (EmptyExtensionsText != null) EmptyExtensionsText.Text = loc["Extensions_NoInstalled"];
            if (FlyoutManageExtensionsText != null) FlyoutManageExtensionsText.Text = loc["Menu_ManageExtensions"];
            if (FlyoutChromeStoreText != null) FlyoutChromeStoreText.Text = loc["Menu_ChromeWebStore"];

            if (MenuNewTabItem != null) MenuNewTabItem.Text = loc["Menu_NewTab"];
            if (MenuNewWindowItem != null) MenuNewWindowItem.Text = loc["Menu_NewWindow"];
            if (MenuNewInPrivateItem != null) MenuNewInPrivateItem.Text = loc["Menu_NewInPrivate"];
            if (MenuFavoritesItem != null) MenuFavoritesItem.Text = loc["Menu_Favorites"];
            if (MenuHistoryItem != null) MenuHistoryItem.Text = loc["Menu_History"];
            if (MenuDownloadsItem != null) MenuDownloadsItem.Text = loc["Menu_Downloads"];
            if (MenuExtensionsSubItem != null) MenuExtensionsSubItem.Text = loc["Menu_Extensions"];
            if (MenuManageExtensionsItem != null) MenuManageExtensionsItem.Text = loc["Menu_ManageExtensions"];
            if (MenuOpenExtensionStoreItem != null) MenuOpenExtensionStoreItem.Text = loc["Menu_ChromeWebStore"];
            if (MenuPasswordsItem != null) MenuPasswordsItem.Text = loc["Menu_Passwords"];
            if (MenuDevToolsItem != null) MenuDevToolsItem.Text = loc["Menu_DevTools"];
            if (MenuSettingsItem != null) MenuSettingsItem.Text = loc["Menu_Settings"];
            if (MenuHelpSubItem != null) MenuHelpSubItem.Text = loc["Menu_HelpAndFeedback"];
            if (MenuAboutItem != null) MenuAboutItem.Text = loc["Menu_About"];
            if (MenuFeedbackItem != null) MenuFeedbackItem.Text = loc["Menu_Feedback"];
            if (MenuExitItem != null) MenuExitItem.Text = loc["Menu_CloseBrowser"];
        }

private void UpdateBookmarkState()
        {
            string url = AddressBar.Text.Trim();
            bool isBookmarked = !string.IsNullOrEmpty(url) && _bookmarkService.IsBookmarked(url);

            BookmarkIcon.Glyph = isBookmarked ? "\uE735" : "\uE734";
            BookmarkIcon.Foreground = isBookmarked
                ? new SolidColorBrush(Microsoft.UI.Colors.Gold)
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }

        private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            string url = AddressBar.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("bear://")) return;

            string title = !string.IsNullOrWhiteSpace(_currentPageTitle) ? _currentPageTitle : url;

            if (!_bookmarkService.IsBookmarked(url))
            {
                await _bookmarkService.AddOrUpdateBookmarkAsync(url, title, "Sık kullanılanlar çubuğu");
                UpdateBookmarkState();
            }

            BookmarkEditFlyout.ShowAt(BookmarkButton);
        }

        private void BookmarkEditFlyout_Opening(object? sender, object e)
        {
            string url = AddressBar.Text.Trim();
            var bookmark = _bookmarkService.GetBookmark(url);

            BookmarkNameTextBox.Text = bookmark != null ? bookmark.Title : _currentPageTitle;
            BookmarkFolderComboBox.ItemsSource = _bookmarkService.Folders;
            BookmarkFolderComboBox.SelectedItem = bookmark != null ? bookmark.Folder : "Sık kullanılanlar çubuğu";
        }

        private async void DoneBookmarkBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = AddressBar.Text.Trim();
            string name = BookmarkNameTextBox.Text.Trim();
            string folder = BookmarkFolderComboBox.SelectedItem as string ?? "Sık kullanılanlar çubuğu";

            if (!string.IsNullOrWhiteSpace(url))
            {
                await _bookmarkService.AddOrUpdateBookmarkAsync(url, string.IsNullOrWhiteSpace(name) ? url : name, folder);
                UpdateBookmarkState();
            }

            BookmarkEditFlyout.Hide();
        }

        private async void RemoveBookmarkBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = AddressBar.Text.Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                await _bookmarkService.RemoveBookmarkByUrlAsync(url);
                UpdateBookmarkState();
            }

            BookmarkEditFlyout.Hide();
        }

        private void CloseBookmarkFlyout_Click(object sender, RoutedEventArgs e)
        {
            BookmarkEditFlyout.Hide();
        }

        private void MoreMenuFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            if (_keepMenuOpen)
            {
                args.Cancel = true;
                _keepMenuOpen = false;
            }
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            CacheZoomLabel(sender);
            _keepMenuOpen = true;
            ZoomInRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            CacheZoomLabel(sender);
            _keepMenuOpen = true;
            ZoomOutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FullScreenBtn_Click(object sender, RoutedEventArgs e)
        {
            _keepMenuOpen = false;
            MoreMenuFlyout.Hide();
            FullScreenRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CacheZoomLabel(object sender)
        {
            if (_cachedZoomLabel == null && sender is FrameworkElement element && element.Parent is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is TextBlock tb && tb.Name == "ZoomPercentLabel")
                    {
                        _cachedZoomLabel = tb;
                        break;
                    }
                }
            }
        }

public void SetDownloadStatus(bool isDownloading, bool isCompleted)
        {
            if (isDownloading)
            {
                DownloadProgressRing.Visibility = Visibility.Visible;
                DownloadProgressRing.IsActive = true;
                DownloadCompletedBadge.Visibility = Visibility.Collapsed;
                if (!DownloadsFlyout.IsOpen)
                {
                    DownloadsFlyout.ShowAt(DownloadButton);
                }
            }
            else
            {
                DownloadProgressRing.IsActive = false;
                DownloadProgressRing.Visibility = Visibility.Collapsed;
                DownloadCompletedBadge.Visibility = isCompleted ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadCompletedBadge.Visibility = Visibility.Collapsed;
            DownloadsFlyout.ShowAt(DownloadButton);
        }

        private void DownloadsFlyout_Opening(object? sender, object e)
        {
            RefreshDownloadsFlyoutList();
        }

        private void RefreshDownloadsFlyoutList()
        {
            var list = _downloadService.Downloads.Take(6).ToList();
            RecentDownloadsList.ItemsSource = list;
            EmptyDownloadsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseDownloadsFlyout_Click(object sender, RoutedEventArgs e)
        {
            DownloadsFlyout.Hide();
        }

        private async void DeleteDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                await _downloadService.RemoveDownloadAsync(id);
            }
        }

        private void OpenDownloadsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                System.Diagnostics.Process.Start("explorer.exe", downloadsDir);
            }
            catch { }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string path && File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch { }
        }

        private void ShowInFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string path && File.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    OpenDownloadsFolder_Click(sender, e);
                }
            }
            catch { }
        }

        private void OpenAllDownloadsPage_Click(object sender, RoutedEventArgs e)
        {
            DownloadsFlyout.Hide();
            MenuActionTriggered?.Invoke(this, "Downloads");
        }

        private void ExtensionsButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshExtensionsFlyoutList();
        }

        private void ExtensionsFlyout_Opening(object sender, object e)
        {
            RefreshExtensionsFlyoutList();
        }

        private void RefreshExtensionsFlyoutList()
        {
            var list = _extensionService.InstalledExtensions;
            ExtensionsFlyoutListView.ItemsSource = list;
            EmptyExtensionsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseExtensionsFlyout_Click(object sender, RoutedEventArgs e)
        {
            ExtensionsFlyout.Hide();
        }

        private async void FlyoutExtensionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && toggle.Tag is string extId)
            {
                await _extensionService.SetExtensionEnabledAsync(extId, toggle.IsOn, null);
            }
        }

        private void OpenAllExtensionsPage_Click(object sender, RoutedEventArgs e)
        {
            ExtensionsFlyout.Hide();
            NavigateRequested?.Invoke(this, "bear://settings/extensions");
        }

        private void OpenChromeStore_Click(object sender, RoutedEventArgs e)
        {
            ExtensionsFlyout.Hide();
            NavigateRequested?.Invoke(this, "https://chromewebstore.google.com");
        }

        private void OpenEdgeStore_Click(object sender, RoutedEventArgs e)
        {
            ExtensionsFlyout.Hide();
            NavigateRequested?.Invoke(this, "https://microsoftedge.microsoft.com/addons");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
        private void ForwardButton_Click(object sender, RoutedEventArgs e) => ForwardRequested?.Invoke(this, EventArgs.Empty);
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                StopRequested?.Invoke(this, EventArgs.Empty);
            else
                RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AddressBar_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
                TriggerNavigate();
        }

        private void GoButton_Click(object sender, RoutedEventArgs e) => TriggerNavigate();

        private void TriggerNavigate()
        {
            if (!string.IsNullOrWhiteSpace(AddressBar.Text))
                NavigateRequested?.Invoke(this, AddressBar.Text.Trim());
        }

        private void ManageProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ManageProfileRequested?.Invoke(this, EventArgs.Empty);
        }

        private void MenuItem_Action_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string action)
            {
                if (action == "NewTab")
                {
                    NewTabRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    MenuActionTriggered?.Invoke(this, action);
                }
            }
        }
    }
}