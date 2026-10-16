using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services.Interfaces
{
    public interface IWebViewService
    {
        Task<CoreWebView2Environment> GetEnvironmentAsync();
        Task InitializeWebViewAsync(WebView2 webView, bool isInPrivate = false);
    }
}
