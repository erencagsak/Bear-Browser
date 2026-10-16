using System.Threading.Tasks;

namespace Bear_Browser.Platform.Interfaces
{
    /// <summary>
    /// Varsayılan tarayıcı ayarları ve kontrolleri için platform bağımsız sözleşme.
    /// </summary>
    public interface IDefaultBrowserService
    {
        /// <summary>
        /// İşletim sisteminin varsayılan uygulamalar / tarayıcı ayarları ekranını açar.
        /// </summary>
        Task<bool> OpenDefaultBrowserSettingsAsync();

        /// <summary>
        /// Mevcut uygulamanın varsayılan tarayıcı olup olmadığını denetler.
        /// </summary>
        bool IsDefaultBrowser();
    }
}
