using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Data.Implementations;
using Bear_Browser.Platform.Interfaces;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services;
using Bear_Browser.Services.Interfaces;
using Bear_Browser.ViewModels;

namespace Bear_Browser
{
    public partial class App : Application
    {
        private Window? _window;

        public new static App Current => (App)Application.Current;

        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            Services = ConfigureServices();

            Services.GetRequiredService<ICrashReportingService>().Initialize(this);
            Services.GetRequiredService<ILocalizationService>().Initialize();
            InitializeComponent();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            services.AddSingleton<IJsonStorageService, JsonStorageService>();

            services.AddSingleton<IDataProtectionService, WindowsDataProtectionService>();
            services.AddSingleton<IDefaultBrowserService, WindowsDefaultBrowserService>();
            services.AddSingleton<IPlatformWindowService, WindowsPlatformWindowService>();
            services.AddSingleton<IPlatformProcessService, WindowsPlatformProcessService>();

            services.AddSingleton<IBookmarkService, BookmarkService>();
            services.AddSingleton<IHistoryService, HistoryService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<IProfileService, ProfileService>();
            services.AddSingleton<IWebViewService, WebViewService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();
            services.AddSingleton<IExtensionService, ExtensionService>();
            services.AddSingleton<ICrashReportingService, CrashReportingService>();
            services.AddSingleton<IAdBlockService, AdBlockService>();
            services.AddSingleton<IMemoryOptimizerService, MemoryOptimizerService>();
            services.AddSingleton<IExtensionInstallerService, ExtensionInstallerService>();
            services.AddSingleton<IAutofillService, AutofillService>();
            services.AddSingleton<IDownloadHandlerService, DownloadHandlerService>();
            services.AddSingleton<IUpdateService, UpdateService>();

            services.AddTransient<SettingsViewModel>();
            services.AddTransient<NavigationBarViewModel>();
            services.AddTransient<DownloadsViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<PasswordsViewModel>();
            services.AddTransient<FavoritesViewModel>();
            services.AddTransient<ExtensionsViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            string[] cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Any(a => a.Equals("--test-crash", StringComparison.OrdinalIgnoreCase)))
            {
                Services.GetRequiredService<ICrashReportingService>().TriggerTestCrash();
            }
        }
    }
}
