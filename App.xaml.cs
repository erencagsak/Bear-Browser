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
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use.
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Services = ConfigureServices();

            Bear_Browser.Services.CrashReportingService.Instance.Initialize(this);
            Bear_Browser.Services.LocalizationService.Instance.Initialize();
            InitializeComponent();
        }

        /// <summary>
        /// Configures the dependency injection container and registers all application services.
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // MVVM Messaging
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // Data & Storage Services
            services.AddSingleton<IJsonStorageService, JsonStorageService>();

            // Platform Services (WinUI)
            services.AddSingleton<IDataProtectionService, WindowsDataProtectionService>();
            services.AddSingleton<IDefaultBrowserService, WindowsDefaultBrowserService>();
            services.AddSingleton<IPlatformWindowService, WindowsPlatformWindowService>();
            services.AddSingleton<IPlatformProcessService, WindowsPlatformProcessService>();

            // Domain Services
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

            // ViewModels
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<NavigationBarViewModel>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();

            string[] cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Any(a => a.Equals("--test-crash", StringComparison.OrdinalIgnoreCase)))
            {
                Bear_Browser.Services.CrashReportingService.Instance.TriggerTestCrash();
            }
        }
    }
}
