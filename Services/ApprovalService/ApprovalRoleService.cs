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
    public class ApprovalRoleService : IApprovalRoleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ApprovalRoleService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ApprovalRoleGetDto>>> GetAllApprovalRolesAsync(PagedRequest request)
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
                    { "approvalRoleGroupName", "ApprovalRoleGroup.Name" }
                };

                var query = _unitOfWork.ApprovalRoles
                    .Query()
                    .Where(ar => !ar.IsDeleted)
                    .Include(ar => ar.CreatedByUser)
                    .Include(ar => ar.UpdatedByUser)
                    .Include(ar => ar.DeletedByUser)
                    .Include(ar => ar.ApprovalRoleGroup)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(ApprovalRole.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ApprovalRoleGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ApprovalRoleGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ApprovalRoleGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRolesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ApprovalRoleGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalRoleService.GetAllApprovalRolesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalRoleGetDto>> GetApprovalRoleByIdAsync(long id)
        {
            try
            {
                var approvalRole = await _unitOfWork.ApprovalRoles.GetByIdAsync(id).ConfigureAwait(false);
                if (approvalRole == null)
                {
                    return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var approvalRoleWithNav = await _unitOfWork.ApprovalRoles
                    .Query()
                    .Include(ar => ar.CreatedByUser)
                    .Include(ar => ar.UpdatedByUser)
                    .Include(ar => ar.DeletedByUser)
                    .Include(ar => ar.ApprovalRoleGroup)
                    .FirstOrDefaultAsync(ar => ar.Id == id && !ar.IsDeleted).ConfigureAwait(false);

                var approvalRoleDto = _mapper.Map<ApprovalRoleGetDto>(approvalRoleWithNav ?? approvalRole);
                return ApiResponse<ApprovalRoleGetDto>.SuccessResult(approvalRoleDto, _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalRoleService.GetApprovalRoleByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalRoleGetDto>> CreateApprovalRoleAsync(ApprovalRoleCreateDto approvalRoleCreateDto)
        {
            try
            {
                var approvalRole = _mapper.Map<ApprovalRole>(approvalRoleCreateDto);
                await _unitOfWork.ApprovalRoles.AddAsync(approvalRole).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var approvalRoleWithNav = await _unitOfWork.ApprovalRoles
                    .Query()
                    .Include(ar => ar.CreatedByUser)
                    .Include(ar => ar.UpdatedByUser)
                    .Include(ar => ar.DeletedByUser)
                    .Include(ar => ar.ApprovalRoleGroup)
                    .FirstOrDefaultAsync(ar => ar.Id == approvalRole.Id && !ar.IsDeleted).ConfigureAwait(false);

                if (approvalRoleWithNav == null)
                {
                    return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalRoleDto = _mapper.Map<ApprovalRoleGetDto>(approvalRoleWithNav);

                return ApiResponse<ApprovalRoleGetDto>.SuccessResult(approvalRoleDto, _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalRoleService.CreateApprovalRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalRoleGetDto>> UpdateApprovalRoleAsync(long id, ApprovalRoleUpdateDto approvalRoleUpdateDto)
        {
            try
            {
                // Get tracked entity for update
                var approvalRole = await _unitOfWork.ApprovalRoles.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (approvalRole == null)
                {
                    return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(approvalRoleUpdateDto, approvalRole);
                await _unitOfWork.ApprovalRoles.UpdateAsync(approvalRole).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping (read-only)
                var approvalRoleWithNav = await _unitOfWork.ApprovalRoles
                    .Query()
                    .Include(ar => ar.CreatedByUser)
                    .Include(ar => ar.UpdatedByUser)
                    .Include(ar => ar.DeletedByUser)
                    .Include(ar => ar.ApprovalRoleGroup)
                    .FirstOrDefaultAsync(ar => ar.Id == id).ConfigureAwait(false);

                if (approvalRoleWithNav == null)
                {
                    return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalRoleDto = _mapper.Map<ApprovalRoleGetDto>(approvalRoleWithNav);

                return ApiResponse<ApprovalRoleGetDto>.SuccessResult(approvalRoleDto, _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalRoleGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalRoleService.UpdateApprovalRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteApprovalRoleAsync(long id)
        {
            try
            {
                var deleted = await _unitOfWork.ApprovalRoles.SoftDeleteAsync(id).ConfigureAwait(false);
                if (!deleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ApprovalRoleService.ApprovalRoleDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalRoleService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalRoleService.DeleteApprovalRoleExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
