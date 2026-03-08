using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class ActivityTypeService : IActivityTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ActivityTypeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ActivityTypeGetDto>>> GetAllActivityTypesAsync(PagedRequest request)
        {
            try
            {
                if (request == null)
                {
                    request = new PagedRequest();
                }

                if (request.Filters == null)
                {
                    request.Filters = new List<Filter>();
                }

                var query = _unitOfWork.ActivityTypes.Query()
                    .AsNoTracking()
                    .Where(at => !at.IsDeleted)
                    .Include(at => at.CreatedByUser)
                    .Include(at => at.UpdatedByUser)
                    .Include(at => at.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(ActivityType.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ActivityTypeGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ActivityTypeGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ActivityTypeGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ActivityTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityTypeService.GetAllActivityTypesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTypeGetDto>> GetActivityTypeByIdAsync(long id)
        {
            try
            {
                var activityType = await _unitOfWork.ActivityTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (activityType == null)
                {
                    return ApiResponse<ActivityTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var activityTypeWithNav = await _unitOfWork.ActivityTypes.Query()
                    .AsNoTracking()
                    .Include(at => at.CreatedByUser)
                    .Include(at => at.UpdatedByUser)
                    .Include(at => at.DeletedByUser)
                    .FirstOrDefaultAsync(at => at.Id == id && !at.IsDeleted).ConfigureAwait(false);

                var activityTypeDto = _mapper.Map<ActivityTypeGetDto>(activityTypeWithNav ?? activityType);

                return ApiResponse<ActivityTypeGetDto>.SuccessResult(activityTypeDto, _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityTypeService.GetActivityTypeByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTypeGetDto>> CreateActivityTypeAsync(ActivityTypeCreateDto createActivityTypeDto)
        {
            try
            {
                var activityType = _mapper.Map<ActivityType>(createActivityTypeDto);
                activityType.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.ActivityTypes.AddAsync(activityType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var activityTypeWithNav = await _unitOfWork.ActivityTypes.Query()
                    .AsNoTracking()
                    .Include(at => at.CreatedByUser)
                    .Include(at => at.UpdatedByUser)
                    .Include(at => at.DeletedByUser)
                    .FirstOrDefaultAsync(at => at.Id == activityType.Id && !at.IsDeleted).ConfigureAwait(false);

                var activityTypeDto = _mapper.Map<ActivityTypeGetDto>(activityTypeWithNav ?? activityType);

                return ApiResponse<ActivityTypeGetDto>.SuccessResult(activityTypeDto, _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityTypeService.CreateActivityTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityTypeGetDto>> UpdateActivityTypeAsync(long id, ActivityTypeUpdateDto updateActivityTypeDto)
        {
            try
            {
                var existingActivityType = await _unitOfWork.ActivityTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (existingActivityType == null)
                {
                    return ApiResponse<ActivityTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateActivityTypeDto, existingActivityType);
                existingActivityType.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.ActivityTypes.UpdateAsync(existingActivityType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var activityTypeWithNav = await _unitOfWork.ActivityTypes.Query()
                    .AsNoTracking()
                    .Include(at => at.CreatedByUser)
                    .Include(at => at.UpdatedByUser)
                    .Include(at => at.DeletedByUser)
                    .FirstOrDefaultAsync(at => at.Id == existingActivityType.Id && !at.IsDeleted).ConfigureAwait(false);

                var activityTypeDto = _mapper.Map<ActivityTypeGetDto>(activityTypeWithNav ?? existingActivityType);

                return ApiResponse<ActivityTypeGetDto>.SuccessResult(activityTypeDto, _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityTypeService.UpdateActivityTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteActivityTypeAsync(long id)
        {
            try
            {
                var activityType = await _unitOfWork.ActivityTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (activityType == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.ActivityTypes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ActivityTypeService.ActivityTypeDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityTypeService.DeleteActivityTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
