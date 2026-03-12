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
    public class OrderLineService : IOrderLineService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IUserService _userService;

        public OrderLineService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IUserService userService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _userService = userService;
        }

        public async Task<ApiResponse<PagedResponse<OrderLineGetDto>>> GetAllOrderLinesAsync(PagedRequest request)
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

                var query = _unitOfWork.OrderLines.Query()
                    .AsNoTracking()
                    .Where(ql => !ql.IsDeleted)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(OrderLine.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .Select(x => _mapper.Map<OrderLineGetDto>(x))
                    .ToListAsync().ConfigureAwait(false);

                var pagedResponse = new PagedResponse<OrderLineGetDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<OrderLineGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("OrderLineService.OrderLinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<OrderLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.GetAllExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<OrderLineGetDto>> GetOrderLineByIdAsync(long id)
        {
            try
            {
                var line = await _unitOfWork.OrderLines.GetByIdAsync(id).ConfigureAwait(false);
                if (line == null)
                {
                    return ApiResponse<OrderLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<OrderLineGetDto>(line);
                return ApiResponse<OrderLineGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("OrderLineService.OrderLineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.GetByIdExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<OrderLineDto>> CreateOrderLineAsync(CreateOrderLineDto createOrderLineDto)
        {
            try
            {
                var entity = _mapper.Map<OrderLine>(createOrderLineDto);
                entity.CreatedDate = DateTimeProvider.Now;

                await _unitOfWork.OrderLines.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<OrderLineDto>(entity);
                return ApiResponse<OrderLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("OrderLineService.OrderLineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<OrderLineDto>>> CreateOrderLinesAsync(List<CreateOrderLineDto> createOrderLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<OrderLine>>(createOrderLineDtos);
                await _unitOfWork.OrderLines.AddAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<OrderLineDto>>(entities);
                return ApiResponse<List<OrderLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("OrderLineService.OrderLinesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<OrderLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.CreateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<OrderLineDto>>> UpdateOrderLinesAsync(List<OrderLineDto> orderLineDtos)
        {
            try
            {
                var entities = _mapper.Map<List<OrderLine>>(orderLineDtos);
                await _unitOfWork.OrderLines.UpdateAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<List<OrderLineDto>>(entities);
                return ApiResponse<List<OrderLineDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("OrderLineService.OrderLinesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<OrderLineDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.UpdateExceptionMessage",
                     ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<OrderLineDto>> UpdateOrderLineAsync(long id, UpdateOrderLineDto updateOrderLineDto)
        {
            try
            {
                var existing = await _unitOfWork.OrderLines.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null)
                {
                    return ApiResponse<OrderLineDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateOrderLineDto, existing);
                existing.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.OrderLines.UpdateAsync(existing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var dto = _mapper.Map<OrderLineDto>(existing);
                return ApiResponse<OrderLineDto>.SuccessResult(dto, _localizationService.GetLocalizedString("OrderLineService.OrderLineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderLineDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.UpdateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<object>> DeleteOrderLineAsync(long id)
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
                var existing = await _unitOfWork.OrderLines
                    .Query()
                    .Where(x => x.Id == id && !x.IsDeleted)
                    .Select(x => new { x.RelatedProductKey, x.OrderId })
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var rowsAffected = await _unitOfWork.OrderLines.Query()
                    .Where(x => x.RelatedProductKey == existing.RelatedProductKey
                            && x.OrderId == existing.OrderId
                            && !x.IsDeleted)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.IsDeleted, true)
                        .SetProperty(p => p.DeletedDate, DateTime.UtcNow)
                        .SetProperty(p => p.DeletedBy, currentUserId)).ConfigureAwait(false);

                if (rowsAffected == 0)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotDeleted"),
                        _localizationService.GetLocalizedString("OrderLineService.OrderLineNotDeleted"),
                        StatusCodes.Status400BadRequest);
                }

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("OrderLineService.OrderLineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<OrderLineGetDto>>> GetOrderLinesByOrderIdAsync(long orderId)
        {
            try
            {
                var dtos = await _unitOfWork.OrderLines
                    .Query()
                    .Where(q => q.OrderId == orderId && !q.IsDeleted)
                    .Join(
                        _unitOfWork.Stocks.Query(),
                        ql => ql.ProductCode,
                        s => s.ErpStockCode,
                        (ql, s) => new
                        {
                            OrderLine = ql,
                            ProductName = s.StockName,
                            GroupCode = s.GrupKodu
                        })
                    .Select(x => new OrderLineGetDto
                    {
                        Id = x.OrderLine.Id,
                        CreatedDate = x.OrderLine.CreatedDate,
                        UpdatedDate = x.OrderLine.UpdatedDate,
                        IsDeleted = x.OrderLine.IsDeleted,
                        DeletedDate = x.OrderLine.DeletedDate,
                        OrderId = x.OrderLine.OrderId,
                        ProductCode = x.OrderLine.ProductCode,
                        ProductName = x.ProductName,
                        GroupCode = x.GroupCode,
                        Quantity = x.OrderLine.Quantity,
                        UnitPrice = x.OrderLine.UnitPrice,
                        DiscountRate1 = x.OrderLine.DiscountRate1,
                        DiscountAmount1 = x.OrderLine.DiscountAmount1,
                        DiscountRate2 = x.OrderLine.DiscountRate2,
                        DiscountAmount2 = x.OrderLine.DiscountAmount2,
                        DiscountRate3 = x.OrderLine.DiscountRate3,
                        DiscountAmount3 = x.OrderLine.DiscountAmount3,
                        VatRate = x.OrderLine.VatRate,
                        VatAmount = x.OrderLine.VatAmount,
                        LineTotal = x.OrderLine.LineTotal,
                        LineGrandTotal = x.OrderLine.LineGrandTotal,
                        Description = x.OrderLine.Description,
                        Description1 = x.OrderLine.Description1,
                        Description2 = x.OrderLine.Description2,
                        Description3 = x.OrderLine.Description3,
                        PricingRuleHeaderId = x.OrderLine.PricingRuleHeaderId,
                        RelatedStockId = x.OrderLine.RelatedStockId,
                        RelatedProductKey = x.OrderLine.RelatedProductKey,
                        IsMainRelatedProduct = x.OrderLine.IsMainRelatedProduct,
                        ErpProjectCode = x.OrderLine.ErpProjectCode,
                        ApprovalStatus = x.OrderLine.ApprovalStatus
                    })
                    .ToListAsync().ConfigureAwait(false);

                return ApiResponse<List<OrderLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("OrderLineService.OrderLinesByOrderRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<OrderLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("OrderLineService.GetByOrderIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
