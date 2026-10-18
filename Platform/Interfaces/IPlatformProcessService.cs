using System;

namespace Bear_Browser.Platform.Interfaces
{
    public interface IPlatformProcessService
    {
        bool EmptyWorkingSet();

        void ShowNativeMessageBox(IntPtr windowHandle, string text, string caption, bool isError);
    }
}
