using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Models
{
    public class BrowserTab : INotifyPropertyChanged
    {
        private string _title = "Yeni Sekme";
        private string _url = "https://www.google.com";
        private bool _isLoading;
        private int _blockedAdsCount;
        private long _lastLoadTimeMs;

        public Guid Id { get; } = Guid.NewGuid();

        // Sayfa yükleme süresini ölçen kronometremiz
        public Stopwatch LoadTimer { get; } = new();

        public bool IsPopupTab { get; set; } = false;
        public bool IsWebViewInitialized { get; set; } = false;

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
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(LoadingVisibility));
                    OnPropertyChanged(nameof(FaviconVisibility));
                }
            }
        }

        public Visibility LoadingVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FaviconVisibility => _isLoading ? Visibility.Collapsed : Visibility.Visible;

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

        private Uri? _faviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
        public Uri? FaviconUri
        {
            get => _faviconUri;
            set => SetProperty(ref _faviconUri, value);
        }
        public WebView2 WebView { get; set; } = new WebView2();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public double ZoomFactor { get; set; } = 1.0; // Varsayılan %100

        public System.Collections.Generic.List<string> HistoryStack { get; } = new();
        public int HistoryIndex { get; set; } = -1;

        public void PushHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Ardışık aynı adresi tekrar geçmişe ekleme
            if (HistoryIndex >= 0 && HistoryIndex < HistoryStack.Count && HistoryStack[HistoryIndex].Equals(url, StringComparison.OrdinalIgnoreCase))
                return;

            // Geçmişin ortasından yeni bir adrese gidildiyse ileri geçmişi temizle
            if (HistoryIndex >= 0 && HistoryIndex < HistoryStack.Count - 1)
            {
                HistoryStack.RemoveRange(HistoryIndex + 1, HistoryStack.Count - (HistoryIndex + 1));
            }

            HistoryStack.Add(url);
            HistoryIndex = HistoryStack.Count - 1;
        }
    }
}