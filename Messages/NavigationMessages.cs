using System;

namespace Bear_Browser.Messages
{
    public record NavigateRequestedMessage(string Url, Guid? TabId = null);
    public record GoBackRequestedMessage(Guid? TabId = null);
    public record GoForwardRequestedMessage(Guid? TabId = null);
    public record RefreshRequestedMessage(Guid? TabId = null);
    public record StopRequestedMessage(Guid? TabId = null);
    public record SearchRequestedMessage(string Query);
}
