using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class ActivityService : IActivityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly ITenantGoogleOAuthSettingsService _tenantGoogleOAuthSettingsService;
        private readonly IUserContextService _userContextService;

        public ActivityService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IGoogleCalendarService googleCalendarService,
            ITenantGoogleOAuthSettingsService tenantGoogleOAuthSettingsService,
            IUserContextService userContextService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _googleCalendarService = googleCalendarService;
            _tenantGoogleOAuthSettingsService = tenantGoogleOAuthSettingsService;
            _userContextService = userContextService;
        }

        public async Task<ApiResponse<PagedResponse<ActivityDto>>> GetAllActivitiesAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.Activities.Query()
                    .AsNoTracking()
                    .Where(a => !a.IsDeleted)
                    .Include(a => a.ActivityType)
                    .Include(a => a.PotentialCustomer)
                    .Include(a => a.Contact)
                    .Include(a => a.AssignedUser)
                    .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.UpdatedByUser)
                    .Include(a => a.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(Activity.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync();
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync();

                var dtos = items.Select(x => _mapper.Map<ActivityDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ActivityDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ActivityDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("ActivityService.ActivitiesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ActivityDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityService.GetAllActivitiesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityDto>> GetActivityByIdAsync(long id)
        {
            try
            {
                var activity = await _unitOfWork.Activities.Query()
                    .AsNoTracking()
                    .Include(a => a.ActivityType)
                    .Include(a => a.PotentialCustomer)
                    .Include(a => a.Contact)
                    .Include(a => a.AssignedUser)
                    .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.UpdatedByUser)
                    .Include(a => a.DeletedByUser)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

                if (activity == null)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var activityDto = _mapper.Map<ActivityDto>(activity);
                return ApiResponse<ActivityDto>.SuccessResult(
                    activityDto,
                    _localizationService.GetLocalizedString("ActivityService.ActivityRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityService.GetActivityByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityDto>> CreateActivityAsync(CreateActivityDto createActivityDto)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var validationError = await ValidateForeignKeysAsync(
                    createActivityDto.ActivityTypeId,
                    createActivityDto.AssignedUserId,
                    createActivityDto.ContactId,
                    createActivityDto.PotentialCustomerId);
                if (validationError != null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return validationError;
                }

                if (createActivityDto.EndDateTime.HasValue && createActivityDto.EndDateTime < createActivityDto.StartDateTime)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                var activity = _mapper.Map<Activity>(createActivityDto);
                var createdActivity = await _unitOfWork.Activities.AddAsync(activity);
                await _unitOfWork.SaveChangesAsync();

                var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty);
                if (oauthSettings?.IsEnabled == true)
                {
                    var currentUserId = _userContextService.GetCurrentUserId();
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        return ApiResponse<ActivityDto>.ErrorResult(
                            "Aktivite kaydı oluşturulamadı.",
                            "Kullanıcı oturumu bulunamadı. Lütfen tekrar giriş yapın.",
                            StatusCodes.Status400BadRequest);
                    }

                    await _googleCalendarService.CreateActivityEventAsync(currentUserId.Value, createdActivity);
                }

                await _unitOfWork.CommitTransactionAsync();

                var createdWithNav = await LoadActivityWithRelationsAsync(createdActivity.Id, asNoTracking: true);
                var dto = _mapper.Map<ActivityDto>(createdWithNav ?? createdActivity);

                return ApiResponse<ActivityDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("ActivityService.ActivityCreated"));
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackTransactionAsync();

                return ApiResponse<ActivityDto>.ErrorResult(
                    "Aktivite kaydı oluşturulamadı.",
                    ex.Message,
                    StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityService.CreateActivityExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityDto>> UpdateActivityAsync(long id, UpdateActivityDto updateActivityDto)
        {
            try
            {
                var activity = await _unitOfWork.Activities.Query()
                    .Include(a => a.Reminders)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

                if (activity == null)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var validationError = await ValidateForeignKeysAsync(
                    updateActivityDto.ActivityTypeId,
                    updateActivityDto.AssignedUserId,
                    updateActivityDto.ContactId,
                    updateActivityDto.PotentialCustomerId);
                if (validationError != null)
                {
                    return validationError;
                }

                if (updateActivityDto.EndDateTime.HasValue && updateActivityDto.EndDateTime < updateActivityDto.StartDateTime)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(updateActivityDto, activity);
                activity.UpdatedDate = DateTime.UtcNow;

                foreach (var existingReminder in activity.Reminders.Where(r => !r.IsDeleted))
                {
                    existingReminder.IsDeleted = true;
                    existingReminder.DeletedDate = DateTime.UtcNow;
                }

                foreach (var reminderDto in updateActivityDto.Reminders)
                {
                    var newReminder = _mapper.Map<ActivityReminder>(reminderDto);
                    newReminder.ActivityId = activity.Id;
                    activity.Reminders.Add(newReminder);
                }

                await _unitOfWork.Activities.UpdateAsync(activity);
                await _unitOfWork.SaveChangesAsync();

                var updatedWithNav = await LoadActivityWithRelationsAsync(activity.Id, asNoTracking: true);
                var dto = _mapper.Map<ActivityDto>(updatedWithNav ?? activity);

                return ApiResponse<ActivityDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("ActivityService.ActivityUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityService.UpdateActivityExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteActivityAsync(long id)
        {
            try
            {
                var activity = await _unitOfWork.Activities.Query()
                    .Include(a => a.Reminders)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);

                if (activity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.Activities.SoftDeleteAsync(id);

                foreach (var reminder in activity.Reminders.Where(r => !r.IsDeleted))
                {
                    reminder.IsDeleted = true;
                    reminder.DeletedDate = DateTime.UtcNow;
                }

                await _unitOfWork.SaveChangesAsync();

                return ApiResponse<object>.SuccessResult(
                    activity,
                    _localizationService.GetLocalizedString("ActivityService.ActivityDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    _localizationService.GetLocalizedString("ActivityService.DeleteActivityExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<Activity?> LoadActivityWithRelationsAsync(long id, bool asNoTracking)
        {
            var query = _unitOfWork.Activities.Query();
            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                .Include(a => a.ActivityType)
                .Include(a => a.PotentialCustomer)
                .Include(a => a.Contact)
                .Include(a => a.AssignedUser)
                .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                .Include(a => a.CreatedByUser)
                .Include(a => a.UpdatedByUser)
                .Include(a => a.DeletedByUser)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        }

        private async Task<ApiResponse<ActivityDto>?> ValidateForeignKeysAsync(
            long activityTypeId,
            long assignedUserId,
            long? contactId,
            long? potentialCustomerId)
        {
            var activityTypeExists = await _unitOfWork.ActivityTypes.Query(tracking: false)
                .AnyAsync(x => x.Id == activityTypeId && !x.IsDeleted);
            if (!activityTypeExists)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    StatusCodes.Status400BadRequest);
            }

            var assignedUserExists = await _unitOfWork.Users.Query(tracking: false)
                .AnyAsync(x => x.Id == assignedUserId && !x.IsDeleted);
            if (!assignedUserExists)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    StatusCodes.Status400BadRequest);
            }

            if (contactId.HasValue)
            {
                var contactExists = await _unitOfWork.Contacts.Query(tracking: false)
                    .AnyAsync(x => x.Id == contactId.Value && !x.IsDeleted);
                if (!contactExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }
            }

            if (potentialCustomerId.HasValue)
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(x => x.Id == potentialCustomerId.Value && !x.IsDeleted);
                if (!customerExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }
            }

            return null;
        }
    }
}
