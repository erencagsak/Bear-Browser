using System;
using Bear_Browser.Models;

namespace Bear_Browser.Messages
{
    public record TabCreatedMessage(BrowserTab Tab);
    public record TabClosedMessage(Guid TabId);
    public record TabSelectedMessage(BrowserTab Tab);
    public record NewTabRequestedMessage(string? Url = null, bool IsInPrivate = false);
    public record CloseCurrentTabRequestedMessage();
}
