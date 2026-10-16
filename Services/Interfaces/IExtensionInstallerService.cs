using System;
using Bear_Browser.Models;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services.Interfaces
{
    public interface IExtensionInstallerService
    {
        bool TryHandleCrxDownload(
            CoreWebView2DownloadStartingEventArgs args,
            BrowserTab tab,
            Action<string, string, string> onProgressStarted,
            Action<string> onInstalledSuccess,
            Action<string> onInstallFailed,
            Action onPopupCloseTab);
    }
}
