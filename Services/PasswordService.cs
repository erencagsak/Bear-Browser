using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;

namespace Bear_Browser.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly IJsonStorageService _storageService;
        private readonly string _passwordsFile;

        public List<PasswordItem> Passwords { get; private set; } = new();
        public event EventHandler? PasswordsUpdated;

        public PasswordService(IJsonStorageService storageService, string? baseFolder = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            string folder = baseFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(folder);
            _passwordsFile = Path.Combine(folder, "passwords.json");

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_passwordsFile))
                    Passwords = _storageService.Load<List<PasswordItem>>(_passwordsFile) ?? new();
            }
            catch { }
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

            await _storageService.SaveAsync(_passwordsFile, Passwords);
            PasswordsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemovePasswordAsync(Guid id)
        {
            var item = Passwords.Find(p => p.Id == id);
            if (item != null)
            {
                Passwords.Remove(item);
                await _storageService.SaveAsync(_passwordsFile, Passwords);
                PasswordsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ClearPasswordsAsync()
        {
            Passwords.Clear();
            await _storageService.SaveAsync(_passwordsFile, Passwords);
            PasswordsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task ExportPasswordsCsvAsync(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("name,url,username,password");
            foreach (var p in Passwords)
            {
                string name = EscapeCsv(p.Domain);
                string url = EscapeCsv(p.Url);
                string user = EscapeCsv(p.UserName);
                string pass = EscapeCsv(p.Password);
                sb.AppendLine($"{name},{url},{user},{pass}");
            }
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        }

        public async Task<int> ImportPasswordsCsvAsync(string filePath)
        {
            if (!File.Exists(filePath)) return 0;
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
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
            var cur = new StringBuilder();

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
