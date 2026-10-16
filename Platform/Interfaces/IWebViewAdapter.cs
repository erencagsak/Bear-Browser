using System;
using System.Threading.Tasks;

namespace Bear_Browser.Platform.Interfaces
{
    public interface IWebViewAdapter
    {
        Uri? Source { get; set; }
        bool CanGoBack { get; }
        bool CanGoForward { get; }

        void Navigate(string url);
        void GoBack();
        void GoForward();
        void Reload();
        void Stop();
        void Close();
        Task<string> ExecuteScriptAsync(string script);
        void SetLowMemoryUsage(bool isLow);
        void OpenDevTools();

        event EventHandler<string>? NavigationStarting;
        event EventHandler<(bool IsSuccess, int WebErrorStatus)>? NavigationCompleted;
        event EventHandler<string>? WebMessageReceived;
    }
}
