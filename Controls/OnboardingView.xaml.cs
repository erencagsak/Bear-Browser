using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Bear_Browser.Controls
{
    public sealed partial class OnboardingView : UserControl
    {
        public event EventHandler? OnboardingCompleted;

        private readonly ISettingsService _settingsService;
        private readonly IProfileService _profileService;
        private readonly ILocalizationService _localizationService;
        private readonly IAdBlockService _adBlockService;

        private int _currentStep = 1;
        private const int TotalSteps = 7;
        private bool _isInitialized = false;

        private string _selectedProfileName = "Kullanıcı 1";
        private string? _selectedAvatarPath;
        private string _selectedTabPosition = "Top";
        private bool _selectedShowFavoritesBar = true;
        private string _selectedSearchEngine = "Google";
        private string _selectedLanguage = "tr";
        private string _selectedTheme = "Dark";
        private bool _selectedAdBlock = true;
        private bool _selectedDoNotTrack = true;
        private bool _selectedAskPassword = true;

        public OnboardingView()
        {
            _settingsService = App.Services.GetRequiredService<ISettingsService>();
            _profileService = App.Services.GetRequiredService<IProfileService>();
            _localizationService = App.Services.GetRequiredService<ILocalizationService>();
            _adBlockService = App.Services.GetRequiredService<IAdBlockService>();

            this.InitializeComponent();

            InitDefaultsFromServices();
            UpdateStepUI();

            _isInitialized = true;
        }

        private void InitDefaultsFromServices()
        {
            try
            {
                var profile = _profileService.CurrentProfile;
                if (!string.IsNullOrWhiteSpace(profile?.Name))
                {
                    _selectedProfileName = profile.Name;
                    ProfileNameInput.Text = profile.Name;
                }

                if (!string.IsNullOrEmpty(profile?.AvatarImagePath) && File.Exists(profile.AvatarImagePath))
                {
                    _selectedAvatarPath = profile.AvatarImagePath;
                    AvatarImagePreview.Source = new BitmapImage(new Uri(profile.AvatarImagePath));
                    AvatarImagePreview.Visibility = Visibility.Visible;
                    AvatarEmojiPreview.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AvatarEmojiPreview.Text = "🐻";
                    AvatarEmojiPreview.Visibility = Visibility.Visible;
                    AvatarImagePreview.Visibility = Visibility.Collapsed;
                }

                var s = _settingsService.Settings;
                if (!string.IsNullOrWhiteSpace(s.TabPosition)) _selectedTabPosition = s.TabPosition;
                _selectedShowFavoritesBar = s.ShowFavoritesBar;
                if (!string.IsNullOrWhiteSpace(s.SearchEngine)) _selectedSearchEngine = s.SearchEngine;
                if (!string.IsNullOrWhiteSpace(s.Theme) && s.Theme != "System") _selectedTheme = s.Theme;
                else _selectedTheme = "Dark";
                if (!string.IsNullOrWhiteSpace(s.Language) && s.Language != "system") _selectedLanguage = s.Language;

                _selectedAdBlock = s.AdBlockEnabled;
                _selectedDoNotTrack = s.DoNotTrack;
                _selectedAskPassword = s.AskPasswordSave;

                AdBlockToggle.IsOn = _selectedAdBlock;
                DoNotTrackToggle.IsOn = _selectedDoNotTrack;
                AskPasswordToggle.IsOn = _selectedAskPassword;

                UpdateTabPosCardUI();
                UpdateFavBarCardUI();
                UpdateSearchCardUI();
                UpdateLangCardUI();
                UpdateThemeCardUI();
            }
            catch { }
        }

private void UpdateStepUI()
        {
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            Step4Panel.Visibility = _currentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
            Step5Panel.Visibility = _currentStep == 5 ? Visibility.Visible : Visibility.Collapsed;
            Step6Panel.Visibility = _currentStep == 6 ? Visibility.Visible : Visibility.Collapsed;
            Step7Panel.Visibility = _currentStep == 7 ? Visibility.Visible : Visibility.Collapsed;

            PrevStepBtn.Visibility = _currentStep > 1 && _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
            SkipBtn.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;
            NextStepBtn.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;

            StepCountText.Text = $"Adım {_currentStep} / {TotalSteps}";

            UpdateDot(StepDot1, _currentStep == 1);
            UpdateDot(StepDot2, _currentStep == 2);
            UpdateDot(StepDot3, _currentStep == 3);
            UpdateDot(StepDot4, _currentStep == 4);
            UpdateDot(StepDot5, _currentStep == 5);
            UpdateDot(StepDot6, _currentStep == 6);
            UpdateDot(StepDot7, _currentStep == 7);

            if (_currentStep == TotalSteps)
            {
                UpdateSummaryCard();
            }
        }

        private static void UpdateDot(Border dot, bool isActive)
        {
            dot.Width = isActive ? 28 : 8;
            dot.Background = new SolidColorBrush(isActive
                ? Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E)
                : Windows.UI.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
        }

        private void NextStepBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < TotalSteps)
            {
                _currentStep++;
                UpdateStepUI();
            }
        }

        private void PrevStepBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private async void SkipBtn_Click(object sender, RoutedEventArgs e)
        {
            await CompleteOnboardingAsync();
        }

        private async void StartBrowsingBtn_Click(object sender, RoutedEventArgs e)
        {
            await CompleteOnboardingAsync();
        }

private void ProfileNameInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized || _profileService == null) return;

            string name = ProfileNameInput.Text?.Trim() ?? "";
            _selectedProfileName = string.IsNullOrWhiteSpace(name) ? "Kullanıcı 1" : name;
            _ = _profileService.UpdateProfileNameAsync(_selectedProfileName);
        }

        private async void PresetAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string emoji)
            {
                AvatarEmojiPreview.Text = emoji;
                AvatarEmojiPreview.Visibility = Visibility.Visible;
                AvatarImagePreview.Visibility = Visibility.Collapsed;

                await Task.Delay(50);
                await SaveEmojiAsAvatarAsync();
            }
        }

        private async Task SaveEmojiAsAvatarAsync()
        {
            try
            {
                var rtb = new RenderTargetBitmap();
                await rtb.RenderAsync(AvatarBorderContainer);
                var pixelBuffer = await rtb.GetPixelsAsync();
                byte[] pixels = pixelBuffer.ToArray();

                string profileFolder = _profileService.CurrentProfile.ProfileFolderPath;
                string avatarFileName = $"avatar_emoji_{DateTime.UtcNow.Ticks}.png";
                string destPath = Path.Combine(profileFolder, avatarFileName);

                foreach (var old in Directory.GetFiles(profileFolder, "avatar*"))
                {
                    try { File.Delete(old); } catch { }
                }

                var folder = await StorageFolder.GetFolderFromPathAsync(profileFolder);
                var file = await folder.CreateFileAsync(avatarFileName, CreationCollisionOption.ReplaceExisting);

                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)rtb.PixelWidth,
                        (uint)rtb.PixelHeight,
                        96, 96,
                        pixels);
                    await encoder.FlushAsync();
                }

                _selectedAvatarPath = destPath;
                await _profileService.UpdateProfileAvatarPathAsync(_selectedProfileName, destPath);
            }
            catch { }
        }

        private async void PickCustomAvatarBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ProfileEditDialog(_profileService.CurrentProfile)
                {
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                ReloadProfile();
            }
            catch { }
        }

        private void ReloadProfile()
        {
            var p = _profileService.CurrentProfile;
            if (!string.IsNullOrWhiteSpace(p?.Name))
            {
                _selectedProfileName = p.Name;
                ProfileNameInput.Text = p.Name;
            }

            if (!string.IsNullOrEmpty(p?.AvatarImagePath) && File.Exists(p.AvatarImagePath))
            {
                _selectedAvatarPath = p.AvatarImagePath;
                AvatarImagePreview.Source = new BitmapImage(new Uri(p.AvatarImagePath));
                AvatarImagePreview.Visibility = Visibility.Visible;
                AvatarEmojiPreview.Visibility = Visibility.Collapsed;
            }
        }

private async void TabPosTopCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _selectedTabPosition = "Top";
            UpdateTabPosCardUI();

            _settingsService.Settings.TabPosition = "Top";
            await _settingsService.SaveAsync();
        }

        private async void TabPosLeftCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _selectedTabPosition = "Left";
            UpdateTabPosCardUI();

            _settingsService.Settings.TabPosition = "Left";
            await _settingsService.SaveAsync();
        }

        private void UpdateTabPosCardUI()
        {
            bool isTop = _selectedTabPosition == "Top";
            var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E));
            var inactiveBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));

            TabPosTopCard.BorderBrush = isTop ? activeBrush : inactiveBrush;
            TabPosTopCard.BorderThickness = new Thickness(isTop ? 2 : 1.5);
            TabTopCheckIcon.Visibility = isTop ? Visibility.Visible : Visibility.Collapsed;

            TabPosLeftCard.BorderBrush = !isTop ? activeBrush : inactiveBrush;
            TabPosLeftCard.BorderThickness = new Thickness(!isTop ? 2 : 1.5);
            TabLeftCheckIcon.Visibility = !isTop ? Visibility.Visible : Visibility.Collapsed;
        }

private async void FavBarShowCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _selectedShowFavoritesBar = true;
            UpdateFavBarCardUI();

            _settingsService.Settings.ShowFavoritesBar = true;
            await _settingsService.SaveAsync();
        }

        private async void FavBarHideCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _selectedShowFavoritesBar = false;
            UpdateFavBarCardUI();

            _settingsService.Settings.ShowFavoritesBar = false;
            await _settingsService.SaveAsync();
        }

        private void UpdateFavBarCardUI()
        {
            var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E));
            var inactiveBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));

            FavBarShowCard.BorderBrush = _selectedShowFavoritesBar ? activeBrush : inactiveBrush;
            FavBarShowCard.BorderThickness = new Thickness(_selectedShowFavoritesBar ? 2 : 1.5);
            FavShowCheckIcon.Visibility = _selectedShowFavoritesBar ? Visibility.Visible : Visibility.Collapsed;

            FavBarHideCard.BorderBrush = !_selectedShowFavoritesBar ? activeBrush : inactiveBrush;
            FavBarHideCard.BorderThickness = new Thickness(!_selectedShowFavoritesBar ? 2 : 1.5);
            FavHideCheckIcon.Visibility = !_selectedShowFavoritesBar ? Visibility.Visible : Visibility.Collapsed;
        }

private async void SearchCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border b && b.Tag is string engine)
            {
                _selectedSearchEngine = engine;
                UpdateSearchCardUI();

                _settingsService.UpdateSearchEngine(engine);
                await _settingsService.SaveAsync();
            }
        }

        private void UpdateSearchCardUI()
        {
            ResetSearchBorders();
            var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E));

            if (_selectedSearchEngine == "DuckDuckGo")
            {
                SearchDuckCard.BorderBrush = activeBrush;
                SearchDuckCard.BorderThickness = new Thickness(2);
                SearchDuckCheck.Visibility = Visibility.Visible;
            }
            else if (_selectedSearchEngine == "Bing")
            {
                SearchBingCard.BorderBrush = activeBrush;
                SearchBingCard.BorderThickness = new Thickness(2);
                SearchBingCheck.Visibility = Visibility.Visible;
            }
            else if (_selectedSearchEngine == "Yandex")
            {
                SearchYandexCard.BorderBrush = activeBrush;
                SearchYandexCard.BorderThickness = new Thickness(2);
                SearchYandexCheck.Visibility = Visibility.Visible;
            }
            else
            {
                SearchGoogleCard.BorderBrush = activeBrush;
                SearchGoogleCard.BorderThickness = new Thickness(2);
                SearchGoogleCheck.Visibility = Visibility.Visible;
            }
        }

        private void ResetSearchBorders()
        {
            var defaultBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
            SearchGoogleCard.BorderBrush = defaultBrush; SearchGoogleCard.BorderThickness = new Thickness(1.5); SearchGoogleCheck.Visibility = Visibility.Collapsed;
            SearchDuckCard.BorderBrush = defaultBrush; SearchDuckCard.BorderThickness = new Thickness(1.5); SearchDuckCheck.Visibility = Visibility.Collapsed;
            SearchBingCard.BorderBrush = defaultBrush; SearchBingCard.BorderThickness = new Thickness(1.5); SearchBingCheck.Visibility = Visibility.Collapsed;
            SearchYandexCard.BorderBrush = defaultBrush; SearchYandexCard.BorderThickness = new Thickness(1.5); SearchYandexCheck.Visibility = Visibility.Collapsed;
        }

private async void LangCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border b && b.Tag is string lang)
            {
                _selectedLanguage = lang;
                UpdateLangCardUI();

                _localizationService.SetLanguage(lang);
                _settingsService.Settings.Language = lang;
                await _settingsService.SaveAsync();
            }
        }

        private void UpdateLangCardUI()
        {
            var defBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
            var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E));

            LangTrCard.BorderBrush = defBrush; LangTrCard.BorderThickness = new Thickness(1);
            LangEnCard.BorderBrush = defBrush; LangEnCard.BorderThickness = new Thickness(1);
            LangDeCard.BorderBrush = defBrush; LangDeCard.BorderThickness = new Thickness(1);
            LangEsCard.BorderBrush = defBrush; LangEsCard.BorderThickness = new Thickness(1);
            LangFrCard.BorderBrush = defBrush; LangFrCard.BorderThickness = new Thickness(1);

            switch (_selectedLanguage.ToLowerInvariant())
            {
                case "en":
                    LangEnCard.BorderBrush = activeBrush; LangEnCard.BorderThickness = new Thickness(2); break;
                case "de":
                    LangDeCard.BorderBrush = activeBrush; LangDeCard.BorderThickness = new Thickness(2); break;
                case "es":
                    LangEsCard.BorderBrush = activeBrush; LangEsCard.BorderThickness = new Thickness(2); break;
                case "fr":
                    LangFrCard.BorderBrush = activeBrush; LangFrCard.BorderThickness = new Thickness(2); break;
                default:
                    LangTrCard.BorderBrush = activeBrush; LangTrCard.BorderThickness = new Thickness(2); break;
            }
        }

        private async void ThemeCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Border b && b.Tag is string theme)
            {
                _selectedTheme = theme;
                UpdateThemeCardUI();

                _settingsService.Settings.Theme = theme;
                await _settingsService.SaveAsync();
            }
        }

        private void UpdateThemeCardUI()
        {
            var defBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x25, 0xFF, 0xFF, 0xFF));
            var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x7A, 0x1E));

            bool isDark = _selectedTheme != "Light";
            ThemeDarkCard.BorderBrush = isDark ? activeBrush : defBrush;
            ThemeDarkCard.BorderThickness = new Thickness(isDark ? 2 : 1);

            ThemeLightCard.BorderBrush = !isDark ? activeBrush : defBrush;
            ThemeLightCard.BorderThickness = new Thickness(!isDark ? 2 : 1);
        }

private async void AdBlockToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _settingsService == null || _adBlockService == null) return;

            _selectedAdBlock = AdBlockToggle.IsOn;
            _settingsService.Settings.AdBlockEnabled = _selectedAdBlock;
            _adBlockService.IsEnabled = _selectedAdBlock;
            await _settingsService.SaveAsync();
        }

        private async void DoNotTrackToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _settingsService == null) return;

            _selectedDoNotTrack = DoNotTrackToggle.IsOn;
            _settingsService.Settings.DoNotTrack = _selectedDoNotTrack;
            await _settingsService.SaveAsync();
        }

        private async void AskPasswordToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || _settingsService == null) return;

            _selectedAskPassword = AskPasswordToggle.IsOn;
            _settingsService.Settings.AskPasswordSave = _selectedAskPassword;
            await _settingsService.SaveAsync();
        }

private void UpdateSummaryCard()
        {
            _selectedAdBlock = AdBlockToggle.IsOn;
            _selectedDoNotTrack = DoNotTrackToggle.IsOn;
            _selectedAskPassword = AskPasswordToggle.IsOn;

            SummaryProfileText.Text = _selectedProfileName;
            SummaryTabPosText.Text = _selectedTabPosition == "Left" ? "Sol Sekmeler (Dikey)" : "Üst Sekmeler (Yatay)";
            SummaryFavBarText.Text = _selectedShowFavoritesBar ? "Her Zaman Göster" : "Gizli";
            SummarySearchText.Text = _selectedSearchEngine;
            SummaryAdBlockText.Text = _selectedAdBlock ? "Aktif" : "Devre Dışı";
            SummaryAdBlockText.Foreground = new SolidColorBrush(_selectedAdBlock
                ? Windows.UI.Color.FromArgb(255, 0x10, 0xB9, 0x81)
                : Windows.UI.Color.FromArgb(255, 0xEF, 0x44, 0x44));
        }

        private async Task CompleteOnboardingAsync()
        {
            try
            {
                var s = _settingsService.Settings;
                s.TabPosition = _selectedTabPosition;
                s.ShowFavoritesBar = _selectedShowFavoritesBar;
                s.SearchEngine = _selectedSearchEngine;
                s.Theme = _selectedTheme;
                s.Language = _selectedLanguage;
                s.AdBlockEnabled = AdBlockToggle.IsOn;
                s.DoNotTrack = DoNotTrackToggle.IsOn;
                s.AskPasswordSave = AskPasswordToggle.IsOn;
                s.IsOnboardingCompleted = true;

                await _settingsService.SaveAsync();

                if (!string.IsNullOrWhiteSpace(_selectedProfileName))
                {
                    await _profileService.UpdateProfileNameAsync(_selectedProfileName);
                }

                _localizationService.SetLanguage(_selectedLanguage);
                _adBlockService.IsEnabled = s.AdBlockEnabled;
            }
            catch { }

            OnboardingCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
