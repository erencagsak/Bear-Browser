using System;

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
}
