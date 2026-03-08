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
    public class PowerBIGroupService : IPowerBIGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PowerBIGroupService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PowerBIGroupGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.PowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PowerBIGroup.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PowerBIGroupGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PowerBIGroupGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PowerBIGroupGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("PowerBIGroupService.GroupsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PowerBIGroupGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroups.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIGroupGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entityWithNav = await _unitOfWork.PowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var dto = _mapper.Map<PowerBIGroupGetDto>(entityWithNav ?? entity);

                return ApiResponse<PowerBIGroupGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIGroupService.GroupRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupGetDto>> CreateAsync(CreatePowerBIGroupDto dto)
        {
            try
            {
                var entity = _mapper.Map<PowerBIGroup>(dto);

                await _unitOfWork.PowerBIGroups.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var createdEntity = await _unitOfWork.PowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIGroupGetDto>(createdEntity ?? entity);

                return ApiResponse<PowerBIGroupGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIGroupService.GroupCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupGetDto>> UpdateAsync(long id, UpdatePowerBIGroupDto dto)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroups.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIGroupGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);

                await _unitOfWork.PowerBIGroups.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updatedEntity = await _unitOfWork.PowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIGroupGetDto>(updatedEntity ?? entity);

                return ApiResponse<PowerBIGroupGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIGroupService.GroupUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroups.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupService.GroupNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PowerBIGroups.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("PowerBIGroupService.GroupDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
