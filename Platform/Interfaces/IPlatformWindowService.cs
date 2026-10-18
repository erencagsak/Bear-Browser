using System;

namespace Bear_Browser.Platform.Interfaces
{
    public interface IPlatformWindowService
    {
        void SetAppUserModelId(string appId);

        void SetWindowIcon(IntPtr windowHandle, string iconPath);

        bool TryGetCursorPos(out int x, out int y);
    }
}
