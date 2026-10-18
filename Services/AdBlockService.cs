using System;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class AdBlockService : Bear_Browser.Services.Interfaces.IAdBlockService
    {
        public bool IsEnabled { get; set; } = true;

        private static readonly string[] AdDomains = new[]
        {
            "doubleclick.", "googlesyndication.", "googleadservices.",
            "google-analytics.", "googletagmanager.", "googletagservices.",
            "adservice.google.", "pagead2.", "adsbygoogle", "criteo.", "taboola.",
            "outbrain.", "adnxs.", "rubiconproject.", "pubmatic.", "openx.",
            "amazon-adsystem.", "adroll.", "popads.", "smartadserver.",
            "indexexchange.", "moatads.", "scorecardresearch.", "hotjar.",
            "clarity.ms", "mouseflow.", "pixel.facebook.", "an.facebook.",
            "connect.facebook.net/en_US/fbevents.js", "ads.twitter.", "ads.linkedin.",
            "ads.tiktok.", "trafficjunky.", "exoclick.", "juicyads.",
            "propellerads.", "media.net", "mgid.", "adfox.yandex", "metrika.yandex"
        };

        private static bool ShouldBlock(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return false;

            if (uri.Contains("accounts.google.", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("mail.google.", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("account.proton.", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("mail.proton.", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("googlevideo.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("youtube.com/youtubei/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 0; i < AdDomains.Length; i++)
            {
                if (uri.Contains(AdDomains[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public async void AttachToWebView(WebView2 webView, Action? onAdBlocked = null)
        {
            if (webView.CoreWebView2 == null) return;

            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (() => {
                    const adRegex = /(doubleclick|googlesyndication|googleadservices|google-analytics|googleanalytics|googletagmanager|googletagservices|adservice\.google|pagead|gampad|adsbygoogle|criteo|taboola|outbrain|adnxs|rubiconproject|pubmatic|openx|amazon-adsystem|fls-na\.amazon|advice-ads|adtago|analyticsengine|advertising-api|adroll|popads|smartadserver|casalemedia|indexexchange|moatads|scorecardresearch|quantserve|quantcast|hotjar|clarity\.ms|mouseflow|freshmarketer|luckyorange|stats\.wp|pixel\.wp|heapanalytics|mixpanel|amplitude|segment\.(io|com)|fullstory|cloudflareinsights|posthog|rudderstack|rudderlabs|snowplow|sentry|bugsnag|newrelic|nr-data|datadog|logrocket|fingerprintjs|fpjs|siftscience|permutive|bluekai|onetag|pippio|id5-sync|mathtag|crwdcntrl|everesttech|uidapi|rlcdn|appsflyer|adjust\.com|branch\.io|bnc\.lt|kochava|singular\.net|wzrkt|coinimp|webminepool|minero|mineralt|monerominer|popcash|popmyads|clickadu|trafficjunky|exoclick|juicyads|statdynamic|pixel\.facebook|connect\.facebook|an\.facebook|ads-twitter|analytics\.twitter|ads\.x\.com|analytics\.x\.com|ads\.linkedin|licdn|events\.reddit|pixel\.reddit|analytics\.tiktok|ads\.tiktok|ads-api\.tiktok|byteoversea|tiktokv|ads\.pinterest|ct\.pinterest|trk\.pinterest|pixel\.quora|px\.srvcs\.tumblr|ads\.vk|vk\.com\/rtrg|top-fwz1\.mail\.ru|ad\.mail\.ru|metrika\.yandex|adfox\.yandex|appmetrica\.yandex|advertising\.apple|iadsdk|metrics\.apple|metrics\.icloud|mzstatic|realme\.com|oppomobile\.com|oneplus\.(cn|net)|hicloud\.com|mistat\.xiaomi|ad\.xiaomi|tracking\.miui|samsungads|nmetrics\.samsung|smetrics\.samsung|adlog\.vivo|ads-api\.vivo|a\.lenovo|lgsmartad|lgappstv|smartclip|yumenetworks|vortex\.data\.microsoft|telemetry\.microsoft|browser\.events\.data\.msn|device-metrics.*amazon|mads-eu\.amazon|roku\.com|vizio\.com|tvinteractive|tvpixel|cookielaw\.org|onetrust|cookiebot|trustarc|privacy-center|privacy-mgmt|usercentrics|consensu\.org|osano|fundingchoices|anrdoezrs|dpbolvw|tkqlhce|shareasale|linksynergy|impactradius|awin1|zenaps|prf\.hn|partnerstack|refersion|pepperjam|skimresources|redirectingat|viglink|optimizely|dynamicyield|launchdarkly|list-manage|hubspot\.com|marketo\.net|mailchimp|intercom\.io|driftt|braze\.com|onesignal|klaviyo|customer\.io|dai\.google|jwpsrv|jwpcdn|jwpltx|fwmrm\.net|connatix|brightcove|innovid|tremorhub|adcolony|media\.net|mgid|propellerads|propellerclick|onclickads|applovin|liftoff\.io|vungle|spotxchange|htlbid|analytics\.yahoo|partnerads\.ysm|gemini\.yahoo|unityads|adtech\.yahoo|inmobi|ironsource|chartboost|supersonicads|adsrvr\.org|smartyads|contextweb|sharethrough|stackadapt|stickyadstv|3lift\.com|sonobi|gumgum|teads\.tv|kargo\.com|pangleglobal|doubleverify|adsafeprotected|insightexpressai|omtrdc\.net|adobe\.io|\/ads|\/adserver|\/pagead|\/gampad|\/vast|\/vpaid|\/prebid|\/bidder|\/ads\.js|\/advertisement\.js|\/adsbygoogle\.js|\/tracking\/pixel)/i;

                    function isAd(url) {
                        if (!url) return false;
                        if (url.includes('googlevideo.com') ||
                            url.includes('youtube.com/youtubei/') ||
                            url.includes('youtube.com/watch') ||
                            url.includes('youtube.com/results') ||
                            url.includes('accounts.google.com')) {
                            return false;
                        }
                        return adRegex.test(url);
                    }

                    const origFetch = window.fetch;
                    window.fetch = async function(...args) {
                        const url = args[0] ? (typeof args[0] === 'string' ? args[0] : args[0].url) : '';
                        if (isAd(url)) {
                            try { window.chrome.webview.postMessage('adBlocked'); } catch {}
                            return new Response('', { status: 204, statusText: 'No Content' });
                        }
                        return origFetch.apply(this, args);
                    };

                    const origOpen = XMLHttpRequest.prototype.open;
                    XMLHttpRequest.prototype.open = function(method, url, ...rest) {
                        if (isAd(url)) {
                            this._isBlocked = true;
                        }
                        return origOpen.call(this, method, url, ...rest);
                    };
                    const origSend = XMLHttpRequest.prototype.send;
                    XMLHttpRequest.prototype.send = function(...args) {
                        if (this._isBlocked) {
                            try { window.chrome.webview.postMessage('adBlocked'); } catch {}
                            this.dispatchEvent(new ProgressEvent('error'));
                            return;
                        }
                        return origSend.apply(this, args);
                    };

                    const style = document.createElement('style');
                    style.textContent = `
                        .adsbox, .ad-banner, .adsbygoogle, .ad-slot, .ad_unit,
                        [id^='google_ads_'], [class*='advertisement'], [class*='sponsored'],
                        [class*='ad-container'] {
                            display: none !important;
                            visibility: hidden !important;
                        }
                    `;
                    document.documentElement.appendChild(style);
                })();
            ");

            webView.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                if (args.TryGetWebMessageAsString() == "adBlocked")
                {
                    onAdBlocked?.Invoke();
                }
            };

            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Script);
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Image);
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.XmlHttpRequest);
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Fetch);
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.Other);
            webView.CoreWebView2.WebResourceRequested += (sender, args) =>
            {
                if (!IsEnabled) return;

                if (ShouldBlock(args.Request.Uri))
                {
                    var emptyStream = new InMemoryRandomAccessStream();
                    args.Response = sender.Environment.CreateWebResourceResponse(
                        emptyStream, 403, "Blocked by Bear AdBlock", "Content-Type: text/plain"
                    );
                    onAdBlocked?.Invoke();
                }
            };
        }
    }
}