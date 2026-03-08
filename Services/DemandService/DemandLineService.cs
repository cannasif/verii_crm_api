using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using System;

namespace crm_api.Services
{
    public class DemandLineService : IDemandLineService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IUserService _userService;

        public DemandLineService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _userService = userService;
        }

        public async Task<ApiResponse<PagedResponse<DemandLineGetDto>>> GetAllDemandLinesAsync(PagedRequest request)
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

                var query = _unitOfWork.DemandLines.Query()
                    .AsNoTracking()
                    .Where(ql => !ql.IsDeleted)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(DemandLine.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<DemandLineGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DemandLineGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DemandLineGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DemandLineService.DemandLinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DemandLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.GetAllExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<DemandLineGetDto>> GetDemandLineByIdAsync(long id)
        {
            try
            {
                var line = await _unitOfWork.DemandLines.GetByIdAsync(id).ConfigureAwait(false);
                if (line == null)
                {
                    return ApiResponse<DemandLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<DemandLineGetDto>(line);
                return ApiResponse<DemandLineGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("DemandLineService.DemandLineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.GetByIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<DemandLineDto>> CreateDemandLineAsync(CreateDemandLineDto createDemandLineDto)
        {
            try
            {
                var entity = _mapper.Map<DemandLine>(createDemandLineDto);
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.DemandLines.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<DemandLineDto>(entity);
                return ApiResponse<DemandLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("DemandLineService.DemandLineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<DemandLineDto>>> CreateDemandLinesAsync(List<CreateDemandLineDto> createDemandLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<DemandLine>>(createDemandLineDtos);
                await _unitOfWork.DemandLines.AddAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<DemandLineDto>>(entities);
                return ApiResponse<List<DemandLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("DemandLineService.DemandLinesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<DemandLineDto>>> UpdateDemandLinesAsync(List<DemandLineDto> demandLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<DemandLine>>(demandLineDtos);
                await _unitOfWork.DemandLines.UpdateAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<DemandLineDto>>(entities);
                return ApiResponse<List<DemandLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("DemandLineService.DemandLinesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.UpdateExceptionMessage",
                     ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<DemandLineDto>> UpdateDemandLineAsync(long id, UpdateDemandLineDto updateDemandLineDto)
        {
            try
            {
                var existing = await _unitOfWork.DemandLines.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null)
                {
                    return ApiResponse<DemandLineDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDemandLineDto, existing);
                existing.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.DemandLines.UpdateAsync(existing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<DemandLineDto>(existing);
                return ApiResponse<DemandLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("DemandLineService.DemandLineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.UpdateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<object>> DeleteDemandLineAsync(long id)
        {
            try
            {
                var currentUserResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!currentUserResponse.Success)
                {
                    return ApiResponse<object>.ErrorResult(
                        currentUserResponse.Message,
                        currentUserResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                long currentUserId = currentUserResponse.Data;
                var existing = await _unitOfWork.DemandLines
                    .Query()
                    .Where(x => x.Id == id && !x.IsDeleted)
                    .Select(x => new { x.RelatedProductKey, x.DemandId })
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var rowsAffected = await _unitOfWork.DemandLines.Query()
                    .Where(x => x.RelatedProductKey == existing.RelatedProductKey
                            && x.DemandId == existing.DemandId
                            && !x.IsDeleted)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.IsDeleted, true)
                        .SetProperty(p => p.DeletedDate, DateTime.UtcNow)
                        .SetProperty(p => p.DeletedBy, currentUserId)).ConfigureAwait(false);

                if (rowsAffected == 0)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotDeleted"),
                        _localizationService.GetLocalizedString("DemandLineService.DemandLineNotDeleted"),
                        StatusCodes.Status400BadRequest);
                }

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("DemandLineService.DemandLineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<DemandLineGetDto>>> GetDemandLinesByDemandIdAsync(long demandId)
        {
            try
            {
                var dtos = await _unitOfWork.DemandLines
                    .Query()
                    .Where(q => q.DemandId == demandId && !q.IsDeleted)
                    .Join(
                        _unitOfWork.Stocks.Query(),
                        ql => ql.ProductCode,
                        s => s.ErpStockCode,
                        (ql, s) => new
                        {
                            DemandLine = ql,
                            ProductName = s.StockName,
                            GroupCode = s.GrupKodu
                        })
                    .Select(x => new DemandLineGetDto
                    {
                        Id = x.DemandLine.Id,
                        CreatedDate = x.DemandLine.CreatedDate,
                        UpdatedDate = x.DemandLine.UpdatedDate,
                        IsDeleted = x.DemandLine.IsDeleted,
                        DeletedDate = x.DemandLine.DeletedDate,
                        DemandId = x.DemandLine.DemandId,
                        ProductCode = x.DemandLine.ProductCode,
                        ProductName = x.ProductName,
                        GroupCode = x.GroupCode,
                        Quantity = x.DemandLine.Quantity,
                        UnitPrice = x.DemandLine.UnitPrice,
                        DiscountRate1 = x.DemandLine.DiscountRate1,
                        DiscountAmount1 = x.DemandLine.DiscountAmount1,
                        DiscountRate2 = x.DemandLine.DiscountRate2,
                        DiscountAmount2 = x.DemandLine.DiscountAmount2,
                        DiscountRate3 = x.DemandLine.DiscountRate3,
                        DiscountAmount3 = x.DemandLine.DiscountAmount3,
                        VatRate = x.DemandLine.VatRate,
                        VatAmount = x.DemandLine.VatAmount,
                        LineTotal = x.DemandLine.LineTotal,
                        LineGrandTotal = x.DemandLine.LineGrandTotal,
                        Description = x.DemandLine.Description,
                        Description1 = x.DemandLine.Description1,
                        Description2 = x.DemandLine.Description2,
                        Description3 = x.DemandLine.Description3,
                        PricingRuleHeaderId = x.DemandLine.PricingRuleHeaderId,
                        RelatedStockId = x.DemandLine.RelatedStockId,
                        RelatedProductKey = x.DemandLine.RelatedProductKey,
                        IsMainRelatedProduct = x.DemandLine.IsMainRelatedProduct,
                        ErpProjectCode = x.DemandLine.ErpProjectCode,
                        ApprovalStatus = x.DemandLine.ApprovalStatus
                    })
                    .ToListAsync().ConfigureAwait(false);

                return ApiResponse<List<DemandLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("DemandLineService.DemandLinesByDemandRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandLineService.GetByDemandIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }
    }
}
