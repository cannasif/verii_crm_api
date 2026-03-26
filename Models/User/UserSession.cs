namespace crm_api.Models
{
    public class UserSession : BaseEntity
    {
        public long UserId { get; set; }
        public User? User { get; set; }

        public Guid SessionId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTimeProvider.UtcNow;

        public DateTime? RevokedAt { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public string? DeviceInfo { get; set; }

        public bool IsActive => RevokedAt == null && CreatedAt.AddDays(30) > DateTimeProvider.UtcNow;
    }
}
