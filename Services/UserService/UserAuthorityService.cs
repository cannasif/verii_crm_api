using AutoMapper;
using Microsoft.EntityFrameworkCore;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class UserAuthorityService : IUserAuthorityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public UserAuthorityService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<UserAuthorityDto>>> GetAllAsync(PagedRequest request)
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

                var query = _unitOfWork.UserAuthorities.Query()
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(UserAuthority.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<UserAuthorityDto>(x)).ToList();

                var pagedResponse = new PagedResponse<UserAuthorityDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<UserAuthorityDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("UserAuthorityService.UserAuthoritiesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<UserAuthorityDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorRetrievingUserAuthorities"),
                    _localizationService.GetLocalizedString("UserAuthorityService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserAuthorityDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.UserAuthorities.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<UserAuthorityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserAuthorities.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted).ConfigureAwait(false);

                var dto = _mapper.Map<UserAuthorityDto>(entityWithNav ?? entity);
                return ApiResponse<UserAuthorityDto>.SuccessResult(dto, _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserAuthorityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorRetrievingUserAuthority"),
                    _localizationService.GetLocalizedString("UserAuthorityService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserAuthorityDto>> CreateAsync(CreateUserAuthorityDto createDto)
        {
            try
            {
                var entity = _mapper.Map<UserAuthority>(createDto);
                await _unitOfWork.UserAuthorities.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserAuthorities.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == entity.Id && !u.IsDeleted).ConfigureAwait(false);

                var dto = _mapper.Map<UserAuthorityDto>(entityWithNav ?? entity);
                return ApiResponse<UserAuthorityDto>.SuccessResult(dto, _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserAuthorityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorCreatingUserAuthority"),
                    _localizationService.GetLocalizedString("UserAuthorityService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserAuthorityDto>> UpdateAsync(long id, UpdateUserAuthorityDto updateDto)
        {
            try
            {
                var entity = await _unitOfWork.UserAuthorities.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<UserAuthorityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, entity);
                await _unitOfWork.UserAuthorities.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserAuthorities.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == entity.Id && !u.IsDeleted).ConfigureAwait(false);

                var dto = _mapper.Map<UserAuthorityDto>(entityWithNav ?? entity);
                return ApiResponse<UserAuthorityDto>.SuccessResult(dto, _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserAuthorityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorUpdatingUserAuthority"),
                    _localizationService.GetLocalizedString("UserAuthorityService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> SoftDeleteAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.UserAuthorities.ExistsAsync(id).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.UserAuthorities.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("UserAuthorityService.UserAuthorityDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorDeletingUserAuthority"),
                    _localizationService.GetLocalizedString("UserAuthorityService.SoftDeleteExceptionMessage", ex.Message),
                    500);
            }
        }

        public async Task<ApiResponse<bool>> ExistsAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.UserAuthorities.ExistsAsync(id).ConfigureAwait(false);
                return ApiResponse<bool>.SuccessResult(exists, _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("UserAuthorityService.ErrorCheckingExists"),
                    _localizationService.GetLocalizedString("UserAuthorityService.ExistsExceptionMessage", ex.Message),
                    500);
            }
        }
    }
}
