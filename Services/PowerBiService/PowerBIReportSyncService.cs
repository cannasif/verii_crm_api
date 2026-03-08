using System.Net.Http.Headers;
using System.Text.Json;
using crm_api.DTOs;
using crm_api.DTOs.PowerBi;
using crm_api.Helpers;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using crm_api.Models.PowerBi;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace crm_api.Services
{
    public class PowerBIReportSyncService : IPowerBIReportSyncService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILocalizationService _localizationService;
        private readonly IConfiguration _configuration;
        private readonly AzureAdSettings _azureAd;
        private readonly PowerBISettings _powerBi;

        public PowerBIReportSyncService(
            IUnitOfWork unitOfWork,
            IHttpClientFactory httpClientFactory,
            ILocalizationService localizationService,
            IConfiguration configuration,
            IOptions<AzureAdSettings> azureAd,
            IOptions<PowerBISettings> powerBi)
        {
            _unitOfWork = unitOfWork;
            _httpClientFactory = httpClientFactory;
            _localizationService = localizationService;
            _configuration = configuration;
            _azureAd = azureAd.Value;
            _powerBi = powerBi.Value;
        }

        public async Task<ApiResponse<PowerBIReportSyncResultDto>> SyncAsync(Guid? workspaceId)
        {
            try
            {
                var config = await GetEffectiveConfigAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(config.TenantId) ||
                    string.IsNullOrWhiteSpace(config.ClientId) ||
                    config.DefaultWorkspaceId == Guid.Empty)
                {
                    return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.ConfigurationMissing"),
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.ConfigurationMissing"),
                        StatusCodes.Status400BadRequest);
                }

                if (string.IsNullOrWhiteSpace(config.ClientSecret))
                {
                    return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.ClientSecretMissing"),
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.ClientSecretMissing"),
                        StatusCodes.Status400BadRequest);
                }

                var targetWorkspaceId = workspaceId ?? config.DefaultWorkspaceId;
                if (targetWorkspaceId == Guid.Empty)
                {
                    return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.WorkspaceRequired"),
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.WorkspaceRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var accessToken = await GetAzureAdAccessTokenAsync(config).ConfigureAwait(false);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.TokenAcquisitionFailed"),
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.TokenAcquisitionFailed"),
                        StatusCodes.Status502BadGateway);
                }

                var remoteReports = await GetWorkspaceReportsAsync(targetWorkspaceId, accessToken, config.ApiBaseUrl).ConfigureAwait(false);
                if (remoteReports == null)
                {
                    return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.SyncFailed"),
                        _localizationService.GetLocalizedString("PowerBIReportSyncService.SyncFailed"),
                        StatusCodes.Status502BadGateway);
                }

                var existing = await _unitOfWork.PowerBIReportDefinitions
                    .Query(tracking: true, ignoreQueryFilters: true)
                    .Where(r => r.WorkspaceId == targetWorkspaceId)
                    .ToListAsync().ConfigureAwait(false);

                var byReportId = existing.ToDictionary(r => r.ReportId, r => r);
                var remoteIds = new HashSet<Guid>(remoteReports.Select(r => r.Id));

                var created = 0;
                var updated = 0;
                var reactivated = 0;
                var deleted = 0;

                foreach (var report in remoteReports)
                {
                    if (byReportId.TryGetValue(report.Id, out var entity))
                    {
                        entity.Name = report.Name;
                        entity.EmbedUrl = report.EmbedUrl;
                        entity.DatasetId = report.DatasetId;
                        entity.IsActive = true;
                        if (entity.IsDeleted)
                        {
                            entity.IsDeleted = false;
                            entity.DeletedDate = null;
                            entity.DeletedBy = null;
                            reactivated++;
                        }
                        else
                        {
                            updated++;
                        }
                        await _unitOfWork.PowerBIReportDefinitions.UpdateAsync(entity).ConfigureAwait(false);
                        continue;
                    }

                    var newEntity = new PowerBIReportDefinition
                    {
                        Name = report.Name,
                        Description = null,
                        WorkspaceId = targetWorkspaceId,
                        ReportId = report.Id,
                        DatasetId = report.DatasetId,
                        EmbedUrl = report.EmbedUrl,
                        IsActive = true
                    };
                    await _unitOfWork.PowerBIReportDefinitions.AddAsync(newEntity).ConfigureAwait(false);
                    created++;
                }

                foreach (var entity in existing.Where(e => !e.IsDeleted && !remoteIds.Contains(e.ReportId)))
                {
                    var deletedResult = await _unitOfWork.PowerBIReportDefinitions.SoftDeleteAsync(entity.Id).ConfigureAwait(false);
                    if (deletedResult)
                        deleted++;
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var result = new PowerBIReportSyncResultDto
                {
                    TotalRemote = remoteReports.Count,
                    Created = created,
                    Updated = updated,
                    Reactivated = reactivated,
                    Deleted = deleted
                };

                return ApiResponse<PowerBIReportSyncResultDto>.SuccessResult(
                    result,
                    _localizationService.GetLocalizedString("PowerBIReportSyncService.SyncCompleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportSyncResultDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportSyncService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportSyncService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<List<PowerBIReportItem>?> GetWorkspaceReportsAsync(Guid workspaceId, string accessToken, string apiBaseUrl)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var url = $"{apiBaseUrl.TrimEnd('/')}/v1.0/myorg/groups/{workspaceId}/reports";
            var response = await client.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<PowerBIReportListResponse>(json);
            if (payload?.Value == null)
                return new List<PowerBIReportItem>();

            return payload.Value
                .Select(r => new PowerBIReportItem
                {
                    Id = Guid.TryParse(r.Id, out var id) ? id : Guid.Empty,
                    Name = r.Name ?? string.Empty,
                    EmbedUrl = r.EmbedUrl,
                    DatasetId = Guid.TryParse(r.DatasetId, out var ds) ? ds : null
                })
                .Where(r => r.Id != Guid.Empty)
                .ToList();
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
                    DefaultWorkspaceId = dbConfig.WorkspaceId
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
                    DefaultWorkspaceId = entity.WorkspaceId
                };
            }

            return new EmbedServiceConfig
            {
                TenantId = _azureAd.TenantId ?? string.Empty,
                ClientId = _azureAd.ClientId ?? string.Empty,
                ClientSecret = _azureAd.ClientSecret ?? string.Empty,
                Scope = _powerBi.Scope ?? "https://analysis.windows.net/powerbi/api/.default",
                ApiBaseUrl = _powerBi.ApiBaseUrl?.TrimEnd('/') ?? "https://api.powerbi.com",
                DefaultWorkspaceId = fallbackWorkspaceId
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
        }

        private class PowerBIReportListResponse
        {
            public List<PowerBIReportListItem>? Value { get; set; }
        }

        private class PowerBIReportListItem
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? EmbedUrl { get; set; }
            public string? DatasetId { get; set; }
        }

        private class PowerBIReportItem
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? EmbedUrl { get; set; }
            public Guid? DatasetId { get; set; }
        }
    }
}
