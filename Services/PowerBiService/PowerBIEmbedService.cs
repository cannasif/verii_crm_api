using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models.PowerBi;
using crm_api.UnitOfWork;
using crm_api.DTOs.PowerBi;
using crm_api.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace crm_api.Services
{
    public class PowerBIEmbedService : IPowerBIEmbedService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IConfiguration _configuration;
        private readonly AzureAdSettings _azureAd;
        private readonly PowerBISettings _powerBi;
        private readonly IHostEnvironment _environment;

        public PowerBIEmbedService(
            IUnitOfWork unitOfWork,
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IConfiguration configuration,
            IHostEnvironment environment,
            IOptions<AzureAdSettings> azureAd,
            IOptions<PowerBISettings> powerBi)
        {
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _configuration = configuration;
            _environment = environment;
            _azureAd = azureAd.Value;
            _powerBi = powerBi.Value;
        }

        /// <summary>
        /// Resolves config from DB (single IsDeleted=false record) when present;
        /// ClientSecret is always from env/secret store (never from DB or UI).
        /// </summary>
        private async Task<EmbedServiceConfig> GetEffectiveConfigAsync()
        {
            var dbConfig = await _unitOfWork.PowerBIConfigurations
                .Query()
                .AsNoTracking()
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (dbConfig != null)
            {
                var clientSecret = _configuration["PowerBi:ClientSecret"]
                    ?? _configuration["AzureAd:ClientSecret"]
                    ?? Environment.GetEnvironmentVariable("PowerBi__ClientSecret")
                    ?? Environment.GetEnvironmentVariable("AzureAd__ClientSecret")
                    ?? string.Empty;

                return new EmbedServiceConfig
                {
                    TenantId = dbConfig.TenantId,
                    ClientId = dbConfig.ClientId,
                    ClientSecret = clientSecret,
                    Scope = string.IsNullOrWhiteSpace(dbConfig.Scope) ? "https://analysis.windows.net/powerbi/api/.default" : dbConfig.Scope.Trim(),
                    ApiBaseUrl = string.IsNullOrWhiteSpace(dbConfig.ApiBaseUrl) ? "https://api.powerbi.com" : dbConfig.ApiBaseUrl.Trim(),
                    DefaultWorkspaceId = dbConfig.WorkspaceId,
                    EmbedBaseUrl = "https://app.powerbi.com"
                };
            }

            var fallbackTenantId = _azureAd.TenantId?.Trim();
            var fallbackClientId = _azureAd.ClientId?.Trim();
            var fallbackWorkspaceId = Guid.TryParse(_powerBi.WorkspaceId, out var w) ? w : Guid.Empty;
            if (!string.IsNullOrWhiteSpace(fallbackTenantId) &&
                !string.IsNullOrWhiteSpace(fallbackClientId) &&
                fallbackWorkspaceId != Guid.Empty)
            {
                var entity = new PowerBIConfiguration
                {
                    TenantId = fallbackTenantId,
                    ClientId = fallbackClientId,
                    WorkspaceId = fallbackWorkspaceId,
                    ApiBaseUrl = _powerBi.ApiBaseUrl?.Trim(),
                    Scope = _powerBi.Scope?.Trim()
                };
                await _unitOfWork.PowerBIConfigurations.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var clientSecret = _configuration["PowerBi:ClientSecret"]
                    ?? _configuration["AzureAd:ClientSecret"]
                    ?? Environment.GetEnvironmentVariable("PowerBi__ClientSecret")
                    ?? Environment.GetEnvironmentVariable("AzureAd__ClientSecret")
                    ?? string.Empty;

                return new EmbedServiceConfig
                {
                    TenantId = entity.TenantId ?? string.Empty,
                    ClientId = entity.ClientId ?? string.Empty,
                    ClientSecret = clientSecret,
                    Scope = string.IsNullOrWhiteSpace(entity.Scope) ? "https://analysis.windows.net/powerbi/api/.default" : entity.Scope.Trim(),
                    ApiBaseUrl = string.IsNullOrWhiteSpace(entity.ApiBaseUrl) ? "https://api.powerbi.com" : entity.ApiBaseUrl.Trim(),
                    DefaultWorkspaceId = entity.WorkspaceId,
                    EmbedBaseUrl = "https://app.powerbi.com"
                };
            }

            return new EmbedServiceConfig
            {
                TenantId = _azureAd.TenantId ?? string.Empty,
                ClientId = _azureAd.ClientId ?? string.Empty,
                ClientSecret = _azureAd.ClientSecret ?? string.Empty,
                Scope = _powerBi?.Scope ?? "https://analysis.windows.net/powerbi/api/.default",
                ApiBaseUrl = _powerBi?.ApiBaseUrl?.TrimEnd('/') ?? "https://api.powerbi.com",
                DefaultWorkspaceId = fallbackWorkspaceId,
                EmbedBaseUrl = _powerBi?.EmbedBaseUrl?.TrimEnd('/') ?? "https://app.powerbi.com"
            };
        }

        private struct EmbedServiceConfig
        {
            public string TenantId;
            public string ClientId;
            public string ClientSecret;
            public string Scope;
            public string ApiBaseUrl;
            public Guid DefaultWorkspaceId;
            public string EmbedBaseUrl;
        }

        public async Task<ApiResponse<EmbedConfigDto>> GetEmbedConfigAsync(long reportDefinitionId)
        {
            try
            {
                var definition = await _unitOfWork.PowerBIReportDefinitions.GetByIdAsync(reportDefinitionId).ConfigureAwait(false);
                if (definition == null)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ReportNotFound"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ReportNotFound"),
                        StatusCodes.Status404NotFound);
                }

                if (!definition.IsActive)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ReportInactive"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ReportInactive"),
                        StatusCodes.Status400BadRequest);
                }

                var rlsResult = await ResolveRlsRolesAsync(definition).ConfigureAwait(false);
                if (rlsResult.Denied != null)
                    return rlsResult.Denied;

                if (rlsResult.HasRls && !definition.DatasetId.HasValue)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.DatasetRequired"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.DatasetRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var config = await GetEffectiveConfigAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(config.TenantId) ||
                    string.IsNullOrWhiteSpace(config.ClientId) ||
                    config.DefaultWorkspaceId == Guid.Empty)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ConfigurationMissing"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ConfigurationMissing"),
                        StatusCodes.Status400BadRequest);
                }

                if (string.IsNullOrWhiteSpace(config.ClientSecret))
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ClientSecretMissing"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.ClientSecretMissing"),
                        StatusCodes.Status400BadRequest);
                }

                var workspaceId = definition.WorkspaceId;
                if (workspaceId == Guid.Empty)
                    workspaceId = config.DefaultWorkspaceId;
                if (workspaceId == Guid.Empty)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.WorkspaceRequired"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.WorkspaceRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var isMockMode = _environment.IsDevelopment() && (
                    string.Equals(_configuration["PowerBi:MockMode"], "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Environment.GetEnvironmentVariable("PowerBi__MockMode"), "true", StringComparison.OrdinalIgnoreCase)
                );
                if (isMockMode)
                {
                    var mockEmbedUrl = definition.EmbedUrl;
                    if (string.IsNullOrWhiteSpace(mockEmbedUrl))
                        mockEmbedUrl = $"{config.EmbedBaseUrl}/reportEmbed?reportId={definition.ReportId}&groupId={workspaceId}";

                    var mockDto = new EmbedConfigDto
                    {
                        ReportId = definition.ReportId,
                        EmbedUrl = mockEmbedUrl,
                        AccessToken = "mock-token",
                        Expiration = DateTime.UtcNow.AddHours(1).ToString("o")
                    };

                    return ApiResponse<EmbedConfigDto>.SuccessResult(
                        mockDto,
                        _localizationService.GetLocalizedString("PowerBIEmbedService.EmbedConfigRetrieved"));
                }

                var accessToken = await GetAzureAdAccessTokenAsync(config).ConfigureAwait(false);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.TokenAcquisitionFailed"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.TokenAcquisitionFailed"),
                        StatusCodes.Status502BadGateway);
                }

                var embedResult = await GetEmbedTokenAsync(definition, workspaceId, accessToken, config, rlsResult.Roles).ConfigureAwait(false);
                if (embedResult == null)
                {
                    return ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.EmbedTokenFailed"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.EmbedTokenFailed"),
                        StatusCodes.Status502BadGateway);
                }

                var embedUrl = definition.EmbedUrl;
                if (string.IsNullOrWhiteSpace(embedUrl))
                    embedUrl = $"{config.EmbedBaseUrl}/reportEmbed?reportId={definition.ReportId}&groupId={workspaceId}";

                var dto = new EmbedConfigDto
                {
                    ReportId = definition.ReportId,
                    EmbedUrl = embedUrl,
                    AccessToken = embedResult.Token,
                    Expiration = embedResult.Expiration ?? string.Empty
                };

                return ApiResponse<EmbedConfigDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIEmbedService.EmbedConfigRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<EmbedConfigDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIEmbedService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIEmbedService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string?> GetAzureAdAccessTokenAsync(EmbedServiceConfig config)
        {
            var client = _httpClientFactory.CreateClient();
            var authority = $"https://login.microsoftonline.com/{config.TenantId}";
            var tokenUrl = $"{authority}/oauth2/v2.0/token";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["scope"] = config.Scope
            });

            var response = await client.PostAsync(tokenUrl, content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                return tokenProp.GetString();
            return null;
        }

        private async Task<PowerBIEmbedTokenResponse?> GetEmbedTokenAsync(PowerBIReportDefinition definition, Guid workspaceId, string accessToken, EmbedServiceConfig config, List<string> rlsRoles)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"{config.ApiBaseUrl.TrimEnd('/')}/v1.0/myorg/groups/{workspaceId}/reports/{definition.ReportId}/GenerateToken";

            object requestBody = BuildGenerateTokenRequest(definition, rlsRoles);
            var body = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<PowerBIEmbedTokenResponse>(json);
        }

        private async Task<(ApiResponse<EmbedConfigDto>? Denied, List<string> Roles, bool HasRls)> ResolveRlsRolesAsync(PowerBIReportDefinition definition)
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")?.Value;

            if (!string.IsNullOrWhiteSpace(definition.AllowedUserIds))
            {
                var allowedIds = definition.AllowedUserIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(userId) || !allowedIds.Contains(userId))
                {
                    return (ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        StatusCodes.Status403Forbidden), new List<string>(), false);
                }
            }

            var mappings = await _unitOfWork.PowerBIReportRoleMappings
                .Query()
                .AsNoTracking()
                .Where(x => x.PowerBIReportDefinitionId == definition.Id)
                .ToListAsync().ConfigureAwait(false);

            if (mappings.Count > 0)
            {
                var userRoleIds = GetUserRoleIds();
                if (userRoleIds.Count == 0)
                {
                    return (ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        StatusCodes.Status403Forbidden), new List<string>(), false);
                }

                var matched = mappings.Where(m => userRoleIds.Contains(m.RoleId)).ToList();
                if (matched.Count == 0)
                {
                    return (ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        StatusCodes.Status403Forbidden), new List<string>(), false);
                }

                var roles = matched
                    .SelectMany(m => (m.RlsRoles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (roles.Count == 0)
                {
                    return (ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        StatusCodes.Status403Forbidden), new List<string>(), false);
                }

                return (null, roles, true);
            }

            if (!string.IsNullOrWhiteSpace(definition.AllowedRoleIds))
            {
                var user = _httpContextAccessor.HttpContext?.User;
                var roleClaims = user?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
                var roleNameClaims = user?.FindAll("role").Select(c => c.Value) ?? Enumerable.Empty<string>();
                var roleIdClaim = user?.FindFirst("RoleId")?.Value;
                var roleValues = roleClaims
                    .Concat(roleNameClaims)
                    .Concat(string.IsNullOrWhiteSpace(roleIdClaim) ? Enumerable.Empty<string>() : new[] { roleIdClaim })
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var allowedRoleIds = definition.AllowedRoleIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (roleValues.Count == 0 || !roleValues.Any(allowedRoleIds.Contains))
                {
                    return (ApiResponse<EmbedConfigDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        _localizationService.GetLocalizedString("PowerBIEmbedService.AccessDenied"),
                        StatusCodes.Status403Forbidden), new List<string>(), false);
                }
            }

            var fallbackRoles = (definition.RlsRoles ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (null, fallbackRoles, fallbackRoles.Count > 0);
        }

        private List<long> GetUserRoleIds()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var roleIdClaims = user?.FindAll("RoleId").Select(c => c.Value) ?? Enumerable.Empty<string>();
            var roleIdValues = roleIdClaims
                .Select(v => long.TryParse(v, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            return roleIdValues;
        }

        private object BuildGenerateTokenRequest(PowerBIReportDefinition definition, List<string> rlsRoles)
        {
            var hasRls = rlsRoles.Count > 0;
            if (!hasRls)
                return new { accessLevel = "View" };

            var roles = rlsRoles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var username = GetCurrentUserIdentity();
            var datasetIds = definition.DatasetId.HasValue
                ? new[] { definition.DatasetId.Value.ToString() }
                : Array.Empty<string>();

            var identity = new
            {
                username,
                roles,
                datasets = datasetIds
            };

            return new
            {
                accessLevel = "View",
                identities = new[] { identity }
            };
        }

        private string GetCurrentUserIdentity()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var email = user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.FindFirst("email")?.Value
                ?? user?.FindFirst("preferred_username")?.Value;
            if (!string.IsNullOrEmpty(email))
                return email;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst("UserId")?.Value;
            return string.IsNullOrEmpty(userId) ? "anonymous" : $"user_{userId}";
        }

        private class PowerBIEmbedTokenResponse
        {
            [JsonPropertyName("token")]
            public string Token { get; set; } = string.Empty;

            [JsonPropertyName("expiration")]
            public string? Expiration { get; set; }
        }
    }
}
