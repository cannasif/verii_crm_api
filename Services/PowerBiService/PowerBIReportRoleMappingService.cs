using AutoMapper;
using crm_api.DTOs;
using crm_api.DTOs.PowerBi;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models.PowerBi;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class PowerBIReportRoleMappingService : IPowerBIReportRoleMappingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PowerBIReportRoleMappingService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PowerBIReportRoleMappingGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.Role)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PowerBIReportRoleMapping.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PowerBIReportRoleMappingGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PowerBIReportRoleMappingGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PowerBIReportRoleMappingGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PowerBIReportRoleMappingGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportRoleMappingGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIReportRoleMappings.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entityWithNav = await _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.Role)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var dto = _mapper.Map<PowerBIReportRoleMappingGetDto>(entityWithNav ?? entity);

                return ApiResponse<PowerBIReportRoleMappingGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportRoleMappingGetDto>> CreateAsync(CreatePowerBIReportRoleMappingDto dto)
        {
            try
            {
                var exists = await _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .AnyAsync(x => x.PowerBIReportDefinitionId == dto.PowerBIReportDefinitionId && x.RoleId == dto.RoleId).ConfigureAwait(false);
                if (exists)
                {
                    return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.Duplicate"),
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.Duplicate"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = _mapper.Map<PowerBIReportRoleMapping>(dto);

                await _unitOfWork.PowerBIReportRoleMappings.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var created = await _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.Role)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIReportRoleMappingGetDto>(created ?? entity);

                return ApiResponse<PowerBIReportRoleMappingGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIReportRoleMappingGetDto>> UpdateAsync(long id, UpdatePowerBIReportRoleMappingDto dto)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIReportRoleMappings.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var duplicate = await _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .AnyAsync(x => x.Id != id &&
                                   x.PowerBIReportDefinitionId == dto.PowerBIReportDefinitionId &&
                                   x.RoleId == dto.RoleId).ConfigureAwait(false);
                if (duplicate)
                {
                    return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.Duplicate"),
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.Duplicate"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(dto, entity);
                await _unitOfWork.PowerBIReportRoleMappings.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updated = await _unitOfWork.PowerBIReportRoleMappings
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.Role)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIReportRoleMappingGetDto>(updated ?? entity);

                return ApiResponse<PowerBIReportRoleMappingGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIReportRoleMappingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIReportRoleMappings.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PowerBIReportRoleMappings.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.ItemDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIReportRoleMappingService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
