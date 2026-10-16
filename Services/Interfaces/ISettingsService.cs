using System;
using System.Threading.Tasks;

namespace Bear_Browser.Services.Interfaces
{
    public interface ISettingsService
    {
        BrowserSettings Settings { get; }
        event EventHandler<BrowserSettings>? SettingsChanged;

        void Load();
        Task SaveAsync();
        string GetSearchEngineHomeUrl();
        void UpdateSearchEngine(string engine);
        string GetSearchUrl(string query);
    }
}
