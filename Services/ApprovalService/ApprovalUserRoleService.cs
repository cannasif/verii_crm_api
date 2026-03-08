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
    public class ApprovalUserRoleService : IApprovalUserRoleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ApprovalUserRoleService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ApprovalUserRoleGetDto>>> GetAllApprovalUserRolesAsync(PagedRequest request)
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

                var columnMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "approvalRoleName", "ApprovalRole.Name" }
                };

                var query = _unitOfWork.ApprovalUserRoles
                    .Query()
                    .Where(aur => !aur.IsDeleted)
                    .Include(aur => aur.CreatedByUser)
                    .Include(aur => aur.UpdatedByUser)
                    .Include(aur => aur.DeletedByUser)
                    .Include(aur => aur.User)
                    .Include(aur => aur.ApprovalRole)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(ApprovalUserRole.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ApprovalUserRoleGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ApprovalUserRoleGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ApprovalUserRoleGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRolesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ApprovalUserRoleGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.GetAllApprovalUserRolesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalUserRoleGetDto>> GetApprovalUserRoleByIdAsync(long id)
        {
            try
            {
                var approvalUserRole = await _unitOfWork.ApprovalUserRoles.GetByIdAsync(id).ConfigureAwait(false);
                if (approvalUserRole == null)
                {
                    return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var approvalUserRoleWithNav = await _unitOfWork.ApprovalUserRoles
                    .Query()
                    .Include(aur => aur.CreatedByUser)
                    .Include(aur => aur.UpdatedByUser)
                    .Include(aur => aur.DeletedByUser)
                    .Include(aur => aur.User)
                    .Include(aur => aur.ApprovalRole)
                    .FirstOrDefaultAsync(aur => aur.Id == id && !aur.IsDeleted).ConfigureAwait(false);

                var approvalUserRoleDto = _mapper.Map<ApprovalUserRoleGetDto>(approvalUserRoleWithNav ?? approvalUserRole);
                return ApiResponse<ApprovalUserRoleGetDto>.SuccessResult(approvalUserRoleDto, _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.GetApprovalUserRoleByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalUserRoleGetDto>> CreateApprovalUserRoleAsync(ApprovalUserRoleCreateDto approvalUserRoleCreateDto)
        {
            try
            {
                var approvalUserRole = _mapper.Map<ApprovalUserRole>(approvalUserRoleCreateDto);
                await _unitOfWork.ApprovalUserRoles.AddAsync(approvalUserRole).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var approvalUserRoleWithNav = await _unitOfWork.ApprovalUserRoles
                    .Query()
                    .Include(aur => aur.CreatedByUser)
                    .Include(aur => aur.UpdatedByUser)
                    .Include(aur => aur.DeletedByUser)
                    .Include(aur => aur.User)
                    .Include(aur => aur.ApprovalRole)
                    .FirstOrDefaultAsync(aur => aur.Id == approvalUserRole.Id && !aur.IsDeleted).ConfigureAwait(false);

                if (approvalUserRoleWithNav == null)
                {
                    return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalUserRoleDto = _mapper.Map<ApprovalUserRoleGetDto>(approvalUserRoleWithNav);

                return ApiResponse<ApprovalUserRoleGetDto>.SuccessResult(approvalUserRoleDto, _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.CreateApprovalUserRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalUserRoleGetDto>> UpdateApprovalUserRoleAsync(long id, ApprovalUserRoleUpdateDto approvalUserRoleUpdateDto)
        {
            try
            {
                // Get tracked entity for update
                var approvalUserRole = await _unitOfWork.ApprovalUserRoles.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (approvalUserRole == null)
                {
                    return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(approvalUserRoleUpdateDto, approvalUserRole);
                await _unitOfWork.ApprovalUserRoles.UpdateAsync(approvalUserRole).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping (read-only)
                var approvalUserRoleWithNav = await _unitOfWork.ApprovalUserRoles
                    .Query()
                    .Include(aur => aur.CreatedByUser)
                    .Include(aur => aur.UpdatedByUser)
                    .Include(aur => aur.DeletedByUser)
                    .Include(aur => aur.User)
                    .Include(aur => aur.ApprovalRole)
                    .FirstOrDefaultAsync(aur => aur.Id == id).ConfigureAwait(false);

                if (approvalUserRoleWithNav == null)
                {
                    return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalUserRoleDto = _mapper.Map<ApprovalUserRoleGetDto>(approvalUserRoleWithNav);

                return ApiResponse<ApprovalUserRoleGetDto>.SuccessResult(approvalUserRoleDto, _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalUserRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.UpdateApprovalUserRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteApprovalUserRoleAsync(long id)
        {
            try
            {
                var deleted = await _unitOfWork.ApprovalUserRoles.SoftDeleteAsync(id).ConfigureAwait(false);
                if (!deleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ApprovalUserRoleService.ApprovalUserRoleDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalUserRoleService.DeleteApprovalUserRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
