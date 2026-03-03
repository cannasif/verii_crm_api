using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using crm_api.DTOs;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace crm_api.Services
{
    public class GoogleOAuthService : IGoogleOAuthService
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IUserContextService _userContextService;
        private readonly ITenantGoogleOAuthSettingsService _tenantGoogleOAuthSettingsService;
        private readonly ILogger<GoogleOAuthService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public GoogleOAuthService(
            IHttpClientFactory httpClientFactory,
            IMemoryCache memoryCache,
            IUserContextService userContextService,
            ITenantGoogleOAuthSettingsService tenantGoogleOAuthSettingsService,
            ILogger<GoogleOAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _userContextService = userContextService;
            _tenantGoogleOAuthSettingsService = tenantGoogleOAuthSettingsService;
            _logger = logger;
        }

        public async Task<string> CreateAuthorizeUrlAsync(long userId, CancellationToken cancellationToken = default)
        {
            var tenantId = _userContextService.GetCurrentTenantId() ?? Guid.Empty;
            var settings = await GetValidatedSettingsAsync(tenantId, cancellationToken);

            var randomState = WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var state = $"{userId}.{tenantId:D}.{randomState}";
            var stateKey = BuildStateCacheKey(userId, state);

            _memoryCache.Set(stateKey, true, TimeSpan.FromMinutes(10));

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = settings.ClientId,
                ["redirect_uri"] = settings.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = settings.Scopes,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["include_granted_scopes"] = "true",
                ["state"] = state,
            };

            var url = QueryHelpers.AddQueryString(AuthorizationEndpoint, query!);
            return url;
        }

        public Task<bool> ValidateAndConsumeStateAsync(long userId, string state)
        {
            var stateKey = BuildStateCacheKey(userId, state);
            var isValid = _memoryCache.TryGetValue(stateKey, out _);
            if (isValid)
            {
                _memoryCache.Remove(stateKey);
            }

            return Task.FromResult(isValid);
        }

        public bool TryExtractStateContext(string state, out long userId, out Guid tenantId)
        {
            userId = 0;
            tenantId = Guid.Empty;

            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            var parts = state.Split('.', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            return long.TryParse(parts[0], out userId)
                   && userId > 0
                   && Guid.TryParse(parts[1], out tenantId)
                   && tenantId != Guid.Empty;
        }

        public async Task<GoogleOAuthTokenResult> ExchangeCodeForTokensAsync(string code, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var settings = await GetValidatedSettingsAsync(tenantId, cancellationToken);

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["redirect_uri"] = settings.RedirectUri,
            };

            return await SendTokenRequestAsync(formData, cancellationToken);
        }

        public async Task<GoogleOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, Guid tenantId, CancellationToken cancellationToken = default)
        {
            var settings = await GetValidatedSettingsAsync(tenantId, cancellationToken);

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
            };

            return await SendTokenRequestAsync(formData, cancellationToken);
        }

        public async Task<string?> GetGoogleEmailAsync(string accessToken, string? idToken, CancellationToken cancellationToken = default)
        {
            var emailFromIdToken = TryReadEmailFromIdToken(idToken);
            if (!string.IsNullOrWhiteSpace(emailFromIdToken))
            {
                return emailFromIdToken;
            }

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google userinfo request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var payload = JsonSerializer.Deserialize<GoogleUserInfoResponse>(responseBody, JsonOptions);
            return payload?.Email;
        }

        public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, RevokeEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = token,
                }),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token revoke request failed with status code {StatusCode}", response.StatusCode);
            }
        }

        private async Task<GoogleOAuthTokenResult> SendTokenRequestAsync(
            Dictionary<string, string> formData,
            CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(formData),
            };

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string? providerError = null;
                string? providerErrorDescription = null;
                try
                {
                    using var errorDoc = JsonDocument.Parse(responseBody);
                    if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        providerError = errorElement.GetString();
                    }

                    if (errorDoc.RootElement.TryGetProperty("error_description", out var descriptionElement))
                    {
                        providerErrorDescription = descriptionElement.GetString();
                    }
                }
                catch
                {
                    // ignore parse error and keep generic logging/exception below
                }

                _logger.LogWarning(
                    "Google token endpoint returned non-success status code {StatusCode}. Body: {Body}",
                    response.StatusCode,
                    responseBody);

                var providerMessage = string.IsNullOrWhiteSpace(providerError)
                    ? "unknown_error"
                    : providerError;

                if (!string.IsNullOrWhiteSpace(providerErrorDescription))
                {
                    providerMessage = $"{providerMessage}: {providerErrorDescription}";
                }

                throw new InvalidOperationException($"Google token request failed: {providerMessage}");
            }

            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseBody, JsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("Google token response is invalid.");
            }

            return new GoogleOAuthTokenResult
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresInSeconds = tokenResponse.ExpiresIn,
                Scope = tokenResponse.Scope,
                IdToken = tokenResponse.IdToken,
            };
        }

        private async Task<TenantGoogleOAuthRuntimeSettings> GetValidatedSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var settings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(tenantId, cancellationToken);
            if (settings == null || !settings.IsEnabled)
            {
                throw new InvalidOperationException("Google OAuth ayarları yapılandırılmamış.");
            }

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(settings.ClientId))
            {
                missing.Add("ClientId");
            }

            if (string.IsNullOrWhiteSpace(settings.ClientSecret))
            {
                missing.Add("ClientSecret");
            }

            if (string.IsNullOrWhiteSpace(settings.RedirectUri))
            {
                missing.Add("RedirectUri");
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Google OAuth ayarları yapılandırılmamış: {string.Join(", ", missing)}");
            }

            return settings;
        }

        private static string BuildStateCacheKey(long userId, string state)
        {
            return $"google_oauth_state:{userId}:{state}";
        }

        private static string? TryReadEmailFromIdToken(string? idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(idToken))
            {
                return null;
            }

            var jwt = handler.ReadJwtToken(idToken);
            var emailClaim = jwt.Claims.FirstOrDefault(c => string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase));
            return emailClaim?.Value;
        }

        private sealed class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }

            [JsonPropertyName("id_token")]
            public string? IdToken { get; set; }
        }

        private sealed class GoogleUserInfoResponse
        {
            public string? Email { get; set; }
        }
    }
}
