using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;

namespace Bear_Browser.Services
{
    public class DownloadService : IDownloadService
    {
        private readonly IJsonStorageService _storageService;
        private readonly string _downloadsFile;

        public List<DownloadItem> Downloads { get; private set; } = new();
        public event EventHandler? DownloadsUpdated;

        public DownloadService(IJsonStorageService storageService, string? baseFolder = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            string folder = baseFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(folder);
            _downloadsFile = Path.Combine(folder, "downloads.json");

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_downloadsFile))
                    Downloads = _storageService.Load<List<DownloadItem>>(_downloadsFile) ?? new();
            }
            catch { }
        }

        public void AddActiveDownload(DownloadItem item)
        {
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task CompleteDownloadAsync(DownloadItem item)
        {
            item.State = "Tamamlandı";
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            await _storageService.SaveAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task AddDownloadAsync(DownloadItem item)
        {
            if (!Downloads.Contains(item))
            {
                Downloads.Insert(0, item);
            }
            await _storageService.SaveAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async Task RemoveDownloadAsync(Guid id)
        {
            var item = Downloads.Find(d => d.Id == id);
            if (item != null)
            {
                Downloads.Remove(item);
                await _storageService.SaveAsync(_downloadsFile, Downloads.Where(d => d.State == "Tamamlandı").ToList());
                DownloadsUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ClearDownloadsAsync()
        {
            Downloads.Clear();
            await _storageService.SaveAsync(_downloadsFile, Downloads);
            DownloadsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
