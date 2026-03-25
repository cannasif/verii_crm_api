using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class ActivityShippingService : IActivityShippingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ActivityShippingService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ActivityShippingGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.ActivityShippings.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplySearch(request.Search, QueryHelper.CommonSearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(ActivityShipping.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query.ApplyPagination(request.PageNumber, request.PageSize).ToListAsync().ConfigureAwait(false);

                return ApiResponse<PagedResponse<ActivityShippingGetDto>>.SuccessResult(new PagedResponse<ActivityShippingGetDto>
                {
                    Items = items.Select(_mapper.Map<ActivityShippingGetDto>).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                }, "Activity shippings retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ActivityShippingGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityShippingGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityShippings.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<ActivityShippingGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                return ApiResponse<ActivityShippingGetDto>.SuccessResult(_mapper.Map<ActivityShippingGetDto>(entity), "Activity shipping retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityShippingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityShippingGetDto>> CreateAsync(ActivityShippingCreateDto dto)
        {
            try
            {
                var entity = _mapper.Map<ActivityShipping>(dto);
                entity.CreatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityShippings.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var created = await _unitOfWork.ActivityShippings.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityShippingGetDto>.SuccessResult(_mapper.Map<ActivityShippingGetDto>(created ?? entity), "Activity shipping created");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityShippingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityShippingGetDto>> UpdateAsync(long id, ActivityShippingUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.ActivityShippings.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<ActivityShippingGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityShippings.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var updated = await _unitOfWork.ActivityShippings.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityShippingGetDto>.SuccessResult(_mapper.Map<ActivityShippingGetDto>(updated ?? entity), "Activity shipping updated");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityShippingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityShippings.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                await _unitOfWork.ActivityShippings.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(null, "Activity shipping deleted");
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
