using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _loc;

        public UserSessionService(IUnitOfWork uow, IMapper mapper, ILocalizationService loc)
        {
            _uow = uow; _mapper = mapper; _loc = loc;
        }

        public async Task<ApiResponse<PagedResponse<UserSessionDto>>> GetAllSessionsAsync(PagedRequest request)
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

                var query = _uow.UserSessions.Query()
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(UserSession.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<UserSessionDto>(x)).ToList();

                var pagedResponse = new PagedResponse<UserSessionDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<UserSessionDto>>.SuccessResult(pagedResponse, _loc.GetLocalizedString("UserSessionService.UserSessionsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<UserSessionDto>>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.GetAllSessionsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserSessionDto>> GetSessionByIdAsync(long id)
        {
            try
            {
                var item = await _uow.UserSessions.GetByIdAsync(id).ConfigureAwait(false);
                if (item == null) return ApiResponse<UserSessionDto>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    StatusCodes.Status404NotFound);

                // Reload with navigation properties for mapping
                var itemWithNav = await _uow.UserSessions.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted).ConfigureAwait(false);

                var dto = _mapper.Map<UserSessionDto>(itemWithNav ?? item);
                return ApiResponse<UserSessionDto>.SuccessResult(dto, _loc.GetLocalizedString("UserSessionService.UserSessionRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserSessionDto>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.GetSessionByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserSessionDto>> CreateSessionAsync(CreateUserSessionDto dto)
        {
            try
            {
                var entity = _mapper.Map<UserSession>(dto);
                await _uow.UserSessions.AddAsync(entity).ConfigureAwait(false);
                await _uow.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var itemWithNav = await _uow.UserSessions.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == entity.Id && !u.IsDeleted).ConfigureAwait(false);

                var outDto = _mapper.Map<UserSessionDto>(itemWithNav ?? entity);
                return ApiResponse<UserSessionDto>.SuccessResult(outDto, _loc.GetLocalizedString("UserSessionService.UserSessionCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserSessionDto>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.CreateSessionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> RevokeSessionAsync(long id)
        {
            try
            {
                var entity = await _uow.UserSessions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null) return ApiResponse<object>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    StatusCodes.Status404NotFound);
                entity.RevokedAt = DateTime.UtcNow;
                await _uow.UserSessions.UpdateAsync(entity).ConfigureAwait(false);
                await _uow.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(null, _loc.GetLocalizedString("UserSessionService.UserSessionRevoked"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.RevokeSessionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteSessionAsync(long id)
        {
            try
            {
                var entity = await _uow.UserSessions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null) return ApiResponse<object>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    _loc.GetLocalizedString("UserSessionService.UserSessionNotFound"),
                    StatusCodes.Status404NotFound);
                await _uow.UserSessions.SoftDeleteAsync(id).ConfigureAwait(false);
                await _uow.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(null, _loc.GetLocalizedString("UserSessionService.UserSessionDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.DeleteSessionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> RevokeActiveSessionByUserIdAsync(long userId)
        {
            try
            {
                var activeSessions = await _uow.UserSessions.FindAsync(s => s.UserId == userId && s.RevokedAt == null).ConfigureAwait(false);
                var sessionsList = activeSessions.ToList();
                if (sessionsList != null && sessionsList.Any())
                {
                    foreach (var session in sessionsList)
                    {
                        session.RevokedAt = DateTime.UtcNow;
                        await _uow.UserSessions.UpdateAsync(session).ConfigureAwait(false);
                    }
                    await _uow.SaveChangesAsync().ConfigureAwait(false);
                }
                return ApiResponse<object>.SuccessResult(null, _loc.GetLocalizedString("UserSessionService.UserSessionRevoked"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _loc.GetLocalizedString("UserSessionService.InternalServerError"),
                    _loc.GetLocalizedString("UserSessionService.RevokeActiveSessionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
