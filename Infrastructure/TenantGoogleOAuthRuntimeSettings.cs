namespace crm_api.Infrastructure
{
    public sealed class TenantGoogleOAuthRuntimeSettings
    {
        public Guid TenantId { get; init; }
        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string RedirectUri { get; init; } = string.Empty;
        public string Scopes { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
    }
}
