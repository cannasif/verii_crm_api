using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using crm_api.Interfaces;
using crm_api.Models;

namespace crm_api.Services
{
    public class GoogleCalendarService : IGoogleCalendarService
    {
        private const string CalendarEventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

        private readonly IGoogleTokenService _googleTokenService;
        private readonly ITenantGoogleOAuthSettingsService _tenantGoogleOAuthSettingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(
            IGoogleTokenService googleTokenService,
            ITenantGoogleOAuthSettingsService tenantGoogleOAuthSettingsService,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleCalendarService> logger)
        {
            _googleTokenService = googleTokenService;
            _tenantGoogleOAuthSettingsService = tenantGoogleOAuthSettingsService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> CreateTestEventAsync(long userId, CancellationToken cancellationToken = default)
        {
            var accessToken = await EnsureSyncReadyAndGetAccessTokenAsync(userId, cancellationToken);

            var startUtc = DateTimeOffset.UtcNow.AddMinutes(30);
            var endUtc = startUtc.AddMinutes(30);
            var istanbulOffset = TimeSpan.FromHours(3);

            var requestBody = new
            {
                summary = "CRM Test Etkinliği",
                description = "CRM tarafından otomatik oluşturulan test etkinliği.",
                start = new
                {
                    dateTime = startUtc.ToOffset(istanbulOffset).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    timeZone = "Europe/Istanbul",
                },
                end = new
                {
                    dateTime = endUtc.ToOffset(istanbulOffset).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    timeZone = "Europe/Istanbul",
                },
            };

            var json = JsonSerializer.Serialize(requestBody);
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, CalendarEventsEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google calendar test event creation failed for user {UserId}. StatusCode: {StatusCode}, Body: {Body}",
                    userId,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException("Google calendar test event request failed.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                throw new InvalidOperationException("Google calendar event response does not contain event id.");
            }

            return idElement.GetString() ?? throw new InvalidOperationException("Google calendar event id is empty.");
        }

        public async Task<string> CreateActivityEventAsync(long userId, Activity activity, CancellationToken cancellationToken = default)
        {
            var accessToken = await EnsureSyncReadyAndGetAccessTokenAsync(userId, cancellationToken);
            var start = activity.StartDateTime;
            var end = activity.EndDateTime ?? activity.StartDateTime.AddMinutes(30);
            var now = DateTimeOffset.UtcNow;

            var requestBody = new
            {
                summary = $"[CRM] {activity.Subject}",
                description =
                    $"ActivityId: {activity.Id}\n" +
                    $"Durum: {activity.Status}\n" +
                    $"Oncelik: {activity.Priority}\n" +
                    $"CustomerId: {activity.PotentialCustomerId?.ToString() ?? "-"}\n" +
                    $"ContactId: {activity.ContactId?.ToString() ?? "-"}\n\n" +
                    $"{activity.Description ?? string.Empty}",
                start = new
                {
                    dateTime = new DateTimeOffset(start, TimeSpan.FromHours(3)).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    timeZone = "Europe/Istanbul",
                },
                end = new
                {
                    dateTime = new DateTimeOffset(end, TimeSpan.FromHours(3)).ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
                    timeZone = "Europe/Istanbul",
                },
                extendedProperties = new
                {
                    @private = new Dictionary<string, string>
                    {
                        ["crmActivityId"] = activity.Id.ToString(),
                        ["crmType"] = "Activity",
                        ["crmStatus"] = activity.Status.ToString(),
                        ["crmPriority"] = activity.Priority.ToString(),
                        ["crmCustomerId"] = activity.PotentialCustomerId?.ToString() ?? string.Empty,
                        ["crmContactId"] = activity.ContactId?.ToString() ?? string.Empty,
                        ["crmVersion"] = now.ToUnixTimeMilliseconds().ToString(),
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, CalendarEventsEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google calendar activity event creation failed for user {UserId}, activity {ActivityId}. StatusCode: {StatusCode}, Body: {Body}",
                    userId,
                    activity.Id,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException("Google calendar activity event request failed.");
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                throw new InvalidOperationException("Google calendar activity event response does not contain event id.");
            }

            return idElement.GetString() ?? throw new InvalidOperationException("Google calendar activity event id is empty.");
        }

        private async Task<string> EnsureSyncReadyAndGetAccessTokenAsync(long userId, CancellationToken cancellationToken)
        {
            var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty, cancellationToken);
            if (oauthSettings == null || !oauthSettings.IsEnabled)
            {
                throw new InvalidOperationException("Google OAuth etkin değil. Önce Google OAuth'u etkinleştirin.");
            }

            var account = await _googleTokenService.GetAccountAsync(userId, cancellationToken);
            if (account == null || !account.IsConnected)
            {
                throw new InvalidOperationException("Google hesabı bağlı değil. Önce Google ile giriş yapın.");
            }

            var accessToken = await _googleTokenService.GetValidAccessTokenAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Google oturumu geçersiz veya süresi dolmuş. Lütfen tekrar Google ile bağlanın.");
            }

            return accessToken;
        }
    }
}
