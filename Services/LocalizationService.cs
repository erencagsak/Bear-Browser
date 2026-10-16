using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace Bear_Browser.Services
{
    public class LanguageItem : INotifyPropertyChanged
    {
        public string Code { get; set; } = "";
        public string NativeName { get; set; } = "";
        public string EnglishName { get; set; } = "";
        public string Flag { get; set; } = "";
        public bool IsSystem { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBadgeText));
                    OnPropertyChanged(nameof(StatusBadgeVisibility));
                    OnPropertyChanged(nameof(CheckmarkVisibility));
                    OnPropertyChanged(nameof(SelectButtonVisibility));
                }
            }
        }

        public string DisplayTitle => $"{Flag}  {NativeName} ({EnglishName})";
        public string Subtitle => IsSystem
            ? "Windows sistem dili ayarını otomatik takip eder."
            : $"Bear Browser arayüzünü {NativeName} olarak görüntüler.";

        public string StatusBadgeText => IsSelected ? (IsSystem ? "Sistem Dili (Etkin)" : "Etkin") : "";
        public Visibility StatusBadgeVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CheckmarkVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SelectButtonVisibility => IsSelected ? Visibility.Collapsed : Visibility.Visible;
        public string SelectButtonText => LocalizationService.Instance.GetString("Languages_ApplyButton", "Bu dili kullan");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }

    public class LocalizationService : ILocalizationService, INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance
        {
            get
            {
                if (App.Services?.GetService<ILocalizationService>() is LocalizationService s)
                    return s;
                return _instance ??= new LocalizationService();
            }
        }

        private readonly ISettingsService? _settingsService;
        private Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _fallbackStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Nav_Back"] = "Back",
            ["Nav_Forward"] = "Forward",
            ["Nav_Refresh"] = "Refresh",
            ["Nav_Stop"] = "Stop",
            ["Nav_AddressPlaceholder"] = "Search or enter web address...",
            ["Nav_Go"] = "Go",
            ["Nav_Bookmark"] = "Add this page to favorites",
            ["Nav_Downloads"] = "Downloads",
            ["Nav_Extensions"] = "Extensions",
            ["Nav_Profile"] = "Profile",
            ["Nav_More"] = "Settings and more",
            ["Nav_Zoom"] = "Zoom",
            ["Nav_ZoomIn"] = "Zoom in",
            ["Nav_ZoomOut"] = "Zoom out",
            ["Nav_FullScreen"] = "Full screen",

            ["Menu_NewTab"] = "New tab",
            ["Menu_NewWindow"] = "New window",
            ["Menu_NewInPrivate"] = "New InPrivate window",
            ["Menu_Favorites"] = "Favorites",
            ["Menu_History"] = "History",
            ["Menu_Downloads"] = "Downloads",
            ["Menu_Extensions"] = "Extensions",
            ["Menu_ManageExtensions"] = "Manage extensions",
            ["Menu_ChromeWebStore"] = "Chrome Web Store",
            ["Menu_Passwords"] = "Passwords",
            ["Menu_DevTools"] = "Developer tools",
            ["Menu_Settings"] = "Settings",
            ["Menu_HelpAndFeedback"] = "Help and feedback",
            ["Menu_About"] = "About Bear Browser",
            ["Menu_Feedback"] = "Send feedback",
            ["Menu_CloseBrowser"] = "Close Bear Browser",

            ["Tab_NewTab"] = "New Tab",
            ["Tab_InPrivate"] = "InPrivate Tab",
            ["Tab_Favorites"] = "Favorites",
            ["Tab_History"] = "History",
            ["Tab_Downloads"] = "Downloads",
            ["Tab_Passwords"] = "Passwords",
            ["Tab_Settings"] = "Settings",
            ["Tab_Extensions"] = "Extensions",

            ["Bookmark_Title"] = "Favorite added",
            ["Bookmark_Name"] = "Name",
            ["Bookmark_Folder"] = "Folder",
            ["Bookmark_Done"] = "Done",
            ["Bookmark_Remove"] = "Remove",

            ["Profile_Manage"] = "Manage Profile",
            ["Profile_Offline"] = "Local Profile (Offline)",

            ["Extensions_Title"] = "Extensions",
            ["Extensions_Subtitle"] = "Add new features and capabilities to Bear Browser.",
            ["Extensions_NoInstalled"] = "No extensions installed",
            ["Extensions_StoreHint"] = "You can get extensions from the Chrome Web Store.",
            ["Extensions_AllowedOnAllSites"] = "Allowed on all sites",
            ["Extensions_SearchPlaceholder"] = "Search extensions...",
            ["Extensions_DevMode"] = "Developer Mode",
            ["Extensions_DevMode_Desc"] = "Load unpacked extensions and develop extensions locally.",
            ["Extensions_LoadUnpacked"] = "Load unpacked extension",
            ["Extensions_InstalledTitle"] = "Installed Extensions",
            ["Extensions_Empty"] = "No extensions installed yet",
            ["Extensions_Remove"] = "Remove",
            ["Extensions_StoreCard_Title"] = "Discover more extensions",
            ["Extensions_StoreCard_Desc"] = "You can add thousands of extensions to your browser from official stores.",

            ["NewTab_Tagline"] = "Fast, Secure and Free Web Experience",
            ["NewTab_SearchPlaceholder"] = "Search the web or type a URL...",
            ["NewTab_SearchTooltip"] = "Search",
            ["NewTab_QuickAccess"] = "Quick Access",

            ["Settings_Nav_SearchPlaceholder"] = "Search settings...",
            ["Settings_Nav_Profiles"] = "Profiles",
            ["Settings_Nav_Privacy"] = "Privacy, search, and services",
            ["Settings_Nav_Appearance"] = "Appearance",
            ["Settings_Nav_DefaultBrowser"] = "Default Browser",
            ["Settings_Nav_Languages"] = "Languages",
            ["Settings_Nav_Extensions"] = "Extensions",
            ["Settings_Nav_Downloads"] = "Downloads",
            ["Settings_Nav_System"] = "System and performance",
            ["Settings_Nav_About"] = "About Bear Browser",

            ["Languages_Title"] = "Languages",
            ["Languages_Subtitle"] = "Customize display languages for Bear Browser interface and websites.",
            ["Languages_PreferredCard_Title"] = "Preferred Languages",
            ["Languages_PreferredCard_Desc"] = "Select the display language for the Bear Browser user interface and web pages.",
            ["Languages_DefaultBadge"] = "Default",
            ["Languages_ActiveBadge"] = "Active",
            ["Languages_SystemBadge"] = "System Language (Auto)",
            ["Languages_ApplyButton"] = "Use this language",
            ["Languages_ActiveDesc"] = "Bear Browser user interface is displayed in this language.",
            ["Languages_TranslateOffer_Title"] = "Offer to translate web pages",
            ["Languages_TranslateOffer_Desc"] = "Provides the option to automatically translate web pages you can't read into your preferred language.",

            ["DefaultBrowser_Title"] = "Default Browser",
            ["DefaultBrowser_Subtitle"] = "Make Bear Browser your primary web browser.",
            ["DefaultBrowser_Card_Title"] = "Make Bear Browser default",
            ["DefaultBrowser_Card_Desc"] = "Automatically open all web links, HTML documents, and searches with Bear Browser.",
            ["DefaultBrowser_Button"] = "Make default",

            ["About_Title"] = "About Bear Browser",
            ["About_Tagline"] = "Fast, Secure and Customizable Web Experience",
            ["About_CheckUpdates"] = "Check for updates",
            ["About_Checking"] = "Checking...",
            ["About_UpToDate"] = "Bear Browser is up to date • You are using the latest version",

            ["Common_Save"] = "Save",
            ["Common_Cancel"] = "Cancel",
            ["Common_Delete"] = "Delete",
            ["Common_Edit"] = "Edit",
            ["Common_Close"] = "Close",
            ["Common_Copied"] = "Copied"
        };

        public string CurrentLanguageCode { get; private set; } = "system";
        public string EffectiveLanguageCode { get; private set; } = "en";

        public event EventHandler? LanguageChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public LocalizationService(ISettingsService? settingsService = null)
        {
            _settingsService = settingsService;
            Initialize();
        }

        public void Initialize()
        {
            var settings = _settingsService?.Settings ?? SettingsService.Instance.Settings;
            string saved = settings?.Language ?? "system";
            SetLanguageInternal(saved, notify: false);
        }

        public bool IsSupported(string code)
        {
            return code switch
            {
                "tr" or "en" or "de" or "es" or "fr" => true,
                _ => false
            };
        }

        public void SetLanguage(string code)
        {
            SetLanguageInternal(code, notify: true);
            var settingsService = _settingsService ?? SettingsService.Instance;
            settingsService.Settings.Language = code;
            _ = settingsService.SaveAsync();
        }

        private void SetLanguageInternal(string code, bool notify)
        {
            CurrentLanguageCode = string.IsNullOrWhiteSpace(code) ? "system" : code.ToLowerInvariant().Trim();

            string resolved = CurrentLanguageCode;
            if (resolved == "system")
            {
                try
                {
                    string osIso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                    if (IsSupported(osIso))
                    {
                        resolved = osIso;
                    }
                    else
                    {
                        resolved = "en";
                    }
                }
                catch
                {
                    resolved = "en";
                }
            }

            EffectiveLanguageCode = resolved;
            LoadTranslations(resolved);

            if (notify)
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCode)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EffectiveLanguageCode)));
            }
        }

        private void LoadTranslations(string langCode)
        {
            _strings.Clear();

            try
            {
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Languages", $"{langCode}.json");
                if (!File.Exists(jsonPath))
                {
                    string devPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Assets\Languages", $"{langCode}.json"));
                    if (File.Exists(devPath))
                    {
                        jsonPath = devPath;
                    }
                }

                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _strings[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch { }
        }

        public string GetString(string key, string? fallback = null)
        {
            if (_strings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            if (_fallbackStrings.TryGetValue(key, out var fbVal) && !string.IsNullOrWhiteSpace(fbVal))
            {
                return fbVal;
            }

            return fallback ?? key;
        }

        public string this[string key] => GetString(key);

        public List<LanguageItem> GetAvailableLanguages()
        {
            string osName = CultureInfo.CurrentUICulture.NativeName;
            var list = new List<LanguageItem>
            {
                new LanguageItem
                {
                    Code = "system",
                    NativeName = "Sistem Varsayılanı",
                    EnglishName = $"System Default ({osName})",
                    Flag = "💻",
                    IsSystem = true,
                    IsSelected = CurrentLanguageCode == "system"
                },
                new LanguageItem
                {
                    Code = "tr",
                    NativeName = "Türkçe",
                    EnglishName = "Turkish (Türkiye)",
                    Flag = "🇹🇷",
                    IsSystem = false,
                    IsSelected = CurrentLanguageCode == "tr"
                },
                new LanguageItem
                {
                    Code = "en",
                    NativeName = "English",
                    EnglishName = "English (United States)",
                    Flag = "🇺🇸",
                    IsSystem = false,
                    IsSelected = CurrentLanguageCode == "en"
                },
                new LanguageItem
                {
                    Code = "de",
                    NativeName = "Deutsch",
                    EnglishName = "German (Deutschland)",
                    Flag = "🇩🇪",
                    IsSystem = false,
                    IsSelected = CurrentLanguageCode == "de"
                },
                new LanguageItem
                {
                    Code = "es",
                    NativeName = "Español",
                    EnglishName = "Spanish (España)",
                    Flag = "🇪🇸",
                    IsSystem = false,
                    IsSelected = CurrentLanguageCode == "es"
                },
                new LanguageItem
                {
                    Code = "fr",
                    NativeName = "Français",
                    EnglishName = "French (France)",
                    Flag = "🇫🇷",
                    IsSystem = false,
                    IsSelected = CurrentLanguageCode == "fr"
                }
            };

            return list;
        }
    }
}
