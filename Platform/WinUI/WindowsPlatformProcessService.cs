using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Bear_Browser.Platform.Interfaces;

namespace Bear_Browser.Platform.WinUI
{
    public class WindowsPlatformProcessService : IPlatformProcessService
    {
        private const uint MB_ICONERROR = 0x00000010;
        private const uint MB_OK = 0x00000000;

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public bool EmptyWorkingSet()
        {
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                return EmptyWorkingSet(currentProcess.Handle);
            }
            catch
            {
                return false;
            }
        }

        public void ShowNativeMessageBox(IntPtr windowHandle, string text, string caption, bool isError)
        {
            try
            {
                uint type = isError ? MB_ICONERROR : MB_OK;
                MessageBox(windowHandle, text, caption, type);
            }
            catch
            {
            }
        }
    }
}
