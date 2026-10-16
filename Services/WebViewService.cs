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
        private static WebViewService? _instance;
        public static WebViewService Instance
        {
            get
            {
                if (App.Services?.GetService<IWebViewService>() is WebViewService ws)
                    return ws;
                return _instance ??= new WebViewService();
            }
        }

        private static readonly Lazy<string> ShortcutsScript = new(() => LoadEmbeddedResource("Shortcuts.js"));
        private static readonly Lazy<string> AutofillScript = new(() => LoadEmbeddedResource("Autofill.js"));
        private static readonly Lazy<string> WebStoreHookScript = new(() => LoadEmbeddedResource("WebStoreHook.js"));

        private CoreWebView2Environment? _environment;
        private readonly string _userDataFolder;

        public WebViewService()
        {
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
                    "--enable-quic " +
                    "--enable-async-dns " +
                    "--enable-tcp-fast-open " +
                    "--enable-gpu-rasterization " +
                    "--enable-zero-copy " +
                    "--use-angle=d3d11 " +
                    "--enable-accelerated-2d-canvas " +
                    "--process-per-site " +
                    "--renderer-process-limit=4 " +
                    "--disable-features=IsolateOrigins,site-per-process,Translate,OptimizationHints,MediaRouter " +
                    "--enable-features=TurnOffStreamingMediaWithHiddenWindow,BackForwardCache,AutomaticTabDiscarding,WebContentsDiscard,CalculateNativeWinOcclusion,UseEcoQoSForBackgroundProcess,UseEcoQoSForRenderers " +
                    "--disk-cache-size=104857600 " +
                    "--media-cache-size=52428800 " +
                    "--js-flags=\"--max_old_space_size=256 --optimize_for_size --expose-gc\" " +
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
                        "--enable-gpu-rasterization --enable-zero-copy --use-angle=d3d11 " +
                        "--process-per-site --renderer-process-limit=3 " +
                        "--disable-features=IsolateOrigins,site-per-process " +
                        "--enable-features=TurnOffStreamingMediaWithHiddenWindow,CalculateNativeWinOcclusion,UseEcoQoSForBackgroundProcess,UseEcoQoSForRenderers " +
                        "--js-flags=\"--max_old_space_size=256 --optimize_for_size\""
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
                _ = ExtensionService.Instance.SyncInstalledExtensionsAsync(webView.CoreWebView2.Profile);
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
                    CrashReportingService.Instance.LogCustomError("WebView2 İşlem Hatası (ProcessFailed)", info);
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