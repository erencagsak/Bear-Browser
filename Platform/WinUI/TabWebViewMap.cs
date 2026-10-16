using System;
using System.Collections.Concurrent;
using Bear_Browser.Models;
using Bear_Browser.Platform.Interfaces;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Platform.WinUI
{
    public static class TabWebViewMap
    {
        private static readonly ConcurrentDictionary<Guid, WebView2> _map = new();
        private static readonly ConcurrentDictionary<Guid, IWebViewAdapter> _adapterMap = new();

        public static WebView2 GetOrCreate(Guid tabId)
        {
            return _map.GetOrAdd(tabId, _ => new WebView2());
        }

        public static WebView2? Get(Guid tabId)
        {
            _map.TryGetValue(tabId, out var wv);
            return wv;
        }

        public static void Set(Guid tabId, WebView2 webView)
        {
            _map[tabId] = webView;
            _adapterMap[tabId] = new WebView2Adapter(webView);
        }

        public static bool Remove(Guid tabId, out WebView2? webView)
        {
            _adapterMap.TryRemove(tabId, out _);
            return _map.TryRemove(tabId, out webView);
        }

        public static void Clear()
        {
            _adapterMap.Clear();
            _map.Clear();
        }

        public static IWebViewAdapter GetOrCreateAdapter(Guid tabId)
        {
            return _adapterMap.GetOrAdd(tabId, id => new WebView2Adapter(GetOrCreate(id)));
        }

        // Extension methods for BrowserTab
        public static WebView2 GetWebView(this BrowserTab tab)
        {
            return GetOrCreate(tab.Id);
        }

        public static IWebViewAdapter GetAdapter(this BrowserTab tab)
        {
            return GetOrCreateAdapter(tab.Id);
        }

        public static void SetWebView(this BrowserTab tab, WebView2 webView)
        {
            Set(tab.Id, webView);
        }

        public static bool RemoveWebView(this BrowserTab tab, out WebView2? webView)
        {
            return Remove(tab.Id, out webView);
        }
    }
}
