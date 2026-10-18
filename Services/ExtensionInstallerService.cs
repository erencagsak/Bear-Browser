using System;
using System.IO;
using System.Text.RegularExpressions;
using Bear_Browser.Models;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class ExtensionInstallerService : IExtensionInstallerService
    {
        private readonly IExtensionService _extensionService;
        private readonly ICrashReportingService _crashReportingService;

        public ExtensionInstallerService(
            IExtensionService extensionService,
            ICrashReportingService crashReportingService)
        {
            _extensionService = extensionService ?? throw new ArgumentNullException(nameof(extensionService));
            _crashReportingService = crashReportingService ?? throw new ArgumentNullException(nameof(crashReportingService));
        }

        public bool TryHandleCrxDownload(
            CoreWebView2DownloadStartingEventArgs args,
            BrowserTab tab,
            Action<string, string, string> onProgressStarted,
            Action<string> onInstalledSuccess,
            Action<string> onInstallFailed,
            Action onPopupCloseTab)
        {
            var op = args.DownloadOperation;
            string uri = op.Uri ?? "";
            string targetPath = args.ResultFilePath ?? "";

            if (!uri.Contains("clients2.google.com/service/update2/crx", StringComparison.OrdinalIgnoreCase) &&
                !uri.Contains("edge.microsoft.com/extensionwebstorebase", StringComparison.OrdinalIgnoreCase) &&
                !uri.EndsWith(".crx", StringComparison.OrdinalIgnoreCase) &&
                !targetPath.EndsWith(".crx", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            args.Handled = true;

            string extId = "";
            var m = Regex.Match(uri, @"[?&;]x=id(?:%3D|=)([a-zA-Z0-9]{32})", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                extId = m.Groups[1].Value.ToLowerInvariant();
            }
            else
            {
                var m2 = Regex.Match(uri, @"[?&;]id=([a-zA-Z0-9]{32})", RegexOptions.IgnoreCase);
                if (m2.Success)
                {
                    extId = m2.Groups[1].Value.ToLowerInvariant();
                }
                else if (_extensionService.IsStoreExtensionUrl(tab.Url, out _, out string fromTabId))
                {
                    extId = fromTabId;
                }
                else if (!string.IsNullOrEmpty(targetPath))
                {
                    string fn = Path.GetFileNameWithoutExtension(targetPath);
                    if (fn.Length == 32) extId = fn.ToLowerInvariant();
                }
            }

            if (string.IsNullOrEmpty(extId))
            {
                extId = Guid.NewGuid().ToString("N")[..32];
            }

            string storeType = uri.Contains("edge.microsoft.com", StringComparison.OrdinalIgnoreCase) ? "Edge" : "Chrome";
            string tempCrxPath = Path.Combine(Path.GetTempPath(), $"BearExt_{extId}_{Guid.NewGuid():N}.crx");
            args.ResultFilePath = tempCrxPath;

            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
            {
                if (tab.IsPopupTab)
                {
                    onPopupCloseTab();
                }

                onProgressStarted(storeType, extId, tab.Title);
            });

            op.StateChanged += async (s, e) =>
            {
                if (op.State == CoreWebView2DownloadState.Completed)
                {
                    try
                    {
                        var profile = tab.GetWebView().CoreWebView2?.Profile;
                        var info = await _extensionService.InstallFromCrxFileAsync(tempCrxPath, extId, storeType, tab.Title, profile);
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (info != null)
                            {
                                onInstalledSuccess(info.Name);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _crashReportingService.LogCustomError("Extension Auto-Install Failed", ex.Message);
                        tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                        {
                            onInstallFailed(ex.Message);
                        });
                    }
                }
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                {
                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                    {
                        onInstallFailed($"İndirme kesintiye uğradı: {op.InterruptReason}");
                    });
                }
            };

            return true;
        }
    }
}
