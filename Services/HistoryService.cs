using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bear_Browser.Data.Interfaces;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;

namespace Bear_Browser.Services
{
    public class HistoryService : IHistoryService
    {
        private readonly IJsonStorageService _storageService;
        private readonly string _historyFile;

        public List<HistoryItem> History { get; private set; } = new();
        public event EventHandler? HistoryUpdated;

        public HistoryService(IJsonStorageService storageService, string? baseFolder = null)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));

            string folder = baseFolder ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BearBrowser", "Profiles", "Default"
            );

            Directory.CreateDirectory(folder);
            _historyFile = Path.Combine(folder, "history.json");

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_historyFile))
                    History = _storageService.Load<List<HistoryItem>>(_historyFile) ?? new();
            }
            catch { }
        }

        public async Task AddHistoryAsync(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("bear://") || url.StartsWith("edge://") || url.StartsWith("about:")) return;

            History.Insert(0, new HistoryItem
            {
                Title = string.IsNullOrWhiteSpace(title) ? url : title,
                Url = url,
                VisitedAt = DateTime.Now
            });

            if (History.Count > 2000) History.RemoveAt(History.Count - 1);
            await _storageService.SaveAsync(_historyFile, History);
        }

        public async Task RemoveHistoryItemAsync(Guid id)
        {
            var item = History.Find(h => h.Id == id);
            if (item != null)
            {
                History.Remove(item);
                await _storageService.SaveAsync(_historyFile, History);
                HistoryUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ClearHistoryAsync()
        {
            History.Clear();
            await _storageService.SaveAsync(_historyFile, History);
            HistoryUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
