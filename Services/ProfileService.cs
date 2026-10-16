using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services
{
    public class ProfileService
    {
        private static ProfileService? _instance;
        public static ProfileService Instance => _instance ??= new ProfileService();

        public UserProfile CurrentProfile { get; private set; }
        public event EventHandler<UserProfile>? ProfileUpdated;
        public event EventHandler? BookmarksUpdated;
        public event EventHandler? HistoryUpdated;
        public event EventHandler? DownloadsUpdated;
        public event EventHandler? PasswordsUpdated;

        private readonly string _profileFile;
        private readonly string _historyFile;
        private readonly string _bookmarksFile;
        private readonly string _downloadsFile;
        private readonly string _passwordsFile;
        private readonly string _foldersFile;

        public List<HistoryItem> History { get; private set; } = new();
        public List<BookmarkItem> Bookmarks { get; private set; } = new();
        public List<DownloadItem> Downloads { get; private set; } = new();
        public List<PasswordItem> Passwords { get; private set; } = new();

        // Yalnızca gerçek varsayılan klasörler (Örnek sahte klasörler kaldırıldı)
        public List<string> Folders { get; private set; } = new()
        {
            "Sık kullanılanlar çubuğu"
        };

        public ProfileService()
        {
            string baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(baseFolder);

            _profileFile = Path.Combine(baseFolder, "profile.json");
            _historyFile = Path.Combine(baseFolder, "history.json");
            _bookmarksFile = Path.Combine(baseFolder, "bookmarks.json");
            _downloadsFile = Path.Combine(baseFolder, "downloads.json");
            _passwordsFile = Path.Combine(baseFolder, "passwords.json");
            _foldersFile = Path.Combine(baseFolder, "folders.json");

            CurrentProfile = new UserProfile
            {
                ProfileFolderPath = baseFolder
            };

            LoadAllData();
        }

        private void LoadAllData()
        {
            try
            {
                if (File.Exists(_profileFile))
                {
                    var loadedProfile = JsonSerializer.Deserialize<UserProfile>(File.ReadAllText(_profileFile));
                    if (loadedProfile != null)
                    {
                        CurrentProfile = loadedProfile;
                        CurrentProfile.ProfileFolderPath = Path.GetDirectoryName(_profileFile) ?? "";
                    }
                }

                if (File.Exists(_historyFile))
                    History = JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(_historyFile)) ?? new();
                if (File.Exists(_bookmarksFile))
                    Bookmarks = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(_bookmarksFile)) ?? new();
                if (File.Exists(_downloadsFile))
                    Downloads = JsonSerializer.Deserialize<List<DownloadItem>>(File.ReadAllText(_downloadsFile)) ?? new();
                if (File.Exists(_passwordsFile))
                    Passwords = JsonSerializer.Deserialize<List<PasswordItem>>(File.ReadAllText(_passwordsFile)) ?? new();

                // LoadAllData() içindeki filtreleme:
                if (File.Exists(_foldersFile))
                {
                    var loadedFolders = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_foldersFile));
                    if (loadedFolders != null)
                    {
                        var removeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Videolar", "Dizi", "AI", "İş Başvuru", "Assets", "Diğer sık kullanılanlar"
                        };
                        Folders = loadedFolders.Where(f => !removeSet.Contains(f)).Distinct().ToList();

                        if (!Folders.Contains("Sık kullanılanlar çubuğu"))
                           Folders.Insert(0, "Sık kullanılanlar çubuğu");
                    }
                }
            }
            catch { }
        }

        public async Task UpdateProfileAsync(string newName, string? newSourceImagePath)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            if (!string.IsNullOrWhiteSpace(newSourceImagePath) && File.Exists(newSourceImagePath))
            {
                string extension = Path.GetExtension(newSourceImagePath);
                string newFileName = $"avatar_{DateTime.UtcNow.Ticks}{extension}";
                string destPath = Path.Combine(CurrentProfile.ProfileFolderPath, newFileName);

                try
                {
                    foreach (var old in Directory.GetFiles(CurrentProfile.ProfileFolderPath, "avatar*"))
                    {
                        try { File.Delete(old); } catch { }
                    }
                    File.Copy(newSourceImagePath, destPath, overwrite: true);
                    CurrentProfile.AvatarImagePath = destPath;
                }
                catch { }
            }

            await SaveToFileAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        public async Task UpdateProfileAvatarPathAsync(string newName, string avatarPath)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            CurrentProfile.AvatarImagePath = avatarPath;
            await SaveToFileAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        public async Task UpdateProfileNameAsync(string newName)
        {
            if (!string.IsNullOrWhiteSpace(newName))
                CurrentProfile.Name = newName.Trim();

            await SaveToFileAsync(_profileFile, CurrentProfile);
            ProfileUpdated?.Invoke(this, CurrentProfile);
        }

        public async Task AddHistoryAsync(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("bear://") || url.StartsWith("edge://") || url.StartsWith("about:")) return;

            History.Insert(0, new HistoryItem
            {
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                Url = url,
                VisitedAt = DateTime.Now
            });

            if (History.Count > 2000) History.RemoveAt(History.Count - 1);
            await SaveToFileAsync(_historyFile, History);
        }

        public async Task<BookmarkItem> AddOrUpdateBookmarkAsync(string url, string title, string folder)
        {
            var existing = Bookmarks.Find(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Title = title;
                existing.Folder = string.IsNullOrWhiteSpace(folder) ? "Sık kullanılanlar çubuğu" : folder;
            }
            else
            {
                existing = new BookmarkItem
                {
                    Title = string.IsNullOrWhiteSpace(title) ? url : title,
                    Url = url,
                    Folder = string.IsNullOrWhiteSpace(folder) ? "Sık kullanılanlar çubuğu" : folder,
                    CreatedAt = DateTime.Now
                };
                Bookmarks.Insert(0, existing);
            }

            await SaveToFileAsync(_bookmarksFile, Bookmarks);
            BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            return existing;
        }

        public async Task RemoveBookmarkByUrlAsync(string url)
        {
            var existing = Bookmarks.Find(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Bookmarks.Remove(existing);
                await SaveToFileAsync(_bookmarksFile, Bookmarks);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task RemoveBookmarkAsync(Guid id)
        {
            var existing = Bookmarks.Find(b => b.Id == id);
            if (existing != null)
            {
                Bookmarks.Remove(existing);
                await SaveToFileAsync(_bookmarksFile, Bookmarks);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task AddFolderAsync(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return;
            string clean = folderName.Trim();
            if (!Folders.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                Folders.Add(clean);
                await SaveToFileAsync(_foldersFile, Folders);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task RemoveFolderAsync(string folderName)
        {
            if (folderName == "Sık kullanılanlar çubuğu") return;
            Folders.Remove(folderName);
            foreach (var b in Bookmarks.Where(b => b.Folder.Equals(folderName, StringComparison.OrdinalIgnoreCase)))
            {
                b.Folder = "Sık kullanılanlar çubuğu";
            }
            await SaveToFileAsync(_foldersFile, Folders);
            await SaveToFileAsync(_bookmarksFile, Bookmarks);
            BookmarksUpdated?.Invoke(this, EventArgs.Empty);
        }

        public bool IsBookmarked(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return Bookmarks.Exists(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        }

        public BookmarkItem? GetBookmark(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            return Bookmarks.Find(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
        }

        private async Task SaveToFileAsync<T>(string filePath, T data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json);
            }
            catch { }
        }
        public async Task RemoveHistoryItemAsync(Guid id)
        {
            var item = History.Find(h => h.Id == id);
            if (item != null)
            {
                History.Remove(item);
                await SaveToFileAsync(_historyFile, History);
                HistoryUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
        public async Task ClearHistoryAsync()
        {
            History.Clear();
            await SaveToFileAsync(_historyFile, History);
            HistoryUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void AddActiveDownload(DownloadItem item)
        {
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task CompleteDownloadAsync(DownloadItem item)
        {
            item.State = "Tamamlandı";
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            await SaveToFileAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task AddDownloadAsync(DownloadItem item)
        {
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            await SaveToFileAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemoveDownloadAsync(Guid id)
        {
            var item = Downloads.Find(d => d.Id == id);
            if (item != null)
            {
                Downloads.Remove(item);
                await SaveToFileAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
                DownloadsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ClearDownloadsAsync()
        {
            Downloads.Clear();
            await SaveToFileAsync(_downloadsFile, Downloads);
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task AddOrUpdatePasswordAsync(string url, string userName, string password)
        {
            string domain = "";
            try { domain = new Uri(url).Host; } catch { domain = url; }

            var existing = Passwords.Find(p => p.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) && p.UserName == userName);
            if (existing != null)
            {
                existing.Password = password;
                existing.SavedAt = DateTime.Now;
            }
            else
            {
                Passwords.Insert(0, new PasswordItem
                {
                    Url = url,
                    Domain = domain,
                    UserName = userName,
                    Password = password
                });
            }

            await SaveToFileAsync(_passwordsFile, Passwords);
            PasswordsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemovePasswordAsync(Guid id)
        {
            var item = Passwords.Find(p => p.Id == id);
            if (item != null)
            {
                Passwords.Remove(item);
                await SaveToFileAsync(_passwordsFile, Passwords);
                PasswordsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ClearPasswordsAsync()
        {
            Passwords.Clear();
            await SaveToFileAsync(_passwordsFile, Passwords);
            PasswordsUpdated?.Invoke(this, EventArgs.Empty);
        }

        // --- SIK KULLANILANLAR (BOOKMARKS) İÇERİ VE DIŞARI AKTARMA ---

        public async Task ExportBookmarksHtmlAsync(string filePath)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
            sb.AppendLine("<!-- This is an automatically generated file. It will be read and overwritten. Do NOT edit! -->");
            sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
            sb.AppendLine("<TITLE>Bookmarks</TITLE>");
            sb.AppendLine("<H1>Bookmarks</H1>");
            sb.AppendLine("<DL><p>");

            foreach (var folder in Folders)
            {
                var folderBookmarks = Bookmarks.Where(b => b.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList();
                if (folderBookmarks.Count == 0) continue;

                sb.AppendLine($"    <DT><H3>{System.Net.WebUtility.HtmlEncode(folder)}</H3>");
                sb.AppendLine("    <DL><p>");
                foreach (var b in folderBookmarks)
                {
                    sb.AppendLine($"        <DT><A HREF=\"{System.Net.WebUtility.HtmlEncode(b.Url)}\">{System.Net.WebUtility.HtmlEncode(b.Title)}</A>");
                }
                sb.AppendLine("    </DL><p>");
            }

            sb.AppendLine("</DL><p>");
            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        public async Task<int> ImportBookmarksHtmlAsync(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            string content = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);

            int imported = 0;
            string currentFolder = "Sık kullanılanlar çubuğu";

            var tagRegex = new System.Text.RegularExpressions.Regex(@"<(H3)[^>]*>(?<folder>[^<]+)<\/H3>|<(A)\s+[^>]*?HREF=""(?<url>[^""]*)""[^>]*>(?<title>[^<]*)<\/A>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var matches = tagRegex.Matches(content);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups["folder"].Success)
                {
                    string fName = System.Net.WebUtility.HtmlDecode(match.Groups["folder"].Value.Trim());
                    if (!string.IsNullOrWhiteSpace(fName))
                    {
                        currentFolder = fName;
                        if (!Folders.Contains(currentFolder, StringComparer.OrdinalIgnoreCase))
                        {
                            Folders.Add(currentFolder);
                        }
                    }
                }
                else if (match.Groups["url"].Success)
                {
                    string url = System.Net.WebUtility.HtmlDecode(match.Groups["url"].Value.Trim());
                    string title = System.Net.WebUtility.HtmlDecode(match.Groups["title"].Value.Trim());
                    if (string.IsNullOrWhiteSpace(title)) title = url;

                    if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!Bookmarks.Any(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase) && b.Folder.Equals(currentFolder, StringComparison.OrdinalIgnoreCase)))
                        {
                            string domain = "";
                            try { domain = new Uri(url).Host; } catch { }
                            string favicon = !string.IsNullOrEmpty(domain)
                                ? $"https://www.google.com/s2/favicons?domain={domain}&sz=32"
                                : "ms-appx:///Assets/Bear-Browser-Logo.jpg";

                            Bookmarks.Add(new BookmarkItem
                            {
                                Title = title,
                                Url = url,
                                Folder = currentFolder
                            });
                            imported++;
                        }
                    }
                }
            }

            if (imported > 0)
            {
                await SaveToFileAsync(_foldersFile, Folders);
                await SaveToFileAsync(_bookmarksFile, Bookmarks);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }

            return imported;
        }

        // --- KAYITLI PAROLALAR İÇERİ VE DIŞARI AKTARMA (CSV) ---

        public async Task ExportPasswordsCsvAsync(string filePath)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("name,url,username,password");
            foreach (var p in Passwords)
            {
                string name = EscapeCsv(p.Domain);
                string url = EscapeCsv(p.Url);
                string user = EscapeCsv(p.UserName);
                string pass = EscapeCsv(p.Password);
                sb.AppendLine($"{name},{url},{user},{pass}");
            }
            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        public async Task<int> ImportPasswordsCsvAsync(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            var lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);
            if (lines.Length <= 1) return 0;

            int imported = 0;
            bool isFirstLine = true;
            int nameIdx = 0, urlIdx = 1, userIdx = 2, passIdx = 3;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = ParseCsvLine(line);
                if (parts.Count < 3) continue;

                if (isFirstLine)
                {
                    isFirstLine = false;
                    if (parts.Any(p => p.Equals("url", StringComparison.OrdinalIgnoreCase) || p.Equals("username", StringComparison.OrdinalIgnoreCase) || p.Equals("password", StringComparison.OrdinalIgnoreCase)))
                    {
                        nameIdx = parts.FindIndex(p => p.Equals("name", StringComparison.OrdinalIgnoreCase));
                        urlIdx = parts.FindIndex(p => p.Equals("url", StringComparison.OrdinalIgnoreCase));
                        userIdx = parts.FindIndex(p => p.Equals("username", StringComparison.OrdinalIgnoreCase) || p.Equals("user", StringComparison.OrdinalIgnoreCase) || p.Equals("login", StringComparison.OrdinalIgnoreCase));
                        passIdx = parts.FindIndex(p => p.Equals("password", StringComparison.OrdinalIgnoreCase) || p.Equals("pass", StringComparison.OrdinalIgnoreCase));
                        if (urlIdx == -1) urlIdx = 1;
                        if (userIdx == -1) userIdx = 2;
                        if (passIdx == -1) passIdx = 3;
                        continue;
                    }
                }

                string url = urlIdx >= 0 && urlIdx < parts.Count ? parts[urlIdx] : "";
                string user = userIdx >= 0 && userIdx < parts.Count ? parts[userIdx] : "";
                string pass = passIdx >= 0 && passIdx < parts.Count ? parts[passIdx] : "";
                string domain = nameIdx >= 0 && nameIdx < parts.Count ? parts[nameIdx] : "";

                if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(domain))
                {
                    url = domain.StartsWith("http") ? domain : "https://" + domain;
                }

                if (!string.IsNullOrWhiteSpace(url) && (!string.IsNullOrWhiteSpace(user) || !string.IsNullOrWhiteSpace(pass)))
                {
                    await AddOrUpdatePasswordAsync(url, user, pass);
                    imported++;
                }
            }

            return imported;
        }

        private static string EscapeCsv(string val)
        {
            if (string.IsNullOrEmpty(val)) return "\"\"";
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(cur.ToString().Trim());
                    cur.Clear();
                }
                else
                {
                    cur.Append(c);
                }
            }
            result.Add(cur.ToString().Trim());
            return result;
        }
    }
}