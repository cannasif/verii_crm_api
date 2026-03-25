using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class ActivityMeetingTypeService : IActivityMeetingTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ActivityMeetingTypeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ActivityMeetingTypeGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.ActivityMeetingTypes.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplySearch(request.Search, QueryHelper.CommonSearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(ActivityMeetingType.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query.ApplyPagination(request.PageNumber, request.PageSize).ToListAsync().ConfigureAwait(false);

                return ApiResponse<PagedResponse<ActivityMeetingTypeGetDto>>.SuccessResult(new PagedResponse<ActivityMeetingTypeGetDto>
                {
                    Items = items.Select(_mapper.Map<ActivityMeetingTypeGetDto>).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                }, "Activity meeting types retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ActivityMeetingTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityMeetingTypeGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityMeetingTypes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<ActivityMeetingTypeGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                return ApiResponse<ActivityMeetingTypeGetDto>.SuccessResult(_mapper.Map<ActivityMeetingTypeGetDto>(entity), "Activity meeting type retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityMeetingTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityMeetingTypeGetDto>> CreateAsync(ActivityMeetingTypeCreateDto dto)
        {
            try
            {
                var entity = _mapper.Map<ActivityMeetingType>(dto);
                entity.CreatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityMeetingTypes.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var created = await _unitOfWork.ActivityMeetingTypes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityMeetingTypeGetDto>.SuccessResult(_mapper.Map<ActivityMeetingTypeGetDto>(created ?? entity), "Activity meeting type created");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityMeetingTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityMeetingTypeGetDto>> UpdateAsync(long id, ActivityMeetingTypeUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.ActivityMeetingTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<ActivityMeetingTypeGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityMeetingTypes.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var updated = await _unitOfWork.ActivityMeetingTypes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityMeetingTypeGetDto>.SuccessResult(_mapper.Map<ActivityMeetingTypeGetDto>(updated ?? entity), "Activity meeting type updated");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityMeetingTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityMeetingTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                await _unitOfWork.ActivityMeetingTypes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(null, "Activity meeting type deleted");
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
