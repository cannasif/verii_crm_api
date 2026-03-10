namespace crm_api.DTOs
{
    public class OutlookEntegrationStatusDto
    {
        public bool IsConnected { get; set; }
        public bool IsOAuthConfigured { get; set; }
        public string? OutlookEmail { get; set; }
        public string? Scopes { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    public class OutlookEntegrationAuthorizeUrlDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class SendOutlookMailDto
    {
        public long? CustomerId { get; set; }
        public long? ContactId { get; set; }

        public string To { get; set; } = string.Empty;
        public string? Cc { get; set; }
        public string? Bcc { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;

        public string? TemplateKey { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateVersion { get; set; }
    }

    public class OutlookMailSendResultDto
    {
        public bool IsSuccess { get; set; }
        public string? MessageId { get; set; }
        public string? ConversationId { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public long? LogId { get; set; }
    }

    public class CreateOutlookCalendarEventDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public string StartDateTime { get; set; } = string.Empty;
        public string EndDateTime { get; set; } = string.Empty;
        public string TimeZone { get; set; } = "Turkey Standard Time";
        public string? LocationDisplayName { get; set; }
        public string? Attendees { get; set; }
        public long? ActivityId { get; set; }
    }

    public class UpdateOutlookCalendarEventDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public string StartDateTime { get; set; } = string.Empty;
        public string EndDateTime { get; set; } = string.Empty;
        public string TimeZone { get; set; } = "Turkey Standard Time";
        public string? LocationDisplayName { get; set; }
        public string? Attendees { get; set; }
    }

    public class OutlookCalendarEventResultDto
    {
        public bool IsSuccess { get; set; }
        public string? EventId { get; set; }
        public string? WebLink { get; set; }
    }

    public class OutlookEntegrationLogDto
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string? Severity { get; set; }
        public string Provider { get; set; } = "outlook";
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public string? ActivityId { get; set; }
        public string? ProviderEventId { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class OutlookEntegrationLogsQueryDto : PagedRequest
    {
        public bool ErrorsOnly { get; set; }
    }

    public class OutlookCustomerMailLogDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public long? ContactId { get; set; }
        public string? ContactName { get; set; }
        public long SentByUserId { get; set; }
        public string? SentByUserName { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? SenderEmail { get; set; }
        public string ToEmails { get; set; } = string.Empty;
        public string? CcEmails { get; set; }
        public string? BccEmails { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? BodyPreview { get; set; }
        public bool IsHtml { get; set; }
        public string? TemplateKey { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateVersion { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OutlookMessageId { get; set; }
        public string? OutlookConversationId { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class OutlookCustomerMailLogQueryDto : PagedRequest
    {
        public long? CustomerId { get; set; }
        public bool ErrorsOnly { get; set; }
    }
}
