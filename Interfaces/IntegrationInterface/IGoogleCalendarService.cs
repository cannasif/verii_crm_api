using crm_api.Models;

namespace crm_api.Interfaces
{
    public interface IGoogleCalendarService
    {
        Task<string> CreateTestEventAsync(long userId, CancellationToken cancellationToken = default);
        Task<string> CreateActivityEventAsync(long userId, Activity activity, CancellationToken cancellationToken = default);
    }
}
