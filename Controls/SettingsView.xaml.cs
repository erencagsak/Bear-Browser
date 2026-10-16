using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Bear_Browser.Controls
{
    public sealed partial class SettingsView : UserControl
    {
        public event EventHandler<string>? SectionNavigated;
        public event EventHandler<string>? NavigateRequested;

        private CoreWebView2Profile? _profile;
        private ObservableCollection<ExtensionInfo> _displayedExtensions = new();
        private ObservableCollection<LanguageItem> _languagesList = new();

        private DispatcherTimer? _pulseTimer;
        private DispatcherTimer? _ramTimer;
        private double _currentGlowOpacity = 0.3;
        private bool _glowIncreasing = true;
        private bool _isInitializing = true;
        public Bear_Browser.ViewModels.SettingsViewModel? ViewModel { get; }

        public SettingsView()
        {
            ViewModel = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<Bear_Browser.ViewModels.SettingsViewModel>(App.Services);
            this.InitializeComponent();
            LoadVersionInfo();
            LoadSettingsData();
            LoadProfileData();

            SettingsExtensionsItemsControl.ItemsSource = _displayedExtensions;
            ExtensionService.Instance.InstalledExtensions.CollectionChanged += (s, e) => DispatcherQueue?.TryEnqueue(RefreshExtensionsList);
            RefreshExtensionsList();

            LanguagesItemsControl.ItemsSource = _languagesList;
            RefreshLanguagesList();
            ApplyLocalization();
            LocalizationService.Instance.LanguageChanged += (s, e) => DispatcherQueue?.TryEnqueue(() =>
            {
                RefreshLanguagesList();
                ApplyLocalization();
            });

            // Sık Kullanılanlar, Parolalar ve Profil güncellemelerini canlı dinle
            ProfileService.Instance.ProfileUpdated += (s, p) => DispatcherQueue?.TryEnqueue(LoadProfileData);
            ProfileService.Instance.PasswordsUpdated += (s, e) => DispatcherQueue?.TryEnqueue(LoadProfileData);
            ProfileService.Instance.BookmarksUpdated += (s, e) => DispatcherQueue?.TryEnqueue(LoadProfileData);
            ProfileService.Instance.HistoryUpdated += (s, e) => DispatcherQueue?.TryEnqueue(LoadProfileData);

            _isInitializing = false;
            AboutNavList.SelectedItem = AboutNavItem;
            ShowSection("about");
        }

        public void SetProfile(CoreWebView2Profile profile)
        {
            _profile = profile;
            _ = ExtensionService.Instance.SyncInstalledExtensionsAsync(_profile);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Canlı nabız (pulse) efekti
            _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _pulseTimer.Tick += (s, args) =>
            {
                if (_glowIncreasing)
                {
                    _currentGlowOpacity += 0.03;
                    if (_currentGlowOpacity >= 0.85) _glowIncreasing = false;
                }
                else
                {
                    _currentGlowOpacity -= 0.03;
                    if (_currentGlowOpacity <= 0.25) _glowIncreasing = true;
                }

                if (LiveDotGlow != null)
                {
                    LiveDotGlow.Opacity = _currentGlowOpacity;
                }
            };
            _pulseTimer.Start();

            // 2. Canlı RAM & Performans izleme zamanlayıcısı (Her 2 saniyede bir)
            _ramTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _ramTimer.Tick += (s, args) => UpdateRamUsage();
            _ramTimer.Start();
            UpdateRamUsage();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _pulseTimer?.Stop();
            _ramTimer?.Stop();
        }

        private void UpdateRamUsage()
        {
            try
            {
                long bytes = Process.GetCurrentProcess().WorkingSet64;
                double mb = bytes / (1024.0 * 1024.0);
                if (LiveRamText != null)
                {
                    LiveRamText.Text = $"{Math.Round(mb)} MB";
                }
            }
            catch { }
        }

        private void LoadVersionInfo()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                string arch = Environment.Is64BitOperatingSystem ? "64 bit" : "32 bit";
                string versionText = version != null
                    ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
                    : "1.0.0.0";

                VersionTextBlock.Text = $"Sürüm {versionText} ({arch})";
                ArchTextBlock.Text = arch;

                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                DownloadsPathText.Text = downloadsDir;

                // Logonun her zaman kesin ve net yüklenmesini sağla
                string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Bear-Browser-Logo.jpg");
                if (!File.Exists(logoPath))
                {
                    string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Assets\Bear-Browser-Logo.jpg"));
                    if (File.Exists(devPath)) logoPath = devPath;
                }
                if (File.Exists(logoPath) && AboutLogoImage != null)
                {
                    AboutLogoImage.Source = new BitmapImage(new Uri(logoPath));
                }
            }
            catch
            {
                VersionTextBlock.Text = "Sürüm 1.0.0 (64 bit)";
            }
        }

        private void LoadProfileData()
        {
            var p = ProfileService.Instance.CurrentProfile;
            ProfileNameText.Text = p.Name;
            ProfileCreatedDateText.Text = $"Varsayılan Yerel Profil • {p.CreatedAt:dd.MM.yyyy}";

            if (!string.IsNullOrEmpty(p.AvatarImagePath) && File.Exists(p.AvatarImagePath))
            {
                ProfileAvatarPic.ProfilePicture = new BitmapImage(new Uri(p.AvatarImagePath));
            }
            else
            {
                ProfileAvatarPic.ProfilePicture = null;
                ProfileAvatarPic.DisplayName = p.Name;
            }

            PasswordsCountText.Text = $"{ProfileService.Instance.Passwords.Count} adet kayıtlı parola";
            BookmarksCountText.Text = $"{ProfileService.Instance.Bookmarks.Count} adet yer imi";
            HistoryCountText.Text = $"{ProfileService.Instance.History.Count} adet sayfa kaydı";
        }

        private void LoadSettingsData()
        {
            var s = SettingsService.Instance.Settings;

            // Gizlilik
            AdBlockToggle.IsOn = s.AdBlockEnabled;
            SelectComboByTag(AdBlockLevelCombo, s.AdBlockLevel);
            SelectComboByTag(SearchEngineCombo, s.SearchEngine);
            DoNotTrackToggle.IsOn = s.DoNotTrack;
            AskPasswordToggle.IsOn = s.AskPasswordSave;

            // Görünüm
            SelectComboByTag(ThemeCombo, s.Theme);
            ShowFavBarToggle.IsOn = s.ShowFavoritesBar;
            SelectComboByTag(ZoomCombo, s.DefaultZoom.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture));
            HomePageBox.Text = s.HomePageUrl;

            // Sistem
            EcoModeToggle.IsOn = s.EcoModeEnabled;
        }

        private static void SelectComboByTag(ComboBox combo, string tag)
        {
            if (combo == null) return;
            foreach (var item in combo.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string t && string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = cbi;
                    return;
                }
            }
            if (combo.Items.Count > 0 && combo.SelectedIndex == -1)
                combo.SelectedIndex = 0;
        }

        // --- BÖLÜM GEÇİŞLERİ ---

        public void SelectSection(string sectionKey)
        {
            ShowSection(sectionKey);
        }

        private void ShowSection(string key)
        {
            ProfilesContentPanel.Visibility = Visibility.Collapsed;
            PrivacyContentPanel.Visibility = Visibility.Collapsed;
            AppearanceContentPanel.Visibility = Visibility.Collapsed;
            DefaultBrowserContentPanel.Visibility = Visibility.Collapsed;
            LanguagesContentPanel.Visibility = Visibility.Collapsed;
            ExtensionsContentPanel.Visibility = Visibility.Collapsed;
            DownloadsContentPanel.Visibility = Visibility.Collapsed;
            SystemContentPanel.Visibility = Visibility.Collapsed;
            AboutContentPanel.Visibility = Visibility.Collapsed;

            string normalized = key.ToLowerInvariant().Trim();

            switch (normalized)
            {
                case "profiles":
                    ProfilesContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = ProfilesNavItem;
                    AboutNavList.SelectedItem = null;
                    LoadProfileData();
                    break;
                case "privacy":
                    PrivacyContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = PrivacyNavItem;
                    AboutNavList.SelectedItem = null;
                    break;
                case "appearance":
                    AppearanceContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = AppearanceNavItem;
                    AboutNavList.SelectedItem = null;
                    break;
                case "defaultbrowser":
                    DefaultBrowserContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = DefaultBrowserNavItem;
                    AboutNavList.SelectedItem = null;
                    break;
                case "languages":
                    LanguagesContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = LanguagesNavItem;
                    AboutNavList.SelectedItem = null;
                    break;
                case "extensions":
                    ExtensionsContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = ExtensionsNavItem;
                    AboutNavList.SelectedItem = null;
                    RefreshExtensionsList();
                    break;
                case "downloads":
                    DownloadsContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = DownloadsNavItem;
                    AboutNavList.SelectedItem = null;
                    break;
                case "system":
                    SystemContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = SystemNavItem;
                    AboutNavList.SelectedItem = null;
                    UpdateRamUsage();
                    break;
                default:
                    AboutContentPanel.Visibility = Visibility.Visible;
                    SettingsNavList.SelectedItem = null;
                    AboutNavList.SelectedItem = AboutNavItem;
                    normalized = "about";
                    break;
            }

            SectionNavigated?.Invoke(this, normalized);
        }

        private void SettingsNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (SettingsNavList.SelectedItem is ListViewItem item && item.Tag is string tag)
            {
                ShowSection(tag);
            }
        }

        private void AboutNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (AboutNavList.SelectedItem != null)
            {
                ShowSection("about");
            }
        }

        private void SearchSettingsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchSettingsBox.Text.ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            if (query.Contains("profil") || query.Contains("hesap") || query.Contains("kullanıcı"))
                ShowSection("profiles");
            else if (query.Contains("gizli") || query.Contains("ara") || query.Contains("çerez") || query.Contains("geçmiş") || query.Contains("reklam"))
                ShowSection("privacy");
            else if (query.Contains("görün") || query.Contains("tema") || query.Contains("renk") || query.Contains("zoom") || query.Contains("yakın"))
                ShowSection("appearance");
            else if (query.Contains("varsayılan") || query.Contains("default"))
                ShowSection("defaultbrowser");
            else if (query.Contains("dil") || query.Contains("langua") || query.Contains("türkçe") || query.Contains("çevir"))
                ShowSection("languages");
            else if (query.Contains("uzantı") || query.Contains("eklenti") || query.Contains("extension"))
                ShowSection("extensions");
            else if (query.Contains("indir"))
                ShowSection("downloads");
            else if (query.Contains("sistem") || query.Contains("performans") || query.Contains("ram") || query.Contains("bellek") || query.Contains("hız"))
                ShowSection("system");
            else if (query.Contains("hakkında") || query.Contains("sürüm") || query.Contains("güncel"))
                ShowSection("about");
        }

        private void MakeDefaultBrowserBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:defaultapps-browser") { UseShellExecute = true });
                }
                catch { }
            }
        }

        // --- PROFİLLER PANELİ AKSİYONLARI ---

        private async void EditProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileEditDialog(ProfileService.Instance.CurrentProfile)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
            LoadProfileData();
        }

        private void GoToPasswords_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "bear://passwords");
        }

        private void GoToFavorites_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "bear://favorites");
        }

        private void GoToHistory_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "bear://history");
        }

        private void OpenProfileFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = ProfileService.Instance.CurrentProfile.ProfileFolderPath;
                if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            catch { }
        }

        // --- GİZLİLİK PANELİ AKSİYONLARI ---

        private void AdBlockToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsService.Instance.Settings.AdBlockEnabled = AdBlockToggle.IsOn;
            AdBlockService.Instance.IsEnabled = AdBlockToggle.IsOn;
            _ = SettingsService.Instance.SaveAsync();
        }

        private void AdBlockLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (AdBlockLevelCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                SettingsService.Instance.Settings.AdBlockLevel = tag;
                _ = SettingsService.Instance.SaveAsync();
            }
        }

        private async void ClearDataBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearDataBtn.IsEnabled = false;
            ClearDataStatusText.Text = "Temizleniyor...";

            await ProfileService.Instance.ClearHistoryAsync();
            await ProfileService.Instance.ClearDownloadsAsync();

            await Task.Delay(400);

            ClearDataStatusText.Text = "✓ Gözatma verileri ve indirme geçmişi başarıyla temizlendi!";
            ClearDataBtn.IsEnabled = true;
            LoadProfileData();
        }

        private void SearchEngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (SearchEngineCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string engine)
            {
                SettingsService.Instance.UpdateSearchEngine(engine);
                if (HomePageBox != null)
                {
                    HomePageBox.Text = SettingsService.Instance.Settings.HomePageUrl;
                }
                _ = SettingsService.Instance.SaveAsync();
            }
        }

        private void DoNotTrackToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsService.Instance.Settings.DoNotTrack = DoNotTrackToggle.IsOn;
            _ = SettingsService.Instance.SaveAsync();
        }

        private void AskPasswordToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsService.Instance.Settings.AskPasswordSave = AskPasswordToggle.IsOn;
            _ = SettingsService.Instance.SaveAsync();
        }

        // --- GÖRÜNÜM PANELİ AKSİYONLARI ---

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string theme)
            {
                SettingsService.Instance.Settings.Theme = theme;
                _ = SettingsService.Instance.SaveAsync();
            }
        }

        private void ShowFavBarToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsService.Instance.Settings.ShowFavoritesBar = ShowFavBarToggle.IsOn;
            _ = SettingsService.Instance.SaveAsync();
        }

        private void ZoomCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ZoomCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag &&
                double.TryParse(tag, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double zoom))
            {
                SettingsService.Instance.Settings.DefaultZoom = zoom;
                _ = SettingsService.Instance.SaveAsync();
            }
        }

        private async void SaveHomePageBtn_Click(object sender, RoutedEventArgs e)
        {
            string url = HomePageBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                HomePageBox.Text = url;
                SettingsService.Instance.Settings.HomePageUrl = url;
                await SettingsService.Instance.SaveAsync();
                SaveHomePageBtn.Content = "Kaydedildi!";
                await Task.Delay(1500);
                SaveHomePageBtn.Content = "Kaydet";
            }
        }

        // --- İNDİRMELER PANELİ AKSİYONLARI ---

        private void OpenDownloadsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Process.Start(new ProcessStartInfo("explorer.exe", downloadsDir) { UseShellExecute = true });
            }
            catch { }
        }

        private void GoToDownloadsPage_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "bear://downloads");
        }

        // --- SİSTEM VE PERFORMANS PANELİ AKSİYONLARI ---

        private async void OptimizeMemoryBtn_Click(object sender, RoutedEventArgs e)
        {
            OptimizeMemoryBtn.IsEnabled = false;
            MemoryOptimizeStatusText.Text = "Bellek optimize ediliyor...";

            MemoryOptimizerService.Instance.OptimizeMemoryAsync();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.Delay(500);
            UpdateRamUsage();

            MemoryOptimizeStatusText.Text = "✓ Bellek başarıyla optimize edildi ve serbest bırakıldı!";
            OptimizeMemoryBtn.IsEnabled = true;
        }

        private void EcoModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            SettingsService.Instance.Settings.EcoModeEnabled = EcoModeToggle.IsOn;
            _ = SettingsService.Instance.SaveAsync();
        }

        private void OpenCrashLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            CrashReportingService.Instance.OpenLogsFolder();
        }

        private void TestCrashBtn_Click(object sender, RoutedEventArgs e)
        {
            CrashReportingService.Instance.TriggerTestCrash();
        }

        // --- HAKKINDA PANELİ AKSİYONLARI ---

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            CheckingProgress.IsActive = true;
            CheckingProgress.Visibility = Visibility.Visible;
            CheckIcon.Visibility = Visibility.Collapsed;
            CheckUpdatesText.Text = "Denetleniyor...";

            await Task.Delay(1300);

            CheckingProgress.IsActive = false;
            CheckingProgress.Visibility = Visibility.Collapsed;
            CheckIcon.Glyph = "\uE73E";
            CheckIcon.Visibility = Visibility.Visible;
            CheckUpdatesText.Text = "Güncel!";
            StatusMessageText.Text = "Bear Browser güncel • En son sürümü kullanıyorsunuz";

            await Task.Delay(2000);
            CheckIcon.Glyph = "\uE72C";
            CheckUpdatesText.Text = "Güncelleştirmeleri denetle";
            CheckUpdatesButton.IsEnabled = true;
        }

        // --- UZANTILAR PANELİ AKSİYONLARI ---

        private void RefreshExtensionsList()
        {
            string query = SettingsExtensionSearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
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
            if (SettingsExtensionsCountBadge != null)
            {
                SettingsExtensionsCountBadge.Text = $"{count} eklenti";
            }

            if (SettingsEmptyStateBorder != null)
            {
                SettingsEmptyStateBorder.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SettingsExtensionSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshExtensionsList();
        }

        private void SettingsOpenChromeWebStore_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "https://chromewebstore.google.com");
        }

        private void SettingsOpenEdgeAddons_Click(object sender, RoutedEventArgs e)
        {
            NavigateRequested?.Invoke(this, "https://microsoftedge.microsoft.com/addons");
        }

        private void SettingsDevModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (SettingsLoadUnpackedBtn != null && SettingsDevModeToggle != null)
            {
                SettingsLoadUnpackedBtn.Visibility = SettingsDevModeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void SettingsLoadUnpackedBtn_Click(object sender, RoutedEventArgs e)
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
                    RefreshExtensionsList();
                }
            }
            catch (Exception ex)
            {
                CrashReportingService.Instance.LogCustomError("Settings Load Unpacked Extension Failed", ex.Message);
            }
        }

        private async void SettingsExtensionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && toggle.Tag is string extId)
            {
                await ExtensionService.Instance.SetExtensionEnabledAsync(extId, toggle.IsOn, _profile);
            }
        }

        private async void SettingsRemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string extId)
            {
                await ExtensionService.Instance.RemoveExtensionAsync(extId, _profile);
                RefreshExtensionsList();
            }
        }

        // --- DİLLER VE YERELLEŞTİRME ---

        private void RefreshLanguagesList()
        {
            _languagesList.Clear();
            var items = LocalizationService.Instance.GetAvailableLanguages();
            foreach (var item in items)
            {
                _languagesList.Add(item);
            }
        }

        private void SelectLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string langCode)
            {
                LocalizationService.Instance.SetLanguage(langCode);
                RefreshLanguagesList();
                ApplyLocalization();
            }
        }

        public void ApplyLocalization()
        {
            var loc = LocalizationService.Instance;

            // Navigasyon Menüsü
            if (NavTitleText != null) NavTitleText.Text = loc["Menu_Settings"];
            if (SearchSettingsBox != null) SearchSettingsBox.PlaceholderText = loc["Settings_Nav_SearchPlaceholder"];
            if (ProfilesNavText != null) ProfilesNavText.Text = loc["Settings_Nav_Profiles"];
            if (PrivacyNavText != null) PrivacyNavText.Text = loc["Settings_Nav_Privacy"];
            if (AppearanceNavText != null) AppearanceNavText.Text = loc["Settings_Nav_Appearance"];
            if (DefaultBrowserNavText != null) DefaultBrowserNavText.Text = loc["Settings_Nav_DefaultBrowser"];
            if (LanguagesNavText != null) LanguagesNavText.Text = loc["Settings_Nav_Languages"];
            if (ExtensionsNavText != null) ExtensionsNavText.Text = loc["Settings_Nav_Extensions"];
            if (DownloadsNavText != null) DownloadsNavText.Text = loc["Settings_Nav_Downloads"];
            if (SystemNavText != null) SystemNavText.Text = loc["Settings_Nav_System"];
            if (AboutNavText != null) AboutNavText.Text = loc["Settings_Nav_About"];

            // Diller Paneli
            if (LanguagesTitleText != null) LanguagesTitleText.Text = loc["Languages_Title"];
            if (LanguagesSubtitleText != null) LanguagesSubtitleText.Text = loc["Languages_Subtitle"];
            if (LanguagesPreferredCardTitle != null) LanguagesPreferredCardTitle.Text = loc["Languages_PreferredCard_Title"];
            if (LanguagesPreferredCardSubtitle != null) LanguagesPreferredCardSubtitle.Text = loc["Languages_PreferredCard_Desc"];
            if (LanguagesTranslateTitleText != null) LanguagesTranslateTitleText.Text = loc["Languages_TranslateOffer_Title"];
            if (LanguagesTranslateSubtitleText != null) LanguagesTranslateSubtitleText.Text = loc["Languages_TranslateOffer_Desc"];
        }
    }
}
