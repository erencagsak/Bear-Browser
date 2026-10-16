using System;
using System.Threading.Tasks;
using Bear_Browser.Platform.Interfaces;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Platform.WinUI
{
    public class WebView2Adapter : IWebViewAdapter
    {
        private readonly WebView2 _webView;

        public WebView2 NativeWebView => _webView;

        public Uri? Source
        {
            get => _webView.Source;
            set => _webView.Source = value;
        }

        public bool CanGoBack => _webView.CoreWebView2?.CanGoBack ?? _webView.CanGoBack;
        public bool CanGoForward => _webView.CoreWebView2?.CanGoForward ?? _webView.CanGoForward;

        public event EventHandler<string>? NavigationStarting;
        public event EventHandler<(bool IsSuccess, int WebErrorStatus)>? NavigationCompleted;
        public event EventHandler<string>? WebMessageReceived;

        public WebView2Adapter(WebView2 webView)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));

            _webView.NavigationStarting += (s, e) =>
            {
                NavigationStarting?.Invoke(this, e.Uri);
            };

            _webView.NavigationCompleted += (s, e) =>
            {
                NavigationCompleted?.Invoke(this, (e.IsSuccess, (int)e.WebErrorStatus));
            };

            _webView.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string msg = e.TryGetWebMessageAsString();
                    if (!string.IsNullOrEmpty(msg))
                    {
                        WebMessageReceived?.Invoke(this, msg);
                    }
                }
                catch { }
            };
        }

        public void Navigate(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Navigate(url);
            }
            else
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    _webView.Source = uri;
                }
            }
        }

        public void GoBack()
        {
            if (_webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoBack)
            {
                _webView.CoreWebView2.GoBack();
            }
            else if (_webView.CanGoBack)
            {
                _webView.GoBack();
            }
        }

        public void GoForward()
        {
            if (_webView.CoreWebView2 != null && _webView.CoreWebView2.CanGoForward)
            {
                _webView.CoreWebView2.GoForward();
            }
            else if (_webView.CanGoForward)
            {
                _webView.GoForward();
            }
        }

        public void Reload()
        {
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.Reload();
            }
            else
            {
                _webView.Reload();
            }
        }

        public void Stop()
        {
            try
            {
                _webView.CoreWebView2?.Stop();
            }
            catch { }
        }

        public void Close()
        {
            try
            {
                _webView.Close();
            }
            catch { }
        }

        public async Task<string> ExecuteScriptAsync(string script)
        {
            if (_webView.CoreWebView2 != null)
            {
                return await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            return string.Empty;
        }

        public void SetLowMemoryUsage(bool isLow)
        {
            try
            {
                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.MemoryUsageTargetLevel = isLow
                        ? CoreWebView2MemoryUsageTargetLevel.Low
                        : CoreWebView2MemoryUsageTargetLevel.Normal;
                }
            }
            catch { }
        }

        public void OpenDevTools()
        {
            try
            {
                _webView.CoreWebView2?.OpenDevToolsWindow();
            }
            catch { }
        }
    }
}
