using System;

namespace Bear_Browser.Messages
{
    public record ToggleFullScreenMessage();
    public record ExitFullScreenMessage();
    public record OpenDevToolsMessage(Guid? TabId = null);
    public record FindInPageMessage();
}
