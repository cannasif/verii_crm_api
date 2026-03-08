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
    public class ApprovalActionService : IApprovalActionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ApprovalActionService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ApprovalActionGetDto>>> GetAllApprovalActionsAsync(PagedRequest request)
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

                var query = _unitOfWork.ApprovalActions
                    .Query()
                    .Where(aa => !aa.IsDeleted)
                    .Include(aa => aa.CreatedByUser)
                    .Include(aa => aa.UpdatedByUser)
                    .Include(aa => aa.DeletedByUser)
                    .Include(aa => aa.ApprovedByUser)
                    .Include(aa => aa.ApprovalRequest)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(ApprovalAction.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ApprovalActionGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ApprovalActionGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ApprovalActionGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ApprovalActionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalActionService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalActionService.GetAllApprovalActionsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalActionGetDto>> GetApprovalActionByIdAsync(long id)
        {
            try
            {
                var approvalAction = await _unitOfWork.ApprovalActions.GetByIdAsync(id).ConfigureAwait(false);
                if (approvalAction == null)
                {
                    return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var approvalActionWithNav = await _unitOfWork.ApprovalActions
                    .Query()
                    .Include(aa => aa.CreatedByUser)
                    .Include(aa => aa.UpdatedByUser)
                    .Include(aa => aa.DeletedByUser)
                    .Include(aa => aa.ApprovedByUser)
                    .Include(aa => aa.ApprovalRequest)
                    .FirstOrDefaultAsync(aa => aa.Id == id && !aa.IsDeleted).ConfigureAwait(false);

                var approvalActionDto = _mapper.Map<ApprovalActionGetDto>(approvalActionWithNav ?? approvalAction);
                return ApiResponse<ApprovalActionGetDto>.SuccessResult(approvalActionDto, _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalActionService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalActionService.GetApprovalActionByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalActionGetDto>> CreateApprovalActionAsync(ApprovalActionCreateDto approvalActionCreateDto)
        {
            try
            {
                var approvalAction = _mapper.Map<ApprovalAction>(approvalActionCreateDto);
                await _unitOfWork.ApprovalActions.AddAsync(approvalAction).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var approvalActionWithNav = await _unitOfWork.ApprovalActions
                    .Query()
                    .Include(aa => aa.CreatedByUser)
                    .Include(aa => aa.UpdatedByUser)
                    .Include(aa => aa.DeletedByUser)
                    .Include(aa => aa.ApprovedByUser)
                    .Include(aa => aa.ApprovalRequest)
                    .FirstOrDefaultAsync(aa => aa.Id == approvalAction.Id && !aa.IsDeleted).ConfigureAwait(false);

                if (approvalActionWithNav == null)
                {
                    return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalActionDto = _mapper.Map<ApprovalActionGetDto>(approvalActionWithNav);

                return ApiResponse<ApprovalActionGetDto>.SuccessResult(approvalActionDto, _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalActionService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalActionService.CreateApprovalActionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalActionGetDto>> UpdateApprovalActionAsync(long id, ApprovalActionUpdateDto approvalActionUpdateDto)
        {
            try
            {
                // Get tracked entity for update
                var approvalAction = await _unitOfWork.ApprovalActions.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (approvalAction == null)
                {
                    return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(approvalActionUpdateDto, approvalAction);
                await _unitOfWork.ApprovalActions.UpdateAsync(approvalAction).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping (read-only)
                var approvalActionWithNav = await _unitOfWork.ApprovalActions
                    .Query()
                    .Include(aa => aa.CreatedByUser)
                    .Include(aa => aa.UpdatedByUser)
                    .Include(aa => aa.DeletedByUser)
                    .Include(aa => aa.ApprovedByUser)
                    .Include(aa => aa.ApprovalRequest)
                    .FirstOrDefaultAsync(aa => aa.Id == id).ConfigureAwait(false);

                if (approvalActionWithNav == null)
                {
                    return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalActionDto = _mapper.Map<ApprovalActionGetDto>(approvalActionWithNav);

                return ApiResponse<ApprovalActionGetDto>.SuccessResult(approvalActionDto, _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalActionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalActionService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalActionService.UpdateApprovalActionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteApprovalActionAsync(long id)
        {
            try
            {
                var deleted = await _unitOfWork.ApprovalActions.SoftDeleteAsync(id).ConfigureAwait(false);
                if (!deleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ApprovalActionService.ApprovalActionDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalActionService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalActionService.DeleteApprovalActionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
