using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class DistrictService : IDistrictService
    {
        private const string CacheVersionKey = "district:version";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IMemoryCache _memoryCache;

        public DistrictService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IMemoryCache memoryCache)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _memoryCache = memoryCache;
        }

        public async Task<ApiResponse<PagedResponse<DistrictGetDto>>> GetAllDistrictsAsync(PagedRequest request)
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
                if (_memoryCache.TryGetValue(cacheKey, out PagedResponse<DistrictGetDto>? cachedResponse) && cachedResponse != null)
                {
                    return ApiResponse<PagedResponse<DistrictGetDto>>.SuccessResult(
                        cachedResponse,
                        _localizationService.GetLocalizedString("DistrictService.DistrictsRetrieved"));
                }

                var query = _unitOfWork.Districts
                    .Query()
                    .Where(d => !d.IsDeleted)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .Include(d => d.City)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(District.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<DistrictGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DistrictGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                _memoryCache.Set(cacheKey, pagedResponse, CacheDuration);

                return ApiResponse<PagedResponse<DistrictGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DistrictService.DistrictsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DistrictGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DistrictService.InternalServerError"),
                    _localizationService.GetLocalizedString("DistrictService.GetAllDistrictsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DistrictGetDto>> GetDistrictByIdAsync(long id)
        {
            try
            {
                var cacheKey = GetByIdCacheKey(id);
                if (_memoryCache.TryGetValue(cacheKey, out DistrictGetDto? cachedDistrict) && cachedDistrict != null)
                {
                    return ApiResponse<DistrictGetDto>.SuccessResult(
                        cachedDistrict,
                        _localizationService.GetLocalizedString("DistrictService.DistrictRetrieved"));
                }

                var district = await _unitOfWork.Districts.GetByIdAsync(id).ConfigureAwait(false);
                if (district == null)
                {
                    return ApiResponse<DistrictGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var districtWithNav = await _unitOfWork.Districts
                    .Query()
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .Include(d => d.City)
                    .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted).ConfigureAwait(false);

                var districtDto = _mapper.Map<DistrictGetDto>(districtWithNav ?? district);
                _memoryCache.Set(cacheKey, districtDto, CacheDuration);
                return ApiResponse<DistrictGetDto>.SuccessResult(districtDto, _localizationService.GetLocalizedString("DistrictService.DistrictRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DistrictGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DistrictService.InternalServerError"),
                    _localizationService.GetLocalizedString("DistrictService.GetDistrictByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DistrictGetDto>> CreateDistrictAsync(DistrictCreateDto districtCreateDto)
        {
            try
            {
                var district = _mapper.Map<District>(districtCreateDto);
                await _unitOfWork.Districts.AddAsync(district).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                InvalidateCache();

                // Reload with navigation properties for mapping
                var districtWithNav = await _unitOfWork.Districts
                    .Query()
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .Include(d => d.City)
                    .FirstOrDefaultAsync(d => d.Id == district.Id && !d.IsDeleted).ConfigureAwait(false);

                if (districtWithNav == null)
                {
                    return ApiResponse<DistrictGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var districtDto = _mapper.Map<DistrictGetDto>(districtWithNav);

                return ApiResponse<DistrictGetDto>.SuccessResult(districtDto, _localizationService.GetLocalizedString("DistrictService.DistrictCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DistrictGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DistrictService.InternalServerError"),
                    _localizationService.GetLocalizedString("DistrictService.CreateDistrictExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DistrictGetDto>> UpdateDistrictAsync(long id, DistrictUpdateDto districtUpdateDto)
        {
            try
            {
                var district = await _unitOfWork.Districts.GetByIdAsync(id).ConfigureAwait(false);
                if (district == null)
                {
                    return ApiResponse<DistrictGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(districtUpdateDto, district);
                await _unitOfWork.Districts.UpdateAsync(district).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                InvalidateCache();

                // Reload with navigation properties for mapping
                var districtWithNav = await _unitOfWork.Districts
                    .Query()
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .Include(d => d.City)
                    .FirstOrDefaultAsync(d => d.Id == district.Id && !d.IsDeleted).ConfigureAwait(false);

                if (districtWithNav == null)
                {
                    return ApiResponse<DistrictGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var districtDto = _mapper.Map<DistrictGetDto>(districtWithNav);

                return ApiResponse<DistrictGetDto>.SuccessResult(districtDto, _localizationService.GetLocalizedString("DistrictService.DistrictUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DistrictGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DistrictService.InternalServerError"),
                    _localizationService.GetLocalizedString("DistrictService.UpdateDistrictExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteDistrictAsync(long id)
        {
            try
            {
                var district = await _unitOfWork.Districts.GetByIdAsync(id).ConfigureAwait(false);
                if (district == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        _localizationService.GetLocalizedString("DistrictService.DistrictNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.Districts.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                InvalidateCache();

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("DistrictService.DistrictDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DistrictService.InternalServerError"),
                    _localizationService.GetLocalizedString("DistrictService.DeleteDistrictExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private string GetAllCacheKey(PagedRequest request)
        {
            var filters = request.Filters == null
                ? string.Empty
                : string.Join("|", request.Filters.Select(f => $"{f.Column}:{f.Operator}:{f.Value}"));

            return $"district:all:{GetCacheVersion()}:{request.PageNumber}:{request.PageSize}:{request.SortBy}:{request.SortDirection}:{request.FilterLogic}:{filters}";
        }

        private string GetByIdCacheKey(long id)
        {
            return $"district:id:{GetCacheVersion()}:{id}";
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
