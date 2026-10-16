using System;
using Bear_Browser.Models;

namespace Bear_Browser.Messages
{
    public record TabNavigatedMessage(BrowserTab Tab);
    public record TabLoadingChangedMessage(BrowserTab Tab, bool IsLoading);
    public record PageLoadCompletedMessage(long ElapsedMilliseconds);
    public record AdBlockedCountChangedMessage(int BlockedCount);
    public record ShortcutActionTriggeredMessage(string Action);
    public record DownloadStateChangedMessage(bool IsDownloading, bool IsCompleted);
    public record PasswordSavePromptRequestedMessage(string Url, string Username, string Password);
    public record ExtensionInstallPromptRequestedMessage(string StoreType, string ExtensionId, string Name);
    public record ExtensionDownloadProgressStartedMessage(string StoreType, string ExtensionId, string Name);
    public record ExtensionInstalledSuccessfullyMessage(string ExtensionName);
    public record ExtensionInstallFailedMessage(string ErrorMessage);
}
