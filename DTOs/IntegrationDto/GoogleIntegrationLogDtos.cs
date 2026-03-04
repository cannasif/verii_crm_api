using crm_api.Infrastructure.ModelBinding;
using Microsoft.AspNetCore.Mvc;

namespace crm_api.DTOs
{
    public class GoogleIntegrationLogDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? UserId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public long? ActivityId { get; set; }
        public string? GoogleCalendarEventId { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    [ModelBinder(BinderType = typeof(PagedRequestModelBinder))]
    public class GoogleIntegrationLogsQueryDto : PagedRequest
    {
        public bool ErrorsOnly { get; set; }
    }

    public class GoogleIntegrationLogWriteDto
    {
        public Guid? TenantId { get; set; }
        public long? UserId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Severity { get; set; } = "Info";
        public string Provider { get; set; } = "Google";
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public long? ActivityId { get; set; }
        public string? GoogleCalendarEventId { get; set; }
        public object? Metadata { get; set; }
    }
}
