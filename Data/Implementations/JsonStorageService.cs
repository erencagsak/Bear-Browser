using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bear_Browser.Data.Interfaces;

namespace Bear_Browser.Data.Implementations
{
    /// <summary>
    /// ProfileService ve SettingsService ile birebir uyumlu, girintili (WriteIndented)
    /// JSON depolama ve serileştirme servisi.
    /// </summary>
    public class JsonStorageService : IJsonStorageService
    {
        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            WriteIndented = true
        };

        /// <inheritdoc />
        public async Task<T?> LoadAsync<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return default;

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch
            {
                return default;
            }
        }

        /// <inheritdoc />
        public async Task SaveAsync<T>(string filePath, T data)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(data, DefaultOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch
            {
                // Sessizce yutulur veya üst katmanlarda ele alınır
            }
        }

        /// <inheritdoc />
        public T? Load<T>(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return default;

            try
            {
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json, DefaultOptions);
            }
            catch
            {
                return default;
            }
        }

        /// <inheritdoc />
        public void Save<T>(string filePath, T data)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(data, DefaultOptions);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Sessizce yutulur
            }
        }
    }
}
