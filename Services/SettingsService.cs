using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bear_Browser.Services
{
    public class BrowserSettings
    {
        public string Theme { get; set; } = "System"; // "System", "Dark", "Light"
        public string SearchEngine { get; set; } = "Google"; // "Google", "Bing", "DuckDuckGo", "Yandex"
        public bool ShowFavoritesBar { get; set; } = true;
        public bool AdBlockEnabled { get; set; } = true;
        public string AdBlockLevel { get; set; } = "Balanced"; // "Balanced", "Strict"
        public bool DoNotTrack { get; set; } = true;
        public bool AskPasswordSave { get; set; } = true;
        public double DefaultZoom { get; set; } = 1.0;
        public string HomePageUrl { get; set; } = "bear://newtab";
        public bool EcoModeEnabled { get; set; } = true;
        public string Language { get; set; } = "system"; // "system", "tr", "en", "de", "es", "fr"
    }

    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private readonly string _settingsFilePath;
        public BrowserSettings Settings { get; private set; } = new();

        public event EventHandler<BrowserSettings>? SettingsChanged;

        public SettingsService()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser"
            );
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var loaded = JsonSerializer.Deserialize<BrowserSettings>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                    }
                }
            }
            catch { }
        }

        public async Task SaveAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json);
                SettingsChanged?.Invoke(this, Settings);
            }
            catch { }
        }

        public string GetSearchEngineHomeUrl()
        {
            return Settings.SearchEngine.ToLowerInvariant() switch
            {
                "bing" => "https://www.bing.com",
                "duckduckgo" => "https://duckduckgo.com",
                "yandex" => "https://yandex.com.tr",
                _ => "https://www.google.com"
            };
        }

        public void UpdateSearchEngine(string engine)
        {
            string oldHome = Settings.HomePageUrl?.Trim().ToLowerInvariant() ?? "";
            bool isOldEngineHome = string.IsNullOrEmpty(oldHome) ||
                                  oldHome.Contains("google.") ||
                                  oldHome.Contains("bing.") ||
                                  oldHome.Contains("duckduckgo.") ||
                                  oldHome.Contains("yandex.");

            Settings.SearchEngine = engine;
            if (isOldEngineHome)
            {
                Settings.HomePageUrl = GetSearchEngineHomeUrl();
            }
        }

        public string GetSearchUrl(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetSearchEngineHomeUrl();

            string encoded = Uri.EscapeDataString(query);
            return Settings.SearchEngine.ToLowerInvariant() switch
            {
                "bing" => $"https://www.bing.com/search?q={encoded}",
                "duckduckgo" => $"https://duckduckgo.com/?q={encoded}",
                "yandex" => $"https://yandex.com.tr/search/?text={encoded}",
                _ => $"https://www.google.com/search?q={encoded}"
            };
        }
    }
}
