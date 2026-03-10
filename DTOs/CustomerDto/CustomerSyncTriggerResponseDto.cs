namespace crm_api.DTOs
{
    public class CustomerSyncTriggerResponseDto
    {
        public string JobId { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
        public DateTime EnqueuedAtUtc { get; set; }
    }
}
