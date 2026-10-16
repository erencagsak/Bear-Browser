using System;
using System.IO;
using Bear_Browser.Models;
using Bear_Browser.Platform.WinUI;
using Bear_Browser.Services.Interfaces;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class DownloadHandlerService : IDownloadHandlerService
    {
        private readonly IDownloadService _downloadService;
        private readonly ICrashReportingService _crashReportingService;

        public DownloadHandlerService(
            IDownloadService downloadService,
            ICrashReportingService crashReportingService)
        {
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
            _crashReportingService = crashReportingService ?? throw new ArgumentNullException(nameof(crashReportingService));
        }

        public void HandleDownload(
            CoreWebView2DownloadStartingEventArgs args,
            BrowserTab tab,
            Action<bool, bool> onDownloadStateChanged,
            Action onPopupCloseTab)
        {
            var op = args.DownloadOperation;
            args.Handled = true;

            string resultPath = args.ResultFilePath ?? "";
            string fileName = Path.GetFileName(resultPath) ?? "download";
            var downloadItem = new DownloadItem
            {
                FileName = fileName,
                FilePath = resultPath,
                Url = op.Uri ?? "",
                TotalBytes = Math.Max(0, op.TotalBytesToReceive),
                ReceivedBytes = 0,
                State = "İndiriliyor"
            };

            tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
            {
                _downloadService.AddActiveDownload(downloadItem);
                onDownloadStateChanged(true, false);
            });

            op.BytesReceivedChanged += (s, e) =>
            {
                tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                {
                    downloadItem.ReceivedBytes = op.BytesReceived;
                    if (op.TotalBytesToReceive > 0)
                    {
                        downloadItem.TotalBytes = op.TotalBytesToReceive;
                    }
                });
            };

            op.StateChanged += async (s, e) =>
            {
                if (op.State == CoreWebView2DownloadState.Completed)
                {
                    await _downloadService.CompleteDownloadAsync(downloadItem);
                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                    {
                        onDownloadStateChanged(false, true);
                        if (tab.IsPopupTab)
                        {
                            onPopupCloseTab();
                        }
                    });
                }
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                {
                    downloadItem.State = "İptal edildi";
                    _crashReportingService.LogCustomError("İndirme Kesintiye Uğradı", $"Dosya: {downloadItem.FileName}\nNeden: {op.InterruptReason}\nURL: {op.Uri}");
                    tab.GetWebView().DispatcherQueue?.TryEnqueue(() =>
                    {
                        onDownloadStateChanged(false, false);
                        if (tab.IsPopupTab)
                        {
                            onPopupCloseTab();
                        }
                    });
                }
            };
        }
    }
}
