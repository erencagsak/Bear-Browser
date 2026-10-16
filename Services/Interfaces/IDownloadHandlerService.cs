using System;
using Bear_Browser.Models;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services.Interfaces
{
    public interface IDownloadHandlerService
    {
        void HandleDownload(
            CoreWebView2DownloadStartingEventArgs args,
            BrowserTab tab,
            Action<bool, bool> onDownloadStateChanged,
            Action onPopupCloseTab);
    }
}
