using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using crm_api.Helpers;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class UserDiscountLimitService : IUserDiscountLimitService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public UserDiscountLimitService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<UserDiscountLimitDto>>> GetAllAsync(PagedRequest request)
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

                var query = _unitOfWork.UserDiscountLimits.Query()
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(UserDiscountLimit.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<UserDiscountLimitDto>(x)).ToList();

                var pagedResponse = new PagedResponse<UserDiscountLimitDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<UserDiscountLimitDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<UserDiscountLimitDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDiscountLimitDto>> GetByIdAsync(long id)
        {
            try
            {
                var userDiscountLimit = await _unitOfWork.UserDiscountLimits.GetByIdAsync(id).ConfigureAwait(false);
                if (userDiscountLimit == null)
                {
                    return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var userDiscountLimitWithNav = await _unitOfWork.UserDiscountLimits.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted).ConfigureAwait(false);

                var userDiscountLimitDto = _mapper.Map<UserDiscountLimitDto>(userDiscountLimitWithNav ?? userDiscountLimit);
                return ApiResponse<UserDiscountLimitDto>.SuccessResult(userDiscountLimitDto, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<UserDiscountLimitDto>>> GetBySalespersonIdAsync(long salespersonId)
        {
            try
            {
                var userDiscountLimits = await _unitOfWork.UserDiscountLimits.Query()
                    .AsNoTracking()
                    .Where(x => x.SalespersonId == salespersonId && !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);
                var userDiscountLimitDtos = _mapper.Map<List<UserDiscountLimitDto>>(userDiscountLimits);

                return ApiResponse<List<UserDiscountLimitDto>>.SuccessResult(userDiscountLimitDtos, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<UserDiscountLimitDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.GetBySalespersonIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<UserDiscountLimitDto>>> GetByErpProductGroupCodeAsync(string erpProductGroupCode)
        {
            try
            {
                var userDiscountLimits = await _unitOfWork.UserDiscountLimits
                    .FindAsync(x => x.ErpProductGroupCode == erpProductGroupCode).ConfigureAwait(false);
                var userDiscountLimitDtos = _mapper.Map<List<UserDiscountLimitDto>>(userDiscountLimits);

                return ApiResponse<List<UserDiscountLimitDto>>.SuccessResult(userDiscountLimitDtos, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<UserDiscountLimitDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.GetByErpProductGroupCodeExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<UserDiscountLimitDto>> GetBySalespersonAndGroupAsync(long salespersonId, string erpProductGroupCode)
        {
            try
            {
                var userDiscountLimit = await _unitOfWork.UserDiscountLimits
                    .FindAsync(x => x.SalespersonId == salespersonId && x.ErpProductGroupCode == erpProductGroupCode).ConfigureAwait(false);
                var result = userDiscountLimit.FirstOrDefault();
                
                if (result == null)
                {
                    return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var userDiscountLimitDto = _mapper.Map<UserDiscountLimitDto>(result);
                return ApiResponse<UserDiscountLimitDto>.SuccessResult(userDiscountLimitDto, _localizationService.GetLocalizedString("UserDiscountLimitRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.GetBySalespersonAndGroupExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDiscountLimitDto>> CreateAsync(CreateUserDiscountLimitDto createDto)
        {
            try
            {
                var userDiscountLimit = _mapper.Map<UserDiscountLimit>(createDto);
                userDiscountLimit.CreatedDate = DateTimeProvider.Now;
                
                await _unitOfWork.UserDiscountLimits.AddAsync(userDiscountLimit).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var userDiscountLimitWithNav = await _unitOfWork.UserDiscountLimits.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == userDiscountLimit.Id && !u.IsDeleted).ConfigureAwait(false);

                var userDiscountLimitDto = _mapper.Map<UserDiscountLimitDto>(userDiscountLimitWithNav ?? userDiscountLimit);
                return ApiResponse<UserDiscountLimitDto>.SuccessResult(userDiscountLimitDto, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDiscountLimitDto>> UpdateAsync(long id, UpdateUserDiscountLimitDto updateDto)
        {
            try
            {
                var existingUserDiscountLimit = await _unitOfWork.UserDiscountLimits.GetByIdAsync(id).ConfigureAwait(false);
                if (existingUserDiscountLimit == null)
                {
                    return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, existingUserDiscountLimit);
                existingUserDiscountLimit.UpdatedDate = DateTimeProvider.Now;
                
                await _unitOfWork.UserDiscountLimits.UpdateAsync(existingUserDiscountLimit).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var userDiscountLimitWithNav = await _unitOfWork.UserDiscountLimits.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == existingUserDiscountLimit.Id && !u.IsDeleted).ConfigureAwait(false);

                var userDiscountLimitDto = _mapper.Map<UserDiscountLimitDto>(userDiscountLimitWithNav ?? existingUserDiscountLimit);
                return ApiResponse<UserDiscountLimitDto>.SuccessResult(userDiscountLimitDto, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDiscountLimitDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var userDiscountLimit = await _unitOfWork.UserDiscountLimits.GetByIdAsync(id).ConfigureAwait(false);
                if (userDiscountLimit == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitNotFound"),
                        StatusCodes.Status404NotFound);
                }

                userDiscountLimit.IsDeleted = true;
                userDiscountLimit.DeletedDate = DateTimeProvider.Now;
                
                await _unitOfWork.UserDiscountLimits.UpdateAsync(userDiscountLimit).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                
                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> ExistsAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.UserDiscountLimits.ExistsAsync(id).ConfigureAwait(false);
                return ApiResponse<bool>.SuccessResult(exists, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitExistsChecked"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.ExistsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> ExistsBySalespersonAndGroupAsync(long salespersonId, string erpProductGroupCode)
        {
            try
            {
                var userDiscountLimits = await _unitOfWork.UserDiscountLimits
                    .FindAsync(x => x.SalespersonId == salespersonId && x.ErpProductGroupCode == erpProductGroupCode).ConfigureAwait(false);
                var exists = userDiscountLimits.Any();

                return ApiResponse<bool>.SuccessResult(exists, _localizationService.GetLocalizedString("UserDiscountLimitService.UserDiscountLimitExistsChecked"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDiscountLimitService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserDiscountLimitService.ExistsBySalespersonAndGroupExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
        
    }
}
