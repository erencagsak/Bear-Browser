using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Bear_Browser.Models
{
    public class BrowserTab : ObservableObject
    {
        private string _title = "Yeni Sekme";
        private string _url = "https://www.google.com";
        private bool _isLoading;
        private bool _isSleeping;
        private int _blockedAdsCount;
        private long _lastLoadTimeMs;
        private Uri? _faviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");

        public Guid Id { get; } = Guid.NewGuid();

        public Stopwatch LoadTimer { get; } = new();

        public bool IsPopupTab { get; set; } = false;
        public bool IsWebViewInitialized { get; set; } = false;

        public bool IsSleeping
        {
            get => _isSleeping;
            set => SetProperty(ref _isSleeping, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int BlockedAdsCount
        {
            get => _blockedAdsCount;
            set => SetProperty(ref _blockedAdsCount, value);
        }

        public long LastLoadTimeMs
        {
            get => _lastLoadTimeMs;
            set => SetProperty(ref _lastLoadTimeMs, value);
        }

        public Uri? FaviconUri
        {
            get => _faviconUri;
            set => SetProperty(ref _faviconUri, value);
        }

        public double ZoomFactor { get; set; } = 1.0; // Varsayılan %100

        public List<string> HistoryStack { get; } = new();
        public int HistoryIndex { get; set; } = -1;

        public void PushHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (HistoryIndex >= 0 && HistoryIndex < HistoryStack.Count && HistoryStack[HistoryIndex].Equals(url, StringComparison.OrdinalIgnoreCase))
                return;

            if (HistoryIndex >= 0 && HistoryIndex < HistoryStack.Count - 1)
            {
                HistoryStack.RemoveRange(HistoryIndex + 1, HistoryStack.Count - (HistoryIndex + 1));
            }

            HistoryStack.Add(url);
            HistoryIndex = HistoryStack.Count - 1;
        }
    }
}