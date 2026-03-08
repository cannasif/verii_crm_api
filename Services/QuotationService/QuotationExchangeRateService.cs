using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class QuotationExchangeRateService : IQuotationExchangeRateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public QuotationExchangeRateService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<QuotationExchangeRateGetDto>>> GetAllQuotationExchangeRatesAsync(PagedRequest request)
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

                var query = _unitOfWork.QuotationExchangeRates
                    .Query()
                    .Include(e => e.Quotation)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(QuotationExchangeRate.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync();

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync();

                var dtos = items.Select(x => _mapper.Map<QuotationExchangeRateGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<QuotationExchangeRateGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<QuotationExchangeRateGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("QuotationExchangeRateService.RatesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<QuotationExchangeRateGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.GetAllRatesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuotationExchangeRateGetDto>> GetQuotationExchangeRateByIdAsync(long id)
        {
            try
            {
                var exchangeRate = await _unitOfWork.QuotationExchangeRates
                    .Query()
                    .Include(e => e.Quotation)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (exchangeRate == null)
                {
                    return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<QuotationExchangeRateGetDto>(exchangeRate);
                return ApiResponse<QuotationExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("QuotationExchangeRateService.RateRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.GetRateByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuotationExchangeRateGetDto>> CreateQuotationExchangeRateAsync(QuotationExchangeRateCreateDto createDto)
        {
            try
            {
                var exchangeRate = _mapper.Map<QuotationExchangeRate>(createDto);
                await _unitOfWork.QuotationExchangeRates.AddAsync(exchangeRate);
                await _unitOfWork.SaveChangesAsync();

                // Reload with navigation properties
                var exchangeRateWithNav = await _unitOfWork.QuotationExchangeRates
                    .Query()
                    .Include(e => e.Quotation)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == exchangeRate.Id && !e.IsDeleted);

                if (exchangeRateWithNav == null)
                {
                    return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<QuotationExchangeRateGetDto>(exchangeRateWithNav);
                return ApiResponse<QuotationExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("QuotationExchangeRateService.RateCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.CreateRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuotationExchangeRateGetDto>> UpdateQuotationExchangeRateAsync(long id, QuotationExchangeRateUpdateDto updateDto)
        {
            try
            {
                var exchangeRate = await _unitOfWork.QuotationExchangeRates.GetByIdForUpdateAsync(id);
                if (exchangeRate == null)
                {
                    return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, exchangeRate);
                await _unitOfWork.QuotationExchangeRates.UpdateAsync(exchangeRate);
                await _unitOfWork.SaveChangesAsync();

                // Reload with navigation properties
                var exchangeRateWithNav = await _unitOfWork.QuotationExchangeRates
                    .Query()
                    .Include(e => e.Quotation)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == exchangeRate.Id && !e.IsDeleted);

                if (exchangeRateWithNav == null)
                {
                    return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<QuotationExchangeRateGetDto>(exchangeRateWithNav);
                return ApiResponse<QuotationExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("QuotationExchangeRateService.RateUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.UpdateRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

            public async Task<ApiResponse<bool>> UpdateExchangeRateInQuotation(List<QuotationExchangeRateGetDto> updateDtos){
                try
                {
                    foreach (var dto in updateDtos)
                    {
                        var exchangeRate = await _unitOfWork.QuotationExchangeRates.GetByIdForUpdateAsync(dto.Id);

                        if (exchangeRate == null)
                        {
                            return ApiResponse<bool>.ErrorResult(
                                _localizationService.GetLocalizedString("QuotationExchangeRateService.ExchangeRateNotFound"),
                                _localizationService.GetLocalizedString("QuotationExchangeRateService.ExchangeRateNotFound"),
                                StatusCodes.Status404NotFound);
                        }
                        exchangeRate.ExchangeRate = dto.ExchangeRate;
                    }

                    await _unitOfWork.SaveChangesAsync();

                    return ApiResponse<bool>.SuccessResult(
                        true,
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.ExchangeRateUpdated"));
                }
                catch (Exception ex)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.UpdateExchangeRateInQuotationExceptionMessage", ex.Message),
                        StatusCodes.Status500InternalServerError);
                }
            }



        public async Task<ApiResponse<object>> DeleteQuotationExchangeRateAsync(long id)
        {
            try
            {
                var exchangeRate = await _unitOfWork.QuotationExchangeRates.GetByIdAsync(id);
                if (exchangeRate == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("QuotationExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.QuotationExchangeRates.SoftDeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("QuotationExchangeRateService.RateDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.DeleteRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<QuotationExchangeRateGetDto>>> GetQuotationExchangeRatesByQuotationIdAsync(long quotationId)
        {
            try
            {
                var exchangeRates = await _unitOfWork.QuotationExchangeRates
                    .Query()
                    .Where(e => e.QuotationId == quotationId && !e.IsDeleted)
                    .Include(e => e.Quotation)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .ToListAsync();

                var dtos = exchangeRates.Select(x => _mapper.Map<QuotationExchangeRateGetDto>(x)).ToList();
                return ApiResponse<List<QuotationExchangeRateGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("QuotationExchangeRateService.RatesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationExchangeRateGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationExchangeRateService.GetRatesByQuotationIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
