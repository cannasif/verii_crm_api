using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class ActivityTopicPurposeService : IActivityTopicPurposeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ActivityTopicPurposeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ActivityTopicPurposeGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.ActivityTopicPurposes.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplySearch(request.Search, QueryHelper.CommonSearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(ActivityTopicPurpose.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query.ApplyPagination(request.PageNumber, request.PageSize).ToListAsync().ConfigureAwait(false);

                return ApiResponse<PagedResponse<ActivityTopicPurposeGetDto>>.SuccessResult(new PagedResponse<ActivityTopicPurposeGetDto>
                {
                    Items = items.Select(_mapper.Map<ActivityTopicPurposeGetDto>).ToList(),
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                }, "Activity topic purposes retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ActivityTopicPurposeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTopicPurposeGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityTopicPurposes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<ActivityTopicPurposeGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                return ApiResponse<ActivityTopicPurposeGetDto>.SuccessResult(_mapper.Map<ActivityTopicPurposeGetDto>(entity), "Activity topic purpose retrieved");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTopicPurposeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTopicPurposeGetDto>> CreateAsync(ActivityTopicPurposeCreateDto dto)
        {
            try
            {
                var entity = _mapper.Map<ActivityTopicPurpose>(dto);
                entity.CreatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityTopicPurposes.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var created = await _unitOfWork.ActivityTopicPurposes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityTopicPurposeGetDto>.SuccessResult(_mapper.Map<ActivityTopicPurposeGetDto>(created ?? entity), "Activity topic purpose created");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTopicPurposeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTopicPurposeGetDto>> UpdateAsync(long id, ActivityTopicPurposeUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.ActivityTopicPurposes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<ActivityTopicPurposeGetDto>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTimeProvider.Now;
                await _unitOfWork.ActivityTopicPurposes.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var updated = await _unitOfWork.ActivityTopicPurposes.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted)
                    .ConfigureAwait(false);
                return ApiResponse<ActivityTopicPurposeGetDto>.SuccessResult(_mapper.Map<ActivityTopicPurposeGetDto>(updated ?? entity), "Activity topic purpose updated");
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTopicPurposeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityTopicPurposes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult("Not found", "Not found", StatusCodes.Status404NotFound);
                }

                await _unitOfWork.ActivityTopicPurposes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(null, "Activity topic purpose deleted");
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
