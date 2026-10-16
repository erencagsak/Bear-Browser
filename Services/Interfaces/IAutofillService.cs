using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IAutofillService
    {
        void InjectAutofillAccounts(BrowserTab tab, string? targetUrl = null, bool isInPrivate = false);
        bool IsPasswordAlreadySaved(string domain, string username, string password);
    }
}
