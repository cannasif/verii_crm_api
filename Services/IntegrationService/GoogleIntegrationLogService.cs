using System.Text.Json;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class GoogleIntegrationLogService : IGoogleIntegrationLogService
    {
        private readonly IUnitOfWork _uow;
        private readonly IUserContextService _userContextService;
        private readonly ILogger<GoogleIntegrationLogService> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private static readonly IReadOnlyDictionary<string, string> LogColumnMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "Id",
                ["tenantId"] = "TenantId",
                ["userId"] = "UserId",
                ["operation"] = "Operation",
                ["isSuccess"] = "IsSuccess",
                ["severity"] = "Severity",
                ["provider"] = "Provider",
                ["message"] = "Message",
                ["errorCode"] = "ErrorCode",
                ["activityId"] = "ActivityId",
                ["googleCalendarEventId"] = "GoogleCalendarEventId",
                ["createdDate"] = "CreatedDate",
            };

        public GoogleIntegrationLogService(
            IUnitOfWork uow,
            IUserContextService userContextService,
            ILogger<GoogleIntegrationLogService> logger)
        {
            _uow = uow;
            _userContextService = userContextService;
            _logger = logger;
        }

        public async Task WriteAsync(GoogleIntegrationLogWriteDto dto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dto.Operation))
            {
                return;
            }

            try
            {
                var tenantId = ResolveTenantId(dto.TenantId);
                if (tenantId == Guid.Empty)
                {
                    return;
                }

                var metadataJson = SerializeMetadata(dto.Metadata);
                var entity = new GoogleIntegrationLog
                {
                    TenantId = tenantId,
                    UserId = dto.UserId,
                    Operation = dto.Operation.Trim(),
                    IsSuccess = dto.IsSuccess,
                    Severity = NormalizeSeverity(dto.Severity),
                    Provider = string.IsNullOrWhiteSpace(dto.Provider) ? "Google" : dto.Provider.Trim(),
                    Message = TrimToNull(dto.Message, 2000),
                    ErrorCode = TrimToNull(dto.ErrorCode, 256),
                    ActivityId = dto.ActivityId,
                    GoogleCalendarEventId = TrimToNull(dto.GoogleCalendarEventId, 512),
                    MetadataJson = TrimToNull(metadataJson, 4000),
                    CreatedDate = DateTimeProvider.Now,
                    IsDeleted = false,
                };

                await _uow.Repository<GoogleIntegrationLog>().AddAsync(entity).ConfigureAwait(false);
                await _uow.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google integration log write failed for operation {Operation}", dto.Operation);
            }
        }

        public async Task<PagedResponse<GoogleIntegrationLogDto>> GetPagedAsync(
            Guid tenantId,
            long? userId,
            GoogleIntegrationLogsQueryDto request,
            CancellationToken cancellationToken = default)
        {
            var normalizedPageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var normalizedPageSize = Math.Clamp(request.PageSize, 1, 100);

            var logQuery = _uow.Repository<GoogleIntegrationLog>()
                .Query()
                .Where(x => x.TenantId == tenantId);

            if (userId.HasValue && userId.Value > 0)
            {
                logQuery = logQuery.Where(x => x.UserId == userId.Value);
            }

            if (request.ErrorsOnly)
            {
                logQuery = logQuery.Where(x => !x.IsSuccess);
            }

            logQuery = logQuery.ApplyFilters(request.Filters, request.FilterLogic, LogColumnMapping);

            var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "createdDate" : request.SortBy;
            var sortDirection = string.IsNullOrWhiteSpace(request.SortDirection) ? "desc" : request.SortDirection;
            logQuery = logQuery.ApplySorting(sortBy, sortDirection, LogColumnMapping);

            var totalCount = await logQuery.CountAsync(cancellationToken).ConfigureAwait(false);
            var items = await logQuery
                .Skip((normalizedPageNumber - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(x => new GoogleIntegrationLogDto
                {
                    Id = x.Id,
                    TenantId = x.TenantId,
                    UserId = x.UserId,
                    Operation = x.Operation,
                    IsSuccess = x.IsSuccess,
                    Severity = x.Severity,
                    Provider = x.Provider,
                    Message = x.Message,
                    ErrorCode = x.ErrorCode,
                    ActivityId = x.ActivityId,
                    GoogleCalendarEventId = x.GoogleCalendarEventId,
                    MetadataJson = x.MetadataJson,
                    CreatedDate = x.CreatedDate,
                })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return new PagedResponse<GoogleIntegrationLogDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize,
            };
        }

        private Guid ResolveTenantId(Guid? tenantId)
        {
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                return tenantId.Value;
            }

            return _userContextService.GetCurrentTenantId() ?? Guid.Empty;
        }

        private static string NormalizeSeverity(string? severity)
        {
            if (string.IsNullOrWhiteSpace(severity))
            {
                return "Info";
            }

            var value = severity.Trim();
            return value.Length <= 32 ? value : value[..32];
        }

        private static string? SerializeMetadata(object? metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Serialize(metadata, JsonOptions);
            }
            catch
            {
                return metadata.ToString();
            }
        }

        private static string? TrimToNull(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
