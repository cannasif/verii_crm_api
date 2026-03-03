namespace crm_api.Infrastructure
{
    public sealed class GoogleOptions
    {
        public const string SectionName = "Google";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string Scopes { get; set; } = "https://www.googleapis.com/auth/calendar.events";
    }
}
