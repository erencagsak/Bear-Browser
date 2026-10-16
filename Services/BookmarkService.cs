using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;

namespace Bear_Browser.Services
{
    public class BookmarkService : IBookmarkService
    {
        private readonly IJsonStorageService _storageService;
        private readonly string _bookmarksFile;
        private readonly string _foldersFile;

        public List<BookmarkItem> Bookmarks { get; private set; } = new();
        public List<string> Folders { get; private set; } = new()
        {
            "Sık kullanılanlar çubuğu"
        };

        public event EventHandler? BookmarksUpdated;

        public BookmarkService(IJsonStorageService storageService, string? baseFolder = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            string folder = baseFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(folder);
            _bookmarksFile = Path.Combine(folder, "bookmarks.json");
            _foldersFile = Path.Combine(folder, "folders.json");

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_bookmarksFile))
                    Bookmarks = _storageService.Load<List<BookmarkItem>>(_bookmarksFile) ?? new();

                if (File.Exists(_foldersFile))
                {
                    var loadedFolders = _storageService.Load<List<string>>(_foldersFile);
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

            await _storageService.SaveAsync(_bookmarksFile, Bookmarks);
            BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            return existing;
        }

        public async Task RemoveBookmarkByUrlAsync(string url)
        {
            var existing = Bookmarks.Find(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Bookmarks.Remove(existing);
                await _storageService.SaveAsync(_bookmarksFile, Bookmarks);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task RemoveBookmarkAsync(Guid id)
        {
            var existing = Bookmarks.Find(b => b.Id == id);
            if (existing != null)
            {
                Bookmarks.Remove(existing);
                await _storageService.SaveAsync(_bookmarksFile, Bookmarks);
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
                await _storageService.SaveAsync(_foldersFile, Folders);
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
            await _storageService.SaveAsync(_foldersFile, Folders);
            await _storageService.SaveAsync(_bookmarksFile, Bookmarks);
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

        public async Task ExportBookmarksHtmlAsync(string filePath)
        {
            var sb = new StringBuilder();
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

                sb.AppendLine($"    <DT><H3>{WebUtility.HtmlEncode(folder)}</H3>");
                sb.AppendLine("    <DL><p>");
                foreach (var b in folderBookmarks)
                {
                    sb.AppendLine($"        <DT><A HREF=\"{WebUtility.HtmlEncode(b.Url)}\">{WebUtility.HtmlEncode(b.Title)}</A>");
                }
                sb.AppendLine("    </DL><p>");
            }

            sb.AppendLine("</DL><p>");
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        }

        public async Task<int> ImportBookmarksHtmlAsync(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

            int imported = 0;
            string currentFolder = "Sık kullanılanlar çubuğu";

            var tagRegex = new Regex(@"<(H3)[^>]*>(?<folder>[^<]+)<\/H3>|<(A)\s+[^>]*?HREF=""(?<url>[^""]*)""[^>]*>(?<title>[^<]*)<\/A>", RegexOptions.IgnoreCase);
            var matches = tagRegex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Groups["folder"].Success)
                {
                    string fName = WebUtility.HtmlDecode(match.Groups["folder"].Value.Trim());
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
                    string url = WebUtility.HtmlDecode(match.Groups["url"].Value.Trim());
                    string title = WebUtility.HtmlDecode(match.Groups["title"].Value.Trim());
                    if (string.IsNullOrWhiteSpace(title)) title = url;

                    if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!Bookmarks.Any(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase) && b.Folder.Equals(currentFolder, StringComparison.OrdinalIgnoreCase)))
                        {
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
                await _storageService.SaveAsync(_foldersFile, Folders);
                await _storageService.SaveAsync(_bookmarksFile, Bookmarks);
                BookmarksUpdated?.Invoke(this, EventArgs.Empty);
            }

            return imported;
        }
    }
}
