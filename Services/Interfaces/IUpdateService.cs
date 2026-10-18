using System;
using System.Threading;
using System.Threading.Tasks;
using Bear_Browser.Models;

namespace Bear_Browser.Services.Interfaces
{
    public interface IUpdateService
    {
        string CurrentVersion { get; }

        string GitHubRepo { get; set; }

        Task<UpdateInfo> CheckForUpdatesAsync(bool silent = false);

        Task<string?> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        void LaunchInstallerAndExit(string installerPath);

        UpdateInfo GenerateSimulatedUpdate();
    }
}
