using System;
using System.Text.Json.Serialization;

namespace Bear_Browser.Models
{
    /// <summary>
    /// Sık kullanılan / yer imi öğesini temsil eden model.
    /// </summary>
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
}
