using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IHistoryService
    {
        List<HistoryItem> History { get; }
        event EventHandler? HistoryUpdated;

        Task AddHistoryAsync(string title, string url);
        Task RemoveHistoryItemAsync(Guid id);
        Task ClearHistoryAsync();
    }
}
