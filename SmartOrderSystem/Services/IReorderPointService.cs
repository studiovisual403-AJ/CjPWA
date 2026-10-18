using SmartOrderSystem.ViewModels;

namespace SmartOrderSystem.Services
{
    public interface IReorderPointService
    {
        Task<List<RopRowViewModel>> GetRowsAsync();
    }
}
