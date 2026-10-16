using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bear_Browser.Models
{
    public class ExtensionInfo : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private string _iconPath = string.Empty;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string IconPath
        {
            get => _iconPath;
            set
            {
                if (_iconPath != value)
                {
                    _iconPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasIcon));
                }
            }
        }

        public bool HasIcon => !string.IsNullOrEmpty(_iconPath) && System.IO.File.Exists(_iconPath);

        public string FolderPath { get; set; } = string.Empty;
        public string Source { get; set; } = "Chrome Web Store";
        public string StoreUrl { get; set; } = string.Empty;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string StatusText => IsEnabled ? "Tüm sitelerde izin verilir" : "Devre dışı bırakıldı";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
