using System;
using Microsoft.UI.Xaml;

namespace Bear_Browser.Services.Interfaces
{
    public interface ICrashReportingService
    {
        string LogsDirectory { get; }

        void Initialize(Application? app = null);
        string LogCrash(Exception ex, string source, bool isFatal = false);
        void LogCustomError(string title, string details);
        void PromptUserAboutCrash(Exception ex, string logFilePath);
        void OpenLogsFolder();
        void TriggerTestCrash();
    }
}
