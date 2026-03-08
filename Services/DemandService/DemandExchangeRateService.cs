using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace crm_api.Services
{
    public class DemandExchangeRateService : IDemandExchangeRateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public DemandExchangeRateService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<DemandExchangeRateGetDto>>> GetAllDemandExchangeRatesAsync(PagedRequest request)
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

                var query = _unitOfWork.DemandExchangeRates
                    .Query()
                    .Include(e => e.Demand)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(DemandExchangeRate.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync();

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync();

                var dtos = items.Select(x => _mapper.Map<DemandExchangeRateGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DemandExchangeRateGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DemandExchangeRateGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DemandExchangeRateService.RatesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DemandExchangeRateGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.GetAllRatesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandExchangeRateGetDto>> GetDemandExchangeRateByIdAsync(long id)
        {
            try
            {
                var exchangeRate = await _unitOfWork.DemandExchangeRates
                    .Query()
                    .Include(e => e.Demand)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

                if (exchangeRate == null)
                {
                    return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<DemandExchangeRateGetDto>(exchangeRate);
                return ApiResponse<DemandExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("DemandExchangeRateService.RateRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.GetRateByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandExchangeRateGetDto>> CreateDemandExchangeRateAsync(DemandExchangeRateCreateDto createDto)
        {
            try
            {
                var exchangeRate = _mapper.Map<DemandExchangeRate>(createDto);
                await _unitOfWork.DemandExchangeRates.AddAsync(exchangeRate);
                await _unitOfWork.SaveChangesAsync();

                // Reload with navigation properties
                var exchangeRateWithNav = await _unitOfWork.DemandExchangeRates
                    .Query()
                    .Include(e => e.Demand)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == exchangeRate.Id && !e.IsDeleted);

                if (exchangeRateWithNav == null)
                {
                    return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<DemandExchangeRateGetDto>(exchangeRateWithNav);
                return ApiResponse<DemandExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("DemandExchangeRateService.RateCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.CreateRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandExchangeRateGetDto>> UpdateDemandExchangeRateAsync(long id, DemandExchangeRateUpdateDto updateDto)
        {
            try
            {
                var exchangeRate = await _unitOfWork.DemandExchangeRates.GetByIdForUpdateAsync(id);
                if (exchangeRate == null)
                {
                    return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, exchangeRate);
                await _unitOfWork.DemandExchangeRates.UpdateAsync(exchangeRate);
                await _unitOfWork.SaveChangesAsync();

                // Reload with navigation properties
                var exchangeRateWithNav = await _unitOfWork.DemandExchangeRates
                    .Query()
                    .Include(e => e.Demand)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .FirstOrDefaultAsync(e => e.Id == exchangeRate.Id && !e.IsDeleted);

                if (exchangeRateWithNav == null)
                {
                    return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exchangeRateDto = _mapper.Map<DemandExchangeRateGetDto>(exchangeRateWithNav);
                return ApiResponse<DemandExchangeRateGetDto>.SuccessResult(exchangeRateDto, _localizationService.GetLocalizedString("DemandExchangeRateService.RateUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandExchangeRateGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.UpdateRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> UpdateExchangeRateInDemand(List<DemandExchangeRateGetDto> updateDtos)
        {
            try
            {
                foreach (var dto in updateDtos)
                {
                    var exchangeRate = await _unitOfWork.DemandExchangeRates.GetByIdForUpdateAsync(dto.Id);

                    if (exchangeRate == null)
                    {
                        return ApiResponse<bool>.ErrorResult(
                            _localizationService.GetLocalizedString("DemandExchangeRateService.ExchangeRateNotFound"),
                            _localizationService.GetLocalizedString("DemandExchangeRateService.ExchangeRateNotFound"),
                            StatusCodes.Status404NotFound);
                    }
                    exchangeRate.ExchangeRate = dto.ExchangeRate;
                }

                await _unitOfWork.SaveChangesAsync();

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("DemandExchangeRateService.ExchangeRateUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.UpdateExchangeRateInDemandExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteDemandExchangeRateAsync(long id)
        {
            try
            {
                var exchangeRate = await _unitOfWork.DemandExchangeRates.GetByIdAsync(id);
                if (exchangeRate == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        _localizationService.GetLocalizedString("DemandExchangeRateService.RateNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.DemandExchangeRates.SoftDeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("DemandExchangeRateService.RateDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.DeleteRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<DemandExchangeRateGetDto>>> GetDemandExchangeRatesByDemandIdAsync(long demandId)
        {
            try
            {
                var exchangeRates = await _unitOfWork.DemandExchangeRates
                    .Query()
                    .Where(e => e.DemandId == demandId && !e.IsDeleted)
                    .Include(e => e.Demand)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.UpdatedByUser)
                    .Include(e => e.DeletedByUser)
                    .ToListAsync();

                var dtos = exchangeRates.Select(x => _mapper.Map<DemandExchangeRateGetDto>(x)).ToList();
                return ApiResponse<List<DemandExchangeRateGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("DemandExchangeRateService.RatesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandExchangeRateGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandExchangeRateService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandExchangeRateService.GetRatesByDemandIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
