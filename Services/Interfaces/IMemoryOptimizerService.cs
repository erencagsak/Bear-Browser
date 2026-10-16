using Microsoft.UI.Dispatching;

namespace Bear_Browser.Services.Interfaces
{
    public interface IMemoryOptimizerService
    {
        void Start(DispatcherQueue dispatcherQueue);
        void OptimizeMemoryAsync();
    }
}
