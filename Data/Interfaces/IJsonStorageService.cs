using System.Threading.Tasks;

namespace Bear_Browser.Data.Interfaces
{
    /// <summary>
    /// JSON formatında veri serileştirme ve kalıcı dosya depolama işlemlerini yürüten servis sözleşmesi.
    /// </summary>
    public interface IJsonStorageService
    {
        /// <summary>
        /// Belirtilen dosya yolundan JSON verisini okuyarak nesneye dönüştürür.
        /// </summary>
        Task<T?> LoadAsync<T>(string filePath);

        /// <summary>
        /// Belirtilen nesneyi JSON olarak belirtilen dosya yoluna yazar.
        /// </summary>
        Task SaveAsync<T>(string filePath, T data);

        /// <summary>
        /// Belirtilen dosya yolundan senkron olarak JSON verisini okur.
        /// </summary>
        T? Load<T>(string filePath);

        /// <summary>
        /// Belirtilen nesneyi senkron olarak JSON dosyasına yazar.
        /// </summary>
        void Save<T>(string filePath, T data);
    }
}
