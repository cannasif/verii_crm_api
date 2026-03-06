namespace crm_api.Models
{
    /// <summary>
    /// Hangfire job hatalarının SQL'e kaydedildiği tablo. Müşteri monitoring UI'da görebilir.
    /// </summary>
    public class JobFailureLog : BaseEntity
    {
        public string JobId { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public DateTime FailedAt { get; set; }
        public string? Reason { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
        public string? Queue { get; set; }
        public int RetryCount { get; set; }
    }
}
