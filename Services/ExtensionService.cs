using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class ExtensionService : IExtensionService
    {
        private static ExtensionService? _instance;
        public static ExtensionService Instance
        {
            get
            {
                if (App.Services?.GetService(typeof(Bear_Browser.Services.Interfaces.IExtensionService)) is ExtensionService s)
                    return s;
                return _instance ??= new ExtensionService();
            }
        }

        public string ExtensionsDirectory { get; }
        public ObservableCollection<ExtensionInfo> InstalledExtensions { get; } = new();

        public event EventHandler<ExtensionInfo>? ExtensionInstalled;
        public event EventHandler<string>? ExtensionRemoved;

        private static readonly HttpClient _httpClient = new HttpClient();
        private CoreWebView2Profile? _lastProfile;

        public ExtensionService()
        {
            ExtensionsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser",
                "User Data",
                "Extensions"
            );

            try
            {
                Directory.CreateDirectory(ExtensionsDirectory);
            }
            catch { }

            LoadLocalExtensionsFromDisk();
        }

        public void LoadLocalExtensionsFromDisk()
        {
            try
            {
                if (!Directory.Exists(ExtensionsDirectory)) return;

                var dirs = Directory.GetDirectories(ExtensionsDirectory);
                foreach (var dir in dirs)
                {
                    string manifestPath = Path.Combine(dir, "manifest.json");
                    if (!File.Exists(manifestPath))
                    {
                        var sub = Directory.GetDirectories(dir).FirstOrDefault(d => File.Exists(Path.Combine(d, "manifest.json")));
                        if (sub != null)
                        {
                            SafeCopyDirectory(sub, dir);
                            manifestPath = Path.Combine(dir, "manifest.json");
                        }
                        else
                        {
                            continue;
                        }
                    }

                    string extId = Path.GetFileName(dir).ToLowerInvariant();
                    if (InstalledExtensions.Any(e => e.Id.Equals(extId, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var info = ParseExtensionManifest(dir, extId, "Chrome", null);
                    InstalledExtensions.Add(info);
                }
            }
            catch { }
        }

        public bool IsStoreExtensionUrl(string url, out string storeType, out string extensionId)
        {
            storeType = "";
            extensionId = "";

            if (string.IsNullOrWhiteSpace(url)) return false;

            // Chrome Web Mağazası
            if (url.Contains("chromewebstore.google.com/detail", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("chrome.google.com/webstore/detail", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(url, @"/detail/.*?([a-p]{32})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    storeType = "Chrome";
                    extensionId = match.Groups[1].Value.ToLowerInvariant();
                    return true;
                }
            }

            // Microsoft Edge Eklentileri
            if (url.Contains("microsoftedge.microsoft.com/addons/detail", StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(url, @"/detail/.*?([a-z0-9]{32})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    storeType = "Edge";
                    extensionId = match.Groups[1].Value.ToLowerInvariant();
                    return true;
                }
            }

            return false;
        }

        public async Task<ExtensionInfo?> InstallFromCrxFileAsync(string crxFilePath, string extensionId, string storeType, string? fallbackName, CoreWebView2Profile? profile)
        {
            if (string.IsNullOrWhiteSpace(crxFilePath) || !File.Exists(crxFilePath))
                return null;

            if (profile != null) _lastProfile = profile;
            else profile = _lastProfile;

            string targetFolder = Path.Combine(ExtensionsDirectory, extensionId);

            try
            {
                byte[] crxBytes = await File.ReadAllBytesAsync(crxFilePath);
                if (crxBytes.Length < 16)
                    throw new InvalidOperationException("İndirilen eklenti dosyası geçersiz veya boş.");

                // CRX Başlığını Atlayıp ZIP Arşivini Bul (0x50, 0x4B, 0x03, 0x04)
                int zipOffset = -1;
                for (int i = 0; i < Math.Min(100000, crxBytes.Length - 4); i++)
                {
                    if (crxBytes[i] == 0x50 && crxBytes[i + 1] == 0x4B && crxBytes[i + 2] == 0x03 && crxBytes[i + 3] == 0x04)
                    {
                        zipOffset = i;
                        break;
                    }
                }

                if (zipOffset == -1)
                    throw new InvalidOperationException("CRX dosyasında geçerli bir ZIP arşivi bulunamadı.");

                // Geçici bir klasöre çıkart, ardından kilitli dosyaları atlayarak hedef klasöre güvenle kopyala
                string tempFolder = Path.Combine(Path.GetTempPath(), "BearExt_" + Guid.NewGuid().ToString("N"));
                try
                {
                    Directory.CreateDirectory(tempFolder);
                    using (var ms = new MemoryStream(crxBytes, zipOffset, crxBytes.Length - zipOffset))
                    using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        archive.ExtractToDirectory(tempFolder, overwriteFiles: true);
                    }

                    Directory.CreateDirectory(targetFolder);
                    SafeCopyDirectory(tempFolder, targetFolder);
                }
                finally
                {
                    try { Directory.Delete(tempFolder, true); } catch { }
                    try { File.Delete(crxFilePath); } catch { }
                }

                // Manifest.json'u Ayrıştır ve Bilgileri Oku
                if (!File.Exists(Path.Combine(targetFolder, "manifest.json")))
                {
                    var sub = Directory.GetDirectories(targetFolder).FirstOrDefault(d => File.Exists(Path.Combine(d, "manifest.json")));
                    if (sub != null)
                    {
                        SafeCopyDirectory(sub, targetFolder);
                    }
                }

                var info = ParseExtensionManifest(targetFolder, extensionId, storeType, fallbackName);

                // WebView2 Motoruna Eklentiyi Yükle / Etkinleştir
                if (profile != null)
                {
                    try
                    {
                        var webViewExtensions = await profile.GetBrowserExtensionsAsync();
                        var existingInProfile = webViewExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
                        if (existingInProfile == null)
                        {
                            await profile.AddBrowserExtensionAsync(targetFolder);
                        }
                        else
                        {
                            await existingInProfile.EnableAsync(true);
                        }
                    }
                    catch (Exception pEx)
                    {
                        CrashReportingService.Instance.LogCustomError("Extension Profile Registration Warning", $"Eklenti: {extensionId}\nUyarı: {pEx.Message}");
                    }
                }

                // Listeyi Güncelle
                var existing = InstalledExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    InstalledExtensions.Remove(existing);
                }
                InstalledExtensions.Add(info);

                ExtensionInstalled?.Invoke(this, info);
                return info;
            }
            catch (Exception ex)
            {
                CrashReportingService.Instance.LogCustomError("Extension Installation Failed", $"Eklenti: {extensionId}\nTür: {storeType}\nHata: {ex.Message}\nStack:\n{ex.StackTrace}");
                throw;
            }
        }

        public async Task<ExtensionInfo?> DownloadAndInstallAsync(string storeType, string extensionId, string? fallbackName, CoreWebView2Profile? profile)
        {
            if (string.IsNullOrWhiteSpace(extensionId)) return null;

            if (profile != null) _lastProfile = profile;
            else profile = _lastProfile;

            // 1. CRX Dosyasını İndir (Birincil ve İkincil Mağaza Desteği ile)
            byte[]? crxBytes = null;
            string primaryUrl = storeType.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                ? $"https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect&x=id%3D{extensionId}%26installsource%3Dondemand%26uc"
                : $"https://clients2.google.com/service/update2/crx?response=redirect&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64&prod=chromecrx&prodchannel=&prodversion=130.0.0.0&lang=tr&acceptformat=crx3&x=id%3D{extensionId}%26installsource%3Dondemand%26uc";

            string fallbackUrl = storeType.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                ? $"https://clients2.google.com/service/update2/crx?response=redirect&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64&prod=chromecrx&prodchannel=&prodversion=130.0.0.0&lang=tr&acceptformat=crx3&x=id%3D{extensionId}%26installsource%3Dondemand%26uc"
                : $"https://edge.microsoft.com/extensionwebstorebase/v1/crx?response=redirect&x=id%3D{extensionId}%26installsource%3Dondemand%26uc";

            try
            {
                crxBytes = await FetchCrxBytesAsync(primaryUrl);
            }
            catch
            {
                try
                {
                    crxBytes = await FetchCrxBytesAsync(fallbackUrl);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Eklenti mağazadan indirilemedi. İnternet bağlantınızı veya mağaza adresini kontrol edin ({ex.Message}).");
                }
            }

            if (crxBytes == null || crxBytes.Length < 16)
                throw new InvalidOperationException("İndirilen eklenti dosyası geçersiz veya boş.");

            string tempCrxPath = Path.Combine(Path.GetTempPath(), $"BearExt_{extensionId}_{Guid.NewGuid():N}.crx");
            await File.WriteAllBytesAsync(tempCrxPath, crxBytes);

            return await InstallFromCrxFileAsync(tempCrxPath, extensionId, storeType, fallbackName, profile);
        }

        private static async Task<byte[]> FetchCrxBytesAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        private static void SafeCopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                try
                {
                    File.Copy(file, destFile, overwrite: true);
                }
                catch { }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                SafeCopyDirectory(subDir, destSubDir);
            }
        }

        public async Task SyncInstalledExtensionsAsync(CoreWebView2Profile? profile)
        {
            try
            {
                if (profile != null) _lastProfile = profile;
                else profile = _lastProfile;

                LoadLocalExtensionsFromDisk();

                if (profile != null)
                {
                    var webViewExtensions = await profile.GetBrowserExtensionsAsync();
                    foreach (var ext in InstalledExtensions.ToList())
                    {
                        var existing = webViewExtensions.FirstOrDefault(we => we.Id.Equals(ext.Id, StringComparison.OrdinalIgnoreCase));
                        if (existing == null)
                        {
                            try
                            {
                                var added = await profile.AddBrowserExtensionAsync(ext.FolderPath);
                                if (added != null && !ext.IsEnabled)
                                {
                                    await added.EnableAsync(false);
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            ext.IsEnabled = existing.IsEnabled;
                        }
                    }
                }
            }
            catch { }
        }

        public async Task SetExtensionEnabledAsync(string extensionId, bool enable, CoreWebView2Profile? profile)
        {
            if (profile != null) _lastProfile = profile;
            else profile = _lastProfile;

            var match = InstalledExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                match.IsEnabled = enable;
            }

            if (profile != null)
            {
                try
                {
                    var webViewExtensions = await profile.GetBrowserExtensionsAsync();
                    var target = webViewExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                    {
                        await target.EnableAsync(enable);
                    }
                }
                catch { }
            }
        }

        public async Task RemoveExtensionAsync(string extensionId, CoreWebView2Profile? profile)
        {
            if (profile != null) _lastProfile = profile;
            else profile = _lastProfile;

            if (profile != null)
            {
                try
                {
                    var webViewExtensions = await profile.GetBrowserExtensionsAsync();
                    var target = webViewExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                    {
                        await target.RemoveAsync();
                    }
                }
                catch { }
            }

            string folder = Path.Combine(ExtensionsDirectory, extensionId);
            if (Directory.Exists(folder))
            {
                try { Directory.Delete(folder, true); } catch { }
            }

            var match = InstalledExtensions.FirstOrDefault(e => e.Id.Equals(extensionId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                InstalledExtensions.Remove(match);
            }

            ExtensionRemoved?.Invoke(this, extensionId);
        }

        public async Task<ExtensionInfo?> InstallFromFolderAsync(string folderPath, CoreWebView2Profile? profile)
        {
            if (!Directory.Exists(folderPath)) return null;

            if (profile != null) _lastProfile = profile;
            else profile = _lastProfile;

            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Seçilen klasörde manifest.json dosyası bulunamadı.");
            }

            string extId = Path.GetFileName(folderPath).ToLowerInvariant();
            var info = ParseExtensionManifest(folderPath, extId, "Yerel (Geliştirici)", null);

            if (profile != null)
            {
                try
                {
                    var webViewExtensions = await profile.GetBrowserExtensionsAsync();
                    var existing = webViewExtensions.FirstOrDefault(e => e.Id.Equals(extId, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        await profile.AddBrowserExtensionAsync(folderPath);
                    }
                    else
                    {
                        await existing.EnableAsync(true);
                    }
                }
                catch { }
            }

            var match = InstalledExtensions.FirstOrDefault(e => e.Id.Equals(extId, StringComparison.OrdinalIgnoreCase));
            if (match != null) InstalledExtensions.Remove(match);
            InstalledExtensions.Add(info);

            ExtensionInstalled?.Invoke(this, info);
            return info;
        }

        private ExtensionInfo ParseExtensionManifest(string folderPath, string extensionId, string storeSource, string? fallbackName)
        {
            var info = new ExtensionInfo
            {
                Id = extensionId,
                FolderPath = folderPath,
                Source = storeSource,
                StoreUrl = storeSource.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                    ? $"https://microsoftedge.microsoft.com/addons/detail/{extensionId}"
                    : $"https://chromewebstore.google.com/detail/{extensionId}"
            };

            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(manifestPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("name", out var nameProp))
                    {
                        info.Name = ResolveLocalizedMessage(folderPath, nameProp.GetString() ?? "");
                    }

                    if (root.TryGetProperty("version", out var verProp))
                    {
                        info.Version = verProp.GetString() ?? "1.0.0";
                    }

                    if (root.TryGetProperty("description", out var descProp))
                    {
                        info.Description = ResolveLocalizedMessage(folderPath, descProp.GetString() ?? "");
                    }

                    // İkon arama (manifest icons, action/browser_action default_icon veya klasör araması)
                    info.IconPath = FindExtensionIcon(folderPath, root);
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(info.Name))
            {
                info.Name = fallbackName ?? $"Eklenti ({extensionId[..Math.Min(8, extensionId.Length)]})";
            }

            return info;
        }

        private string ResolveLocalizedMessage(string folderPath, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            if (!raw.StartsWith("__MSG_") || !raw.EndsWith("__")) return raw;

            string key = raw.Substring(6, raw.Length - 8);

            // Önce Türkçe ardından İngilizce dil klasörlerine bak
            string[] preferredLocales = { "tr", "tr_TR", "en", "en_US", "en_GB" };
            foreach (var loc in preferredLocales)
            {
                string msgPath = Path.Combine(folderPath, "_locales", loc, "messages.json");
                if (File.Exists(msgPath))
                {
                    try
                    {
                        string json = File.ReadAllText(msgPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty(key, out var keyProp) &&
                            keyProp.TryGetProperty("message", out var msgVal))
                        {
                            string val = msgVal.GetString() ?? "";
                            if (!string.IsNullOrEmpty(val)) return val;
                        }
                    }
                    catch { }
                }
            }

            return key; // Çözülemezse anahtar adını döndür
        }

        private static string FindExtensionIcon(string folderPath, JsonElement root)
        {
            try
            {
                // 1. "icons" nesnesi
                if (root.TryGetProperty("icons", out var iconsProp) && iconsProp.ValueKind == JsonValueKind.Object)
                {
                    string[] preferredSizes = { "128", "64", "48", "32", "16", "256", "512" };
                    foreach (var size in preferredSizes)
                    {
                        if (iconsProp.TryGetProperty(size, out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                        {
                            string p = pathProp.GetString() ?? "";
                            string fullPath = Path.Combine(folderPath, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(fullPath)) return fullPath;
                        }
                    }

                    foreach (var prop in iconsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            string p = prop.Value.GetString() ?? "";
                            string fullPath = Path.Combine(folderPath, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                            if (File.Exists(fullPath)) return fullPath;
                        }
                    }
                }

                // 2. "action" -> "default_icon" veya "browser_action" -> "default_icon"
                string[] actionKeys = { "action", "browser_action" };
                foreach (var actKey in actionKeys)
                {
                    if (root.TryGetProperty(actKey, out var actProp) && actProp.ValueKind == JsonValueKind.Object)
                    {
                        if (actProp.TryGetProperty("default_icon", out var iconProp))
                        {
                            if (iconProp.ValueKind == JsonValueKind.String)
                            {
                                string p = iconProp.GetString() ?? "";
                                string fullPath = Path.Combine(folderPath, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                                if (File.Exists(fullPath)) return fullPath;
                            }
                            else if (iconProp.ValueKind == JsonValueKind.Object)
                            {
                                string[] preferredSizes = { "128", "64", "48", "32", "16" };
                                foreach (var size in preferredSizes)
                                {
                                    if (iconProp.TryGetProperty(size, out var pProp) && pProp.ValueKind == JsonValueKind.String)
                                    {
                                        string p = pProp.GetString() ?? "";
                                        string fullPath = Path.Combine(folderPath, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                                        if (File.Exists(fullPath)) return fullPath;
                                    }
                                }
                                foreach (var prop in iconProp.EnumerateObject())
                                {
                                    if (prop.Value.ValueKind == JsonValueKind.String)
                                    {
                                        string p = prop.Value.GetString() ?? "";
                                        string fullPath = Path.Combine(folderPath, p.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
                                        if (File.Exists(fullPath)) return fullPath;
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Klasörde doğrudan ikon dosyası arama
                string[] searchPatterns = { "*icon*128*.png", "*icon*48*.png", "*icon*32*.png", "*icon*.png", "*logo*.png", "*.png" };
                foreach (var pattern in searchPatterns)
                {
                    var files = Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }
            catch { }

            return "";
        }
    }
}
