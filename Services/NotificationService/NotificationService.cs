using AutoMapper;
using crm_api.DTOs;
using crm_api.DTOs.NotificationDto;
using crm_api.Interfaces;
using crm_api.Models.Notification;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using crm_api.Hubs;

namespace crm_api.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<NotificationService> _logger;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IUserService _userService;

        public NotificationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            ILogger<NotificationService> logger,
            IHubContext<NotificationHub> hubContext, IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _logger = logger;
            _hubContext = hubContext;
            _userService = userService;
        }

        public async Task<ApiResponse<PagedResponse<NotificationDto>>> GetUserNotificationsAsync(PagedRequest pagedRequest)
        {
            try
            {
                // Ensure pagedRequest is not null
                if (pagedRequest == null)
                {
                    pagedRequest = new PagedRequest();
                }

                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if(!userIdResponse.Success)
                {
                    return ApiResponse<PagedResponse<NotificationDto>>.ErrorResult(userIdResponse.Message, userIdResponse.Message, userIdResponse.StatusCode);
                }
                var userId = userIdResponse.Data;

                var query = _unitOfWork.Notifications.Query()
                    .Where(x => x.UserId == userId && !x.IsDeleted)
                    .OrderByDescending(x => x.CreatedDate);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var notifications = await query
                    .Skip((pagedRequest.PageNumber - 1) * pagedRequest.PageSize)
                    .Take(pagedRequest.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var notificationDtos = new List<NotificationDto>();

                foreach (var notification in notifications)
                {
                    try
                    {
                        var dto = _mapper.Map<NotificationDto>(notification);
                        
                        // Localize Title - Ensure TitleKey is not empty
                        if (!string.IsNullOrEmpty(notification.TitleKey))
                        {
                            object[]? titleArgs = null;
                            if (!string.IsNullOrEmpty(notification.TitleArgs))
                            {
                                try 
                                { 
                                    titleArgs = JsonConvert.DeserializeObject<object[]>(notification.TitleArgs); 
                                }
                                catch 
                                { 
                                    // Ignore deserialization errors
                                }
                            }
                            
                            dto.Title = titleArgs != null && titleArgs.Length > 0
                                ? _localizationService.GetLocalizedString(notification.TitleKey, titleArgs)
                                : _localizationService.GetLocalizedString(notification.TitleKey);
                        }
                        else
                        {
                            dto.Title = string.Empty;
                        }

                        // Localize Message - Ensure MessageKey is not empty
                        if (!string.IsNullOrEmpty(notification.MessageKey))
                        {
                            object[]? messageArgs = null;
                            if (!string.IsNullOrEmpty(notification.MessageArgs))
                            {
                                try 
                                { 
                                    messageArgs = JsonConvert.DeserializeObject<object[]>(notification.MessageArgs); 
                                }
                                catch 
                                { 
                                    // Ignore deserialization errors
                                }
                            }

                            dto.Message = messageArgs != null && messageArgs.Length > 0
                                ? _localizationService.GetLocalizedString(notification.MessageKey, messageArgs)
                                : _localizationService.GetLocalizedString(notification.MessageKey);
                        }
                        else
                        {
                            dto.Message = string.Empty;
                        }

                        notificationDtos.Add(dto);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing notification {NotificationId} for user {UserId}", 
                            notification.Id, userId);
                    }
                }

                var pagedResponse = new PagedResponse<NotificationDto>
                {
                    Items = notificationDtos,
                    PageNumber = pagedRequest.PageNumber,
                    PageSize = pagedRequest.PageSize,
                    TotalCount = totalCount
                };

                return ApiResponse<PagedResponse<NotificationDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("NotificationService.NotificationsRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for user");
                return ApiResponse<PagedResponse<NotificationDto>>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(long userId)
        {
            try
            {
                var count = await _unitOfWork.Notifications
                    .CountAsync(x => x.UserId == userId && !x.IsRead && !x.IsDeleted).ConfigureAwait(false);

                return ApiResponse<int>.SuccessResult(count, _localizationService.GetLocalizedString("NotificationService.UnreadCountRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
                return ApiResponse<int>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }

        public async Task<ApiResponse<bool>> MarkAsReadAsync(long notificationId, long userId)
        {
            try
            {
                var notification = await _unitOfWork.Notifications.Query(tracking: true)
                    .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId).ConfigureAwait(false);

                if (notification == null)
                {
                    return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.NotificationNotFound"), _localizationService.GetLocalizedString("NotificationService.NotificationNotFound"), 404);
                }

                notification.IsRead = true;
                notification.UpdatedDate = DateTimeProvider.Now;
                notification.UpdatedBy = userId;

                await _unitOfWork.Notifications.UpdateAsync(notification).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("NotificationService.NotificationMarkedAsRead"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }

        public async Task<ApiResponse<bool>> MarkAllAsReadAsync(long userId)
        {
            try
            {
                var notifications = await _unitOfWork.Notifications.Query(tracking: true)
                    .Where(x => x.UserId == userId && !x.IsRead)
                    .ToListAsync().ConfigureAwait(false);

                if (notifications.Any())
                {
                    foreach (var notification in notifications)
                    {
                        notification.IsRead = true;
                        notification.UpdatedDate = DateTimeProvider.Now;
                        notification.UpdatedBy = userId;
                        await _unitOfWork.Notifications.UpdateAsync(notification).ConfigureAwait(false);
                    }
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                }

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("NotificationService.AllNotificationsMarkedAsRead"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }

        public async Task<ApiResponse<bool>> CreateNotificationAsync(CreateNotificationDto createDto)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = createDto.UserId,
                    TitleKey = createDto.TitleKey,
                    TitleArgs = createDto.TitleArgs != null ? JsonConvert.SerializeObject(createDto.TitleArgs) : null,
                    MessageKey = createDto.MessageKey,
                    MessageArgs = createDto.MessageArgs != null ? JsonConvert.SerializeObject(createDto.MessageArgs) : null,
                    IsRead = false,
                    RelatedEntityName = createDto.RelatedEntityName,
                    RelatedEntityId = createDto.RelatedEntityId,
                    NotificationType = createDto.NotificationType,
                    CreatedBy = createDto.UserId
                };

                await _unitOfWork.Notifications.AddAsync(notification).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Send real-time notification via SignalR
                var notificationDto = _mapper.Map<NotificationDto>(notification);
                
                // Localize Title - Ensure TitleKey is not empty
                if (!string.IsNullOrEmpty(notification.TitleKey))
                {
                    object[]? titleArgs = null;
                    if (!string.IsNullOrEmpty(notification.TitleArgs))
                    {
                        try { titleArgs = JsonConvert.DeserializeObject<object[]>(notification.TitleArgs); }
                        catch { /* Ignore deserialization errors */ }
                    }
                    
                    notificationDto.Title = titleArgs != null && titleArgs.Length > 0
                        ? _localizationService.GetLocalizedString(notification.TitleKey, titleArgs)
                        : _localizationService.GetLocalizedString(notification.TitleKey);
                }
                else
                {
                    notificationDto.Title = string.Empty;
                }

                // Localize Message - Ensure MessageKey is not empty
                if (!string.IsNullOrEmpty(notification.MessageKey))
                {
                    object[]? messageArgs = null;
                    if (!string.IsNullOrEmpty(notification.MessageArgs))
                    {
                        try { messageArgs = JsonConvert.DeserializeObject<object[]>(notification.MessageArgs); }
                        catch { /* Ignore deserialization errors */ }
                    }

                    notificationDto.Message = messageArgs != null && messageArgs.Length > 0
                        ? _localizationService.GetLocalizedString(notification.MessageKey, messageArgs)
                        : _localizationService.GetLocalizedString(notification.MessageKey);
                }
                else
                {
                    notificationDto.Message = string.Empty;
                }

                // Send to user via SignalR group (user_{userId})
                try
                {
                    await _hubContext.Clients.Group($"user_{createDto.UserId}").SendAsync("ReceiveNotification", notificationDto).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR notification to user {UserId}", createDto.UserId);
                    // Don't fail the whole operation if SignalR fails
                }

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("NotificationService.NotificationCreated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for user {UserId}", createDto.UserId);
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }

        public async Task<ApiResponse<bool>> DeleteNotificationAsync(long notificationId, long userId)
        {
            try
            {
                var notification = await _unitOfWork.Notifications.Query(tracking: true)
                    .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId).ConfigureAwait(false);

                if (notification == null)
                {
                    return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.NotificationNotFound"), _localizationService.GetLocalizedString("NotificationService.NotificationNotFound"), 404);
                }

                notification.IsDeleted = true;
                notification.DeletedDate = DateTimeProvider.Now;
                notification.DeletedBy = userId;

                await _unitOfWork.Notifications.UpdateAsync(notification).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("NotificationService.NotificationDeleted"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}", notificationId, userId);
                return ApiResponse<bool>.ErrorResult(_localizationService.GetLocalizedString("NotificationService.InternalServerError"), _localizationService.GetLocalizedString("NotificationService.InternalServerError"), 500);
            }
        }
    }
}
