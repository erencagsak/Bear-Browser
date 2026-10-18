using System.Threading.Tasks;

namespace Bear_Browser.Platform.Interfaces
{
    public interface IDefaultBrowserService
    {
        Task<bool> OpenDefaultBrowserSettingsAsync();

        bool IsDefaultBrowser();
    }
}
