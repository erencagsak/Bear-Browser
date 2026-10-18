using System;
using System.IO;
using System.Runtime.InteropServices;
using Bear_Browser.Platform.Interfaces;

namespace Bear_Browser.Platform.WinUI
{
    public class WindowsPlatformWindowService : IPlatformWindowService
    {
        private const uint WM_SETICON = 0x0080;
        private const IntPtr ICON_SMALL = 0;
        private const IntPtr ICON_BIG = (IntPtr)1;
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x0010;

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        public void SetAppUserModelId(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId)) return;
            try
            {
                SetCurrentProcessExplicitAppUserModelID(appId);
            }
            catch
            {
            }
        }

        public void SetWindowIcon(IntPtr windowHandle, string iconPath)
        {
            if (windowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
                return;

            try
            {
                IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

                if (hIconBig != IntPtr.Zero)
                    SendMessage(windowHandle, WM_SETICON, ICON_BIG, hIconBig);

                if (hIconSmall != IntPtr.Zero)
                    SendMessage(windowHandle, WM_SETICON, ICON_SMALL, hIconSmall);
            }
            catch
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        public bool TryGetCursorPos(out int x, out int y)
        {
            if (GetCursorPos(out POINT pt))
            {
                x = pt.X;
                y = pt.Y;
                return true;
            }

            x = 0;
            y = 0;
            return false;
        }
    }
}
