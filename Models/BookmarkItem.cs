using System;
using System.Text.Json.Serialization;

namespace Bear_Browser.Models
{
    public class BookmarkItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Folder { get; set; } = "Sık kullanılanlar çubuğu";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CustomFavicon { get; set; }

        [JsonIgnore]
        public string FaviconUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomFavicon))
                    return CustomFavicon;
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return $"https://icons.duckduckgo.com/ip3/{uri.Host}.ico";
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
                if (!string.IsNullOrWhiteSpace(CustomFavicon))
                {
                    try { return new Uri(CustomFavicon); } catch { }
                }
                if (string.IsNullOrWhiteSpace(Url)) return null;
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return new Uri($"https://icons.duckduckgo.com/ip3/{uri.Host}.ico");
                }
                catch { }
                return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
        }
    }
}
