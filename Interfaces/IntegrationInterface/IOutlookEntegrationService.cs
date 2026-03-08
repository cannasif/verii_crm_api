using crm_api.DTOs;

namespace crm_api.Interfaces
{
    public interface IOutlookEntegrationService
    {
        Task<ApiResponse<OutlookEntegrationAuthorizeUrlDto>> CreateConnectUrlAsync(long userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> HandleOAuthCallbackAsync(long userId, string code, string state, CancellationToken cancellationToken = default);
        Task<ApiResponse<OutlookEntegrationStatusDto>> GetStatusAsync(long userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> DisconnectAsync(long userId, CancellationToken cancellationToken = default);

        Task<ApiResponse<OutlookMailSendResultDto>> SendMailAsync(long userId, SendOutlookMailDto dto, CancellationToken cancellationToken = default);

        Task<ApiResponse<OutlookCalendarEventResultDto>> CreateCalendarEventAsync(long userId, CreateOutlookCalendarEventDto dto, CancellationToken cancellationToken = default);
        Task<ApiResponse<OutlookCalendarEventResultDto>> UpdateCalendarEventAsync(long userId, string eventId, UpdateOutlookCalendarEventDto dto, CancellationToken cancellationToken = default);
        Task<ApiResponse<bool>> DeleteCalendarEventAsync(long userId, string eventId, CancellationToken cancellationToken = default);

        Task<ApiResponse<PagedResponse<OutlookEntegrationLogDto>>> GetLogsAsync(long userId, OutlookEntegrationLogsQueryDto query, CancellationToken cancellationToken = default);
    }
}
