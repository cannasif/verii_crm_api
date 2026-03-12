using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class OrderNotesService : IOrderNotesService
    {
        private const int MaxNoteCount = 15;
        private const int MaxNoteLength = 100;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public OrderNotesService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<OrderNotesGetDto>>> GetAllOrderNotesAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.OrderNotes.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(OrderNotes.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .Select(x => _mapper.Map<OrderNotesGetDto>(x))
                    .ToListAsync().ConfigureAwait(false);

                var pagedResponse = new PagedResponse<OrderNotesGetDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<OrderNotesGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<OrderNotesGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<OrderNotesGetDto>> GetOrderNotesByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.OrderNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<OrderNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<OrderNotesGetDto>(entity);
                return ApiResponse<OrderNotesGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<OrderNotesGetDto>> GetNotesByOrderIdAsync(long orderId)
        {
            try
            {
                var orderExists = await _unitOfWork.Orders.Query().AnyAsync(x => x.Id == orderId && !x.IsDeleted).ConfigureAwait(false);
                if (!orderExists)
                {
                    return ApiResponse<OrderNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entity = await _unitOfWork.OrderNotes.Query().AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId && !x.IsDeleted).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<OrderNotesGetDto>.SuccessResult(
                        new OrderNotesGetDto { OrderId = orderId },
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesRetrieved"));
                }

                return ApiResponse<OrderNotesGetDto>.SuccessResult(
                    _mapper.Map<OrderNotesGetDto>(entity),
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<OrderNotesDto>> CreateOrderNotesAsync(CreateOrderNotesDto createOrderNotesDto)
        {
            try
            {
                var orderExists = await _unitOfWork.Orders.Query().AnyAsync(x => x.Id == createOrderNotesDto.OrderId && !x.IsDeleted).ConfigureAwait(false);
                if (!orderExists)
                {
                    return ApiResponse<OrderNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exists = await _unitOfWork.OrderNotes.Query().AnyAsync(x => x.OrderId == createOrderNotesDto.OrderId && !x.IsDeleted).ConfigureAwait(false);
                if (exists)
                {
                    return ApiResponse<OrderNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesAlreadyExist"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesAlreadyExist"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = _mapper.Map<OrderNotes>(createOrderNotesDto);
                entity.CreatedDate = DateTimeProvider.Now;
                await _unitOfWork.OrderNotes.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<OrderNotesDto>.SuccessResult(
                    _mapper.Map<OrderNotesDto>(entity),
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderNotesDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<OrderNotesDto>> UpdateOrderNotesAsync(long id, UpdateOrderNotesDto updateOrderNotesDto)
        {
            try
            {
                var existing = await _unitOfWork.OrderNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null || existing.IsDeleted)
                {
                    return ApiResponse<OrderNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateOrderNotesDto, existing);
                existing.UpdatedDate = DateTimeProvider.Now;
                await _unitOfWork.OrderNotes.UpdateAsync(existing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<OrderNotesDto>.SuccessResult(
                    _mapper.Map<OrderNotesDto>(existing),
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderNotesDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<OrderNotesGetDto>> UpdateNotesListByOrderIdAsync(long orderId, UpdateOrderNotesListDto request)
        {
            try
            {
                var orderExists = await _unitOfWork.Orders.Query().AnyAsync(x => x.Id == orderId && !x.IsDeleted).ConfigureAwait(false);
                if (!orderExists)
                {
                    return ApiResponse<OrderNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var notes = (request?.Notes ?? new List<string>())
                    .Where(x => x != null)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (notes.Count > MaxNoteCount)
                {
                    var maxCountMessage = _localizationService.GetLocalizedString("OrderNotesService.MaxNoteCountExceeded", MaxNoteCount);
                    return ApiResponse<OrderNotesGetDto>.ErrorResult(maxCountMessage, maxCountMessage, StatusCodes.Status400BadRequest);
                }

                if (notes.Any(x => x.Length > MaxNoteLength))
                {
                    var maxLengthMessage = _localizationService.GetLocalizedString("OrderNotesService.MaxNoteLengthExceeded", MaxNoteLength);
                    return ApiResponse<OrderNotesGetDto>.ErrorResult(maxLengthMessage, maxLengthMessage, StatusCodes.Status400BadRequest);
                }

                var entity = await _unitOfWork.OrderNotes.Query().FirstOrDefaultAsync(x => x.OrderId == orderId && !x.IsDeleted).ConfigureAwait(false);
                if (entity == null)
                {
                    entity = new OrderNotes { OrderId = orderId, CreatedDate = DateTimeProvider.Now };
                    await _unitOfWork.OrderNotes.AddAsync(entity).ConfigureAwait(false);
                }
                else
                {
                    entity.UpdatedDate = DateTimeProvider.Now;
                    await _unitOfWork.OrderNotes.UpdateAsync(entity).ConfigureAwait(false);
                }

                entity.Note1 = notes.ElementAtOrDefault(0);
                entity.Note2 = notes.ElementAtOrDefault(1);
                entity.Note3 = notes.ElementAtOrDefault(2);
                entity.Note4 = notes.ElementAtOrDefault(3);
                entity.Note5 = notes.ElementAtOrDefault(4);
                entity.Note6 = notes.ElementAtOrDefault(5);
                entity.Note7 = notes.ElementAtOrDefault(6);
                entity.Note8 = notes.ElementAtOrDefault(7);
                entity.Note9 = notes.ElementAtOrDefault(8);
                entity.Note10 = notes.ElementAtOrDefault(9);
                entity.Note11 = notes.ElementAtOrDefault(10);
                entity.Note12 = notes.ElementAtOrDefault(11);
                entity.Note13 = notes.ElementAtOrDefault(12);
                entity.Note14 = notes.ElementAtOrDefault(13);
                entity.Note15 = notes.ElementAtOrDefault(14);

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<OrderNotesGetDto>.SuccessResult(
                    _mapper.Map<OrderNotesGetDto>(entity),
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<OrderNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteOrderNotesAsync(long id)
        {
            try
            {
                var existing = await _unitOfWork.OrderNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null || existing.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        _localizationService.GetLocalizedString("OrderNotesService.OrderNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.OrderNotes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("OrderNotesService.OrderNotesDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("OrderService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
