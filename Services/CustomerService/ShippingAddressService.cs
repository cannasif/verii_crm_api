using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using crm_api.Helpers;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class ShippingAddressService : IShippingAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IGeocodingService _geocodingService;

        public ShippingAddressService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IGeocodingService geocodingService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _geocodingService = geocodingService;
        }

        public async Task<ApiResponse<PagedResponse<ShippingAddressGetDto>>> GetAllShippingAddressesAsync(PagedRequest request)
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

                var columnMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "customerName", "Customer.CustomerName" },
                    { "countryName", "Country.Name" },
                    { "cityName", "City.Name" },
                    { "districtName", "District.Name" }
                };

                var query = _unitOfWork.ShippingAddresses
                    .Query()
                    .Where(sa => !sa.IsDeleted)
                    .Include(sa => sa.Customer)
                    .Include(sa => sa.Country)
                    .Include(sa => sa.City)
                    .Include(sa => sa.District)
                    .Include(sa => sa.CreatedByUser)
                    .Include(sa => sa.UpdatedByUser)
                    .Include(sa => sa.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(ShippingAddress.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ShippingAddressGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ShippingAddressGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ShippingAddressGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ShippingAddressGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.GetAllShippingAddressesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ShippingAddressGetDto>> GetShippingAddressByIdAsync(long id)
        {
            try
            {
                var shippingAddress = await _unitOfWork.ShippingAddresses.GetByIdAsync(id).ConfigureAwait(false);

                if (shippingAddress == null)
                {
                    return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var shippingAddressWithNav = await _unitOfWork.ShippingAddresses
                    .Query()
                    .Include(sa => sa.CreatedByUser)
                    .Include(sa => sa.UpdatedByUser)
                    .Include(sa => sa.DeletedByUser)
                    .FirstOrDefaultAsync(sa => sa.Id == id && !sa.IsDeleted).ConfigureAwait(false);

                if (shippingAddressWithNav == null)
                {
                    return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var shippingAddressDto = _mapper.Map<ShippingAddressGetDto>(shippingAddressWithNav);

                return ApiResponse<ShippingAddressGetDto>.SuccessResult(shippingAddressDto, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.GetShippingAddressByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ShippingAddressGetDto>>> GetShippingAddressesByCustomerIdAsync(long customerId)
        {
            try
            {
                var shippingAddresses = await _unitOfWork.ShippingAddresses.FindAsync(sa => sa.CustomerId == customerId).ConfigureAwait(false);

                var shippingAddressDtos = _mapper.Map<List<ShippingAddressGetDto>>(shippingAddresses);

                return ApiResponse<List<ShippingAddressGetDto>>.SuccessResult(shippingAddressDtos, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressesByCustomerRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ShippingAddressGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.GetShippingAddressesByCustomerIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ShippingAddressGetDto>> CreateShippingAddressAsync(CreateShippingAddressDto createShippingAddressDto)
        {
            try
            {
                var shippingAddress = _mapper.Map<ShippingAddress>(createShippingAddressDto);
                await TryFillCoordinatesFromAddressAsync(shippingAddress).ConfigureAwait(false);

                await _unitOfWork.ShippingAddresses.AddAsync(shippingAddress).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var shippingAddressWithNav = await _unitOfWork.ShippingAddresses
                    .Query()
                    .Include(sa => sa.CreatedByUser)
                    .Include(sa => sa.UpdatedByUser)
                    .Include(sa => sa.DeletedByUser)
                    .FirstOrDefaultAsync(sa => sa.Id == shippingAddress.Id && !sa.IsDeleted).ConfigureAwait(false);

                if (shippingAddressWithNav == null)
                {
                    return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var shippingAddressDto = _mapper.Map<ShippingAddressGetDto>(shippingAddressWithNav);

                return ApiResponse<ShippingAddressGetDto>.SuccessResult(shippingAddressDto, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.CreateShippingAddressExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ShippingAddressGetDto>> UpdateShippingAddressAsync(long id, UpdateShippingAddressDto updateShippingAddressDto)
        {
            try
            {
                var existingShippingAddress = await _unitOfWork.ShippingAddresses.GetByIdAsync(id).ConfigureAwait(false);

                if (existingShippingAddress == null)
                {
                    return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var addressBefore = (existingShippingAddress.Address, existingShippingAddress.CountryId, existingShippingAddress.CityId, existingShippingAddress.DistrictId);
                _mapper.Map(updateShippingAddressDto, existingShippingAddress);
                existingShippingAddress.UpdatedDate = DateTimeProvider.Now;
                var addressAfter = (existingShippingAddress.Address, existingShippingAddress.CountryId, existingShippingAddress.CityId, existingShippingAddress.DistrictId);
                var addressChanged = addressBefore != addressAfter;

                if (addressChanged)
                    await TryFillCoordinatesFromAddressAsync(existingShippingAddress, allowOverwriteExistingCoords: true).ConfigureAwait(false);
                else
                    await TryFillCoordinatesFromAddressAsync(existingShippingAddress).ConfigureAwait(false);

                await _unitOfWork.ShippingAddresses.UpdateAsync(existingShippingAddress).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var shippingAddressWithNav = await _unitOfWork.ShippingAddresses
                    .Query()
                    .Include(sa => sa.CreatedByUser)
                    .Include(sa => sa.UpdatedByUser)
                    .Include(sa => sa.DeletedByUser)
                    .FirstOrDefaultAsync(sa => sa.Id == id && !sa.IsDeleted).ConfigureAwait(false);

                if (shippingAddressWithNav == null)
                {
                    return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var shippingAddressDto = _mapper.Map<ShippingAddressGetDto>(shippingAddressWithNav);

                return ApiResponse<ShippingAddressGetDto>.SuccessResult(shippingAddressDto, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ShippingAddressGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.UpdateShippingAddressExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteShippingAddressAsync(long id)
        {
            try
            {
                var shippingAddress = await _unitOfWork.ShippingAddresses.GetByIdAsync(id).ConfigureAwait(false);

                if (shippingAddress == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressNotFound"),
                        StatusCodes.Status404NotFound);
                }

           

                await _unitOfWork.ShippingAddresses.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ShippingAddressService.ShippingAddressDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ShippingAddressService.InternalServerError"),
                    _localizationService.GetLocalizedString("ShippingAddressService.DeleteShippingAddressExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Adres metninden koordinat doldurur. allowOverwriteExistingCoords=false ise sadece boşsa doldurulur;
        /// update path'ta adres alanları değiştiyse true ile çağrılarak eski koordinatlar güncellenir.
        /// </summary>
        private async Task TryFillCoordinatesFromAddressAsync(ShippingAddress shippingAddress, bool allowOverwriteExistingCoords = false)
        {
            if (!allowOverwriteExistingCoords && shippingAddress.Latitude.HasValue && shippingAddress.Longitude.HasValue)
                return;

            var fullAddress = await BuildFullAddressAsync(shippingAddress.Address, shippingAddress.CountryId, shippingAddress.CityId, shippingAddress.DistrictId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fullAddress))
                return;

            var coords = await _geocodingService.GeocodeAsync(fullAddress).ConfigureAwait(false);
            if (coords.HasValue)
            {
                shippingAddress.Latitude = coords.Value.Latitude;
                shippingAddress.Longitude = coords.Value.Longitude;
            }
        }

        private async Task<string?> BuildFullAddressAsync(string? address, long? countryId, long? cityId, long? districtId)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(address))
                parts.Add(address.Trim());
            if (districtId.HasValue)
            {
                var district = await _unitOfWork.Districts.GetByIdAsync(districtId.Value).ConfigureAwait(false);
                if (district?.Name != null)
                    parts.Add(district.Name.Trim());
            }
            if (cityId.HasValue)
            {
                var city = await _unitOfWork.Cities.GetByIdAsync(cityId.Value).ConfigureAwait(false);
                if (city?.Name != null)
                    parts.Add(city.Name.Trim());
            }
            if (countryId.HasValue)
            {
                var country = await _unitOfWork.Countries.GetByIdAsync(countryId.Value).ConfigureAwait(false);
                if (country?.Name != null)
                    parts.Add(country.Name.Trim());
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
