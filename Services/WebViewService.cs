using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class WebViewService : IWebViewService
    {
        private static readonly Lazy<string> ShortcutsScript = new(() => LoadEmbeddedResource("Shortcuts.js"));
        private static readonly Lazy<string> AutofillScript = new(() => LoadEmbeddedResource("Autofill.js"));
        private static readonly Lazy<string> WebStoreHookScript = new(() => LoadEmbeddedResource("WebStoreHook.js"));

        private CoreWebView2Environment? _environment;
        private readonly string _userDataFolder;
        private readonly IExtensionService? _extensionService;
        private readonly ICrashReportingService? _crashReportingService;

        public WebViewService(IExtensionService? extensionService = null, ICrashReportingService? crashReportingService = null)
        {
            _extensionService = extensionService;
            _crashReportingService = crashReportingService;
            _userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser",
                "Profile"
            );
        }

        public async Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environment != null)
                return _environment;

            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true,
                AdditionalBrowserArguments =
                    "--disable-blink-features=AutomationControlled " +
                    "--enable-smooth-scrolling " +
                    "--enable-gpu-rasterization " +
                    "--enable-zero-copy " +
                    "--use-angle=d3d11 " +
                    "--enable-accelerated-2d-canvas " +
                    "--enable-accelerated-video-decode " +
                    "--enable-quic " +
                    "--enable-async-dns " +
                    "--enable-tcp-fast-open " +
                    "--process-per-site " +
                    "--disable-features=Translate,OptimizationHints,MediaRouter " +
                    "--enable-features=SmoothScrolling,CanvasOopRasterization,VaapiVideoDecoder,BackForwardCache,AutomaticTabDiscarding,WebContentsDiscard,CalculateNativeWinOcclusion,TurnOffStreamingMediaWithHiddenWindow " +
                    "--high-dpi-support=1"
            };

            _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataFolder,
                options: options
            );

            return _environment;
        }

        public async Task InitializeWebViewAsync(WebView2 webView, bool isInPrivate = false)
        {
            CoreWebView2Environment env;

            if (isInPrivate)
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), $"Bear_InPrivate_{Guid.NewGuid()}");
                var options = new CoreWebView2EnvironmentOptions
                {
                    AreBrowserExtensionsEnabled = true,
                    AdditionalBrowserArguments =
                        "--disable-blink-features=AutomationControlled " +
                        "--enable-smooth-scrolling " +
                        "--enable-gpu-rasterization " +
                        "--enable-zero-copy " +
                        "--use-angle=d3d11 " +
                        "--enable-accelerated-2d-canvas " +
                        "--enable-accelerated-video-decode " +
                        "--process-per-site " +
                        "--disable-features=Translate,OptimizationHints,MediaRouter " +
                        "--enable-features=SmoothScrolling,CanvasOopRasterization,VaapiVideoDecoder,CalculateNativeWinOcclusion,TurnOffStreamingMediaWithHiddenWindow " +
                        "--high-dpi-support=1"
                };
                env = await CoreWebView2Environment.CreateWithOptionsAsync(null, tempFolder, options);
            }
            else
            {
                env = await GetEnvironmentAsync();
            }

            await webView.EnsureCoreWebView2Async(env);

            if (webView.CoreWebView2 != null && !isInPrivate)
            {
                var extService = _extensionService ?? App.Services?.GetService<IExtensionService>();
                if (extService != null)
                {
                    _ = extService.SyncInstalledExtensionsAsync(webView.CoreWebView2.Profile);
                }
            }

            if (webView.CoreWebView2 != null)
            {
                var settings = webView.CoreWebView2.Settings;
                settings.IsStatusBarEnabled = false;
                settings.AreDevToolsEnabled = true;
                settings.IsBuiltInErrorPageEnabled = false;
                settings.IsGeneralAutofillEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;

                // ⚡ KRİTİK AYAR: Chromium'un Ctrl+T / Ctrl+W tuşlarını yutmasını engelliyoruz!
                settings.AreBrowserAcceleratorKeysEnabled = false;

                // WebView2 Motoru veya Render İşlemi Çökmesi Yakalayıcısı
                webView.CoreWebView2.ProcessFailed += (s, args) =>
                {
                    string info = $"İşlem Türü: {args.ProcessFailedKind}\nNeden: {args.Reason}\nÇıkış Kodu: {args.ExitCode}\nAçıklama: {args.ProcessDescription}";
                    var crashService = _crashReportingService ?? App.Services?.GetService<ICrashReportingService>();
                    crashService?.LogCustomError("WebView2 İşlem Hatası (ProcessFailed)", info);
                };

                // Betikleri Embedded Resource üzerinden enjekte et
                if (!string.IsNullOrEmpty(ShortcutsScript.Value))
                {
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ShortcutsScript.Value);
                }

                if (!string.IsNullOrEmpty(AutofillScript.Value))
                {
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(AutofillScript.Value);
                }

                if (!string.IsNullOrEmpty(WebStoreHookScript.Value))
                {
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(WebStoreHookScript.Value);
                }
            }
        }

        private static string LoadEmbeddedResource(string resourceSuffix)
        {
            try
            {
                var assembly = typeof(WebViewService).Assembly;
                string? resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

                if (resourceName == null)
                    return string.Empty;

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return string.Empty;

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}