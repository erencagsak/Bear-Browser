using System;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Services.Interfaces
{
    public interface IAdBlockService
    {
        bool IsEnabled { get; set; }
        void AttachToWebView(WebView2 webView, Action? onAdBlocked = null);
    }
}
