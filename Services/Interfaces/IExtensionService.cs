using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Bear_Browser.Models;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services.Interfaces
{
    public interface IExtensionService
    {
        string ExtensionsDirectory { get; }
        ObservableCollection<ExtensionInfo> InstalledExtensions { get; }

        event EventHandler<ExtensionInfo>? ExtensionInstalled;
        event EventHandler<string>? ExtensionRemoved;

        void LoadLocalExtensionsFromDisk();
        bool IsStoreExtensionUrl(string url, out string storeType, out string extensionId);
        Task<ExtensionInfo?> InstallFromCrxFileAsync(string crxFilePath, string extensionId, string storeType, string? fallbackName, CoreWebView2Profile? profile);
        Task<ExtensionInfo?> DownloadAndInstallAsync(string storeType, string extensionId, string? fallbackName, CoreWebView2Profile? profile);
        Task SyncInstalledExtensionsAsync(CoreWebView2Profile? profile);
        Task SetExtensionEnabledAsync(string extensionId, bool enable, CoreWebView2Profile? profile);
        Task RemoveExtensionAsync(string extensionId, CoreWebView2Profile? profile);
        Task<ExtensionInfo?> InstallFromFolderAsync(string folderPath, CoreWebView2Profile? profile);
    }
}
