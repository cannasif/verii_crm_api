using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace crm_api.Services
{
    public class CountryService : ICountryService
    {
        private const string CacheVersionKey = "country:version";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IMemoryCache _memoryCache;

        public CountryService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IMemoryCache memoryCache)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _memoryCache = memoryCache;
        }

        public async Task<ApiResponse<PagedResponse<CountryGetDto>>> GetAllCountriesAsync(PagedRequest request)
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

                var cacheKey = GetAllCacheKey(request);
                if (_memoryCache.TryGetValue(cacheKey, out PagedResponse<CountryGetDto>? cachedResponse) && cachedResponse != null)
                {
                    return ApiResponse<PagedResponse<CountryGetDto>>.SuccessResult(
                        cachedResponse,
                        _localizationService.GetLocalizedString("CountryService.CountriesRetrieved"));
                }

                var query = _unitOfWork.Countries
                    .Query()
                    .Where(c => !c.IsDeleted)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(Country.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<CountryGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<CountryGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                _memoryCache.Set(cacheKey, pagedResponse, CacheDuration);

                return ApiResponse<PagedResponse<CountryGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("CountryService.CountriesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<CountryGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("CountryService.InternalServerError"),
                    _localizationService.GetLocalizedString("CountryService.GetAllCountriesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CountryGetDto>> GetCountryByIdAsync(long id)
        {
            try
            {
                var cacheKey = GetByIdCacheKey(id);
                if (_memoryCache.TryGetValue(cacheKey, out CountryGetDto? cachedCountry) && cachedCountry != null)
                {
                    return ApiResponse<CountryGetDto>.SuccessResult(
                        cachedCountry,
                        _localizationService.GetLocalizedString("CountryService.CountryRetrieved"));
                }

                // Get entity with audit navigation properties
                var country = await _unitOfWork.Countries.GetByIdAsync(id).ConfigureAwait(false);

                if (country == null)
                {
                    return ApiResponse<CountryGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                        _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var countryDto = _mapper.Map<CountryGetDto>(country);
                _memoryCache.Set(cacheKey, countryDto, CacheDuration);
                return ApiResponse<CountryGetDto>.SuccessResult(countryDto, _localizationService.GetLocalizedString("CountryService.CountryRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<CountryGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CountryService.InternalServerError"),
                    _localizationService.GetLocalizedString("CountryService.GetCountryByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CountryGetDto>> CreateCountryAsync(CountryCreateDto countryCreateDto)
        {
            var country = _mapper.Map<Country>(countryCreateDto);
            await _unitOfWork.Countries.AddAsync(country).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
            InvalidateCache();

            var countryDto = _mapper.Map<CountryGetDto>(country);
            return ApiResponse<CountryGetDto>.SuccessResult(countryDto, _localizationService.GetLocalizedString("CountryService.CountryCreated"));
        }

        public async Task<ApiResponse<CountryGetDto>> UpdateCountryAsync(long id, CountryUpdateDto countryUpdateDto)
        {
            // Get tracked entity for update
            var country = await _unitOfWork.Countries.GetByIdForUpdateAsync(id).ConfigureAwait(false);
            if (country == null)
            {
                return ApiResponse<CountryGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                    _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                    StatusCodes.Status404NotFound);
            }

            _mapper.Map(countryUpdateDto, country);
            await _unitOfWork.Countries.UpdateAsync(country).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
            InvalidateCache();

            // Reload with audit navigation properties for mapping (read-only)
            var countryWithNav = await _unitOfWork.Countries.GetByIdAsync(id).ConfigureAwait(false);

            if (countryWithNav == null)
            {
                return ApiResponse<CountryGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                    _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                    StatusCodes.Status404NotFound);
            }

            var countryDto = _mapper.Map<CountryGetDto>(countryWithNav);
            return ApiResponse<CountryGetDto>.SuccessResult(countryDto, _localizationService.GetLocalizedString("CountryService.CountryUpdated"));
        }

        public async Task<ApiResponse<object>> DeleteCountryAsync(long id)
        {
            try
            {
                var deleted = await _unitOfWork.Countries.SoftDeleteAsync(id).ConfigureAwait(false);
                if (!deleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                        _localizationService.GetLocalizedString("CountryService.CountryNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                InvalidateCache();

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("CountryService.CountryDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("CountryService.InternalServerError"),
                    _localizationService.GetLocalizedString("CountryService.DeleteCountryExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private string GetAllCacheKey(PagedRequest request)
        {
            var filters = request.Filters == null
                ? string.Empty
                : string.Join("|", request.Filters.Select(f => $"{f.Column}:{f.Operator}:{f.Value}"));

            return $"country:all:{GetCacheVersion()}:{request.PageNumber}:{request.PageSize}:{request.SortBy}:{request.SortDirection}:{request.FilterLogic}:{filters}";
        }

        private string GetByIdCacheKey(long id)
        {
            return $"country:id:{GetCacheVersion()}:{id}";
        }

        private string GetCacheVersion()
        {
            if (_memoryCache.TryGetValue(CacheVersionKey, out string? version) && !string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            version = Guid.NewGuid().ToString("N");
            _memoryCache.Set(CacheVersionKey, version, CacheDuration);
            return version;
        }

        private void InvalidateCache()
        {
            _memoryCache.Set(CacheVersionKey, Guid.NewGuid().ToString("N"), CacheDuration);
        }
    }
}
