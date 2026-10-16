namespace Bear_Browser.Messages
{
    public record ShowNotificationMessage(string Title, string Message, string Severity = "Informational");
}
