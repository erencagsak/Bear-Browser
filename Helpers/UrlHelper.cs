using System;
using System.Linq;

namespace Bear_Browser.Helpers
{
    /// <summary>
    /// URL çözümleme, dosya indirme bağlantısı tespiti ve alan adı (domain) eşleştirme yardımcı metotları.
    /// </summary>
    public static class UrlHelper
    {
        private static readonly string[] DownloadFileExtensions =
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
            ".exe", ".msi", ".bat", ".apk", ".dmg", ".pkg", ".deb",
            ".iso", ".img", ".bin",
            ".pdf", ".epub", ".doc", ".docx", ".xls", ".xlsx",
            ".mp3", ".wav", ".flac", ".mp4", ".mkv", ".avi",
            ".torrent"
        };

        /// <summary>
        /// Verilen URL'in doğrudan indirilebilir bir dosya veya indirme eylemi olup olmadığını belirler.
        /// </summary>
        public static bool IsDownloadUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            string cleanUrl = url.Split('?')[0].ToLowerInvariant();
            string lowerUrl = url.ToLowerInvariant();

            // 1. Bilinen dosya uzantıları
            if (DownloadFileExtensions.Any(ext => cleanUrl.EndsWith(ext)))
                return true;

            // 2. Doğrudan indirme parametreleri
            if (lowerUrl.Contains("export=download") ||
                lowerUrl.Contains("action=download") ||
                lowerUrl.Contains("response-content-disposition=attachment") ||
                lowerUrl.Contains("/download/") ||
                lowerUrl.Contains("download.php") ||
                lowerUrl.Contains("file_download"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Bir URL veya Host metninden Host kısmını ayıklar.
        /// </summary>
        public static string GetHost(string urlOrHost)
        {
            if (string.IsNullOrWhiteSpace(urlOrHost)) return string.Empty;
            try
            {
                if (urlOrHost.Contains("://"))
                {
                    return new Uri(urlOrHost).Host;
                }
                return new Uri("https://" + urlOrHost).Host;
            }
            catch
            {
                return urlOrHost.Trim();
            }
        }

        /// <summary>
        /// Alan adının kök (root) domainini çıkarır (örn: mail.google.com -> google.com, test.co.uk -> test.co.uk).
        /// </summary>
        public static string GetRootDomain(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return string.Empty;

            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 2) return host;

            if (parts.Length >= 3 && (parts[^2] == "com" || parts[^2] == "co" || parts[^2] == "org" || parts[^2] == "net" || parts[^2] == "edu") && parts[^1].Length == 2)
            {
                return $"{parts[^3]}.{parts[^2]}.{parts[^1]}";
            }

            return $"{parts[^2]}.{parts[^1]}";
        }

        /// <summary>
        /// İki alan adının aynı kök domain veya alt domaine sahip olup olmadığını karşılaştırır.
        /// </summary>
        public static bool IsDomainMatch(string currentHost, string savedDomain)
        {
            if (string.IsNullOrEmpty(currentHost) || string.IsNullOrEmpty(savedDomain))
                return false;

            currentHost = currentHost.ToLowerInvariant().Trim();
            savedDomain = savedDomain.ToLowerInvariant().Trim();

            if (savedDomain.Contains("://"))
            {
                try { savedDomain = new Uri(savedDomain).Host.ToLowerInvariant().Trim(); } catch { }
            }

            if (savedDomain.Contains('/')) savedDomain = savedDomain.Split('/')[0];
            if (savedDomain.Contains(':')) savedDomain = savedDomain.Split(':')[0];
            if (savedDomain.Contains('?')) savedDomain = savedDomain.Split('?')[0];

            if (currentHost.Contains("://"))
            {
                try { currentHost = new Uri(currentHost).Host.ToLowerInvariant().Trim(); } catch { }
            }

            if (currentHost.Contains('/')) currentHost = currentHost.Split('/')[0];
            if (currentHost.Contains(':')) currentHost = currentHost.Split(':')[0];
            if (currentHost.Contains('?')) currentHost = currentHost.Split('?')[0];

            if (currentHost == savedDomain) return true;

            if (currentHost.EndsWith("." + savedDomain) || savedDomain.EndsWith("." + currentHost))
                return true;

            string root1 = GetRootDomain(currentHost);
            string root2 = GetRootDomain(savedDomain);

            if (!string.IsNullOrEmpty(root1) && !string.IsNullOrEmpty(root2) && root1 == root2)
                return true;

            if (currentHost.Contains(savedDomain) || savedDomain.Contains(currentHost))
                return true;

            return false;
        }
    }
}
