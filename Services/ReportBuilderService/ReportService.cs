using System.Text.Json;
using AutoMapper;
using crm_api.DTOs;
using crm_api.DTOs.ReportBuilderDto;
using crm_api.Interfaces;
using crm_api.Models.ReportBuilder;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services.ReportBuilderService
{
    public class ReportService : IReportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IReportingConnectionService _connectionService;
        private readonly IReportingCatalogService _catalogService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<ReportService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public ReportService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IReportingConnectionService connectionService,
            IReportingCatalogService catalogService,
            ILocalizationService localizationService,
            ILogger<ReportService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _connectionService = connectionService;
            _catalogService = catalogService;
            _localizationService = localizationService;
            _logger = logger;
        }

        public async Task<ApiResponse<ReportDetailDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ReportDefinitions.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted).ConfigureAwait(false);
                if (entity == null)
                    return ApiResponse<ReportDetailDto>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ReportNotFound"), null, 404);
                var dto = _mapper.Map<ReportDetailDto>(entity);
                return ApiResponse<ReportDetailDto>.SuccessResult(dto, _localizationService.GetLocalizedString("ReportService.ReportRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetById report {Id}", id);
                return ApiResponse<ReportDetailDto>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ErrorRetrievingReport"), _localizationService.GetLocalizedString("ReportService.ErrorRetrievingReport"), 500);
            }
        }

        public async Task<ApiResponse<PagedResponse<ReportListItemDto>>> ListAsync(string? search, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                var query = _unitOfWork.ReportDefinitions.Query().AsNoTracking().Where(r => !r.IsDeleted);
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    query = query.Where(r => r.Name.Contains(term) || (r.Description != null && r.Description.Contains(term)));
                }
                var total = await query.CountAsync().ConfigureAwait(false);
                var list = await query
                    .OrderByDescending(r => r.UpdatedDate ?? r.CreatedDate)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync().ConfigureAwait(false);
                var items = _mapper.Map<List<ReportListItemDto>>(list);
                var paged = new PagedResponse<ReportListItemDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
                return ApiResponse<PagedResponse<ReportListItemDto>>.SuccessResult(paged, _localizationService.GetLocalizedString("ReportService.ReportListRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "List reports");
                return ApiResponse<PagedResponse<ReportListItemDto>>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ErrorListingReports"), _localizationService.GetLocalizedString("ReportService.ErrorListingReports"), 500);
            }
        }

        public async Task<ApiResponse<ReportDetailDto>> CreateAsync(ReportCreateDto dto, long userId)
        {
            var validation = await ValidateForSaveAsync(dto.ConnectionKey, dto.DataSourceType, dto.DataSourceName, dto.ConfigJson).ConfigureAwait(false);
            if (!validation.Success)
                return ApiResponse<ReportDetailDto>.ErrorResult(validation.Message, validation.ExceptionMessage, validation.StatusCode);

            try
            {
                var entity = _mapper.Map<ReportDefinition>(dto);
                entity.CreatedBy = userId;
                entity.CreatedDate = DateTimeProvider.Now;
                entity.IsDeleted = false;
                var repo = _unitOfWork.Repository<ReportDefinition>();
                await repo.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var created = await _unitOfWork.ReportDefinitions.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == entity.Id).ConfigureAwait(false);
                var detail = _mapper.Map<ReportDetailDto>(created!);
                return ApiResponse<ReportDetailDto>.SuccessResult(detail, _localizationService.GetLocalizedString("ReportService.ReportCreated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create report");
                return ApiResponse<ReportDetailDto>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ErrorCreatingReport"), _localizationService.GetLocalizedString("ReportService.ErrorCreatingReport"), 500);
            }
        }

        public async Task<ApiResponse<ReportDetailDto>> UpdateAsync(long id, ReportUpdateDto dto, long userId)
        {
            var validation = await ValidateForSaveAsync(dto.ConnectionKey, dto.DataSourceType, dto.DataSourceName, dto.ConfigJson).ConfigureAwait(false);
            if (!validation.Success)
                return ApiResponse<ReportDetailDto>.ErrorResult(validation.Message, validation.ExceptionMessage, validation.StatusCode);

            var repo = _unitOfWork.Repository<ReportDefinition>();
            var entity = await repo.GetByIdForUpdateAsync(id).ConfigureAwait(false);
            if (entity == null)
                return ApiResponse<ReportDetailDto>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ReportNotFound"), null, 404);

            try
            {
                entity.Name = dto.Name;
                entity.Description = dto.Description;
                entity.ConnectionKey = dto.ConnectionKey;
                entity.DataSourceType = dto.DataSourceType;
                entity.DataSourceName = dto.DataSourceName;
                entity.ConfigJson = dto.ConfigJson;
                entity.UpdatedBy = userId;
                entity.UpdatedDate = DateTimeProvider.Now;
                await repo.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var updated = await _unitOfWork.ReportDefinitions.Query().AsNoTracking().FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
                var detail = _mapper.Map<ReportDetailDto>(updated!);
                return ApiResponse<ReportDetailDto>.SuccessResult(detail, _localizationService.GetLocalizedString("ReportService.ReportUpdated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update report {Id}", id);
                return ApiResponse<ReportDetailDto>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ErrorUpdatingReport"), _localizationService.GetLocalizedString("ReportService.ErrorUpdatingReport"), 500);
            }
        }

        public async Task<ApiResponse<bool>> SoftDeleteAsync(long id)
        {
            var repo = _unitOfWork.Repository<ReportDefinition>();
            var ok = await repo.SoftDeleteAsync(id).ConfigureAwait(false);
            if (!ok)
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("ReportService.ReportNotFound"), null, 404);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
            return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("ReportService.ReportDeleted"));
        }

        private async Task<ApiResponse<object>> ValidateForSaveAsync(string connectionKey, string dataSourceType, string dataSourceName, string configJson)
        {
            var connResp = _connectionService.ResolveConnectionString(connectionKey);
            if (!connResp.Success)
                return ApiResponse<object>.ErrorResult(connResp.Message ?? _localizationService.GetLocalizedString("ReportService.InvalidConnection"), null, connResp.StatusCode);

            var catalogResp = await _catalogService.CheckAndGetSchemaAsync(connectionKey, dataSourceType, dataSourceName).ConfigureAwait(false);
            if (!catalogResp.Success)
                return ApiResponse<object>.ErrorResult(catalogResp.Message ?? _localizationService.GetLocalizedString("ReportService.CatalogError"), catalogResp.ExceptionMessage, catalogResp.StatusCode);
            if (catalogResp.Data == null || catalogResp.Data.Count == 0)
                return ApiResponse<object>.ErrorResult(_localizationService.GetLocalizedString("ReportService.DatasourceNotFoundOrEmpty"), null, 400);

            try
            {
                var config = JsonSerializer.Deserialize<ReportConfig>(configJson, JsonOptions);
                if (config == null)
                    return ApiResponse<object>.ErrorResult(_localizationService.GetLocalizedString("ReportService.InvalidConfigJson"), null, 400);
                var err = ValidateReportConfig(config);
                if (!string.IsNullOrEmpty(err))
                    return ApiResponse<object>.ErrorResult(err, null, 400);
            }
            catch (JsonException)
            {
                return ApiResponse<object>.ErrorResult(_localizationService.GetLocalizedString("ReportService.InvalidConfigJsonFormat"), _localizationService.GetLocalizedString("ReportService.InvalidConfigJsonFormat"), 400);
            }

            return ApiResponse<object>.SuccessResult(null!, _localizationService.GetLocalizedString("ReportService.ReportRetrieved"));
        }

        private static readonly HashSet<string> ChartTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "table",
            "bar",
            "stackedbar",
            "line",
            "pie",
            "donut",
            "kpi",
            "matrix"
        };
        private static readonly HashSet<string> Aggregations = new(StringComparer.OrdinalIgnoreCase) { "none", "sum", "count", "avg", "min", "max" };

        private string? ValidateReportConfig(ReportConfig config)
        {
            if (!ChartTypes.Contains(config.ChartType ?? ""))
                return _localizationService.GetLocalizedString("ReportService.ReportConfigInvalidChartType");
            if (config.Values == null || config.Values.Count == 0)
                return _localizationService.GetLocalizedString("ReportService.ReportConfigValueRequired");
            foreach (var v in config.Values)
            {
                if (string.IsNullOrWhiteSpace(v.Field))
                    return _localizationService.GetLocalizedString("ReportService.ReportConfigValueFieldRequired");
                if (!Aggregations.Contains(v.Aggregation ?? "none"))
                    return _localizationService.GetLocalizedString("ReportService.ReportConfigInvalidAggregation");
            }
            return null;
        }
    }
}
