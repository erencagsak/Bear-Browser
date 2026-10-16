using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bear_Browser.Helpers;
using Bear_Browser.Messages;
using Bear_Browser.Models;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILocalizationService _localizationService;
        private readonly IHistoryService _historyService;
        private readonly IBookmarkService _bookmarkService;
        private readonly IDownloadService _downloadService;
        private readonly IPasswordService _passwordService;
        private readonly IProfileService _profileService;
        private readonly IWebViewService _webViewService;
        private readonly ISettingsService _settingsService;
        private readonly IExtensionService _extensionService;
        private readonly ICrashReportingService _crashReportingService;
        private readonly IAdBlockService _adBlockService;
        private readonly IMemoryOptimizerService _memoryOptimizerService;
        private readonly IExtensionInstallerService _extensionInstallerService;
        private readonly IAutofillService _autofillService;
        private readonly IDownloadHandlerService _downloadHandlerService;
        private readonly IMessenger _messenger;

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
                    if (_currentTab != null)
                    {
                        _messenger.Send(new TabSelectedMessage(_currentTab));
                        bool isVideoFullScreen = _currentTab.GetWebView().CoreWebView2?.ContainsFullScreenElement ?? false;
                        FullScreenElementChanged?.Invoke(this, isVideoFullScreen);
                        _messenger.Send(new FullScreenElementChangedMessage(isVideoFullScreen));
                    }
                }
            }
        }

        public event EventHandler<bool>? FullScreenElementChanged;
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
            : this(
                isInPrivate,
                App.Services.GetRequiredService<ILocalizationService>(),
                App.Services.GetRequiredService<IHistoryService>(),
                App.Services.GetRequiredService<IBookmarkService>(),
                App.Services.GetRequiredService<IDownloadService>(),
                App.Services.GetRequiredService<IPasswordService>(),
                App.Services.GetRequiredService<IProfileService>(),
                App.Services.GetRequiredService<IWebViewService>(),
                App.Services.GetRequiredService<ISettingsService>(),
                App.Services.GetRequiredService<IExtensionService>(),
                App.Services.GetRequiredService<ICrashReportingService>(),
                App.Services.GetRequiredService<IAdBlockService>(),
                App.Services.GetRequiredService<IMemoryOptimizerService>(),
                App.Services.GetRequiredService<IExtensionInstallerService>(),
                App.Services.GetRequiredService<IAutofillService>(),
                App.Services.GetRequiredService<IDownloadHandlerService>(),
                App.Services.GetRequiredService<IMessenger>())
        {
        }

        public MainViewModel(
            bool isInPrivate,
            ILocalizationService localizationService,
            IHistoryService historyService,
            IBookmarkService bookmarkService,
            IDownloadService downloadService,
            IPasswordService passwordService,
            IProfileService profileService,
            IWebViewService webViewService,
            ISettingsService settingsService,
            IExtensionService extensionService,
            ICrashReportingService crashReportingService,
            IAdBlockService adBlockService,
            IMemoryOptimizerService memoryOptimizerService,
            IExtensionInstallerService extensionInstallerService,
            IAutofillService autofillService,
            IDownloadHandlerService downloadHandlerService,
            IMessenger messenger)
        {
            IsInPrivate = isInPrivate;
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _webViewService = webViewService ?? throw new ArgumentNullException(nameof(webViewService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _extensionService = extensionService ?? throw new ArgumentNullException(nameof(extensionService));
            _crashReportingService = crashReportingService ?? throw new ArgumentNullException(nameof(crashReportingService));
            _adBlockService = adBlockService ?? throw new ArgumentNullException(nameof(adBlockService));
            _memoryOptimizerService = memoryOptimizerService ?? throw new ArgumentNullException(nameof(memoryOptimizerService));
            _extensionInstallerService = extensionInstallerService ?? throw new ArgumentNullException(nameof(extensionInstallerService));
            _autofillService = autofillService ?? throw new ArgumentNullException(nameof(autofillService));
            _downloadHandlerService = downloadHandlerService ?? throw new ArgumentNullException(nameof(downloadHandlerService));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

            // Messenger Subscriptions
            _messenger.Register<NewTabRequestedMessage>(this, (r, m) =>
            {
                _ = CreateNewTabAsync(m.Url ?? "bear://newtab");
            });

            _messenger.Register<CloseCurrentTabRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null)
                {
                    CloseTab(CurrentTab);
                }
            });

            _messenger.Register<NavigateRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null && !string.IsNullOrWhiteSpace(m.Url))
                {
                    CurrentTab.GetWebView().CoreWebView2?.Navigate(m.Url);
                }
            });

            _messenger.Register<GoBackRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null && CurrentTab.GetWebView().CanGoBack)
                {
                    CurrentTab.GetWebView().GoBack();
                }
            });

            _messenger.Register<GoForwardRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null && CurrentTab.GetWebView().CanGoForward)
                {
                    CurrentTab.GetWebView().GoForward();
                }
            });

            _messenger.Register<RefreshRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null)
                {
                    CurrentTab.GetWebView().Reload();
                }
            });

            _messenger.Register<StopRequestedMessage>(this, (r, m) =>
            {
                if (CurrentTab != null)
                {
                    CurrentTab.GetWebView().CoreWebView2?.Stop();
                }
            });
        }

        public async Task<BrowserTab> CreateNewTabAsync(string initialUrl = "bear://newtab", bool isPopup = false)
        {
            string tabTitle = _localizationService["Tab_NewTab"];
            Uri tabFavicon = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");

            if (initialUrl.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = _localizationService["Tab_History"];
                tabFavicon = new Uri("ms-appx:///Assets/history.png");
            }
            else if (initialUrl.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = _localizationService["Tab_Favorites"];
                tabFavicon = new Uri("ms-appx:///Assets/favorites.png");
            }
            else if (initialUrl.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = _localizationService["Tab_Downloads"];
                tabFavicon = new Uri("ms-appx:///Assets/downloads.png");
            }
            else if (initialUrl.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = _localizationService["Tab_Passwords"];
                tabFavicon = new Uri("ms-appx:///Assets/passwords.png");
            }
            else if (initialUrl.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
            {
                tabTitle = _localizationService["Tab_Settings"];
                tabFavicon = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
            else if (IsInPrivate)
            {
                tabTitle = _localizationService["Tab_InPrivate"];
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
            _messenger.Send(new TabCreatedMessage(tab));
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

            // HTML5 Tam Ekran (Video vb.)
            core.ContainsFullScreenElementChanged += (sender, args) =>
            {
                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    if (CurrentTab == tab)
                    {
                        bool isVideoFullScreen = core.ContainsFullScreenElement;
                        FullScreenElementChanged?.Invoke(this, isVideoFullScreen);
                        _messenger.Send(new FullScreenElementChangedMessage(isVideoFullScreen));
                    }
                });
            };

            // Chromium favicon
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

            // NewWindowRequested
            core.NewWindowRequested += async (sender, args) =>
            {
                string targetUrl = args.Uri;

                // 1. Doğrudan dosya bağlantısıysa mevcut sayfada indir:
                if (UrlHelper.IsDownloadUrl(targetUrl))
                {
                    args.Handled = true;
                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                    {
                        tab.GetWebView().CoreWebView2?.Navigate(targetUrl);
                    });
                    return;
                }

                // 2. Yeni sekme aç
                var deferral = args.GetDeferral();
                try
                {
                    var newTab = await CreateNewTabAsync("about:blank");
                    if (newTab.GetWebView().CoreWebView2 != null)
                    {
                        args.NewWindow = newTab.GetWebView().CoreWebView2;
                    }
                }
                catch { }
                finally
                {
                    deferral.Complete();
                }
            };

            // İndirme başladığında çalışan mantık
            core.DownloadStarting += (sender, args) =>
            {
                // 1. CRX indirmesi tespiti
                if (_extensionInstallerService.TryHandleCrxDownload(
                    args,
                    tab,
                    (storeType, extId, name) =>
                    {
                        ExtensionDownloadProgressStarted?.Invoke(this, (storeType, extId, name));
                        _messenger.Send(new ExtensionDownloadProgressStartedMessage(storeType, extId, name));
                    },
                    (name) =>
                    {
                        ExtensionInstalledSuccessfully?.Invoke(this, name);
                        _messenger.Send(new ExtensionInstalledSuccessfullyMessage(name));
                    },
                    (err) =>
                    {
                        ExtensionInstallFailed?.Invoke(this, err);
                        _messenger.Send(new ExtensionInstallFailedMessage(err));
                    },
                    () => CloseTab(tab)))
                {
                    return;
                }

                // 2. Normal dosya indirme mantığı
                _downloadHandlerService.HandleDownload(
                    args,
                    tab,
                    (isDownloading, isCompleted) =>
                    {
                        DownloadStateChanged?.Invoke(this, (isDownloading, isCompleted));
                        _messenger.Send(new DownloadStateChangedMessage(isDownloading, isCompleted));
                    },
                    () => CloseTab(tab));
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
                        {
                            PageLoadCompleted?.Invoke(this, tab.LastLoadTimeMs);
                            _messenger.Send(new PageLoadCompletedMessage(tab.LastLoadTimeMs));
                        }
                    });
                }
            };

            // Sayfa başlığı parse edildiğinde sekmede güncelle
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
                            _messenger.Send(new TabNavigatedMessage(tab));
                        }
                    }
                });
            };

            // Geri/İleri geçmişi değiştiğinde navigasyon buton durumlarını güncelle
            core.HistoryChanged += (s, e) =>
            {
                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    if (CurrentTab == tab)
                    {
                        TabNavigated?.Invoke(this, tab);
                        _messenger.Send(new TabNavigatedMessage(tab));
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
                            _messenger.Send(new TabNavigatedMessage(tab));
                        }

                        if (_extensionService.IsStoreExtensionUrl(core.Source, out string st, out string eId))
                        {
                            string title = tab.Title ?? "Eklenti";
                            ExtensionInstallPromptRequested?.Invoke(this, (st, eId, title));
                            _messenger.Send(new ExtensionInstallPromptRequestedMessage(st, eId, title));
                        }
                    }
                    _autofillService.InjectAutofillAccounts(tab, null, IsInPrivate);
                });
            };

            // Web mesajları
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
                            _messenger.Send(new ShortcutActionTriggeredMessage(action));
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
                            _messenger.Send(new ExtensionInstallPromptRequestedMessage(store, extId, extName));
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

                            if (!_autofillService.IsPasswordAlreadySaved(domain, pUser, pPass))
                            {
                                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                                {
                                    PasswordSavePromptRequested?.Invoke(this, (pUrl, pUser, pPass));
                                    _messenger.Send(new PasswordSavePromptRequestedMessage(pUrl, pUser, pPass));
                                });
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(msg) && msg.StartsWith("request_autofill:"))
                    {
                        string reqUrl = msg.Substring("request_autofill:".Length);
                        _autofillService.InjectAutofillAccounts(tab, reqUrl, IsInPrivate);
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
            _messenger.Send(new TabClosedMessage(tab.Id));

            if (Tabs.Count == 0)
            {
                _ = CreateNewTabAsync();
            }
            else if (CurrentTab == tab)
            {
                int nextIndex = Math.Clamp(index, 0, Tabs.Count - 1);
                CurrentTab = Tabs[nextIndex];
            }

            _memoryOptimizerService.OptimizeMemoryAsync();
        }

        public async Task EnsureTabWebViewInitializedAsync(BrowserTab tab)
        {
            if (tab.IsWebViewInitialized) return;
            tab.IsWebViewInitialized = true;

            await _webViewService.InitializeWebViewAsync(tab.GetWebView(), IsInPrivate);
            _adBlockService.AttachToWebView(tab.GetWebView(), () =>
            {
                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    tab.BlockedAdsCount++;
                    if (CurrentTab == tab)
                    {
                        AdBlockedCountChanged?.Invoke(this, tab.BlockedAdsCount);
                        _messenger.Send(new AdBlockedCountChangedMessage(tab.BlockedAdsCount));
                    }
                });
            });

            try
            {
                var scheme = _settingsService.Settings.Theme switch
                {
                    "Light" => CoreWebView2PreferredColorScheme.Light,
                    "Dark" => CoreWebView2PreferredColorScheme.Dark,
                    _ => CoreWebView2PreferredColorScheme.Auto
                };

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
                    _messenger.Send(new TabLoadingChangedMessage(tab, true));
                    if (CurrentTab == tab && !tab.Url.StartsWith("bear://"))
                    {
                        TabNavigated?.Invoke(this, tab);
                        _messenger.Send(new TabNavigatedMessage(tab));
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
                    _ = _historyService.AddHistoryAsync(tab.Title, tab.Url);
                    _autofillService.InjectAutofillAccounts(tab, null, IsInPrivate);
                }

                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    TabLoadingChanged?.Invoke(this, (tab, false));
                    _messenger.Send(new TabLoadingChangedMessage(tab, false));
                    if (CurrentTab == tab)
                    {
                        TabNavigated?.Invoke(this, tab);
                        _messenger.Send(new TabNavigatedMessage(tab));
                        PageLoadCompleted?.Invoke(this, tab.LastLoadTimeMs);
                        _messenger.Send(new PageLoadCompletedMessage(tab.LastLoadTimeMs));
                        AdBlockedCountChanged?.Invoke(this, tab.BlockedAdsCount);
                        _messenger.Send(new AdBlockedCountChangedMessage(tab.BlockedAdsCount));
                    }
                });
            };
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