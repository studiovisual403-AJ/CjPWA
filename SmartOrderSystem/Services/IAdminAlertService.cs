namespace SmartOrderSystem.Services
{
    public interface IAdminAlertService
    {
        Task<AdminAlertSummaryDto> GetSummaryAsync();
    }
}
