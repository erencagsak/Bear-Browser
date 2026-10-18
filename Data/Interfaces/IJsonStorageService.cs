using System.Threading.Tasks;

namespace Bear_Browser.Data.Interfaces
{
    public interface IJsonStorageService
    {
        Task<T?> LoadAsync<T>(string filePath);

        Task SaveAsync<T>(string filePath, T data);

        T? Load<T>(string filePath);

        void Save<T>(string filePath, T data);
    }
}
