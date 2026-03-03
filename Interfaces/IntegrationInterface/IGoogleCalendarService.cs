namespace crm_api.Interfaces
{
    public interface IGoogleCalendarService
    {
        Task<string> CreateTestEventAsync(long userId, CancellationToken cancellationToken = default);
    }
}
