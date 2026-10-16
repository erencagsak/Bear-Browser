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
        private DispatcherQueueTimer? _trimTimer;
        private bool _isStarted = false;

        public void Start(DispatcherQueue dispatcherQueue)
        {
            if (_isStarted) return;
            _isStarted = true;

            // Her 2 dakikada bir arka planda nazik RAM toparlaması yapar (GPU veya akıcılığı dondurmaz)
            _trimTimer = dispatcherQueue.CreateTimer();
            _trimTimer.Interval = TimeSpan.FromMinutes(2);
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
                    // .NET Managed GC toparlaması (non-blocking, arayüz veya frame çizimini kilitlemez)
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                }
                catch { }
            });
        }
    }
}
