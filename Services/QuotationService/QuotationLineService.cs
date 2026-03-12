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
    public class QuotationLineService : IQuotationLineService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IUserService _userService;

        public QuotationLineService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _userService = userService;
        }

        public async Task<ApiResponse<PagedResponse<QuotationLineGetDto>>> GetAllQuotationLinesAsync(PagedRequest request)
        {
            try
            {
                var query = _unitOfWork.QuotationLines.Query()
                    .AsNoTracking()
                    .Where(ql => !ql.IsDeleted)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(QuotationLine.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .Select(x => _mapper.Map<QuotationLineGetDto>(x))
                    .ToListAsync().ConfigureAwait(false);

                var pagedResponse = new PagedResponse<QuotationLineGetDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<QuotationLineGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("QuotationLineService.QuotationLinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<QuotationLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.GetAllExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<QuotationLineGetDto>> GetQuotationLineByIdAsync(long id)
        {
            try
            {
                var line = await _unitOfWork.QuotationLines.GetByIdAsync(id).ConfigureAwait(false);
                if (line == null)
                {
                    return ApiResponse<QuotationLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<QuotationLineGetDto>(line);
                return ApiResponse<QuotationLineGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("QuotationLineService.QuotationLineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.GetByIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<QuotationLineDto>> CreateQuotationLineAsync(CreateQuotationLineDto createQuotationLineDto)
        {
            try
            {
                var entity = _mapper.Map<QuotationLine>(createQuotationLineDto);
                entity.CreatedDate = DateTimeProvider.Now;

                await _unitOfWork.QuotationLines.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<QuotationLineDto>(entity);
                return ApiResponse<QuotationLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("QuotationLineService.QuotationLineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<QuotationLineDto>>> CreateQuotationLinesAsync(List<CreateQuotationLineDto> createQuotationLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<QuotationLine>>(createQuotationLineDtos);
                await _unitOfWork.QuotationLines.AddAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<QuotationLineDto>>(entities);
                return ApiResponse<List<QuotationLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("QuotationLineService.QuotationLinesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<QuotationLineDto>>> UpdateQuotationLinesAsync(List<QuotationLineDto> quotationLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<QuotationLine>>(quotationLineDtos);
                await _unitOfWork.QuotationLines.UpdateAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<QuotationLineDto>>(entities);
                return ApiResponse<List<QuotationLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("QuotationLineService.QuotationLinesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.UpdateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<QuotationLineDto>> UpdateQuotationLineAsync(long id, UpdateQuotationLineDto updateQuotationLineDto)
        {
            try
            {
                var existing = await _unitOfWork.QuotationLines.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null)
                {
                    return ApiResponse<QuotationLineDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateQuotationLineDto, existing);
                existing.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.QuotationLines.UpdateAsync(existing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<QuotationLineDto>(existing);
                return ApiResponse<QuotationLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("QuotationLineService.QuotationLineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.UpdateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }
        public async Task<ApiResponse<object>> DeleteQuotationLineAsync(long id)
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
                var existing = await _unitOfWork.QuotationLines
                    .Query()
                    .Where(x => x.Id == id && !x.IsDeleted)
                    .Select(x => new { x.RelatedProductKey, x.QuotationId })
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var rowsAffected = await _unitOfWork.QuotationLines.Query()
                    .Where(x => x.RelatedProductKey == existing.RelatedProductKey
                            && x.QuotationId == existing.QuotationId
                            && !x.IsDeleted)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.IsDeleted, true)
                        .SetProperty(p => p.DeletedDate, DateTime.UtcNow)
                        .SetProperty(p => p.DeletedBy, currentUserId)).ConfigureAwait(false);

                if (rowsAffected == 0)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotDeleted"),
                        _localizationService.GetLocalizedString("QuotationLineService.QuotationLineNotDeleted"),
                        StatusCodes.Status400BadRequest);
                }

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("QuotationLineService.QuotationLineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }


        public async Task<ApiResponse<List<QuotationLineGetDto>>> GetQuotationLinesByQuotationIdAsync(long quotationId)
        {
            try
            {
                var dtos = await _unitOfWork.QuotationLines
                    .Query()
                    .Where(q => q.QuotationId == quotationId && !q.IsDeleted)
                    .Join(
                        _unitOfWork.Stocks.Query(),
                        ql => ql.ProductCode,
                        s => s.ErpStockCode,
                        (ql, s) => new
                        {
                            QuotationLine = ql,
                            ProductName = s.StockName,
                            GroupCode = s.GrupKodu
                        })
                    .Select(x => new QuotationLineGetDto
                    {
                        Id = x.QuotationLine.Id,
                        CreatedDate = x.QuotationLine.CreatedDate,
                        UpdatedDate = x.QuotationLine.UpdatedDate,
                        IsDeleted = x.QuotationLine.IsDeleted,
                        DeletedDate = x.QuotationLine.DeletedDate,
                        QuotationId = x.QuotationLine.QuotationId,
                        ProductCode = x.QuotationLine.ProductCode,
                        ProductName = x.ProductName,
                        GroupCode = x.GroupCode,
                        Quantity = x.QuotationLine.Quantity,
                        UnitPrice = x.QuotationLine.UnitPrice,
                        DiscountRate1 = x.QuotationLine.DiscountRate1,
                        DiscountAmount1 = x.QuotationLine.DiscountAmount1,
                        DiscountRate2 = x.QuotationLine.DiscountRate2,
                        DiscountAmount2 = x.QuotationLine.DiscountAmount2,
                        DiscountRate3 = x.QuotationLine.DiscountRate3,
                        DiscountAmount3 = x.QuotationLine.DiscountAmount3,
                        VatRate = x.QuotationLine.VatRate,
                        VatAmount = x.QuotationLine.VatAmount,
                        LineTotal = x.QuotationLine.LineTotal,
                        LineGrandTotal = x.QuotationLine.LineGrandTotal,
                        Description = x.QuotationLine.Description,
                        Description1 = x.QuotationLine.Description1,
                        Description2 = x.QuotationLine.Description2,
                        Description3 = x.QuotationLine.Description3,
                        PricingRuleHeaderId = x.QuotationLine.PricingRuleHeaderId,
                        RelatedStockId = x.QuotationLine.RelatedStockId,
                        RelatedProductKey = x.QuotationLine.RelatedProductKey,
                        IsMainRelatedProduct = x.QuotationLine.IsMainRelatedProduct,
                        ErpProjectCode = x.QuotationLine.ErpProjectCode,
                        ApprovalStatus = x.QuotationLine.ApprovalStatus
                    })
                    .ToListAsync().ConfigureAwait(false);

                return ApiResponse<List<QuotationLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("QuotationLineService.QuotationLinesByQuotationRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationLineService.GetByQuotationIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }
  
    }
}
