using System;
using System.Linq;
using System.Text.Json;
using Bear_Browser.Helpers;
using Bear_Browser.Models;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services.Interfaces;

namespace Bear_Browser.Services
{
    public class AutofillService : IAutofillService
    {
        private readonly IPasswordService _passwordService;

        public AutofillService(IPasswordService passwordService)
        {
            _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        }

        public void InjectAutofillAccounts(BrowserTab tab, string? targetUrl = null, bool isInPrivate = false)
        {
            if (tab.GetWebView().CoreWebView2 == null || isInPrivate)
                return;

            try
            {
                string currentUrl = targetUrl ?? tab.GetWebView().CoreWebView2.Source;
                if (string.IsNullOrWhiteSpace(currentUrl)) currentUrl = tab.Url;
                if (string.IsNullOrWhiteSpace(currentUrl) || currentUrl.StartsWith("bear://"))
                    return;

                string host = "";
                try { host = new Uri(currentUrl).Host; } catch { host = currentUrl; }

                var matchingPasswords = _passwordService.Passwords
                    .Where(p => !string.IsNullOrEmpty(p.Domain) && UrlHelper.IsDomainMatch(host, p.Domain))
                    .GroupBy(p => new { User = p.UserName ?? "", Pass = p.Password ?? "" })
                    .Select(g => new { user = g.Key.User, pass = g.Key.Pass })
                    .ToList();

                string json = JsonSerializer.Serialize(matchingPasswords);
                _ = tab.GetWebView().CoreWebView2.ExecuteScriptAsync($"window.__bearSetAutofillAccounts && window.__bearSetAutofillAccounts({json});");
            }
            catch { }
        }

        public bool IsPasswordAlreadySaved(string domain, string username, string password)
        {
            return _passwordService.Passwords.Any(p =>
                !string.IsNullOrEmpty(p.Domain) &&
                UrlHelper.IsDomainMatch(domain, p.Domain) &&
                p.UserName.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                p.Password == password);
        }
    }
}
