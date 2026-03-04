namespace crm_api.Models
{
    public class GoogleIntegrationLog : BaseEntity
    {
        public Guid TenantId { get; set; }
        public long? UserId { get; set; }
        public User? User { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Severity { get; set; } = "Info";
        public string Provider { get; set; } = "Google";
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public long? ActivityId { get; set; }
        public string? GoogleCalendarEventId { get; set; }
        public string? MetadataJson { get; set; }
    }
}
