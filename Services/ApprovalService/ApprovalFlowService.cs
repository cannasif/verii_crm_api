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
    public class ApprovalFlowService : IApprovalFlowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ApprovalFlowService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ApprovalFlowGetDto>>> GetAllApprovalFlowsAsync(PagedRequest request)
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

                var query = _unitOfWork.ApprovalFlows
                    .Query()
                    .Where(af => !af.IsDeleted)
                    .Include(af => af.CreatedByUser)
                    .Include(af => af.UpdatedByUser)
                    .Include(af => af.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(ApprovalFlow.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ApprovalFlowGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ApprovalFlowGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ApprovalFlowGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ApprovalFlowGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalFlowService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalFlowService.GetAllApprovalFlowsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalFlowGetDto>> GetApprovalFlowByIdAsync(long id)
        {
            try
            {
                var approvalFlow = await _unitOfWork.ApprovalFlows.GetByIdAsync(id).ConfigureAwait(false);
                if (approvalFlow == null)
                {
                    return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var approvalFlowWithNav = await _unitOfWork.ApprovalFlows
                    .Query()
                    .Include(af => af.CreatedByUser)
                    .Include(af => af.UpdatedByUser)
                    .Include(af => af.DeletedByUser)
                    .FirstOrDefaultAsync(af => af.Id == id && !af.IsDeleted).ConfigureAwait(false);

                var approvalFlowDto = _mapper.Map<ApprovalFlowGetDto>(approvalFlowWithNav ?? approvalFlow);
                return ApiResponse<ApprovalFlowGetDto>.SuccessResult(approvalFlowDto, _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalFlowService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalFlowService.GetApprovalFlowByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalFlowGetDto>> CreateApprovalFlowAsync(ApprovalFlowCreateDto approvalFlowCreateDto)
        {
            try
            {
                var approvalFlow = _mapper.Map<ApprovalFlow>(approvalFlowCreateDto);
                await _unitOfWork.ApprovalFlows.AddAsync(approvalFlow).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var approvalFlowWithNav = await _unitOfWork.ApprovalFlows
                    .Query()
                    .Include(af => af.CreatedByUser)
                    .Include(af => af.UpdatedByUser)
                    .Include(af => af.DeletedByUser)
                    .FirstOrDefaultAsync(af => af.Id == approvalFlow.Id && !af.IsDeleted).ConfigureAwait(false);

                if (approvalFlowWithNav == null)
                {
                    return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalFlowDto = _mapper.Map<ApprovalFlowGetDto>(approvalFlowWithNav);

                return ApiResponse<ApprovalFlowGetDto>.SuccessResult(approvalFlowDto, _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalFlowService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalFlowService.CreateApprovalFlowExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ApprovalFlowGetDto>> UpdateApprovalFlowAsync(long id, ApprovalFlowUpdateDto approvalFlowUpdateDto)
        {
            try
            {
                // Get tracked entity for update
                var approvalFlow = await _unitOfWork.ApprovalFlows.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (approvalFlow == null)
                {
                    return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(approvalFlowUpdateDto, approvalFlow);
                await _unitOfWork.ApprovalFlows.UpdateAsync(approvalFlow).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping (read-only)
                var approvalFlowWithNav = await _unitOfWork.ApprovalFlows
                    .Query()
                    .Include(af => af.CreatedByUser)
                    .Include(af => af.UpdatedByUser)
                    .Include(af => af.DeletedByUser)
                    .FirstOrDefaultAsync(af => af.Id == id).ConfigureAwait(false);

                if (approvalFlowWithNav == null)
                {
                    return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var approvalFlowDto = _mapper.Map<ApprovalFlowGetDto>(approvalFlowWithNav);

                return ApiResponse<ApprovalFlowGetDto>.SuccessResult(approvalFlowDto, _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ApprovalFlowGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalFlowService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalFlowService.UpdateApprovalFlowExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteApprovalFlowAsync(long id)
        {
            try
            {
                var deleted = await _unitOfWork.ApprovalFlows.SoftDeleteAsync(id).ConfigureAwait(false);
                if (!deleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ApprovalFlowService.ApprovalFlowDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ApprovalFlowService.InternalServerError"),
                    _localizationService.GetLocalizedString("ApprovalFlowService.DeleteApprovalFlowExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
