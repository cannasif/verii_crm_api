using System.Security.Claims;
using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models.PowerBi;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using crm_api.DTOs.PowerBi;

namespace crm_api.Services
{
    public class PowerBIReportDefinitionService : IPowerBIReportDefinitionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PowerBIReportDefinitionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<PagedResponse<PowerBIReportDefinitionGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("UserId")?.Value;
                var roleId = _httpContextAccessor.HttpContext?.User?.FindFirst("RoleId")?.Value;
                var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;
                var roleIdOrName = roleId ?? role ?? string.Empty;

                var query = _unitOfWork.PowerBIReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                query = query.Where(r =>
                    (string.IsNullOrWhiteSpace(r.AllowedUserIds) || (userId != null && (
                        r.AllowedUserIds == userId
                        || r.AllowedUserIds.StartsWith(userId + ",")
                        || r.AllowedUserIds.EndsWith("," + userId)
                        || r.AllowedUserIds.Contains("," + userId + ","))))
                    && (string.IsNullOrWhiteSpace(r.AllowedRoleIds) || (roleIdOrName != "" && (
                        r.AllowedRoleIds == roleIdOrName
                        || r.AllowedRoleIds.StartsWith(roleIdOrName + ",")
                        || r.AllowedRoleIds.EndsWith("," + roleIdOrName)
                        || r.AllowedRoleIds.Contains("," + roleIdOrName + ",")))));

                var sortBy = request.SortBy ?? nameof(PowerBIReportDefinition.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PowerBIReportDefinitionGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PowerBIReportDefinitionGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PowerBIReportDefinitionGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PowerBIReportDefinitionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportDefinitionGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIReportDefinitions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIReportDefinitionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entityWithNav = await _unitOfWork.PowerBIReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var dto = _mapper.Map<PowerBIReportDefinitionGetDto>(entityWithNav ?? entity);

                return ApiResponse<PowerBIReportDefinitionGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportDefinitionGetDto>> CreateAsync(CreatePowerBIReportDefinitionDto dto)
        {
            try
            {
                var entity = _mapper.Map<PowerBIReportDefinition>(dto);

                await _unitOfWork.PowerBIReportDefinitions.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // reload (audit nav için)
                var createdEntity = await _unitOfWork.PowerBIReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIReportDefinitionGetDto>(createdEntity ?? entity);

                return ApiResponse<PowerBIReportDefinitionGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportDefinitionGetDto>> UpdateAsync(long id, UpdatePowerBIReportDefinitionDto dto)
        {
            try
            {
                // tracked entity lazım
                var entity = await _unitOfWork.PowerBIReportDefinitions.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIReportDefinitionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);

                await _unitOfWork.PowerBIReportDefinitions.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updatedEntity = await _unitOfWork.PowerBIReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIReportDefinitionGetDto>(updatedEntity ?? entity);

                return ApiResponse<PowerBIReportDefinitionGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIReportDefinitions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PowerBIReportDefinitions.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.ReportDefinitionDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportDefinitionService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
