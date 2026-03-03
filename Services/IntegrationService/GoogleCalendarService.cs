using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using crm_api.Interfaces;

namespace crm_api.Services
{
    public class GoogleCalendarService : IGoogleCalendarService
    {
        private const string CalendarEventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

        private readonly IGoogleTokenService _googleTokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleCalendarService> _logger;

        public GoogleCalendarService(
            IGoogleTokenService googleTokenService,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleCalendarService> logger)
        {
            _googleTokenService = googleTokenService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> CreateTestEventAsync(long userId, CancellationToken cancellationToken = default)
        {
            var accessToken = await _googleTokenService.GetValidAccessTokenAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Google account is not connected.");
            }

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
    }
}
