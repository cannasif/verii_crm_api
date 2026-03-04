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
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(
            IGoogleTokenService googleTokenService,
            ITenantGoogleOAuthSettingsService tenantGoogleOAuthSettingsService,
            IHttpClientFactory httpClientFactory,
            ILocalizationService localizationService,
            ILogger<GoogleCalendarService> logger)
        {
            _googleTokenService = googleTokenService;
            _tenantGoogleOAuthSettingsService = tenantGoogleOAuthSettingsService;
            _httpClientFactory = httpClientFactory;
            _localizationService = localizationService;
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
                summary = _localizationService.GetLocalizedString("GoogleCalendarService.TestEventSummary"),
                description = _localizationService.GetLocalizedString("GoogleCalendarService.TestEventDescription"),
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

                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.TestEventRequestFailed"));
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.EventIdMissing"));
            }

            return idElement.GetString()
                ?? throw new InvalidOperationException(_localizationService.GetLocalizedString("GoogleCalendarService.EventIdEmpty"));
        }

        public async Task<string> CreateActivityEventAsync(long userId, Activity activity, CancellationToken cancellationToken = default)
        {
            var accessToken = await EnsureSyncReadyAndGetAccessTokenAsync(userId, cancellationToken);
            var requestBody = BuildActivityEventRequestBody(activity);

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

                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.ActivityCreateRequestFailed"));
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("id", out var idElement))
            {
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.ActivityEventIdMissing"));
            }

            return idElement.GetString()
                ?? throw new InvalidOperationException(_localizationService.GetLocalizedString("GoogleCalendarService.ActivityEventIdEmpty"));
        }

        public async Task<string> SyncActivityEventAsync(long userId, Activity activity, CancellationToken cancellationToken = default)
        {
            var accessToken = await EnsureSyncReadyAndGetAccessTokenAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(activity.GoogleCalendarEventId))
            {
                return await CreateActivityEventAsync(userId, activity, cancellationToken);
            }

            var existingEventId = activity.GoogleCalendarEventId.Trim();
            var client = _httpClientFactory.CreateClient();
            var requestBody = BuildActivityEventRequestBody(activity);
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{CalendarEventsEndpoint}/{Uri.EscapeDataString(existingEventId)}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google calendar activity event sync failed for user {UserId}, activity {ActivityId}, event {EventId}. StatusCode: {StatusCode}, Body: {Body}",
                    userId,
                    activity.Id,
                    existingEventId,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.ActivitySyncRequestFailed"));
            }

            return existingEventId;
        }

        public async Task DeleteActivityEventAsync(long userId, string calendarEventId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(calendarEventId))
            {
                return;
            }

            var accessToken = await EnsureSyncReadyAndGetAccessTokenAsync(userId, cancellationToken);
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"{CalendarEventsEndpoint}/{Uri.EscapeDataString(calendarEventId.Trim())}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Google calendar activity event delete failed for user {UserId}, event {EventId}. StatusCode: {StatusCode}, Body: {Body}",
                userId,
                calendarEventId,
                response.StatusCode,
                responseBody);

            throw new InvalidOperationException(
                _localizationService.GetLocalizedString("GoogleCalendarService.ActivityDeleteRequestFailed"));
        }

        private static object BuildActivityEventRequestBody(Activity activity)
        {
            var start = activity.StartDateTime;
            var end = activity.EndDateTime ?? activity.StartDateTime.AddMinutes(30);
            var now = DateTimeOffset.UtcNow;
            var activityTypeName = activity.ActivityType?.Name;
            var customerName = activity.PotentialCustomer?.CustomerName;
            var contactName = activity.Contact?.FullName;

            return new
            {
                summary = $"[CRM] {activity.Subject}",
                description =
                    $"ActivityId: {activity.Id}\n" +
                    $"Durum: {activity.Status}\n" +
                    $"Oncelik: {activity.Priority}\n" +
                    $"ActivityTypeId: {activity.ActivityTypeId}\n" +
                    $"ActivityTypeName: {activityTypeName ?? "-"}\n" +
                    $"CustomerId: {activity.PotentialCustomerId?.ToString() ?? "-"}\n" +
                    $"CustomerName: {customerName ?? "-"}\n" +
                    $"ContactId: {activity.ContactId?.ToString() ?? "-"}\n" +
                    $"ContactName: {contactName ?? "-"}\n\n" +
                    $"{activity.Description ?? string.Empty}",
                start = new
                {
                    dateTime = ToIstanbulDateTimeString(start),
                    timeZone = "Europe/Istanbul",
                },
                end = new
                {
                    dateTime = ToIstanbulDateTimeString(end),
                    timeZone = "Europe/Istanbul",
                },
                reminders = BuildRemindersPayload(activity),
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
        }

        private static object BuildRemindersPayload(Activity activity)
        {
            var overrides = activity.Reminders
                .Where(r => !r.IsDeleted)
                .Where(r => r.Channel != ReminderChannel.Sms)
                .Select(r => Math.Clamp(r.OffsetMinutes, 0, 40320))
                .Distinct()
                .OrderBy(v => v)
                .Take(5)
                .Select(minutes => new { method = "popup", minutes })
                .ToList();

            return new
            {
                useDefault = overrides.Count == 0,
                overrides
            };
        }

        private async Task<string> EnsureSyncReadyAndGetAccessTokenAsync(long userId, CancellationToken cancellationToken)
        {
            var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty, cancellationToken);
            if (oauthSettings == null || !oauthSettings.IsEnabled)
            {
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.OAuthDisabled"));
            }

            var account = await _googleTokenService.GetAccountAsync(userId, cancellationToken);
            if (account == null || !account.IsConnected)
            {
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.AccountNotConnected"));
            }

            var accessToken = await _googleTokenService.GetValidAccessTokenAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("GoogleCalendarService.TokenInvalidOrExpired"));
            }

            return accessToken;
        }

        private static string ToIstanbulDateTimeString(DateTime value)
        {
            var istanbulOffset = TimeSpan.FromHours(3);

            var offsetDateTime = value.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
                DateTimeKind.Local => new DateTimeOffset(value),
                DateTimeKind.Unspecified => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
                _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
            };

            return offsetDateTime.ToOffset(istanbulOffset).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
        }
    }
}
