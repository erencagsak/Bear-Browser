using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace Bear_Browser.Models
{
    public class UserProfile
    {
        public string Id { get; set; } = "default";
        public string Name { get; set; } = "Kullanıcı 1";
        public string? AvatarImagePath { get; set; } // Seçilen yerel profil fotoğrafı yolu
        public string ProfileFolderPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class HistoryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime VisitedAt { get; set; } = DateTime.Now;

        // XAML x:Bind için saat formatı
        public string FormattedTime => VisitedAt.ToString("HH:mm");

        [JsonIgnore]
        public string FaviconUrl
        {
            get
            {
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32";
                }
                catch { }
                return "ms-appx:///Assets/Bear-Browser-Logo.jpg";
            }
        }

        [JsonIgnore]
        public Uri? FaviconUri
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Url)) return null;
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32");
                }
                catch { }
                return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
        }
    }

    public class BookmarkItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Folder { get; set; } = "Sık kullanılanlar çubuğu";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public string FaviconUrl
        {
            get
            {
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32";
                }
                catch { }
                return "ms-appx:///Assets/Bear-Browser-Logo.jpg";
            }
        }

        [JsonIgnore]
        public Uri? FaviconUri
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Url)) return null;
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32");
                }
                catch { }
                return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
        }
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        private long _totalBytes;
        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                if (_totalBytes != value)
                {
                    _totalBytes = value;
                    OnPropertyChanged(nameof(TotalBytes));
                    OnPropertyChanged(nameof(FormattedSize));
                    OnPropertyChanged(nameof(ProgressValue));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private long _receivedBytes;
        public long ReceivedBytes
        {
            get => _receivedBytes;
            set
            {
                if (_receivedBytes != value)
                {
                    _receivedBytes = value;
                    OnPropertyChanged(nameof(ReceivedBytes));
                    OnPropertyChanged(nameof(ProgressValue));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private string _state = "Tamamlandı";
        public string State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged(nameof(State));
                    OnPropertyChanged(nameof(IsDownloading));
                    OnPropertyChanged(nameof(DownloadingVisibility));
                    OnPropertyChanged(nameof(CompletedVisibility));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public DateTime DownloadedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsDownloading => State == "İndiriliyor";

        [JsonIgnore]
        public Visibility DownloadingVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore]
        public Visibility CompletedVisibility => !IsDownloading ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore]
        public double ProgressValue => TotalBytes > 0 ? (double)ReceivedBytes / TotalBytes * 100.0 : 0;

        [JsonIgnore]
        public string StatusText
        {
            get
            {
                if (IsDownloading)
                {
                    if (TotalBytes > 0)
                        return $"{ReceivedBytes / (1024.0 * 1024.0):F1} MB / {TotalBytes / (1024.0 * 1024.0):F1} MB - İndiriliyor";
                    return $"{ReceivedBytes / (1024.0 * 1024.0):F1} MB - İndiriliyor";
                }
                return FormattedSize;
            }
        }

        [JsonIgnore]
        public string FormattedSize
        {
            get
            {
                if (TotalBytes <= 0) return "Bilinmeyen boyut";
                if (TotalBytes < 1024 * 1024) return $"{TotalBytes / 1024.0:F1} KB";
                return $"{TotalBytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        [JsonIgnore]
        public string FormattedTime => DownloadedAt.ToString("dd.MM.yyyy HH:mm");
    }

    public class PasswordItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Url { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; } = DateTime.Now;

        private bool _isPasswordVisible;
        [JsonIgnore]
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                if (_isPasswordVisible != value)
                {
                    _isPasswordVisible = value;
                    OnPropertyChanged(nameof(IsPasswordVisible));
                    OnPropertyChanged(nameof(DisplayText));
                    OnPropertyChanged(nameof(EyeIconGlyph));
                }
            }
        }

        [JsonIgnore]
        public string DisplayText => IsPasswordVisible ? Password : new string('•', Math.Max(8, Password.Length));

        [JsonIgnore]
        public string EyeIconGlyph => IsPasswordVisible ? "\uED1A" : "\uE7B3";

        [JsonIgnore]
        public string FormattedDate => SavedAt.ToString("dd.MM.yyyy HH:mm");
    }
}