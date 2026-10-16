using Bear_Browser.Controls;
using Bear_Browser.Messages;
using Bear_Browser.Models;
using Bear_Browser.Platform.Interfaces;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services.Interfaces;
using Bear_Browser.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;

namespace Bear_Browser
{
    public sealed partial class MainWindow : Window
    {
        public static IntPtr WindowHandle { get; private set; }
        public MainViewModel ViewModel { get; }
        public bool IsInPrivate { get; }

        private readonly ILocalizationService _localizationService;
        private readonly ISettingsService _settingsService;
        private readonly IProfileService _profileService;
        private readonly IPasswordService _passwordService;
        private readonly IExtensionService _extensionService;
        private readonly IMemoryOptimizerService _memoryOptimizerService;
        private readonly IAdBlockService _adBlockService;
        private readonly ICrashReportingService _crashReportingService;
        private readonly IPlatformWindowService _platformWindowService;
        private readonly IMessenger _messenger;

        private bool _isFullScreen = false;
        private bool _isManualFullScreen = false;
        private bool _isVideoFullScreen = false;
        private DispatcherTimer? _hideTimer;
        private DispatcherTimer? _initialBannerTimer;

        public MainWindow() : this(isInPrivate: false)
        {
        }

        public MainWindow(bool isInPrivate = false)
        {
            _platformWindowService = App.Services.GetRequiredService<IPlatformWindowService>();
            _localizationService = App.Services.GetRequiredService<ILocalizationService>();
            _settingsService = App.Services.GetRequiredService<ISettingsService>();
            _profileService = App.Services.GetRequiredService<IProfileService>();
            _passwordService = App.Services.GetRequiredService<IPasswordService>();
            _extensionService = App.Services.GetRequiredService<IExtensionService>();
            _memoryOptimizerService = App.Services.GetRequiredService<IMemoryOptimizerService>();
            _adBlockService = App.Services.GetRequiredService<IAdBlockService>();
            _crashReportingService = App.Services.GetRequiredService<ICrashReportingService>();
            _messenger = App.Services.GetRequiredService<IMessenger>();

            try
            {
                _platformWindowService.SetAppUserModelId("BearBrowser.App.1.0");
            }
            catch { }

            IsInPrivate = isInPrivate;
            ViewModel = new MainViewModel(isInPrivate);

            InitializeComponent();

            WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Görev çubuğu ve pencere simgesini hem bin klasöründe hem de ana Assets klasöründe ara
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Bear-Browser-Logo.ico");
            if (!System.IO.File.Exists(iconPath))
            {
                // Visual Studio Debug modunda ana proje klasöründen oku
                string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Assets\Bear-Browser-Logo.ico"));
                if (System.IO.File.Exists(devPath)) iconPath = devPath;
            }

            if (System.IO.File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
                try
                {
                    _platformWindowService.SetWindowIcon(WindowHandle, iconPath);
                }
                catch { }
            }

            if (IsInPrivate)
            {
                Title = $"Bear Browser ({_localizationService["Tab_InPrivate"]})";
            }

            _localizationService.LanguageChanged += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (IsInPrivate)
                    {
                        Title = $"Bear Browser ({_localizationService["Tab_InPrivate"]})";
                    }

                    foreach (var tab in ViewModel.Tabs)
                    {
                        if (tab.Url.StartsWith("bear://newtab", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_NewTab"];
                        else if (tab.Url.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_Favorites"];
                        else if (tab.Url.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_History"];
                        else if (tab.Url.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_Downloads"];
                        else if (tab.Url.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_Passwords"];
                        else if (tab.Url.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
                            tab.Title = _localizationService["Tab_Settings"];
                    }

                    if (ViewModel.CurrentTab != null && ViewModel.CurrentTab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ViewModel.CurrentTab.Url.StartsWith("bear://newtab", StringComparison.OrdinalIgnoreCase))
                        {
                            NavBar.UpdateUrl("", ViewModel.CurrentTab.Title);
                        }
                        else
                        {
                            NavBar.UpdateUrl(ViewModel.CurrentTab.Url, ViewModel.CurrentTab.Title);
                        }
                    }
                });
            };

            // Arka plan bellek optimizasyon servisini başlat
            _memoryOptimizerService.Start(this.DispatcherQueue);

            // Ayarları ve temayı uygula
            ApplyTheme(_settingsService.Settings.Theme);
            FavBar.Visibility = _settingsService.Settings.ShowFavoritesBar ? Visibility.Visible : Visibility.Collapsed;
            _adBlockService.IsEnabled = _settingsService.Settings.AdBlockEnabled;

            _settingsService.SettingsChanged += (s, settings) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    ApplyTheme(settings.Theme);
                    FavBar.Visibility = settings.ShowFavoritesBar ? Visibility.Visible : Visibility.Collapsed;
                    _adBlockService.IsEnabled = settings.AdBlockEnabled;
                });
            };

            // Fare üst panelden ayrılınca gecikmeli gizleme zamanlayıcısı (400ms)
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                if (_isFullScreen)
                {
                    HideTopBar();
                }
            };

            // Messenger kayıtları
            _messenger.Register<FindInPageMessage>(this, (r, m) =>
            {
                DispatcherQueue?.TryEnqueue(ToggleFindBar);
            });

            _messenger.Register<ToggleFullScreenMessage>(this, (r, m) =>
            {
                DispatcherQueue?.TryEnqueue(ToggleFullScreen);
            });

            _messenger.Register<ExitFullScreenMessage>(this, (r, m) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    if (_isFullScreen) SetFullScreen(false);
                });
            });

            _messenger.Register<FullScreenElementChangedMessage>(this, (r, m) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    OnFullScreenElementChanged(m.ContainsFullScreenElement);
                });
            });

            ViewModel.FullScreenElementChanged += (s, contains) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    OnFullScreenElementChanged(contains);
                });
            };

            // Sayfa içinden gelen kısayolları bağla
            ViewModel.ShortcutActionTriggered += (s, action) => HandleGlobalAction(action);

            // Sayfa gezindiğinde başlık ve adresi anlık senkronize et (Google/YouTube başlık düzeltmesi)
            ViewModel.TabNavigated += (s, tab) =>
            {
                if (ViewModel.CurrentTab == tab)
                {
                    if (!tab.Url.StartsWith("bear://"))
                    {
                        NavBar.UpdateUrl(tab.Url, tab.Title);
                    }
                    UpdateNavigationButtons(tab);
                }
            };

            // Sık Kullanılanlar çubuğundaki bağlantılara tıklanması
            FavBar.NavigateRequested += (s, url) =>
            {
                NavigateToUrl(url);
            };
            FavBar.NewTabRequested += (s, url) =>
            {
                CreateNewTab(url);
            };

            // Navigasyon bar olayları
            NavBar.NavigateRequested += NavBar_NavigateRequested;
            NavBar.BackRequested += NavBar_BackRequested;
            NavBar.ForwardRequested += NavBar_ForwardRequested;
            NavBar.RefreshRequested += NavBar_RefreshRequested;
            NavBar.StopRequested += (s, e) =>
            {
                if (ViewModel.CurrentTab?.GetWebView().CoreWebView2 != null)
                {
                    try { ViewModel.CurrentTab.GetWebView().CoreWebView2.Stop(); } catch { }
                }
            };
            NavBar.ManageProfileRequested += NavBar_ManageProfileRequested;
            NavBar.NewTabRequested += (s, e) => CreateNewTab();
            NavBar.MenuActionTriggered += (s, action) => HandleGlobalAction(action);

            // Yakınlaştırma ve Tam Ekran
            NavBar.ZoomInRequested += (s, e) => ChangeZoom(0.1);
            NavBar.ZoomOutRequested += (s, e) => ChangeZoom(-0.1);
            NavBar.FullScreenRequested += (s, e) => ToggleFullScreen();

            // Profil güncellemeleri
            _profileService.ProfileUpdated += (s, profile) => NavBar.UpdateProfile(profile);
            NavBar.UpdateProfile(_profileService.CurrentProfile);

            ViewModel.CurrentTabChanged += ViewModel_CurrentTabChanged;
            ViewModel.TabLoadingChanged += (s, args) =>
            {
                if (ViewModel.CurrentTab == args.tab)
                {
                    NavBar.SetLoading(args.isLoading);
                }
            };

            // İlk sekmeyi aç ve hazır olunca loading ekranını yumuşakça kapat
            _ = InitializeStartupAsync();

            ViewModel.DownloadStateChanged += (s, state) =>
            {
                NavBar.SetDownloadStatus(state.isDownloading, state.isCompleted);
            };

            ViewModel.PasswordSavePromptRequested += (s, data) =>
            {
                _pendingPasswordData = data;
                string host = "";
                try { host = new Uri(data.url).Host; } catch { host = data.url; }
                PasswordPromptUserText.Text = string.IsNullOrEmpty(data.username)
                    ? $"{host} sitesi için parola kaydedilsin mi?"
                    : $"{data.username} ({host})";
                PasswordPromptBar.Visibility = Visibility.Visible;
            };

            ViewModel.ExtensionInstallPromptRequested += (s, data) =>
            {
                ShowExtensionInstallPrompt(data.storeType, data.extensionId, data.name);
            };

            ViewModel.ExtensionDownloadProgressStarted += (s, data) =>
            {
                ExtensionInstallTitleText.Text = string.IsNullOrWhiteSpace(data.name) || data.name == "Eklenti"
                    ? "Uzantı indiriliyor ve kuruluyor..."
                    : $"'{data.name}' indiriliyor...";
                ExtensionInstallStoreText.Text = data.storeType.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                    ? "Microsoft Edge Eklentileri"
                    : "Chrome Web Mağazası";
                ExtensionInstallStatusText.Text = "Uzantı dosyası alınıyor, lütfen bekleyin...";
                ConfirmInstallExtensionBtn.IsEnabled = false;
                ConfirmInstallExtensionBtnText.Text = "İndiriliyor...";
                ExtensionInstallProgress.Visibility = Visibility.Visible;
                ExtensionInstallProgress.IsActive = true;
                ExtensionInstallBar.Visibility = Visibility.Visible;
            };

            ViewModel.ExtensionInstalledSuccessfully += (s, extName) =>
            {
                ShowExtensionInstalledNotification(extName);
            };

            ViewModel.ExtensionInstallFailed += (s, errorMsg) =>
            {
                ConfirmInstallExtensionBtnText.Text = "Hata Oluştu";
                ExtensionInstallProgress.Visibility = Visibility.Collapsed;
                ExtensionInstallProgress.IsActive = false;
                ExtensionInstallStatusText.Text = $"Eklenti yüklenirken hata oluştu: {errorMsg}";
                ConfirmInstallExtensionBtn.IsEnabled = true;
            };
        }

        // --- İÇERİK VE SAYFA GEZİNTİSİ ---

        private void ViewModel_CurrentTabChanged(object? sender, BrowserTab? currentTab)
        {
            // Arka plandaki diğer sekmelerin bellek ve kaynak kullanımını minimuma indir
            foreach (var tab in ViewModel.Tabs)
            {
                if (tab != currentTab && tab.GetWebView().CoreWebView2 != null)
                {
                    try
                    {
                        tab.GetWebView().CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    }
                    catch { }
                }
            }

            // Aktif sekmeyi normal performans moduna al
            if (currentTab != null && currentTab.GetWebView().CoreWebView2 != null)
            {
                try
                {
                    currentTab.GetWebView().CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                }
                catch { }
            }

            if (currentTab != null)
            {
                if (currentTab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.IsLoading = false;
                }

                TabsControl.SelectedItem = currentTab;
                NavBar.SetLoading(currentTab.IsLoading);
            }

            RenderCurrentTabContent(currentTab);

            if (currentTab != null && _extensionService.IsStoreExtensionUrl(currentTab.Url, out string st, out string eId))
            {
                ShowExtensionInstallPrompt(st, eId, currentTab.Title);
            }
            else
            {
                ExtensionInstallBar.Visibility = Visibility.Collapsed;
            }

            UpdateNavigationButtons(currentTab);
        }

        private void RenderCurrentTabContent(BrowserTab? currentTab)
        {
            WebContentGrid.Children.Clear();
            if (currentTab != null)
            {
                if (currentTab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.IsLoading = false;
                    NavBar.SetLoading(false);
                }

                if (currentTab.Url.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_Favorites"];
                    currentTab.FaviconUri = new Uri("ms-appx:///Assets/favorites.png");
                    var favView = new FavoritesView();
                    favView.NavigateRequested += (s, url) => NavigateToUrl(url);
                    favView.NewTabRequested += (s, url) => CreateNewTab(url);
                    favView.ToggleFavoritesBarRequested += (s, e) =>
                    {
                        FavBar.Visibility = FavBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    };
                    WebContentGrid.Children.Add(favView);
                    NavBar.UpdateUrl("bear://favorites", currentTab.Title);
                }
                else if (currentTab.Url.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_History"];
                    currentTab.FaviconUri = new Uri("ms-appx:///Assets/history.png");
                    var historyView = new HistoryView();
                    historyView.NavigateRequested += (s, url) => NavigateToUrl(url);
                    WebContentGrid.Children.Add(historyView);
                    NavBar.UpdateUrl("bear://history", currentTab.Title);
                }
                else if (currentTab.Url.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_Downloads"];
                    currentTab.FaviconUri = new Uri("ms-appx:///Assets/downloads.png");
                    var downloadsView = new DownloadsView();
                    WebContentGrid.Children.Add(downloadsView);
                    NavBar.UpdateUrl("bear://downloads", currentTab.Title);
                }
                else if (currentTab.Url.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_Passwords"];
                    currentTab.FaviconUri = new Uri("ms-appx:///Assets/passwords.png");
                    var passwordsView = new PasswordsView();
                    WebContentGrid.Children.Add(passwordsView);
                    NavBar.UpdateUrl("bear://passwords", currentTab.Title);
                }
                else if (currentTab.Url.StartsWith("bear://extensions", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Url = "bear://settings/extensions";
                    RenderCurrentTabContent(currentTab);
                    return;
                }
                else if (currentTab.Url.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_Settings"];
                    var settingsView = new SettingsView();
                    if (currentTab.GetWebView().CoreWebView2 != null)
                    {
                        settingsView.SetProfile(currentTab.GetWebView().CoreWebView2.Profile);
                    }
                    settingsView.NavigateRequested += (s, targetUrl) => NavigateToUrl(targetUrl);

                    // Sol menüde gezinildikçe adres çubuğundaki URL'yi senkronize et
                    settingsView.SectionNavigated += (s, section) =>
                    {
                        currentTab.Url = $"bear://settings/{section}";
                        NavBar.UpdateUrl(currentTab.Url, _localizationService["Tab_Settings"]);
                    };

                    if (currentTab.Url.Contains("/profiles"))
                    {
                        settingsView.SelectSection("profiles");
                    }
                    else if (currentTab.Url.Contains("/privacy"))
                    {
                        settingsView.SelectSection("privacy");
                    }
                    else if (currentTab.Url.Contains("/appearance"))
                    {
                        settingsView.SelectSection("appearance");
                    }
                    else if (currentTab.Url.Contains("/extensions"))
                    {
                        settingsView.SelectSection("extensions");
                    }
                    else if (currentTab.Url.Contains("/downloads"))
                    {
                        settingsView.SelectSection("downloads");
                    }
                    else if (currentTab.Url.Contains("/defaultbrowser"))
                    {
                        settingsView.SelectSection("defaultbrowser");
                    }
                    else if (currentTab.Url.Contains("/languages"))
                    {
                        settingsView.SelectSection("languages");
                    }
                    else if (currentTab.Url.Contains("/system"))
                    {
                        settingsView.SelectSection("system");
                    }
                    else
                    {
                        currentTab.Url = "bear://settings/about";
                        settingsView.SelectSection("about");
                    }

                    WebContentGrid.Children.Add(settingsView);
                    NavBar.UpdateUrl(currentTab.Url, _localizationService["Tab_Settings"]);
                }
                else if (currentTab.Url.StartsWith("bear://newtab", StringComparison.OrdinalIgnoreCase))
                {
                    currentTab.Title = _localizationService["Tab_NewTab"];
                    currentTab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                    var newTabView = new NewTabView();
                    newTabView.NavigateRequested += (s, targetUrl) => NavigateToUrl(targetUrl);
                    newTabView.NewTabRequested += (s, targetUrl) => CreateNewTab(targetUrl);
                    WebContentGrid.Children.Add(newTabView);
                    NavBar.UpdateUrl("", _localizationService["Tab_NewTab"]);
                    newTabView.FocusSearchBox();
                }
                else
                {
                    WebContentGrid.Children.Add(currentTab.GetWebView());
                    NavBar.UpdateUrl(currentTab.Url, currentTab.Title);
                    NavBar.UpdateZoomDisplay(currentTab.ZoomFactor);
                }

                UpdateNavigationButtons(currentTab);
            }
        }

        private async void NavigateToUrl(string url, bool isHistoryNavigation = false)
        {
            if (ViewModel.CurrentTab == null) return;

            if (!isHistoryNavigation)
            {
                ViewModel.CurrentTab.PushHistory(url);
            }

            if (url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.IsLoading = false;
                NavBar.SetLoading(false);
            }

            if (url.StartsWith("bear://newtab", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://newtab";
                ViewModel.CurrentTab.Title = "Yeni Sekme";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://crash", StringComparison.OrdinalIgnoreCase))
            {
                _crashReportingService.TriggerTestCrash();
                return;
            }

            if (url.StartsWith("bear://favorites", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://favorites";
                ViewModel.CurrentTab.Title = "Sık Kullanılanlar";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/favorites.png");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://history", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://history";
                ViewModel.CurrentTab.Title = "Geçmiş";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/history.png");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://downloads", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://downloads";
                ViewModel.CurrentTab.Title = "İndirilenler";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/downloads.png");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://settings", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = url;
                ViewModel.CurrentTab.Title = "Ayarlar";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://passwords", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://passwords";
                ViewModel.CurrentTab.Title = "Parolalar";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/passwords.png");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            if (url.StartsWith("bear://extensions", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.CurrentTab.Url = "bear://settings/extensions";
                ViewModel.CurrentTab.Title = "Ayarlar";
                ViewModel.CurrentTab.FaviconUri = new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
                RenderCurrentTabContent(ViewModel.CurrentTab);
                UpdateNavigationButtons(ViewModel.CurrentTab);
                return;
            }

            // Eğer dahili sayfadayken bir web bağlantısına tıklanırsa WebView motorunu hazırla
            if (ViewModel.CurrentTab.GetWebView().CoreWebView2 == null || !ViewModel.CurrentTab.IsWebViewInitialized)
            {
                await ViewModel.EnsureTabWebViewInitializedAsync(ViewModel.CurrentTab);
            }

            ViewModel.CurrentTab.Url = url;
            ViewModel.CurrentTab.IsLoading = true;
            NavBar.SetLoading(true);
            try
            {
                ViewModel.CurrentTab.GetWebView().Source = new Uri(url);
            }
            catch
            {
                ViewModel.CurrentTab.GetWebView().Source = new Uri(_settingsService.GetSearchUrl(url));
            }

            RenderCurrentTabContent(ViewModel.CurrentTab);
            UpdateNavigationButtons(ViewModel.CurrentTab);
        }

        private void NavBar_NavigateRequested(object? sender, string query)
        {
            if (ViewModel.CurrentTab == null) return;

            string targetUrl = query.Trim();
            if (targetUrl.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                NavigateToUrl(targetUrl);
                return;
            }

            if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (targetUrl.Contains(".") && !targetUrl.Contains(" "))
                    targetUrl = "https://" + targetUrl;
                else
                    targetUrl = _settingsService.GetSearchUrl(targetUrl);
            }

            NavigateToUrl(targetUrl);
        }

        // --- TAM EKRAN (FULLSCREEN) YÖNETİMİ ---

        private void ToggleFullScreen()
        {
            SetFullScreen(!_isManualFullScreen);
        }

        public void SetFullScreen(bool enable)
        {
            _isManualFullScreen = enable;

            if (_isManualFullScreen)
            {
                _isFullScreen = true;
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                if (!_isVideoFullScreen)
                {
                    EnterFullScreenUi();
                }
            }
            else
            {
                if (_isVideoFullScreen)
                {
                    ExitVideoFullScreen();
                }

                _isFullScreen = false;
                AppWindow.SetPresenter(AppWindowPresenterKind.Default);
                ExitFullScreenUi();
            }
        }

        private void OnFullScreenElementChanged(bool containsFullScreenElement)
        {
            _isVideoFullScreen = containsFullScreenElement;

            if (_isVideoFullScreen)
            {
                // Video tam ekran oldu: pencereyi tam ekran yap ve tüm tarayıcı chrome'unu gizle
                _isFullScreen = true;
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                EnterVideoFullScreenUi();
            }
            else
            {
                // Video tam ekrandan çıktı:
                TopBarArea.Visibility = Visibility.Visible;
                if (_isManualFullScreen)
                {
                    EnterFullScreenUi();
                }
                else
                {
                    _isFullScreen = false;
                    AppWindow.SetPresenter(AppWindowPresenterKind.Default);
                    ExitFullScreenUi();
                }
            }
        }

        private void EnterVideoFullScreenUi()
        {
            _initialBannerTimer?.Stop();
            _hideTimer?.Stop();

            TopRowDefinition.Height = new GridLength(0);
            Grid.SetRow(WebContentGrid, 0);
            Grid.SetRowSpan(WebContentGrid, 2);
            Grid.SetRowSpan(TopBarArea, 2);

            FullScreenBanner.Visibility = Visibility.Collapsed;
            TopHoverTrigger.Visibility = Visibility.Collapsed;
            TopBarArea.Visibility = Visibility.Collapsed;
            TopBarTransform.Y = -200;
        }

        private void ExitVideoFullScreen()
        {
            try
            {
                ViewModel.CurrentTab?.GetWebView().CoreWebView2?.ExecuteScriptAsync(
                    "if (document.fullscreenElement) { document.exitFullscreen(); }"
                );
            }
            catch { }
            OnFullScreenElementChanged(false);
        }

        private void EnterFullScreenUi()
        {
            TopRowDefinition.Height = new GridLength(0);
            Grid.SetRow(WebContentGrid, 0);
            Grid.SetRowSpan(WebContentGrid, 2);
            Grid.SetRowSpan(TopBarArea, 2);

            TopBarArea.Background = Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(245, 32, 32, 32))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(245, 243, 243, 243));

            FullScreenBanner.Visibility = Visibility.Visible;
            TopHoverTrigger.Visibility = Visibility.Visible;
            TopBarArea.Visibility = Visibility.Visible;

            TopBarTransform.Y = 0;
            _initialBannerTimer?.Stop();
            _initialBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _initialBannerTimer.Tick += (s, e) =>
            {
                _initialBannerTimer.Stop();
                if (_isFullScreen && !_isVideoFullScreen)
                {
                    HideTopBar();
                }
            };
            _initialBannerTimer.Start();
        }

        private void ExitFullScreenUi()
        {
            _initialBannerTimer?.Stop();
            _hideTimer?.Stop();

            TopBarArea.Visibility = Visibility.Visible;
            TopBarTransform.Y = 0;
            TopBarArea.Background = null;
            FullScreenBanner.Visibility = Visibility.Collapsed;
            TopHoverTrigger.Visibility = Visibility.Collapsed;

            TopRowDefinition.Height = GridLength.Auto;
            Grid.SetRow(WebContentGrid, 1);
            Grid.SetRowSpan(WebContentGrid, 1);
            Grid.SetRowSpan(TopBarArea, 1);
        }

        private void ShowTopBar()
        {
            if (_isVideoFullScreen) return;
            _hideTimer?.Stop();
            _initialBannerTimer?.Stop();
            AnimateTopBar(0);
        }

        private void HideTopBar()
        {
            if (_isVideoFullScreen) return;
            double targetY = TopBarArea.ActualHeight > 0 ? -TopBarArea.ActualHeight : -140;
            AnimateTopBar(targetY);
        }

        private void AnimateTopBar(double toY)
        {
            var anim = new DoubleAnimation
            {
                To = toY,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, TopBarTransform);
            Storyboard.SetTargetProperty(anim, "Y");
            sb.Children.Add(anim);
            sb.Begin();
        }

        private void TopHoverTrigger_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_isFullScreen && !_isVideoFullScreen) ShowTopBar();
        }

        private void TopHoverTrigger_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_isFullScreen && !_isVideoFullScreen) ShowTopBar();
        }

        private void TopBarArea_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_isFullScreen && !_isVideoFullScreen) _hideTimer?.Stop();
        }

        private void TopBarArea_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isFullScreen && !_isVideoFullScreen)
            {
                _hideTimer?.Stop();
                _hideTimer?.Start();
            }
        }

        private void ExitFullScreenBtn_Click(object sender, RoutedEventArgs e)
        {
            SetFullScreen(false);
        }

        private void ApplyTheme(string theme)
        {
            RootGrid.RequestedTheme = theme switch
            {
                "Dark" => ElementTheme.Dark,
                "Light" => ElementTheme.Light,
                _ => ElementTheme.Default
            };

            ApplyThemeToWebViews(theme);
        }

        private void ApplyThemeToWebViews(string theme)
        {
            try
            {
                bool isDark = theme == "Dark" ||
                    (theme == "System" && Application.Current.RequestedTheme == ApplicationTheme.Dark);

                var scheme = isDark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : (theme == "Light" ? CoreWebView2PreferredColorScheme.Light : CoreWebView2PreferredColorScheme.Auto);

                foreach (var tab in ViewModel.Tabs)
                {
                    if (tab.GetWebView().CoreWebView2 != null)
                    {
                        try
                        {
                            tab.GetWebView().CoreWebView2.Profile.PreferredColorScheme = scheme;
                            ApplyWebPageDarkScript(tab.GetWebView(), isDark);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static async void ApplyWebPageDarkScript(WebView2 webView, bool isDark)
        {
            if (webView.CoreWebView2 == null) return;
            try
            {
                string script = isDark
                    ? @"(() => {
                        try {
                            if (location.hostname.includes('youtube.com')) {
                                document.documentElement.setAttribute('dark', 'true');
                            }
                            if (!document.getElementById('bear-dark-force-style')) {
                                const s = document.createElement('style');
                                s.id = 'bear-dark-force-style';
                                s.textContent = ':root { color-scheme: dark !important; }';
                                (document.head || document.documentElement).appendChild(s);
                            }
                        } catch(e) {}
                    })();"
                    : @"(() => {
                        try {
                            if (location.hostname.includes('youtube.com')) {
                                document.documentElement.removeAttribute('dark');
                            }
                            const el = document.getElementById('bear-dark-force-style');
                            if (el) el.remove();
                        } catch(e) {}
                    })();";

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        // --- YAKINLAŞTIRMA ---

        private async void ChangeZoom(double delta)
        {
            if (ViewModel.CurrentTab != null)
            {
                double current = ViewModel.CurrentTab.ZoomFactor;
                double next = Math.Clamp(Math.Round(current + delta, 1), 0.25, 5.0);
                ViewModel.CurrentTab.ZoomFactor = next;
                NavBar.UpdateZoomDisplay(next);
                if (ViewModel.CurrentTab.GetWebView().CoreWebView2 != null)
                {
                    string zoomStr = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    await ViewModel.CurrentTab.GetWebView().CoreWebView2.ExecuteScriptAsync(
                        $"document.documentElement.style.zoom = '{zoomStr}';"
                    );
                }
            }
        }

        // --- KISAYOLLAR VE MENÜ AKSİYONLARI ---

        private void HandleGlobalAction(string action)
        {
            switch (action)
            {
                case "NewTab":
                    CreateNewTab();
                    break;
                case "CloseTab":
                    if (ViewModel.CurrentTab != null)
                        ViewModel.CloseTab(ViewModel.CurrentTab);
                    break;
                case "NewWindow":
                    OpenNewWindow(isInPrivate: false);
                    break;
                case "NewInPrivate":
                    OpenNewWindow(isInPrivate: true);
                    break;
                case "ToggleFullScreen":
                    ToggleFullScreen();
                    break;
                case "ExitFullScreen":
                    if (_isFullScreen) SetFullScreen(false);
                    break;
                case "ExitApp":
                    try
                    {
                        foreach (var tab in ViewModel.Tabs)
                        {
                            tab.GetWebView()?.Close();
                        }
                    }
                    catch { }
                    this.Close();
                    break;
                case "Favorites":
                    OpenFavoritesPage();
                    break;
                case "History":
                    OpenHistoryPage();
                    break;
                case "Downloads":
                    CreateNewTab("bear://downloads");
                    break;
                case "ManageExtensions":
                case "Extensions":
                    CreateNewTab("bear://settings/extensions");
                    break;
                case "OpenExtensionStore":
                    CreateNewTab("https://chromewebstore.google.com");
                    break;
                case "Settings":
                    CreateNewTab("bear://settings/profiles");
                    break;
                case "About":
                    CreateNewTab("bear://settings/about");
                    break;
                case "DevTools":
                    ViewModel.CurrentTab?.GetWebView().CoreWebView2?.OpenDevToolsWindow();
                    break;
                case "Passwords":
                    CreateNewTab("bear://passwords");
                    break;
                case "FindInPage":
                    ToggleFindBar();
                    break;
                case "Feedback":
                    CreateNewTab("https://discord.gg/bearbrowser");
                    break;
            }
        }

        private void OpenHistoryPage()
        {
            CreateNewTab("bear://history");
        }

        private async void CreateNewTab(string? url = null)
        {
            string targetUrl = url ?? (!string.IsNullOrWhiteSpace(_settingsService.Settings.HomePageUrl) && _settingsService.Settings.HomePageUrl != "https://www.google.com"
                ? _settingsService.Settings.HomePageUrl
                : "bear://newtab");
            await ViewModel.CreateNewTabAsync(targetUrl);
            TabsControl.SelectedIndex = ViewModel.Tabs.Count - 1;
        }

        private void OpenFavoritesPage()
        {
            CreateNewTab("bear://favorites");
        }

        private void OpenNewWindow(bool isInPrivate)
        {
            var win = new MainWindow(isInPrivate);
            win.Activate();
        }

        private void Shortcut_NewTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            HandleGlobalAction("NewTab");
            args.Handled = true;
        }

        private void Shortcut_NewWindow_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            HandleGlobalAction("NewWindow");
            args.Handled = true;
        }

        private void Shortcut_NewInPrivate_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            HandleGlobalAction("NewInPrivate");
            args.Handled = true;
        }

        private void Shortcut_CloseTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            HandleGlobalAction("CloseTab");
            args.Handled = true;
        }

        private void Shortcut_ToggleFullScreen_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ToggleFullScreen();
            args.Handled = true;
        }

        private void Shortcut_Escape_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (FindBar.Visibility == Visibility.Visible)
            {
                FindBar.Visibility = Visibility.Collapsed;
                args.Handled = true;
                return;
            }

            if (_isVideoFullScreen)
            {
                ExitVideoFullScreen();
                args.Handled = true;
                return;
            }

            if (ViewModel.CurrentTab?.IsLoading == true)
            {
                try { ViewModel.CurrentTab.GetWebView().CoreWebView2?.Stop(); } catch { }
                args.Handled = true;
                return;
            }

            if (_isFullScreen)
            {
                SetFullScreen(false);
                args.Handled = true;
            }
        }

        private void Shortcut_History_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            HandleGlobalAction("History");
            args.Handled = true;
        }

        private async Task InitializeStartupAsync()
        {
            string startUrl = !string.IsNullOrWhiteSpace(_settingsService.Settings.HomePageUrl) && _settingsService.Settings.HomePageUrl != "https://www.google.com"
                ? _settingsService.Settings.HomePageUrl
                : "bear://newtab";

            var tab = await ViewModel.CreateNewTabAsync(startUrl);
            if (tab != null)
            {
                bool dismissed = false;
                void DismissLoading()
                {
                    if (!dismissed)
                    {
                        dismissed = true;
                        HideLoadingScreenWithFade();
                    }
                }

                // Sayfa ilk yüklendiğinde loading ekranını kaldır
                tab.GetWebView().NavigationCompleted += (s, e) => DismissLoading();

                // Güvenlik zaman aşımı: Ağ veya site gecikse bile en geç 1 saniye içinde kaldır
                await Task.Delay(1000);
                DismissLoading();
            }
            else
            {
                HideLoadingScreenWithFade();
            }
        }

        private async void HideLoadingScreenWithFade()
        {
            if (LoadingOverlay == null || LoadingOverlay.Visibility == Visibility.Collapsed) return;

            // Akıcı kaybolma animasyonu (Fade-Out)
            for (double opacity = 1.0; opacity >= 0.0; opacity -= 0.1)
            {
                LoadingOverlay.Opacity = opacity;
                await Task.Delay(20);
            }

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void NavBar_ManageProfileRequested(object? sender, EventArgs e)
        {
            var dialog = new ProfileEditDialog(_profileService.CurrentProfile)
            {
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void NavBar_BackRequested(object? sender, EventArgs e)
        {
            var tab = ViewModel.CurrentTab;
            if (tab == null) return;

            bool isInternalPage = tab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase);

            if (!isInternalPage && tab.GetWebView().CoreWebView2 != null && tab.GetWebView().CoreWebView2.CanGoBack)
            {
                tab.GetWebView().CoreWebView2.GoBack();
            }
            else if (tab.HistoryIndex > 0)
            {
                tab.HistoryIndex--;
                string prevUrl = tab.HistoryStack[tab.HistoryIndex];
                NavigateToUrl(prevUrl, isHistoryNavigation: true);
            }
            else if (tab.GetWebView().CanGoBack)
            {
                tab.GetWebView().GoBack();
            }

            UpdateNavigationButtons(tab);
        }

        private void NavBar_ForwardRequested(object? sender, EventArgs e)
        {
            var tab = ViewModel.CurrentTab;
            if (tab == null) return;

            bool isInternalPage = tab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase);

            if (!isInternalPage && tab.GetWebView().CoreWebView2 != null && tab.GetWebView().CoreWebView2.CanGoForward)
            {
                tab.GetWebView().CoreWebView2.GoForward();
            }
            else if (tab.HistoryIndex >= 0 && tab.HistoryIndex < tab.HistoryStack.Count - 1)
            {
                tab.HistoryIndex++;
                string nextUrl = tab.HistoryStack[tab.HistoryIndex];
                NavigateToUrl(nextUrl, isHistoryNavigation: true);
            }
            else if (tab.GetWebView().CanGoForward)
            {
                tab.GetWebView().GoForward();
            }

            UpdateNavigationButtons(tab);
        }

        private void UpdateNavigationButtons(BrowserTab? tab)
        {
            if (tab == null)
            {
                NavBar.UpdateNavigationState(false, false);
                return;
            }

            bool webCanGoBack = false;
            bool webCanGoForward = false;

            if (tab.GetWebView().CoreWebView2 != null)
            {
                webCanGoBack = tab.GetWebView().CoreWebView2.CanGoBack;
                webCanGoForward = tab.GetWebView().CoreWebView2.CanGoForward;
            }
            else
            {
                webCanGoBack = tab.GetWebView().CanGoBack;
                webCanGoForward = tab.GetWebView().CanGoForward;
            }

            bool canGoBack = webCanGoBack || (tab.HistoryIndex > 0);
            bool canGoForward = webCanGoForward || (tab.HistoryIndex >= 0 && tab.HistoryIndex < tab.HistoryStack.Count - 1);

            NavBar.UpdateNavigationState(canGoBack, canGoForward);
        }

        private void NavBar_RefreshRequested(object? sender, EventArgs e)
        {
            if (ViewModel.CurrentTab == null) return;

            // Eğer Geçmiş veya Sık Kullanılanlar gibi dahili bir sayfadaysak yerel görünümü yenile
            if (ViewModel.CurrentTab.Url.StartsWith("bear://", StringComparison.OrdinalIgnoreCase))
            {
                RenderCurrentTabContent(ViewModel.CurrentTab);
            }
            // Normal bir web sitesindeysek ve WebView hazırsa sayfayı yenile
            else if (ViewModel.CurrentTab.GetWebView().CoreWebView2 != null)
            {
                ViewModel.CurrentTab.GetWebView().Reload();
            }
        }

        private void TabsControl_AddTabButtonClick(TabView sender, object args)
        {
            CreateNewTab();
        }

        private void TabsControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is BrowserTab tab)
            {
                ViewModel.CloseTab(tab);
            }
        }

        private void TabsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsControl.SelectedItem is BrowserTab selectedTab)
            {
                ViewModel.CurrentTab = selectedTab;
            }
        }

        // --- F12 & CTRL + F KISAYOLLARI ---

        private void Shortcut_DevTools_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ViewModel.CurrentTab?.GetWebView().CoreWebView2?.OpenDevToolsWindow();
        }

        private void Shortcut_FindInPage_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            ToggleFindBar();
        }

        // --- CTRL + F SAYFADA BUL MANTIĞI ---

        private void ToggleFindBar()
        {
            if (FindBar.Visibility == Visibility.Visible)
            {
                FindBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                FindBar.Visibility = Visibility.Visible;
                FindTextBox.Focus(FocusState.Programmatic);
                FindTextBox.SelectAll();
                if (!string.IsNullOrEmpty(FindTextBox.Text))
                    PerformFind(FindTextBox.Text, backwards: false);
            }
        }

        private void PerformFind(string query, bool backwards)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            string escaped = query.Replace("'", "\\'");
            string script = $"window.find('{escaped}', false, {backwards.ToString().ToLower()}, true);";
            _ = ViewModel.CurrentTab?.GetWebView().CoreWebView2?.ExecuteScriptAsync(script);
        }

        private void FindTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformFind(FindTextBox.Text, backwards: false);
        }

        private void FindTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
                bool isShift = (shift & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                PerformFind(FindTextBox.Text, backwards: isShift);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                FindBar.Visibility = Visibility.Collapsed;
            }
        }

        private void FindPrevBtn_Click(object sender, RoutedEventArgs e) => PerformFind(FindTextBox.Text, backwards: true);
        private void FindNextBtn_Click(object sender, RoutedEventArgs e) => PerformFind(FindTextBox.Text, backwards: false);
        private void FindCloseBtn_Click(object sender, RoutedEventArgs e) => FindBar.Visibility = Visibility.Collapsed;

        // --- PAROLA BİLDİRİMİ İŞLEMLERİ ---

        private (string url, string username, string password)? _pendingPasswordData;

        private async void SavePasswordPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingPasswordData.HasValue)
            {
                var data = _pendingPasswordData.Value;
                await _passwordService.AddOrUpdatePasswordAsync(data.url, data.username, data.password);
            }
            PasswordPromptBar.Visibility = Visibility.Collapsed;
        }

        private void DismissPasswordPrompt_Click(object sender, RoutedEventArgs e)
        {
            PasswordPromptBar.Visibility = Visibility.Collapsed;
        }

        // --- EKLENTİ YÜKLEME İŞLEMLERİ ---

        private string _pendingExtensionStoreType = "";
        private string _pendingExtensionId = "";
        private string _pendingExtensionName = "";

        private void ShowExtensionInstallPrompt(string storeType, string extensionId, string name)
        {
            _pendingExtensionStoreType = storeType;
            _pendingExtensionId = extensionId;
            _pendingExtensionName = name;

            ExtensionInstallTitleText.Text = string.IsNullOrWhiteSpace(name) || name == "Eklenti"
                ? "Bu uzantıyı eklemek istiyor musunuz?"
                : $"'{name}' eklensin mi?";

            ExtensionInstallStoreText.Text = storeType.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                ? "Microsoft Edge Eklentileri"
                : "Chrome Web Mağazası";

            ExtensionInstallStatusText.Text = "Bu uzantı Bear Browser'a eklenerek web sitelerinde etkinleştirilecek.";
            ConfirmInstallExtensionBtn.IsEnabled = true;
            ConfirmInstallExtensionBtnText.Text = "Bear Browser'a Ekle";
            ExtensionInstallProgress.Visibility = Visibility.Collapsed;
            ExtensionInstallProgress.IsActive = false;
            ExtensionInstallBar.Visibility = Visibility.Visible;
        }

        private async void ConfirmInstallExtensionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_pendingExtensionId)) return;

            ConfirmInstallExtensionBtn.IsEnabled = false;
            ExtensionInstallProgress.Visibility = Visibility.Visible;
            ExtensionInstallProgress.IsActive = true;
            ConfirmInstallExtensionBtnText.Text = "İndiriliyor...";

            try
            {
                var profile = ViewModel.CurrentTab?.GetWebView().CoreWebView2?.Profile;
                var info = await _extensionService.DownloadAndInstallAsync(
                    _pendingExtensionStoreType,
                    _pendingExtensionId,
                    _pendingExtensionName,
                    profile
                );

                ConfirmInstallExtensionBtnText.Text = "✓ Eklendi!";
                ExtensionInstallProgress.Visibility = Visibility.Collapsed;
                ExtensionInstallProgress.IsActive = false;
                _ = ViewModel.CurrentTab?.GetWebView().CoreWebView2?.ExecuteScriptAsync(@"
                    (() => {
                        const btns = Array.from(document.querySelectorAll('button, [role=""button""]'));
                        const btn = btns.find(b => (b.innerText || '').includes('Ekleniyor') || (b.innerText || '').includes('Bear'));
                        if (btn) {
                            btn.innerText = '✓ Bear Browser\'a Eklendi!';
                            btn.style.setProperty('background', '#107c41', 'important');
                            btn.style.setProperty('color', '#ffffff', 'important');
                            btn.style.setProperty('opacity', '1', 'important');
                        }
                    })();
                ");
                await Task.Delay(1800);
                ExtensionInstallBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ConfirmInstallExtensionBtnText.Text = "Hata Oluştu";
                ExtensionInstallProgress.Visibility = Visibility.Collapsed;
                ExtensionInstallProgress.IsActive = false;
                ExtensionInstallStatusText.Text = $"Eklenti yüklenirken hata oluştu: {ex.Message}";
                ConfirmInstallExtensionBtn.IsEnabled = true;
            }
        }

        private void DismissExtensionInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            ExtensionInstallBar.Visibility = Visibility.Collapsed;
        }

        public void ShowExtensionInstalledNotification(string name)
        {
            ExtensionInstallTitleText.Text = $"'{name}' Eklendi!";
            ExtensionInstallStoreText.Text = "Bear Browser Uzantısı";
            ExtensionInstallStatusText.Text = "Uzantı başarıyla yüklendi ve tüm sekmelerde etkinleştirildi.";
            ConfirmInstallExtensionBtn.IsEnabled = false;
            ConfirmInstallExtensionBtnText.Text = "✓ Yüklendi";
            ExtensionInstallProgress.Visibility = Visibility.Collapsed;
            ExtensionInstallProgress.IsActive = false;
            ExtensionInstallBar.Visibility = Visibility.Visible;

            _ = Task.Delay(3500).ContinueWith(_ =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ExtensionInstallBar.Visibility = Visibility.Collapsed;
                });
            });
        }
    }
}