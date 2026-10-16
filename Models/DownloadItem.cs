using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Bear_Browser.Models
{
    /// <summary>
    /// İndirilen dosya öğesini temsil eden model. UI tiplerinden arındırılmıştır.
    /// </summary>
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
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public DateTime DownloadedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsDownloading => State == "İndiriliyor";

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
}
