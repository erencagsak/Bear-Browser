using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Bear_Browser.Models
{
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
                }
            }
        }

        [JsonIgnore]
        public string DisplayText => IsPasswordVisible ? Password : new string('•', Math.Max(8, Password.Length));

        [JsonIgnore]
        public string FormattedDate => SavedAt.ToString("dd.MM.yyyy HH:mm");

        [JsonIgnore]
        public Uri? FaviconUri
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Domain)) return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                try
                {
                    return new Uri($"https://www.google.com/s2/favicons?domain={Domain}&sz=32");
                }
                catch { }
                return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
        }
    }
}
