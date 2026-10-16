using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IDownloadService
    {
        List<DownloadItem> Downloads { get; }
        event EventHandler? DownloadsUpdated;

        void AddActiveDownload(DownloadItem item);
        Task CompleteDownloadAsync(DownloadItem item);
        Task AddDownloadAsync(DownloadItem item);
        Task RemoveDownloadAsync(Guid id);
        Task ClearDownloadsAsync();
    }
}
