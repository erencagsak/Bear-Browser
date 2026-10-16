using Bear_Browser.Helpers;
using Bear_Browser.Models;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Bear_Browser.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public bool IsInPrivate { get; }
        public ObservableCollection<BrowserTab> Tabs { get; } = new();

        private BrowserTab? _currentTab;
        public BrowserTab? CurrentTab
        {
            get => _currentTab;
            set
            {
                if (SetProperty(ref _currentTab, value))
                {
                    CurrentTabChanged?.Invoke(this, _currentTab);
                }
            }
        }

        public event EventHandler<BrowserTab?>? CurrentTabChanged;
        public event EventHandler<BrowserTab>? TabNavigated;
        public event EventHandler<(BrowserTab tab, bool isLoading)>? TabLoadingChanged;
        public event EventHandler<long>? PageLoadCompleted;
        public event EventHandler<int>? AdBlockedCountChanged;
        public event EventHandler<string>? ShortcutActionTriggered;
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<(bool isDownloading, bool isCompleted)>? DownloadStateChanged;
        public event EventHandler<(string url, string username, string password)>? PasswordSavePromptRequested;
        public event EventHandler<(string storeType, string extensionId, string name)>? ExtensionInstallPromptRequested;
        public event EventHandler<(string storeType, string extensionId, string name)>? ExtensionDownloadProgressStarted;
        public event EventHandler<string>? ExtensionInstalledSuccessfully;
        public event EventHandler<string>? ExtensionInstallFailed;

        public MainViewModel(bool isInPrivate = false)
        {
            IsInPrivate = isInPrivate;
        }

        public async Task<BrowserTab> CreateNewTabAsync(string initialUrl = "bear://newtab", bool isPopup = false)
        {
            var loc = LocalizationService.Instance;
            string tabTitle = loc["Tab_NewTab"];
            Uri tabFavicon = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");

            if (initialUrl.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = loc["Tab_History"];
                tabFavicon = new Uri("ms-appx:///Assets/history.png");
            }
            else if (initialUrl.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = loc["Tab_Favorites"];
                tabFavicon = new Uri("ms-appx:///Assets/favorites.png");
            }
            else if (initialUrl.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = loc["Tab_Downloads"];
                tabFavicon = new Uri("ms-appx:///Assets/downloads.png");
            }
            else if (initialUrl.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = loc["Tab_Passwords"];
                tabFavicon = new Uri("ms-appx:///Assets/passwords.png");
            }
            else if (initialUrl.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = loc["Tab_Settings"];
                tabFavicon = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
            else if (IsInPrivate)
            {
                tabTitle = loc["Tab_InPrivate"];
            }

            var tab = new BrowserTab
            {
                Title = tabTitle,
                Url = initialUrl,
                FaviconUri = tabFavicon,
                IsPopupTab = isPopup
            };

            tab.PushHistory(initialUrl);

            Tabs.Add(tab);
            CurrentTab = tab;

            if (!initialUrl.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                if (!initialUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !initialUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    initialUrl = "https://" + initialUrl;
                }

                await EnsureTabWebViewInitializedAsync(tab);
                tab.GetWebView().Source = new Uri(initialUrl);
            }
            else
            {
                tab.IsLoading = false;
            }

            return tab;
        }

        public void HookCoreEvents(BrowserTab tab)
        {
            if (tab.GetWebView().CoreWebView2 is not CoreWebView2 core) return;

            // Chromium kendi faviconunu bulduğunda anında sekmede güncelle
                    core.FaviconChanged += (s, e) =>
                    {
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (!string.IsNullOrEmpty(core.FaviconUri))
                            {
                                try { tab.FaviconUri = new Uri(core.FaviconUri); } catch { }
                            }
                        });
                    };

                    // core olayları içerisindeki NewWindowRequested bloğunu şu şekilde değiştirin:
                    core.NewWindowRequested += async (sender, args) =>
                    {
                        string targetUrl = args.Uri;

                        // 1. Doğrudan dosya bağlantısıysa (.zip, .exe, .rar, .iso vb.) sekme dahi açmadan mevcut sayfada indir:
                        if (IsDownloadUrl(targetUrl))
                        {
                            args.Handled = true;
                            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                            {
                                tab.GetWebView().CoreWebView2?.Navigate(targetUrl);
                            });
                            return;
                        }

                        // 2. Google Drive, Mediafire, Turbobit veya reklam gibi sayfalar için:
                        // Harici ayrı bir Windows penceresi yerine Bear Browser içinde YENİ SEKME aç!
                        var deferral = args.GetDeferral();
                        try
                        {
                            // Yeni bir sekme oluşturuyoruz (Chromium adrese bu sekmeyi kendisi yönlendirecek)
                            var newTab = await CreateNewTabAsync("about:blank");
                            if (newTab.GetWebView().CoreWebView2 != null)
                            {
                                // Chromium'a bu pencereyi yeni sekmemizle ilişkilendirmesini söylüyoruz
                                args.NewWindow = newTab.GetWebView().CoreWebView2;
                            }
                        }
                        catch { }
                        finally
                        {
                            deferral.Complete();
                        }
                    };

                    // 2. İNDİRME BAŞLADIĞINDA ÇALIŞAN MANTIK
                    core.DownloadStarting += (sender, args) =>
                    {
                        var op = args.DownloadOperation;
                        string uri = op.Uri ?? "";
                        string targetPath = args.ResultFilePath ?? "";

                        // ⚡ 1. EKLENTİ (CRX) İNDİRMESİ TESPİTİ (Chrome Web Store veya Edge Addons)
                        if (uri.Contains("clients2.google.com/service/update2/crx", StringComparison.OrdinalIgnoreCase) ||
                            uri.Contains("edge.microsoft.com/extensionwebstorebase", StringComparison.OrdinalIgnoreCase) ||
                            uri.EndsWith(".crx", StringComparison.OrdinalIgnoreCase) ||
                            targetPath.EndsWith(".crx", StringComparison.OrdinalIgnoreCase))
                        {
                            // ⚡ ÖNEMLİ: İndirmeyi ASLA iptal etme (args.Cancel = true YAPMA)!
                            // İptal edilirse Chrome Web Store web sayfasında "Download interrupted" hatası gösterir.
                            // Bunun yerine indirmeyi geçici bir dosyaya yönlendirip tamamlandığında otomatik kuruyoruz:
                            args.Handled = true;

                            string extId = "";
                            var m = System.Text.RegularExpressions.Regex.Match(uri, @"[?&;]x=id(?:%3D|=)([a-zA-Z0-9]{32})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                extId = m.Groups[1].Value.ToLowerInvariant();
                            }
                            else
                            {
                                var m2 = System.Text.RegularExpressions.Regex.Match(uri, @"[?&;]id=([a-zA-Z0-9]{32})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (m2.Success)
                                {
                                    extId = m2.Groups[1].Value.ToLowerInvariant();
                                }
                                else if (ExtensionService.Instance.IsStoreExtensionUrl(tab.Url, out _, out string fromTabId))
                                {
                                    extId = fromTabId;
                                }
                                else if (!string.IsNullOrEmpty(targetPath))
                                {
                                    string fn = System.IO.Path.GetFileNameWithoutExtension(targetPath);
                                    if (fn.Length == 32) extId = fn.ToLowerInvariant();
                                }
                            }

                            if (string.IsNullOrEmpty(extId))
                            {
                                extId = Guid.NewGuid().ToString("N")[..32];
                            }

                            string storeType = uri.Contains("edge.microsoft.com", StringComparison.OrdinalIgnoreCase) ? "Edge" : "Chrome";
                            string tempCrxPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BearExt_{extId}_{Guid.NewGuid():N}.crx");
                            args.ResultFilePath = tempCrxPath;

                            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                            {
                                if (tab.IsPopupTab)
                                {
                                    CloseTab(tab);
                                }

                                ExtensionDownloadProgressStarted?.Invoke(this, (storeType, extId, tab.Title));
                            });

                            op.StateChanged += async (s, e) =>
                            {
                                if (op.State == CoreWebView2DownloadState.Completed)
                                {
                                    try
                                    {
                                        var profile = tab.GetWebView().CoreWebView2?.Profile;
                                        var info = await ExtensionService.Instance.InstallFromCrxFileAsync(tempCrxPath, extId, storeType, tab.Title, profile);
                                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                        {
                                            if (info != null)
                                            {
                                                ExtensionInstalledSuccessfully?.Invoke(this, info.Name);
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        CrashReportingService.Instance.LogCustomError("Extension Auto-Install Failed", ex.Message);
                                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                        {
                                            ExtensionInstallFailed?.Invoke(this, ex.Message);
                                        });
                                    }
                                }
                                else if (op.State == CoreWebView2DownloadState.Interrupted)
                                {
                                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                    {
                                        ExtensionInstallFailed?.Invoke(this, $"İndirme kesintiye uğradı: {op.InterruptReason}");
                                    });
                                }
                            };

                            return;
                        }

                        // ⚡ 2. NORMAL DOSYA İNDİRME MANTIĞI
                        // WebView2'nin sayfanın altında açtığı varsayılan pencereyi gizle
                        args.Handled = true;

                        string resultPath = args.ResultFilePath ?? "";
                        string fileName = System.IO.Path.GetFileName(resultPath) ?? "download";
                        var downloadItem = new DownloadItem
                        {
                            FileName = fileName,
                            FilePath = resultPath,
                            Url = op.Uri ?? "",
                            TotalBytes = Math.Max(0, op.TotalBytesToReceive),
                            ReceivedBytes = 0,
                            State = "İndiriliyor"
                        };

                        // İndirilenler listesine ekle ve sağ üstteki butonda mavi dönen halkayı başlat
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            ProfileService.Instance.AddActiveDownload(downloadItem);
                            DownloadStateChanged?.Invoke(this, (true, false));
                        });

                        // İndirme ilerlemesini canlı güncelle
                        op.BytesReceivedChanged += (s, e) =>
                        {
                            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                            {
                                downloadItem.ReceivedBytes = op.BytesReceived;
                                if (op.TotalBytesToReceive > 0)
                                {
                                    downloadItem.TotalBytes = op.TotalBytesToReceive;
                                }
                            });
                        };

                        op.StateChanged += async (s, e) =>
                        {
                            if (op.State == CoreWebView2DownloadState.Completed)
                            {
                                await ProfileService.Instance.CompleteDownloadAsync(downloadItem);
                                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                {
                                    DownloadStateChanged?.Invoke(this, (false, true)); // Yeşil onay rozeti yak
                                    if (tab.IsPopupTab)
                                    {
                                        CloseTab(tab);
                                    }
                                });
                            }
                            else if (op.State == CoreWebView2DownloadState.Interrupted)
                            {
                                downloadItem.State = "İptal edildi";
                                CrashReportingService.Instance.LogCustomError("İndirme Kesintiye Uğradı", $"Dosya: {downloadItem.FileName}\nNeden: {op.InterruptReason}\nURL: {op.Uri}");
                                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                {
                                    DownloadStateChanged?.Invoke(this, (false, false));
                                    if (tab.IsPopupTab)
                                    {
                                        CloseTab(tab);
                                    }
                                });
                            }
                        };
                    };

                    // DOM yüklendiğinde yükleme süresini hesapla
                    core.DOMContentLoaded += (sender, args) =>
                    {
                        if (tab.LoadTimer.IsRunning)
                        {
                            tab.LoadTimer.Stop();
                            tab.LastLoadTimeMs = tab.LoadTimer.ElapsedMilliseconds;

                            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                            {
                                if (CurrentTab == tab)
                                    PageLoadCompleted?.Invoke(this, tab.LastLoadTimeMs);
                            });
                        }
                    };

                    // Sayfa başlığı parse edildiğinde (sayfa henüz tamamen bitmeden) sekmede güncelle
                    core.DocumentTitleChanged += (s, e) =>
                    {
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(core.DocumentTitle))
                            {
                                tab.Title = core.DocumentTitle;
                                if (CurrentTab == tab && !tab.Url.StartsWith("bear://"))
                                {
                                    TabNavigated?.Invoke(this, tab);
                                }
                            }
                        });
                    };

                    // Geri/İleri geçmişi değiştiğinde navigasyon buton durumlarını anında güncelle
                    core.HistoryChanged += (s, e) =>
                    {
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (CurrentTab == tab)
                            {
                                TabNavigated?.Invoke(this, tab);
                            }
                        });
                    };

                    // SPA veya yönlendirmelerde adres çubuğunu ve kayıtlı parola popup'ını güncelle
                    core.SourceChanged += (s, e) =>
                    {
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(core.Source) && !core.Source.StartsWith("bear://") && !core.Source.StartsWith("about:blank"))
                            {
                                tab.Url = core.Source;
                                if (CurrentTab == tab)
                                {
                                    TabNavigated?.Invoke(this, tab);
                                }

                                if (ExtensionService.Instance.IsStoreExtensionUrl(core.Source, out string st, out string eId))
                                {
                                    string title = tab.Title ?? "Eklenti";
                                    ExtensionInstallPromptRequested?.Invoke(this, (st, eId, title));
                                }
                            }
                            InjectAutofillPopup(tab);
                        });
                    };

                    // Sayfa içinden gelen klavye kısayollarını ve parola formlarını yakala
                    core.WebMessageReceived += (sender, args) =>
                    {
                        try
                        {
                            string msg = args.TryGetWebMessageAsString();
                            if (!string.IsNullOrEmpty(msg) && msg.StartsWith("shortcut:"))
                            {
                                string action = msg.Substring("shortcut:".Length);
                                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                {
                                    ShortcutActionTriggered?.Invoke(this, action);
                                });
                            }
                            else if (!string.IsNullOrEmpty(msg) && msg.StartsWith("install_extension:"))
                            {
                                string payload = msg.Substring("install_extension:".Length);
                                var parts = payload.Split("|||");
                                string extId = parts.Length > 0 ? parts[0] : "";
                                string extName = parts.Length > 1 ? parts[1] : "";
                                string store = tab.Url.Contains("microsoftedge.microsoft.com", StringComparison.OrdinalIgnoreCase) ? "Edge" : "Chrome";
                                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                {
                                    ExtensionInstallPromptRequested?.Invoke(this, (store, extId, extName));
                                });
                            }
                            else if (!string.IsNullOrEmpty(msg) && msg.StartsWith("password_submit:"))
                            {
                                string payload = msg.Substring("password_submit:".Length);
                                var parts = payload.Split("|||");
                                if (parts.Length >= 3)
                                {
                                    string pUrl = parts[0];
                                    string pUser = parts[1];
                                    string pPass = parts[2];

                                    string domain = "";
                                    try { domain = new Uri(pUrl).Host; } catch { domain = pUrl; }

                                    // ⚡ Zaten bu sitede aynı kullanıcı adı ve parola kayıtlıysa tekrar sorma:
                                    bool alreadySaved = ProfileService.Instance.Passwords.Any(p =>
                                        !string.IsNullOrEmpty(p.Domain) &&
                                        IsDomainMatch(domain, p.Domain) &&
                                        p.UserName.Equals(pUser, StringComparison.OrdinalIgnoreCase) &&
                                        p.Password == pPass);

                                    if (!alreadySaved)
                                    {
                                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                        {
                                            PasswordSavePromptRequested?.Invoke(this, (pUrl, pUser, pPass));
                                        });
                                    }
                                }
                            }
                            else if (!string.IsNullOrEmpty(msg) && msg.StartsWith("request_autofill:"))
                            {
                                string reqUrl = msg.Substring("request_autofill:".Length);
                                InjectAutofillPopup(tab, reqUrl);
                            }
                        }
                        catch { }
                    };

        }



        public void CloseTab(BrowserTab tab)
        {
            int index = Tabs.IndexOf(tab);
            if (index == -1) return;

            try
            {
                if (tab.GetWebView().CoreWebView2 != null)
                {
                    tab.GetWebView().CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                }
            }
            catch { }

            tab.GetWebView().Close();
            Tabs.Remove(tab);

            if (Tabs.Count == 0)
            {
                _ = CreateNewTabAsync();
            }
            else if (CurrentTab == tab)
            {
                int nextIndex = Math.Clamp(index, 0, Tabs.Count - 1);
                CurrentTab = Tabs[nextIndex];
            }

            MemoryOptimizerService.Instance.OptimizeMemoryAsync();
        }

        private static bool IsDownloadUrl(string url)
        {
            return UrlHelper.IsDownloadUrl(url);
        }

        public async Task EnsureTabWebViewInitializedAsync(BrowserTab tab)
        {
            if (tab.IsWebViewInitialized) return;
            tab.IsWebViewInitialized = true;

            await WebViewService.Instance.InitializeWebViewAsync(tab.GetWebView(), IsInPrivate);
            AdBlockService.Instance.AttachToWebView(tab.GetWebView(), () =>
            {
                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    tab.BlockedAdsCount++;
                    if (CurrentTab == tab)
                        AdBlockedCountChanged?.Invoke(this, tab.BlockedAdsCount);
                });
            });

            try
            {
                bool isDark = SettingsService.Instance.Settings.Theme == "Dark" ||
                    (SettingsService.Instance.Settings.Theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark);

                var scheme = isDark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : (SettingsService.Instance.Settings.Theme == "Light" ? CoreWebView2PreferredColorScheme.Light : CoreWebView2PreferredColorScheme.Auto);

                if (tab.GetWebView().CoreWebView2 != null)
                {
                    tab.GetWebView().CoreWebView2.Profile.PreferredColorScheme = scheme;

                    await tab.GetWebView().CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    (() => {
                        try {
                            if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
                                if (location.hostname.includes('youtube.com')) {
                                    document.documentElement.setAttribute('dark', 'true');
                                }
                                const s = document.createElement('style');
                                s.id = 'bear-dark-force-style';
                                s.textContent = ':root { color-scheme: dark !important; }';
                                (document.head || document.documentElement).appendChild(s);
                            }
                        } catch(e) {}
                    })();
                ");
                }
            }
            catch { }

            HookCoreEvents(tab);
            HookTabEvents(tab);
        }

        public void HookTabEvents(BrowserTab tab)
        {
            tab.GetWebView().NavigationStarting += (s, e) =>
            {
                tab.IsLoading = true;
                tab.LoadTimer.Restart();
                if (!string.IsNullOrWhiteSpace(e.Uri) && !e.Uri.StartsWith("about:blank"))
                {
                    tab.Url = e.Uri;
                }

                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    TabLoadingChanged?.Invoke(this, (tab, true));
                    if (CurrentTab == tab && !tab.Url.StartsWith("bear://"))
                    {
                        TabNavigated?.Invoke(this, tab);
                    }
                });
            };

            tab.GetWebView().NavigationCompleted += (s, e) =>
            {
                tab.IsLoading = false;
                if (tab.LoadTimer.IsRunning)
                {
                    tab.LoadTimer.Stop();
                    tab.LastLoadTimeMs = tab.LoadTimer.ElapsedMilliseconds;
                }

                if (tab.GetWebView().Source != null)
                {
                    tab.Url = tab.GetWebView().Source.ToString();
                }

                string? docTitle = tab.GetWebView().CoreWebView2?.DocumentTitle;
                if (!string.IsNullOrWhiteSpace(docTitle))
                {
                    tab.Title = docTitle;
                }

                // Sayfa tamamlandığında favicon'u belirle
                if (!string.IsNullOrWhiteSpace(tab.Url))
                {
                    if (tab.Url.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/history.png");
                    }
                    else if (tab.Url.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/favorites.png");
                    }
                    else if (tab.Url.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/downloads.png");
                    }
                    else if (tab.Url.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/passwords.png");
                    }
                    else if (tab.Url.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                    }
                    else if (tab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
                    {
                        tab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                    }
                    else
                    {
                        try
                        {
                            var uri = new Uri(tab.Url);
                            if (!string.IsNullOrEmpty(uri.Host))
                            {
                                tab.FaviconUri = new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32");
                            }
                        }
                        catch { }
                    }
                }

                if (!IsInPrivate && !string.IsNullOrWhiteSpace(tab.Url) && !tab.Url.StartsWith("bear://"))
                {
                    _ = ProfileService.Instance.AddHistoryAsync(tab.Title, tab.Url);

                    // --- ETKİLEŞİMLİ PAROLA POPUP YÖNETİCİSİ (AUTOFILL DROPDOWN) ---
                    InjectAutofillPopup(tab);
                }

                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    TabLoadingChanged?.Invoke(this, (tab, false));
                    if (CurrentTab == tab)
                    {
                        TabNavigated?.Invoke(this, tab);
                        PageLoadCompleted?.Invoke(this, tab.LastLoadTimeMs);
                        AdBlockedCountChanged?.Invoke(this, tab.BlockedAdsCount);
                    }
                });

                // Sayfa yüklenmesi bittikten 2 saniye sonra gereksiz yükleme önbelleklerini temizle
                _ = Task.Delay(2000).ContinueWith(_ =>
                {
                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                    {
                        MemoryOptimizerService.Instance.OptimizeMemoryAsync();
                    });
                });
            };
        }

        private static bool IsDomainMatch(string currentHost, string savedDomain)
        {
            return UrlHelper.IsDomainMatch(currentHost, savedDomain);
        }

        private static string GetRootDomain(string host)
        {
            return UrlHelper.GetRootDomain(host);
        }

        private void InjectAutofillPopup(BrowserTab tab, string? targetUrl = null)
        {
            if (tab.GetWebView().CoreWebView2 == null || IsInPrivate)
                return;

            try
            {
                string currentUrl = targetUrl ?? tab.GetWebView().CoreWebView2.Source;
                if (string.IsNullOrWhiteSpace(currentUrl)) currentUrl = tab.Url;
                if (string.IsNullOrWhiteSpace(currentUrl) || currentUrl.StartsWith("bear://"))
                    return;

                string host = "";
                try { host = new Uri(currentUrl).Host; } catch { host = currentUrl; }

                var matchingPasswords = ProfileService.Instance.Passwords
                    .Where(p => !string.IsNullOrEmpty(p.Domain) && IsDomainMatch(host, p.Domain))
                    .GroupBy(p => new { User = p.UserName ?? "", Pass = p.Password ?? "" })
                    .Select(g => new { user = g.Key.User, pass = g.Key.Pass })
                    .ToList();

                string json = System.Text.Json.JsonSerializer.Serialize(matchingPasswords);
                _ = tab.GetWebView().CoreWebView2.ExecuteScriptAsync($"window.__bearSetAutofillAccounts && window.__bearSetAutofillAccounts({json});");
            }
            catch { }
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}