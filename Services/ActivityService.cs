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
        private static readonly string[] SearchableColumns =
        [
            nameof(Activity.Subject),
            nameof(Activity.Description),
            nameof(Activity.ErpCustomerCode),
            "ActivityType.Name",
            "PotentialCustomer.CustomerCode",
            "PotentialCustomer.CustomerName",
            "Contact.FirstName",
            "Contact.LastName",
            "Contact.FullName",
            "AssignedUser.Username",
            "AssignedUser.Email",
            "AssignedUser.FirstName",
            "AssignedUser.LastName"
        ];

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
                    .Include(a => a.PaymentType)
                    .Include(a => a.ActivityMeetingType)
                    .Include(a => a.ActivityTopicPurpose)
                    .Include(a => a.ActivityShipping)
                    .Include(a => a.PotentialCustomer)
                    .Include(a => a.Contact)
                    .Include(a => a.AssignedUser)
                    .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.UpdatedByUser)
                    .Include(a => a.DeletedByUser)
                    .ApplySearch(request.Search, SearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(Activity.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

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
                    .Include(a => a.PaymentType)
                    .Include(a => a.ActivityMeetingType)
                    .Include(a => a.ActivityTopicPurpose)
                    .Include(a => a.ActivityShipping)
                    .Include(a => a.PotentialCustomer)
                    .Include(a => a.Contact)
                    .Include(a => a.AssignedUser)
                    .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.UpdatedByUser)
                    .Include(a => a.DeletedByUser)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted).ConfigureAwait(false);

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
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                if (createActivityDto.EndDateTime == default)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.EndDateRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var validationError = await ValidateForeignKeysAsync(
                    createActivityDto.ActivityTypeId,
                    createActivityDto.AssignedUserId,
                    createActivityDto.ContactId,
                    createActivityDto.PotentialCustomerId,
                    createActivityDto.PaymentTypeId,
                    createActivityDto.ActivityMeetingTypeId,
                    createActivityDto.ActivityTopicPurposeId,
                    createActivityDto.ActivityShippingId).ConfigureAwait(false);
                if (validationError != null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return validationError;
                }

                if (createActivityDto.EndDateTime < createActivityDto.StartDateTime)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.EndDateBeforeStartDate"),
                        StatusCodes.Status400BadRequest);
                }

                var activity = _mapper.Map<Activity>(createActivityDto);
                var createdActivity = await _unitOfWork.Activities.AddAsync(activity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var activityWithRelations = await LoadActivityWithRelationsAsync(createdActivity.Id, asNoTracking: true).ConfigureAwait(false);

                var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty).ConfigureAwait(false);
                if (oauthSettings?.IsEnabled == true)
                {
                    var currentUserId = _userContextService.GetCurrentUserId();
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<ActivityDto>.ErrorResult(
                            _localizationService.GetLocalizedString("ActivityService.ActivityCreateFailed"),
                            _localizationService.GetLocalizedString("ActivityService.UserSessionNotFound"),
                            StatusCodes.Status400BadRequest);
                    }

                    var calendarEventId = await _googleCalendarService.CreateActivityEventAsync(currentUserId.Value, activityWithRelations ?? createdActivity).ConfigureAwait(false);
                    createdActivity.GoogleCalendarEventId = calendarEventId;
                    await _unitOfWork.Activities.UpdateAsync(createdActivity).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    activityWithRelations = await LoadActivityWithRelationsAsync(createdActivity.Id, asNoTracking: true).ConfigureAwait(false);
                }

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                var dto = _mapper.Map<ActivityDto>(activityWithRelations ?? createdActivity);

                return ApiResponse<ActivityDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("ActivityService.ActivityCreated"));
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<ActivityDto>.ErrorResult(
                    ex.Message,
                    ex.Message,
                    StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
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
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                var activity = await _unitOfWork.Activities.Query()
                    .Include(a => a.Reminders)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted).ConfigureAwait(false);

                if (activity == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                if (updateActivityDto.EndDateTime == default)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.EndDateRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var validationError = await ValidateForeignKeysAsync(
                    updateActivityDto.ActivityTypeId,
                    updateActivityDto.AssignedUserId,
                    updateActivityDto.ContactId,
                    updateActivityDto.PotentialCustomerId,
                    updateActivityDto.PaymentTypeId,
                    updateActivityDto.ActivityMeetingTypeId,
                    updateActivityDto.ActivityTopicPurposeId,
                    updateActivityDto.ActivityShippingId).ConfigureAwait(false);
                if (validationError != null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return validationError;
                }

                if (updateActivityDto.EndDateTime < updateActivityDto.StartDateTime)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.EndDateBeforeStartDate"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(updateActivityDto, activity);
                activity.UpdatedDate = DateTimeProvider.Now;

                foreach (var existingReminder in activity.Reminders.Where(r => !r.IsDeleted))
                {
                    existingReminder.IsDeleted = true;
                    existingReminder.DeletedDate = DateTimeProvider.Now;
                }

                foreach (var reminderDto in updateActivityDto.Reminders)
                {
                    var newReminder = _mapper.Map<ActivityReminder>(reminderDto);
                    newReminder.ActivityId = activity.Id;
                    activity.Reminders.Add(newReminder);
                }

                await _unitOfWork.Activities.UpdateAsync(activity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updatedWithNav = await LoadActivityWithRelationsAsync(activity.Id, asNoTracking: true).ConfigureAwait(false);

                var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty).ConfigureAwait(false);
                if (oauthSettings?.IsEnabled == true)
                {
                    var currentUserId = _userContextService.GetCurrentUserId();
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<ActivityDto>.ErrorResult(
                            _localizationService.GetLocalizedString("ActivityService.ActivityUpdateFailed"),
                            _localizationService.GetLocalizedString("ActivityService.UserSessionNotFound"),
                            StatusCodes.Status400BadRequest);
                    }

                    var calendarEventId = await _googleCalendarService.SyncActivityEventAsync(currentUserId.Value, updatedWithNav ?? activity).ConfigureAwait(false);
                    activity.GoogleCalendarEventId = calendarEventId;
                    await _unitOfWork.Activities.UpdateAsync(activity).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    updatedWithNav = await LoadActivityWithRelationsAsync(activity.Id, asNoTracking: true).ConfigureAwait(false);
                }

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
                var dto = _mapper.Map<ActivityDto>(updatedWithNav ?? activity);

                return ApiResponse<ActivityDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("ActivityService.ActivityUpdated"));
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<ActivityDto>.ErrorResult(
                    ex.Message,
                    ex.Message,
                    StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
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
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                var activity = await _unitOfWork.Activities.Query()
                    .Include(a => a.Reminders)
                    .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted).ConfigureAwait(false);

                if (activity == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var oauthSettings = await _tenantGoogleOAuthSettingsService.GetRuntimeSettingsAsync(Guid.Empty).ConfigureAwait(false);
                if (oauthSettings?.IsEnabled == true && !string.IsNullOrWhiteSpace(activity.GoogleCalendarEventId))
                {
                    var currentUserId = _userContextService.GetCurrentUserId();
                    if (!currentUserId.HasValue || currentUserId.Value <= 0)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<object>.ErrorResult(
                            _localizationService.GetLocalizedString("ActivityService.ActivityDeleteFailed"),
                            _localizationService.GetLocalizedString("ActivityService.UserSessionNotFound"),
                            StatusCodes.Status400BadRequest);
                    }

                    await _googleCalendarService.DeleteActivityEventAsync(currentUserId.Value, activity.GoogleCalendarEventId).ConfigureAwait(false);
                }

                await _unitOfWork.Activities.SoftDeleteAsync(id).ConfigureAwait(false);

                foreach (var reminder in activity.Reminders.Where(r => !r.IsDeleted))
                {
                    reminder.IsDeleted = true;
                    reminder.DeletedDate = DateTimeProvider.Now;
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    activity,
                    _localizationService.GetLocalizedString("ActivityService.ActivityDeleted"));
            }
            catch (InvalidOperationException ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<object>.ErrorResult(
                    ex.Message,
                    ex.Message,
                    StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
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
                .Include(a => a.PaymentType)
                .Include(a => a.ActivityMeetingType)
                .Include(a => a.ActivityTopicPurpose)
                .Include(a => a.ActivityShipping)
                .Include(a => a.PotentialCustomer)
                .Include(a => a.Contact)
                .Include(a => a.AssignedUser)
                .Include(a => a.Reminders.Where(r => !r.IsDeleted))
                    .Include(a => a.Images.Where(i => !i.IsDeleted))
                .Include(a => a.CreatedByUser)
                .Include(a => a.UpdatedByUser)
                .Include(a => a.DeletedByUser)
                .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted).ConfigureAwait(false);
        }

        private async Task<ApiResponse<ActivityDto>?> ValidateForeignKeysAsync(
            long activityTypeId,
            long assignedUserId,
            long? contactId,
            long? potentialCustomerId,
            long? paymentTypeId,
            long? activityMeetingTypeId,
            long? activityTopicPurposeId,
            long? activityShippingId)
        {
            var activityTypeExists = await _unitOfWork.ActivityTypes.Query(tracking: false)
                .AnyAsync(x => x.Id == activityTypeId && !x.IsDeleted).ConfigureAwait(false);
            if (!activityTypeExists)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    _localizationService.GetLocalizedString("ActivityService.InvalidActivityType"),
                    StatusCodes.Status400BadRequest);
            }

            var assignedUserExists = await _unitOfWork.Users.Query(tracking: false)
                .AnyAsync(x => x.Id == assignedUserId && !x.IsDeleted).ConfigureAwait(false);
            if (!assignedUserExists)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    _localizationService.GetLocalizedString("ActivityService.AssignedUserNotFound"),
                    StatusCodes.Status400BadRequest);
            }

            if (contactId.HasValue)
            {
                var contactExists = await _unitOfWork.Contacts.Query(tracking: false)
                    .AnyAsync(x => x.Id == contactId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!contactExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.ContactNotFound"),
                        StatusCodes.Status400BadRequest);
                }
            }

            if (potentialCustomerId.HasValue)
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(x => x.Id == potentialCustomerId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!customerExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("ActivityService.CustomerNotFound"),
                        StatusCodes.Status400BadRequest);
                }
            }

            if (paymentTypeId.HasValue)
            {
                var paymentTypeExists = await _unitOfWork.PaymentTypes.Query(tracking: false)
                    .AnyAsync(x => x.Id == paymentTypeId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!paymentTypeExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        "Payment type not found",
                        StatusCodes.Status400BadRequest);
                }
            }

            if (activityMeetingTypeId.HasValue)
            {
                var meetingTypeExists = await _unitOfWork.ActivityMeetingTypes.Query(tracking: false)
                    .AnyAsync(x => x.Id == activityMeetingTypeId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!meetingTypeExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        "Activity meeting type not found",
                        StatusCodes.Status400BadRequest);
                }
            }

            if (activityTopicPurposeId.HasValue)
            {
                var topicPurposeExists = await _unitOfWork.ActivityTopicPurposes.Query(tracking: false)
                    .AnyAsync(x => x.Id == activityTopicPurposeId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!topicPurposeExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        "Activity topic purpose not found",
                        StatusCodes.Status400BadRequest);
                }
            }

            if (activityShippingId.HasValue)
            {
                var shippingExists = await _unitOfWork.ActivityShippings.Query(tracking: false)
                    .AnyAsync(x => x.Id == activityShippingId.Value && !x.IsDeleted).ConfigureAwait(false);
                if (!shippingExists)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        "Activity shipping not found",
                        StatusCodes.Status400BadRequest);
                }
            }

            return null;
        }
    }
}
