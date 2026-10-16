using System;
using System.Collections.Generic;

namespace Bear_Browser.Services.Interfaces
{
    public interface ILocalizationService
    {
        string CurrentLanguageCode { get; }
        string EffectiveLanguageCode { get; }
        event EventHandler? LanguageChanged;

        void Initialize();
        bool IsSupported(string code);
        void SetLanguage(string code);
        string GetString(string key, string? fallback = null);
        string this[string key] { get; }
        List<LanguageItem> GetAvailableLanguages();
    }
}
