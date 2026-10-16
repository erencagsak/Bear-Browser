using System;

namespace Bear_Browser.Platform.Interfaces
{
    /// <summary>
    /// Süreç yönetimi ve yerel işletim sistemi bildirim/bellek API'leri için sözleşme.
    /// </summary>
    public interface IPlatformProcessService
    {
        /// <summary>
        /// Mevcut sürecin çalışma kümesindeki (working set) kullanılmayan sayfaları RAM'den boşaltır.
        /// </summary>
        bool EmptyWorkingSet();

        /// <summary>
        /// Yerel işletim sistemi mesaj kutusu (MessageBox) gösterir.
        /// </summary>
        void ShowNativeMessageBox(IntPtr windowHandle, string text, string caption, bool isError);
    }
}
