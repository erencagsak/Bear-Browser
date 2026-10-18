using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.UI.Xaml;

namespace Bear_Browser.Services
{
    public class UpdateService : IUpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private readonly ISettingsService _settingsService;

        public string CurrentVersion
        {
            get
            {
                try
                {
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    if (version != null)
                    {
                        return $"{version.Major}.{version.Minor}.{version.Build}";
                    }
                }
                catch { }

                return "1.0.0";
            }
        }

        public static string ConfiguredGitHubRepo { get; set; } = "erencagsak/Bear-Browser";

        public string GitHubRepo
        {
            get => ConfiguredGitHubRepo;
            set => ConfiguredGitHubRepo = value;
        }

        public UpdateService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(bool silent = false)
        {
            var info = new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                IsUpdateAvailable = false
            };

            string repo = ConfiguredGitHubRepo?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(repo))
            {
                info.ErrorMessage = "GitHub deposu kod içerisinde tanımlanmadı (ConfiguredGitHubRepo).";
                return info;
            }

            try
            {
                string apiUrl = $"https://api.github.com/repos/{repo}/releases/latest";

                using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.UserAgent.ParseAdd("BearBrowser-Updater/1.0");
                request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        info.ErrorMessage = $"'{repo}' deposunda yayınlanmış bir sürüm (Release) bulunamadı veya repo özel.";
                    }
                    else
                    {
                        info.ErrorMessage = $"GitHub API Hatası: {(int)response.StatusCode} ({response.ReasonPhrase})";
                    }

                    return info;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
                string releaseName = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";

                info.ReleaseTitle = string.IsNullOrWhiteSpace(releaseName) ? tagName : releaseName;
                info.ReleaseHtmlUrl = htmlUrl;

                string? readmeNotes = await FetchChangelogFromReadmeAsync(repo);
                if (!string.IsNullOrWhiteSpace(readmeNotes))
                {
                    info.ReleaseNotes = readmeNotes;
                }
                else
                {
                    info.ReleaseNotes = string.IsNullOrWhiteSpace(body)
                        ? "Hata düzeltmeleri ve performans iyileştirmeleri içerir."
                        : body;
                }

                if (root.TryGetProperty("published_at", out var pubElem) && pubElem.TryGetDateTimeOffset(out var pubDate))
                {
                    info.PublishedAt = pubDate;
                }

                string cleanedTag = tagName.Trim().TrimStart('v', 'V');
                info.LatestVersion = cleanedTag;

                info.IsUpdateAvailable = IsNewerVersion(cleanedTag, CurrentVersion);

                if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElem.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            assetName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var dl))
                            {
                                info.InstallerDownloadUrl = dl.GetString();
                            }
                            if (asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sizeBytes))
                            {
                                info.InstallerSize = sizeBytes;
                            }
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(info.InstallerDownloadUrl))
                {
                    info.InstallerDownloadUrl = info.ReleaseHtmlUrl;
                }

                return info;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Güncelleme kontrolü sırasında hata: {ex.Message}";
                return info;
            }
        }

        public async Task<string?> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (update.IsTestSimulation)
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    progress?.Report(i);
                    await Task.Delay(120, cancellationToken);
                }

                return Path.Combine(Path.GetTempPath(), "BearBrowserSetup_Simulated.exe");
            }

            if (string.IsNullOrWhiteSpace(update.InstallerDownloadUrl) ||
                !update.InstallerDownloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "BearBrowserUpdate");
                Directory.CreateDirectory(tempDir);

                string fileName = Path.GetFileName(new Uri(update.InstallerDownloadUrl).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = "BearBrowserSetup.exe";
                }

                string destPath = Path.Combine(tempDir, fileName);

                using var response = await _httpClient.GetAsync(update.InstallerDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? (update.InstallerSize > 0 ? update.InstallerSize : -1);
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        double percent = (double)totalRead / totalBytes * 100.0;
                        progress?.Report(Math.Min(100.0, percent));
                    }
                }

                progress?.Report(100.0);
                return destPath;
            }
            catch
            {
                return null;
            }
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            try
            {
                if (File.Exists(installerPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true
                    });

                    Application.Current.Exit();
                }
            }
            catch { }
        }

        public UpdateInfo GenerateSimulatedUpdate()
        {
            return new UpdateInfo
            {
                IsUpdateAvailable = true,
                CurrentVersion = CurrentVersion,
                LatestVersion = "1.1.0",
                ReleaseTitle = "Bear Browser v1.1.0 (Performans & Güvenlik Güncellemesi)",
                ReleaseNotes = "• Sekmeler arası geçiş hızlandırıldı ve bellek tüketimi optimize edildi.\n• Bear AdBlock ve YouTube kalkanı güncellendi.\n• Gelişmiş karanlık tema renk doygunluğu artırıldı.",
                ReleaseHtmlUrl = "https://github.com/",
                InstallerDownloadUrl = "https://github.com/BearBrowserSetup.exe",
                InstallerSize = 75000000,
                PublishedAt = DateTimeOffset.Now,
                IsTestSimulation = true
            };
        }

        private async Task<string?> FetchChangelogFromReadmeAsync(string repo)
        {
            try
            {
                string[] branches = { "main", "master" };
                foreach (var branch in branches)
                {
                    try
                    {
                        string rawUrl = $"https://raw.githubusercontent.com/{repo}/{branch}/README.md";
                        using var req = new HttpRequestMessage(HttpMethod.Get, rawUrl);
                        req.Headers.UserAgent.ParseAdd("BearBrowser-Updater/1.0");

                        using var res = await _httpClient.SendAsync(req);
                        if (res.IsSuccessStatusCode)
                        {
                            string markdown = await res.Content.ReadAsStringAsync();
                            string? notes = ExtractChangelogSection(markdown);
                            if (!string.IsNullOrWhiteSpace(notes))
                            {
                                return notes;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        private static string? ExtractChangelogSection(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return null;

            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inSection = false;
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#") && (
                    trimmed.Contains("Yenilik", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Güncelleme", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Değişiklik", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Sürüm", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Changelog", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("What's New", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("Release Notes", StringComparison.OrdinalIgnoreCase)))
                {
                    inSection = true;
                    continue;
                }

                if (inSection)
                {
                    if (trimmed.StartsWith("#") && !trimmed.StartsWith("###"))
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        sb.AppendLine(trimmed);
                    }
                }
            }

            string result = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            var bulletLines = lines.Where(l => l.TrimStart().StartsWith("- ") || l.TrimStart().StartsWith("* ") || l.TrimStart().StartsWith("• ")).Take(5);
            string bulletResult = string.Join("\n", bulletLines).Trim();
            return !string.IsNullOrWhiteSpace(bulletResult) ? bulletResult : null;
        }

        private static bool IsNewerVersion(string remoteVersionStr, string currentVersionStr)
        {
            try
            {
                string normRemote = NormalizeVersionString(remoteVersionStr);
                string normCurrent = NormalizeVersionString(currentVersionStr);

                if (Version.TryParse(normRemote, out var remoteVer) && Version.TryParse(normCurrent, out var currentVer))
                {
                    return remoteVer > currentVer;
                }
            }
            catch { }

            return string.Compare(remoteVersionStr, currentVersionStr, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private static string NormalizeVersionString(string v)
        {
            v = v.Trim().TrimStart('v', 'V');
            int dotCount = 0;
            foreach (char c in v) if (c == '.') dotCount++;

            if (dotCount == 0) return $"{v}.0.0";
            if (dotCount == 1) return $"{v}.0";
            return v;
        }
    }
}
