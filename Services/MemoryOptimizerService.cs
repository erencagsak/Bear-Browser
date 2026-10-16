using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;

namespace Bear_Browser.Services
{
    public class MemoryOptimizerService : Bear_Browser.Services.Interfaces.IMemoryOptimizerService
    {
        private static MemoryOptimizerService? _instance;
        public static MemoryOptimizerService Instance
        {
            get
            {
                if (App.Services?.GetService(typeof(Bear_Browser.Services.Interfaces.IMemoryOptimizerService)) is MemoryOptimizerService s)
                    return s;
                return _instance ??= new MemoryOptimizerService();
            }
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        private DispatcherQueueTimer? _trimTimer;
        private bool _isStarted = false;

        public void Start(DispatcherQueue dispatcherQueue)
        {
            if (_isStarted) return;
            _isStarted = true;

            // Her 40 saniyede bir arka planda RAM optimizasyonu yapar
            _trimTimer = dispatcherQueue.CreateTimer();
            _trimTimer.Interval = TimeSpan.FromSeconds(40);
            _trimTimer.Tick += (s, e) =>
            {
                OptimizeMemoryAsync();
            };
            _trimTimer.Start();
        }

        public void OptimizeMemoryAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    // 1. .NET Garbage Collector temizliği
                    GC.Collect(2, GCCollectionMode.Forced, false, true);
                    GC.WaitForPendingFinalizers();

                    // 2. Bear-Browser ana işleminin bellek temizliği
                    using (var currentProcess = Process.GetCurrentProcess())
                    {
                        try { EmptyWorkingSet(currentProcess.Handle); } catch { }
                    }

                    // 3. msedgewebview2 alt işlemlerinin (GPU, Renderer vb.) fiziksel bellek temizliği
                    var processes = Process.GetProcessesByName("msedgewebview2");
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (!proc.HasExited)
                            {
                                EmptyWorkingSet(proc.Handle);
                            }
                        }
                        catch { }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch { }
            });
        }
    }
}
