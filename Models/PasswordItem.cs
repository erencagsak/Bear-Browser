using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Bear_Browser.Models
{
    /// <summary>
    /// Kayıtlı parola öğesini temsil eden model. UI tiplerinden ve gliflerinden arındırılmıştır.
    /// </summary>
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
    }
}
