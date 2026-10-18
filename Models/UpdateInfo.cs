using System;

namespace Bear_Browser.Models
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }

        public string CurrentVersion { get; set; } = "1.0.0";

        public string LatestVersion { get; set; } = "";

        public string ReleaseTitle { get; set; } = "";

        public string ReleaseNotes { get; set; } = "";

        public string ReleaseHtmlUrl { get; set; } = "";

        public string? InstallerDownloadUrl { get; set; }

        public long InstallerSize { get; set; }

        public DateTimeOffset? PublishedAt { get; set; }

        public string ErrorMessage { get; set; } = "";

        public bool IsTestSimulation { get; set; }
    }
}
