using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Bear_Browser.Platform.Interfaces;
using Microsoft.Win32;

namespace Bear_Browser.Platform.WinUI
{
    /// <summary>
    /// Windows platformunda varsayılan tarayıcı kontrolü ve sistem ayarları yönlendirmesi.
    /// </summary>
    public class WindowsDefaultBrowserService : IDefaultBrowserService
    {
        /// <inheritdoc />
        public Task<bool> OpenDefaultBrowserSettingsAsync()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
                return Task.FromResult(true);
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("ms-settings:defaultapps-browser") { UseShellExecute = true });
                    return Task.FromResult(true);
                }
                catch
                {
                    return Task.FromResult(false);
                }
            }
        }

        /// <inheritdoc />
        public bool IsDefaultBrowser()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                if (key?.GetValue("ProgId") is string progId)
                {
                    return progId.Contains("BearBrowser", StringComparison.OrdinalIgnoreCase) ||
                           progId.Contains("Bear-Browser", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Registry okuma hatası durumunda varsayılan değil kabul edilir
            }
            return false;
        }
    }
}
