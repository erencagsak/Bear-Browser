using System;

namespace Bear_Browser.Platform.Interfaces
{
    /// <summary>
    /// Pencere yönetimi ve yerel işletim sistemi pencere API'leri için sözleşme.
    /// </summary>
    public interface IPlatformWindowService
    {
        /// <summary>
        /// İşletim sisteminde çalışan uygulama kimliğini (AppUserModelID) ayarlar.
        /// </summary>
        void SetAppUserModelId(string appId);

        /// <summary>
        /// Yerel pencere handle'ı için başlık çubuğu ve görev çubuğu ikonlarını ayarlar.
        /// </summary>
        void SetWindowIcon(IntPtr windowHandle, string iconPath);
    }
}
