using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace crm_api.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IErpService _erpService;
        private readonly ILogger<CustomerService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly IGeocodingService _geocodingService;
        private readonly IFileUploadService _fileUploadService;

        public CustomerService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IErpService erpService,
            ILogger<CustomerService> logger,
            IHttpContextAccessor httpContextAccessor,
            IGeocodingService geocodingService,
            IFileUploadService fileUploadService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _erpService = erpService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _geocodingService = geocodingService;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<PagedResponse<CustomerGetDto>>> GetAllCustomersAsync(PagedRequest request)
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
                    { "name", "CustomerName" },
                    { "phone", "Phone1" },
                    { "countryName", "Country.Name" },
                    { "cityName", "City.Name" },
                    { "districtName", "District.Name" },
                    { "customerTypeName", "CustomerType.Name" }
                };

                var query = ApplyCustomerVisibilityFilter(_unitOfWork.Customers
                    .Query()
                    .Where(c => !c.IsDeleted)
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser))
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(Customer.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<CustomerGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<CustomerGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<CustomerGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("CustomerService.CustomersRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<CustomerGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.GetAllCustomersExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CustomerGetDto>> GetCustomerByIdAsync(long id)
        {
            try
            {
                var customer = await ApplyCustomerVisibilityFilter(_unitOfWork.Customers
                    .Query()
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser))
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted).ConfigureAwait(false);

                if (customer == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var customerDto = _mapper.Map<CustomerGetDto>(customer);
                return ApiResponse<CustomerGetDto>.SuccessResult(customerDto, _localizationService.GetLocalizedString("CustomerService.CustomerRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.GetCustomerByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<NearbyCustomerPinDto>>> GetNearbyCustomersAsync(CustomerNearbyQueryDto query)
        {
            try
            {
                if (query == null)
                {
                    return ApiResponse<List<NearbyCustomerPinDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.InvalidRequest"),
                        _localizationService.GetLocalizedString("CustomerService.InvalidRequest"),
                        StatusCodes.Status400BadRequest);
                }

                if (query.Latitude < -90 || query.Latitude > 90 || query.Longitude < -180 || query.Longitude > 180)
                {
                    return ApiResponse<List<NearbyCustomerPinDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.InvalidCoordinates"),
                        _localizationService.GetLocalizedString("CustomerService.InvalidCoordinates"),
                        StatusCodes.Status400BadRequest);
                }

                var radiusKm = query.RadiusKm <= 0 ? 10d : Math.Min(query.RadiusKm, 50d);

                var columnMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "name", "CustomerName" },
                    { "customerCode", "CustomerCode" },
                    { "customerTypeName", "CustomerType.Name" },
                    { "countryName", "Country.Name" },
                    { "cityName", "City.Name" },
                    { "districtName", "District.Name" },
                    { "phone", "Phone1" },
                    { "branchCode", "BranchCode" }
                };

                var parsedFilters = ParseFiltersJson(query.Filters);

                var customerQuery = ApplyCustomerVisibilityFilter(_unitOfWork.Customers
                    .Query()
                    .Where(c => !c.IsDeleted)
                    .Include(c => c.CustomerType)
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.ShippingAddresses)
                        .ThenInclude(sa => sa.Country)
                    .Include(c => c.ShippingAddresses)
                        .ThenInclude(sa => sa.City)
                    .Include(c => c.ShippingAddresses)
                        .ThenInclude(sa => sa.District))
                    .ApplyFilters(parsedFilters, query.FilterLogic, columnMapping);

                var branchCodeStr = _httpContextAccessor.HttpContext?.Items["BranchCode"]?.ToString();
                if (short.TryParse(branchCodeStr, out var branchCode) && branchCode > 0)
                {
                    customerQuery = customerQuery.Where(c => c.BranchCode == branchCode);
                }

                var customers = await customerQuery.ToListAsync().ConfigureAwait(false);
                var result = new List<NearbyCustomerPinDto>();

                foreach (var customer in customers)
                {
                    if (customer.Latitude.HasValue && customer.Longitude.HasValue)
                    {
                        var distance = CalculateDistanceKm(
                            query.Latitude,
                            query.Longitude,
                            (double)customer.Latitude.Value,
                            (double)customer.Longitude.Value);

                        if (distance <= radiusKm)
                        {
                            result.Add(new NearbyCustomerPinDto
                            {
                                Id = customer.Id,
                                CustomerId = customer.Id,
                                CustomerCode = customer.CustomerCode,
                                Name = customer.CustomerName,
                                AddressDisplay = BuildAddressDisplay(customer.Address, customer.District?.Name, customer.City?.Name, customer.Country?.Name),
                                Latitude = (double)customer.Latitude.Value,
                                Longitude = (double)customer.Longitude.Value,
                                Source = "main",
                                ShippingAddressId = null,
                                CustomerTypeName = customer.CustomerType?.Name,
                                Phone = FirstNonEmpty(customer.Phone1, customer.Phone2)
                            });
                        }
                    }

                    if (!query.IncludeShippingAddresses || customer.ShippingAddresses == null)
                        continue;

                    foreach (var shipping in customer.ShippingAddresses.Where(x => !x.IsDeleted))
                    {
                        if (!shipping.Latitude.HasValue || !shipping.Longitude.HasValue)
                            continue;

                        var distance = CalculateDistanceKm(
                            query.Latitude,
                            query.Longitude,
                            (double)shipping.Latitude.Value,
                            (double)shipping.Longitude.Value);

                        if (distance > radiusKm)
                            continue;

                        result.Add(new NearbyCustomerPinDto
                        {
                            Id = (customer.Id * 1_000_000L) + shipping.Id,
                            CustomerId = customer.Id,
                            CustomerCode = customer.CustomerCode,
                            Name = customer.CustomerName,
                            AddressDisplay = BuildAddressDisplay(shipping.Address, shipping.District?.Name, shipping.City?.Name, shipping.Country?.Name),
                            Latitude = (double)shipping.Latitude.Value,
                            Longitude = (double)shipping.Longitude.Value,
                            Source = "shipping",
                            ShippingAddressId = shipping.Id,
                            CustomerTypeName = customer.CustomerType?.Name,
                            Phone = FirstNonEmpty(shipping.Phone, FirstNonEmpty(customer.Phone1, customer.Phone2))
                        });
                    }
                }

                const int maxItems = 500;
                var ordered = result
                    .OrderBy(x => CalculateDistanceKm(query.Latitude, query.Longitude, x.Latitude, x.Longitude))
                    .Take(maxItems)
                    .ToList();

                return ApiResponse<List<NearbyCustomerPinDto>>.SuccessResult(
                    ordered,
                    _localizationService.GetLocalizedString("CustomerService.CustomersRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<NearbyCustomerPinDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.GetAllCustomersExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private IQueryable<Customer> ApplyCustomerVisibilityFilter(IQueryable<Customer> query)
        {
            if (!ShouldRestrictCustomerVisibility())
            {
                return query;
            }

            return query.Where(c =>
                c.CustomerCode == null ||
                c.CustomerCode == "" ||
                c.CustomerCode.Contains("120-01"));
        }

        private bool ShouldRestrictCustomerVisibility()
        {
            var role = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
                ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value;

            return string.Equals(role?.Trim(), "User", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ApiResponse<CustomerGetDto>> CreateCustomerAsync(CustomerCreateDto customerCreateDto)
        {
            try
            {
                NormalizeCustomerDto(customerCreateDto);
                var governanceError = await ValidateCustomerGovernanceAsync(customerCreateDto, null).ConfigureAwait(false);
                if (governanceError != null)
                {
                    return governanceError;
                }

                var customer = _mapper.Map<Customer>(customerCreateDto);
                await TryFillCoordinatesFromAddressAsync(customer).ConfigureAwait(false);
                await _unitOfWork.Customers.AddAsync(customer).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var customerWithNav = await _unitOfWork.Customers
                    .Query()
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser)
                    .FirstOrDefaultAsync(c => c.Id == customer.Id && !c.IsDeleted).ConfigureAwait(false);

                if (customerWithNav == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var customerDto = _mapper.Map<CustomerGetDto>(customerWithNav);

                return ApiResponse<CustomerGetDto>.SuccessResult(customerDto, _localizationService.GetLocalizedString("CustomerService.CustomerCreated"));
            }
            catch (DbUpdateException ex) when (DbUpdateExceptionHelper.TryGetUniqueViolation(ex, out _))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.RecordAlreadyExists"),
                    _localizationService.GetLocalizedString("General.RecordAlreadyExists"),
                    StatusCodes.Status409Conflict);
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.CreateCustomerExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CustomerCreateFromMobileResultDto>> CreateCustomerFromMobileAsync(CustomerCreateFromMobileDto request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNameRequired"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNameRequired"),
                        StatusCodes.Status400BadRequest);
                }

                if (!IsValidEmail(request.Email))
                {
                    return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.InvalidEmail"),
                        _localizationService.GetLocalizedString("CustomerService.InvalidEmail"),
                        StatusCodes.Status400BadRequest);
                }

                BusinessCardOcrProcessingState processingState;
                Customer customer;
                var shouldRefreshCoordinates = false;
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                try
                {
                    (customer, var customerAction, shouldRefreshCoordinates) = await ResolveBusinessCardCustomerAsync(request).ConfigureAwait(false);
                    var titleResolution = await ResolveBusinessCardTitleAsync(request.Title).ConfigureAwait(false);
                    var contactResolution = await ResolveBusinessCardContactAsync(request, customer.Id, titleResolution.Title.Id).ConfigureAwait(false);

                    processingState = new BusinessCardOcrProcessingState
                    {
                        CustomerId = customer.Id,
                        CustomerAction = customerAction,
                        ContactId = contactResolution.Contact?.Id,
                        ContactAction = contactResolution.Action,
                        TitleId = titleResolution.Title.Id,
                        TitleAction = titleResolution.Action,
                        ResolvedTitleName = titleResolution.Title.TitleName,
                        UsedFallbackTitle = titleResolution.UsedFallbackTitle
                    };

                    await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                    var imageUpload = await TryUploadBusinessCardImageAsync(customer.Id, request).ConfigureAwait(false);
                    await TryRefreshBusinessCardCoordinatesAsync(customer, request, shouldRefreshCoordinates).ConfigureAwait(false);

                    var response = new CustomerCreateFromMobileResultDto
                    {
                        CustomerId = processingState.CustomerId,
                        CustomerCreated = string.Equals(processingState.CustomerAction, BusinessCardOcrActions.Created, StringComparison.Ordinal),
                        CustomerAction = processingState.CustomerAction,
                        ContactId = processingState.ContactId,
                        ContactCreated = string.Equals(processingState.ContactAction, BusinessCardOcrActions.Created, StringComparison.Ordinal),
                        ContactAction = processingState.ContactAction,
                        TitleId = processingState.TitleId,
                        TitleCreated = string.Equals(processingState.TitleAction, BusinessCardOcrActions.Created, StringComparison.Ordinal),
                        TitleAction = processingState.TitleAction,
                        ResolvedTitleName = processingState.ResolvedTitleName,
                        UsedFallbackTitle = processingState.UsedFallbackTitle,
                        ImageUploaded = imageUpload.ImageUploaded,
                        ImageUploadError = imageUpload.ImageUploadError
                    };

                    return ApiResponse<CustomerCreateFromMobileResultDto>.SuccessResult(
                        response,
                        BuildBusinessCardOcrSuccessMessage(processingState));
                }
                catch (BusinessCardOcrConflictException ex)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                        ex.MessageText,
                        ex.Detail,
                        StatusCodes.Status409Conflict);
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    throw;
                }
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.CreateCustomerExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CustomerGetDto>> UpdateCustomerAsync(long id, CustomerUpdateDto customerUpdateDto)
        {
            try
            {
                NormalizeCustomerDto(customerUpdateDto);
                var customer = await _unitOfWork.Customers.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (customer == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var governanceError = await ValidateCustomerGovernanceAsync(customerUpdateDto, id).ConfigureAwait(false);
                if (governanceError != null)
                {
                    return governanceError;
                }

                var addressBefore = (customer.Address, customer.CountryId, customer.CityId, customer.DistrictId);
                _mapper.Map(customerUpdateDto, customer);
                var addressAfter = (customer.Address, customer.CountryId, customer.CityId, customer.DistrictId);
                var addressChanged = addressBefore != addressAfter;

                if (addressChanged)
                {
                    _logger.LogDebug("Customer {CustomerId}: address fields updated, running geocoding (normal existing-customer flow).", id);
                    await TryFillCoordinatesFromAddressAsync(customer, allowOverwriteExistingCoords: true).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogDebug("Customer {CustomerId}: address unchanged, keeping existing coordinates.", id);
                }

                await _unitOfWork.Customers.UpdateAsync(customer).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var customerWithNav = await _unitOfWork.Customers
                    .Query()
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser)
                    .FirstOrDefaultAsync(c => c.Id == customer.Id && !c.IsDeleted).ConfigureAwait(false);

                if (customerWithNav == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var customerDto = _mapper.Map<CustomerGetDto>(customerWithNav);

                return ApiResponse<CustomerGetDto>.SuccessResult(customerDto, _localizationService.GetLocalizedString("CustomerService.CustomerUpdated"));
            }
            catch (DbUpdateException ex) when (DbUpdateExceptionHelper.TryGetUniqueViolation(ex, out _))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.RecordAlreadyExists"),
                    _localizationService.GetLocalizedString("General.RecordAlreadyExists"),
                    StatusCodes.Status409Conflict);
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.UpdateCustomerExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteCustomerAsync(long id)
        {
            try
            {
                var customer = await _unitOfWork.Customers.GetByIdAsync(id).ConfigureAwait(false);
                if (customer == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.Customers.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("CustomerService.CustomerDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.DeleteCustomerExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task SyncCustomersFromErpAsync()
        {
            var erpResponse = await _erpService.GetCarisAsync(null).ConfigureAwait(false);

            if (erpResponse?.Data == null || erpResponse.Data.Count == 0)
            {
                _logger.LogInformation("Customer sync skipped: no ERP records returned.");
                return;
            }

            _logger.LogInformation("Customer sync fetched {Count} ERP records.", erpResponse.Data.Count);

            var existingCustomers = await _unitOfWork.Customers
                .Query(tracking: true, ignoreQueryFilters: true)
                .ToListAsync().ConfigureAwait(false);

            var customerByCode = existingCustomers
                .Where(x => !string.IsNullOrWhiteSpace(x.CustomerCode))
                .ToDictionary(x => x.CustomerCode!, StringComparer.OrdinalIgnoreCase);

            var newCustomers = new List<Customer>();
            var hasAnyChange = false;
            var createdCount = 0;
            var updatedCount = 0;
            var reactivatedCount = 0;

            foreach (var erpCustomer in erpResponse.Data)
            {
                var code = erpCustomer.CariKod?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                // RII_CUSTOMER için null dönmemesi gereken alanlar: ERP null gelirse '' veya 0 atanır
                var taxOffice = erpCustomer.VergiDairesi ?? string.Empty;
                var taxNumber = erpCustomer.VergiNumarasi ?? string.Empty;
                var tcknNumber = erpCustomer.TcknNumber ?? string.Empty;
                var email = erpCustomer.Email ?? string.Empty;
                var website = erpCustomer.Web ?? string.Empty;
                var phone1 = erpCustomer.CariTel ?? string.Empty;
                var address = erpCustomer.CariAdres ?? string.Empty;
                var branchCode = erpCustomer.SubeKodu;
                var businessUnitCode = erpCustomer.IsletmeKodu;

                if (!customerByCode.TryGetValue(code, out var customer))
                {
                    var name = string.IsNullOrWhiteSpace(erpCustomer.CariIsim) ? code : (erpCustomer.CariIsim ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(name)) name = code;
                    newCustomers.Add(new Customer
                    {
                        CustomerCode = code,
                        CustomerName = name,
                        TaxOffice = taxOffice,
                        TaxNumber = taxNumber,
                        TcknNumber = tcknNumber,
                        Email = email,
                        Website = website,
                        Phone1 = phone1,
                        Address = address,
                        BranchCode = branchCode,
                        BusinessUnitCode = businessUnitCode,
                        IsERPIntegrated = true,
                        ERPIntegrationNumber = code,
                        LastSyncDate = DateTime.UtcNow
                    });
                    createdCount++;
                    hasAnyChange = true;
                    continue;
                }

                var updated = false;
                var reactivated = false;
                var newName = string.IsNullOrWhiteSpace(erpCustomer.CariIsim) ? code : (erpCustomer.CariIsim ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(newName)) newName = code;

                if (customer.CustomerName != newName) { customer.CustomerName = newName; updated = true; }
                if (customer.TaxOffice != taxOffice) { customer.TaxOffice = taxOffice; updated = true; }
                if (customer.TaxNumber != taxNumber) { customer.TaxNumber = taxNumber; updated = true; }
                if (customer.TcknNumber != tcknNumber) { customer.TcknNumber = tcknNumber; updated = true; }
                if (customer.Email != email) { customer.Email = email; updated = true; }
                if (customer.Website != website) { customer.Website = website; updated = true; }
                if (customer.Phone1 != phone1) { customer.Phone1 = phone1; updated = true; }
                if (customer.Address != address) { customer.Address = address; updated = true; }
                if (customer.BranchCode != branchCode) { customer.BranchCode = branchCode; updated = true; }
                if (customer.BusinessUnitCode != businessUnitCode) { customer.BusinessUnitCode = businessUnitCode; updated = true; }

                if (customer.IsDeleted)
                {
                    customer.IsDeleted = false;
                    customer.DeletedDate = null;
                    customer.DeletedBy = null;
                    updated = true;
                    reactivated = true;
                    reactivatedCount++;
                }

                if (customer.IsERPIntegrated != true) { customer.IsERPIntegrated = true; updated = true; }
                if (customer.ERPIntegrationNumber != code) { customer.ERPIntegrationNumber = code; updated = true; }

                if (updated)
                {
                    customer.UpdatedDate = DateTimeProvider.Now;
                    customer.UpdatedBy = null; // ERP sync: background job, kullanıcı yok
                    customer.LastSyncDate = DateTime.UtcNow;
                    hasAnyChange = true;
                    if (!reactivated)
                        updatedCount++;
                }
            }

            // ERP'de olmayan müşteriler silinmez; CRM'de manuel eklenen potansiyel müşteriler korunur.

            if (!hasAnyChange)
            {
                _logger.LogInformation("Customer sync completed: no changes detected.");
                return;
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                if (newCustomers.Count > 0)
                    await _unitOfWork.Customers.AddAllAsync(newCustomers).ConfigureAwait(false);

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                _logger.LogInformation(
                    "Customer sync completed: created={Created}, updated={Updated}, reactivated={Reactivated}.",
                    createdCount,
                    updatedCount,
                    reactivatedCount);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<ApiResponse<List<CustomerDuplicateCandidateDto>>> GetDuplicateCandidatesAsync()
        {
            try
            {
                var customers = await _unitOfWork.Customers
                    .Query()
                    .Where(x => !x.IsDeleted)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var candidates = new List<CustomerDuplicateCandidateDto>();
                candidates.AddRange(BuildDuplicateCandidates(customers, c => NormalizeDigits(c.TaxNumber), "TaxNumber", 0.95m));
                candidates.AddRange(BuildDuplicateCandidates(customers, c => NormalizeDigits(c.TcknNumber), "TcknNumber", 0.95m));
                candidates.AddRange(BuildDuplicateCandidates(customers, c => NormalizeText(c.CustomerCode), "CustomerCode", 0.85m));

                var merged = candidates
                    .GroupBy(x => new { x.MasterCustomerId, x.DuplicateCustomerId })
                    .Select(g => g.OrderByDescending(x => x.Score).First())
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.MasterCustomerId)
                    .ThenBy(x => x.DuplicateCustomerId)
                    .ToList();

                return ApiResponse<List<CustomerDuplicateCandidateDto>>.SuccessResult(
                    merged,
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCandidatesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CustomerDuplicateCandidateDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCandidatesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<CustomerGetDto>> MergeCustomersAsync(CustomerMergeRequestDto request)
        {
            try
            {
                if (request.MasterCustomerId <= 0 || request.DuplicateCustomerId <= 0 || request.MasterCustomerId == request.DuplicateCustomerId)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.InvalidMergeRequest"),
                        _localizationService.GetLocalizedString("CustomerService.InvalidMergeRequest"),
                        StatusCodes.Status400BadRequest);
                }

                var master = await _unitOfWork.Customers.GetByIdForUpdateAsync(request.MasterCustomerId).ConfigureAwait(false);
                var duplicate = await _unitOfWork.Customers.GetByIdForUpdateAsync(request.DuplicateCustomerId).ConfigureAwait(false);
                if (master == null || duplicate == null || master.IsDeleted || duplicate.IsDeleted)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var duplicateContacts = await _unitOfWork.Contacts
                    .Query(tracking: true)
                    .Where(x => !x.IsDeleted && x.CustomerId == duplicate.Id)
                    .ToListAsync().ConfigureAwait(false);

                var masterContactKeys = await _unitOfWork.Contacts
                    .Query()
                    .Where(x => !x.IsDeleted && x.CustomerId == master.Id)
                    .Select(x => BuildContactKey(x.FullName, x.Email, x.Mobile, x.Phone))
                    .ToListAsync().ConfigureAwait(false);

                var masterContactSet = masterContactKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var contact in duplicateContacts)
                {
                    var key = BuildContactKey(contact.FullName, contact.Email, contact.Mobile, contact.Phone);
                    if (masterContactSet.Contains(key))
                    {
                        await _unitOfWork.Contacts.SoftDeleteAsync(contact.Id).ConfigureAwait(false);
                        continue;
                    }

                    contact.CustomerId = master.Id;
                    await _unitOfWork.Contacts.UpdateAsync(contact).ConfigureAwait(false);
                    masterContactSet.Add(key);
                }

                if (!request.PreferMasterValues)
                {
                    master.CustomerName = FirstNonEmpty(master.CustomerName, duplicate.CustomerName) ?? master.CustomerName;
                    master.CustomerCode = FirstNonEmpty(master.CustomerCode, duplicate.CustomerCode);
                    master.TaxOffice = FirstNonEmpty(master.TaxOffice, duplicate.TaxOffice);
                    master.TaxNumber = FirstNonEmpty(master.TaxNumber, duplicate.TaxNumber);
                    master.TcknNumber = FirstNonEmpty(master.TcknNumber, duplicate.TcknNumber);
                    master.Email = FirstNonEmpty(master.Email, duplicate.Email);
                    master.Website = FirstNonEmpty(master.Website, duplicate.Website);
                    master.Phone1 = FirstNonEmpty(master.Phone1, duplicate.Phone1);
                    master.Phone2 = FirstNonEmpty(master.Phone2, duplicate.Phone2);
                    master.Address = FirstNonEmpty(master.Address, duplicate.Address);
                    master.Notes = FirstNonEmpty(master.Notes, duplicate.Notes);
                }

                await _unitOfWork.Customers.UpdateAsync(master).ConfigureAwait(false);
                await _unitOfWork.Customers.SoftDeleteAsync(duplicate.Id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation(
                    "Customer merge completed. Master: {MasterId}, Duplicate: {DuplicateId}, PreferMasterValues: {PreferMasterValues}",
                    master.Id,
                    duplicate.Id,
                    request.PreferMasterValues);

                var merged = await _unitOfWork.Customers
                    .Query()
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser)
                    .FirstOrDefaultAsync(c => c.Id == master.Id && !c.IsDeleted).ConfigureAwait(false);

                if (merged == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                return ApiResponse<CustomerGetDto>.SuccessResult(
                    _mapper.Map<CustomerGetDto>(merged),
                    _localizationService.GetLocalizedString("CustomerService.CustomerMerged"));
            }
            catch (Exception ex)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InternalServerError"),
                    _localizationService.GetLocalizedString("CustomerService.MergeCustomersExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<ApiResponse<CustomerGetDto>?> ValidateCustomerGovernanceAsync(CustomerCreateDto dto, long? excludedId)
        {
            NormalizeCustomerDto(dto);

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.CustomerNameRequired"),
                    _localizationService.GetLocalizedString("CustomerService.CustomerNameRequired"),
                    StatusCodes.Status400BadRequest);
            }

            if (!IsValidTaxNumber(dto.TaxNumber))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InvalidTaxNumber"),
                    _localizationService.GetLocalizedString("CustomerService.InvalidTaxNumber"),
                    StatusCodes.Status400BadRequest);
            }

            if (!IsValidTcknNumber(dto.TcknNumber))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InvalidTcknNumber"),
                    _localizationService.GetLocalizedString("CustomerService.InvalidTcknNumber"),
                    StatusCodes.Status400BadRequest);
            }

            if (!IsValidEmail(dto.Email))
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.InvalidEmail"),
                    _localizationService.GetLocalizedString("CustomerService.InvalidEmail"),
                    StatusCodes.Status400BadRequest);
            }

            var tax = NormalizeDigits(dto.TaxNumber);
            var tckn = NormalizeDigits(dto.TcknNumber);
            var customerCode = NormalizeText(dto.CustomerCode);
            var branchCode = dto.BranchCode;

            var duplicateQuery = _unitOfWork.Customers
                .Query()
                .Where(c => !c.IsDeleted);

            if (excludedId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(c => c.Id != excludedId.Value);
            }

            var duplicateCandidates = await duplicateQuery
                .Where(c =>
                    (!string.IsNullOrWhiteSpace(tax) && !string.IsNullOrWhiteSpace(c.TaxNumber)) ||
                    (!string.IsNullOrWhiteSpace(tckn) && !string.IsNullOrWhiteSpace(c.TcknNumber)) ||
                    (!string.IsNullOrWhiteSpace(customerCode) && c.BranchCode == branchCode && !string.IsNullOrWhiteSpace(c.CustomerCode)))
                .Select(c => new
                {
                    c.Id,
                    c.CustomerName,
                    c.TaxNumber,
                    c.TcknNumber,
                    c.CustomerCode,
                    c.BranchCode
                })
                .ToListAsync().ConfigureAwait(false);

            var conflicts = duplicateCandidates
                .SelectMany(c =>
                {
                    var result = new List<CustomerDuplicateConflictDto>();

                    if (!string.IsNullOrWhiteSpace(tax) && NormalizeDigits(c.TaxNumber) == tax)
                    {
                        result.Add(new CustomerDuplicateConflictDto
                        {
                            CustomerId = c.Id,
                            CustomerName = c.CustomerName,
                            Field = "TaxNumber",
                            Value = c.TaxNumber ?? string.Empty,
                            BranchCode = c.BranchCode
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(tckn) && NormalizeDigits(c.TcknNumber) == tckn)
                    {
                        result.Add(new CustomerDuplicateConflictDto
                        {
                            CustomerId = c.Id,
                            CustomerName = c.CustomerName,
                            Field = "TcknNumber",
                            Value = c.TcknNumber ?? string.Empty,
                            BranchCode = c.BranchCode
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(customerCode) &&
                        c.BranchCode == branchCode &&
                        NormalizeText(c.CustomerCode) == customerCode)
                    {
                        result.Add(new CustomerDuplicateConflictDto
                        {
                            CustomerId = c.Id,
                            CustomerName = c.CustomerName,
                            Field = "CustomerCode",
                            Value = c.CustomerCode ?? string.Empty,
                            BranchCode = c.BranchCode
                        });
                    }

                    return result;
                })
                .OrderBy(c => c.CustomerId)
                .ThenBy(c => c.Field)
                .ToList();

            if (conflicts.Count > 0)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCustomer"),
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCustomer"),
                    StatusCodes.Status409Conflict,
                    new CustomerDuplicateConflictListDto
                    {
                        Conflicts = conflicts
                    });
            }

            return null;
        }

        private async Task<ApiResponse<CustomerGetDto>?> ValidateCustomerGovernanceAsync(CustomerUpdateDto dto, long excludedId)
        {
            NormalizeCustomerDto(dto);

            var createLike = new CustomerCreateDto
            {
                Name = dto.Name,
                CustomerCode = dto.CustomerCode,
                TaxOffice = dto.TaxOffice,
                TaxNumber = dto.TaxNumber,
                TcknNumber = dto.TcknNumber,
                Email = dto.Email,
                Website = dto.Website,
                Phone = dto.Phone,
                Phone2 = dto.Phone2,
                Address = dto.Address,
                Notes = dto.Notes,
                CountryId = dto.CountryId,
                CityId = dto.CityId,
                DistrictId = dto.DistrictId,
                CustomerTypeId = dto.CustomerTypeId,
                SalesRepCode = dto.SalesRepCode,
                GroupCode = dto.GroupCode,
                CreditLimit = dto.CreditLimit,
                BranchCode = dto.BranchCode,
                BusinessUnitCode = dto.BusinessUnitCode,
                IsCompleted = dto.IsCompleted
            };

            return await ValidateCustomerGovernanceAsync(createLike, excludedId).ConfigureAwait(false);
        }

        private static void NormalizeCustomerDto(CustomerCreateDto dto)
        {
            dto.CustomerCode = NormalizeNullable(dto.CustomerCode);
            dto.Name = (dto.Name ?? string.Empty).Trim();
            dto.TaxNumber = NormalizeNullable(dto.TaxNumber);
            dto.TaxOffice = NormalizeNullable(dto.TaxOffice);
            dto.TcknNumber = NormalizeNullable(dto.TcknNumber);
            dto.Address = NormalizeNullable(dto.Address);
            dto.Phone = NormalizeNullable(dto.Phone);
            dto.Phone2 = NormalizeNullable(dto.Phone2);
            dto.Email = NormalizeNullable(dto.Email);
            dto.Website = NormalizeNullable(dto.Website);
            dto.Notes = NormalizeNullable(dto.Notes);
            dto.SalesRepCode = NormalizeNullable(dto.SalesRepCode);
            dto.GroupCode = NormalizeNullable(dto.GroupCode);
        }

        private static void NormalizeCustomerDto(CustomerUpdateDto dto)
        {
            dto.CustomerCode = NormalizeNullable(dto.CustomerCode);
            dto.Name = (dto.Name ?? string.Empty).Trim();
            dto.TaxNumber = NormalizeNullable(dto.TaxNumber);
            dto.TaxOffice = NormalizeNullable(dto.TaxOffice);
            dto.TcknNumber = NormalizeNullable(dto.TcknNumber);
            dto.Address = NormalizeNullable(dto.Address);
            dto.Phone = NormalizeNullable(dto.Phone);
            dto.Phone2 = NormalizeNullable(dto.Phone2);
            dto.Email = NormalizeNullable(dto.Email);
            dto.Website = NormalizeNullable(dto.Website);
            dto.Notes = NormalizeNullable(dto.Notes);
            dto.SalesRepCode = NormalizeNullable(dto.SalesRepCode);
            dto.GroupCode = NormalizeNullable(dto.GroupCode);
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<CustomerDuplicateCandidateDto> BuildDuplicateCandidates(
            List<Customer> customers,
            Func<Customer, string> keySelector,
            string matchType,
            decimal score)
        {
            var result = new List<CustomerDuplicateCandidateDto>();
            var groups = customers
                .Select(c => new { Customer = c, Key = keySelector(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var ordered = group.Select(x => x.Customer).OrderBy(x => x.Id).ToList();
                var master = ordered.First();
                foreach (var duplicate in ordered.Skip(1))
                {
                    result.Add(new CustomerDuplicateCandidateDto
                    {
                        MasterCustomerId = master.Id,
                        MasterCustomerName = master.CustomerName,
                        DuplicateCustomerId = duplicate.Id,
                        DuplicateCustomerName = duplicate.CustomerName,
                        MatchType = matchType,
                        Score = score
                    });
                }
            }

            return result;
        }

        private static string BuildContactKey(string? fullName, string? email, string? mobile, string? phone)
        {
            var normalizedName = NormalizeText(fullName);
            var normalizedEmail = NormalizeText(email);
            var normalizedMobile = NormalizeDigits(mobile);
            var normalizedPhone = NormalizeDigits(phone);
            return $"{normalizedName}|{normalizedEmail}|{normalizedMobile}|{normalizedPhone}";
        }

        private static string? FirstNonEmpty(string? first, string? second)
        {
            if (!string.IsNullOrWhiteSpace(first))
                return first;
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        private async Task<(Customer Customer, string Action, bool ShouldRefreshCoordinates)> ResolveBusinessCardCustomerAsync(CustomerCreateFromMobileDto request)
        {
            var normalizedCompanyKey = BusinessCardOcrNormalizer.NormalizeCompanyKey(request.Name);
            var requestedCompanyName = BusinessCardOcrNormalizer.CollapseWhitespace(request.Name);

            IQueryable<Customer> query = _unitOfWork.Customers.Query(tracking: true, ignoreQueryFilters: true);
            if (request.BranchCode.HasValue)
            {
                query = query.Where(c => c.BranchCode == request.BranchCode.Value);
            }

            var prefilterTokens = requestedCompanyName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length >= 3)
                .OrderByDescending(token => token.Length)
                .Take(3)
                .ToList();

            if (prefilterTokens.Count > 0)
            {
                var primaryToken = prefilterTokens[0];
                query = query.Where(c => c.CustomerName.Contains(primaryToken));
            }

            var companyCandidates = await query
                .Where(c => !string.IsNullOrWhiteSpace(c.CustomerName))
                .ToListAsync()
                .ConfigureAwait(false);

            var exactCompanyMatches = companyCandidates
                .Where(c => BusinessCardOcrNormalizer.NormalizeCompanyKey(c.CustomerName) == normalizedCompanyKey)
                .OrderBy(c => c.Id)
                .ToList();

            var activeMatches = exactCompanyMatches.Where(c => !c.IsDeleted).ToList();
            var deletedMatches = exactCompanyMatches.Where(c => c.IsDeleted).ToList();

            if (activeMatches.Count > 1 || (!activeMatches.Any() && deletedMatches.Count > 1))
            {
                var detail = $"sirket-adi=>[{string.Join(", ", exactCompanyMatches.Select(x => x.Id))}] | beklenen=>[{requestedCompanyName}]";
                throw new BusinessCardOcrConflictException(
                    _localizationService.GetLocalizedString("CustomerService.ConflictingCustomerMatches"),
                    _localizationService.GetLocalizedString("CustomerService.ConflictingCustomerMatchesDetail", detail));
            }

            if (activeMatches.Count == 1)
            {
                var existingCustomer = activeMatches[0];
                var shouldRefreshCoordinates = MergeBusinessCardCustomer(existingCustomer, request, overwriteExistingFields: false);
                await _unitOfWork.Customers.UpdateAsync(existingCustomer).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation("Business card OCR reused company {CustomerId} for normalized name {CompanyKey}.", existingCustomer.Id, normalizedCompanyKey);
                return (existingCustomer, BusinessCardOcrActions.Reused, shouldRefreshCoordinates);
            }

            if (deletedMatches.Count == 1)
            {
                var deletedCustomer = deletedMatches[0];
                deletedCustomer.IsDeleted = false;
                deletedCustomer.DeletedDate = null;
                deletedCustomer.DeletedBy = null;
                var shouldRefreshCoordinates = MergeBusinessCardCustomer(deletedCustomer, request, overwriteExistingFields: true);
                await _unitOfWork.Customers.UpdateAsync(deletedCustomer).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation("Business card OCR reactivated company {CustomerId} for normalized name {CompanyKey}.", deletedCustomer.Id, normalizedCompanyKey);
                return (deletedCustomer, BusinessCardOcrActions.Reactivated, shouldRefreshCoordinates);
            }

            var customer = new Customer();
            MergeBusinessCardCustomer(customer, request, overwriteExistingFields: true);
            await _unitOfWork.Customers.AddAsync(customer).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Business card OCR created company {CustomerId} for normalized name {CompanyKey}.", customer.Id, normalizedCompanyKey);
            return (customer, BusinessCardOcrActions.Created, true);
        }

        private async Task<(Title Title, string Action, bool UsedFallbackTitle)> ResolveBusinessCardTitleAsync(string? requestedTitle)
        {
            var localizedUnknownTitle = _localizationService.GetLocalizedString(BusinessCardOcrDefaults.UnknownTitleLocalizationKey);
            var unknownTitleName = string.IsNullOrWhiteSpace(localizedUnknownTitle) ||
                                   string.Equals(localizedUnknownTitle, BusinessCardOcrDefaults.UnknownTitleLocalizationKey, StringComparison.Ordinal)
                ? BusinessCardOcrDefaults.UnknownTitleFallback
                : localizedUnknownTitle;

            var requestedTitleDisplay = BusinessCardOcrNormalizer.NormalizeNullable(requestedTitle);
            var requestedTitleKey = BusinessCardOcrNormalizer.NormalizeTitleKey(requestedTitleDisplay);
            var unknownTitleKey = BusinessCardOcrNormalizer.NormalizeTitleKey(unknownTitleName);

            var titles = await _unitOfWork.Titles
                .Query(tracking: true, ignoreQueryFilters: true)
                .ToListAsync()
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(requestedTitleKey))
            {
                var matchingRequestedTitle = titles.FirstOrDefault(t => BusinessCardOcrNormalizer.NormalizeTitleKey(t.TitleName) == requestedTitleKey);
                if (matchingRequestedTitle != null)
                {
                    var action = matchingRequestedTitle.IsDeleted ? BusinessCardOcrActions.Reactivated : BusinessCardOcrActions.Reused;
                    if (matchingRequestedTitle.IsDeleted)
                    {
                        matchingRequestedTitle.IsDeleted = false;
                        matchingRequestedTitle.DeletedDate = null;
                        matchingRequestedTitle.DeletedBy = null;
                    }

                    matchingRequestedTitle.TitleName = requestedTitleDisplay!;
                    await _unitOfWork.Titles.UpdateAsync(matchingRequestedTitle).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                    _logger.LogInformation("Business card OCR {Action} title {TitleId} using OCR title '{TitleName}'.", action, matchingRequestedTitle.Id, matchingRequestedTitle.TitleName);
                    return (matchingRequestedTitle, action, false);
                }
            }

            var unknownTitle = titles.FirstOrDefault(t => BusinessCardOcrNormalizer.NormalizeTitleKey(t.TitleName) == unknownTitleKey);
            if (unknownTitle != null)
            {
                var action = unknownTitle.IsDeleted ? BusinessCardOcrActions.Reactivated : BusinessCardOcrActions.Reused;
                if (unknownTitle.IsDeleted)
                {
                    unknownTitle.IsDeleted = false;
                    unknownTitle.DeletedDate = null;
                    unknownTitle.DeletedBy = null;
                }

                unknownTitle.TitleName = unknownTitleName;
                await _unitOfWork.Titles.UpdateAsync(unknownTitle).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation("Business card OCR {Action} fallback title {TitleId} as '{TitleName}'.", action, unknownTitle.Id, unknownTitle.TitleName);
                return (unknownTitle, action, true);
            }

            var createdUnknownTitle = new Title
            {
                TitleName = unknownTitleName
            };
            await _unitOfWork.Titles.AddAsync(createdUnknownTitle).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Business card OCR created fallback title {TitleId} as '{TitleName}'.", createdUnknownTitle.Id, createdUnknownTitle.TitleName);
            return (createdUnknownTitle, BusinessCardOcrActions.Created, true);
        }

        private async Task<(Contact? Contact, string? Action)> ResolveBusinessCardContactAsync(CustomerCreateFromMobileDto request, long customerId, long titleId)
        {
            var (firstName, middleName, lastName) = ResolveBusinessCardContactNames(request);
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                return (null, null);
            }

            var fullName = string.Join(" ", new[] { firstName, middleName, lastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var normalizedEmail = BusinessCardOcrNormalizer.NormalizeEmail(request.Email);
            var mobile = SelectMobilePhone(request.Phone, request.Phone2);
            var landline = SelectLandlinePhone(request.Phone, request.Phone2);
            var normalizedMobile = BusinessCardOcrNormalizer.NormalizePhone(mobile);
            var normalizedLandline = BusinessCardOcrNormalizer.NormalizePhone(landline);
            var normalizedFullName = BusinessCardOcrNormalizer.NormalizePersonName(fullName);

            var contacts = await _unitOfWork.Contacts
                .Query(tracking: true, ignoreQueryFilters: true)
                .Where(c => c.CustomerId == customerId &&
                    (
                        (!string.IsNullOrWhiteSpace(normalizedEmail) && !string.IsNullOrWhiteSpace(c.Email)) ||
                        ((!string.IsNullOrWhiteSpace(normalizedMobile) || !string.IsNullOrWhiteSpace(normalizedLandline)) &&
                            (!string.IsNullOrWhiteSpace(c.Mobile) || !string.IsNullOrWhiteSpace(c.Phone))) ||
                        (!string.IsNullOrWhiteSpace(normalizedFullName) && !string.IsNullOrWhiteSpace(c.FullName))
                    ))
                .ToListAsync()
                .ConfigureAwait(false);

            Contact? matchedContact = null;
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                matchedContact = contacts.FirstOrDefault(c => BusinessCardOcrNormalizer.NormalizeEmail(c.Email) == normalizedEmail);
            }

            if (matchedContact == null && !string.IsNullOrWhiteSpace(normalizedMobile))
            {
                matchedContact = contacts.FirstOrDefault(c => BusinessCardOcrNormalizer.NormalizePhone(c.Mobile) == normalizedMobile);
            }

            if (matchedContact == null && !string.IsNullOrWhiteSpace(normalizedLandline))
            {
                matchedContact = contacts.FirstOrDefault(c =>
                {
                    var phoneMatched =
                        BusinessCardOcrNormalizer.NormalizePhone(c.Phone) == normalizedLandline ||
                        BusinessCardOcrNormalizer.NormalizePhone(c.Mobile) == normalizedLandline;

                    if (!phoneMatched)
                    {
                        return false;
                    }

                    return string.IsNullOrWhiteSpace(normalizedFullName) ||
                           BusinessCardOcrNormalizer.NormalizePersonName(c.FullName) == normalizedFullName;
                });
            }

            if (matchedContact == null && !string.IsNullOrWhiteSpace(normalizedFullName))
            {
                matchedContact = contacts.FirstOrDefault(c => BusinessCardOcrNormalizer.NormalizePersonName(c.FullName) == normalizedFullName);
            }

            if (matchedContact != null)
            {
                var action = matchedContact.IsDeleted ? BusinessCardOcrActions.Reactivated : BusinessCardOcrActions.Reused;
                if (matchedContact.IsDeleted)
                {
                    matchedContact.IsDeleted = false;
                    matchedContact.DeletedDate = null;
                    matchedContact.DeletedBy = null;
                }

                MapBusinessCardContactFields(matchedContact, firstName, middleName, lastName, fullName, request, landline, mobile, titleId);
                await _unitOfWork.Contacts.UpdateAsync(matchedContact).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                _logger.LogInformation("Business card OCR {Action} contact {ContactId} under company {CustomerId}.", action, matchedContact.Id, customerId);
                return (matchedContact, action);
            }

            var contact = new Contact();
            MapBusinessCardContactFields(contact, firstName, middleName, lastName, fullName, request, landline, mobile, titleId);
            contact.CustomerId = customerId;

            await _unitOfWork.Contacts.AddAsync(contact).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation("Business card OCR created contact {ContactId} under company {CustomerId}.", contact.Id, customerId);
            return (contact, BusinessCardOcrActions.Created);
        }

        private bool MergeBusinessCardCustomer(Customer customer, CustomerCreateFromMobileDto request, bool overwriteExistingFields)
        {
            var normalizedAddress = BusinessCardOcrNormalizer.NormalizeNullable(request.Address);
            var normalizedWebsite = BusinessCardOcrNormalizer.NormalizeNullable(request.Website);
            var normalizedNotes = BusinessCardOcrNormalizer.NormalizeNullable(request.Notes);
            var normalizedPhone = BusinessCardOcrNormalizer.NormalizeNullable(request.Phone);
            var normalizedPhone2 = BusinessCardOcrNormalizer.NormalizeNullable(request.Phone2);
            var normalizedEmail = BusinessCardOcrNormalizer.NormalizeNullable(request.Email);
            var normalizedSalesRepCode = BusinessCardOcrNormalizer.NormalizeNullable(request.SalesRepCode);
            var normalizedGroupCode = BusinessCardOcrNormalizer.NormalizeNullable(request.GroupCode);
            var requestedCompanyName = BusinessCardOcrNormalizer.CollapseWhitespace(request.Name);

            var shouldRefreshCoordinates =
                customer.Address != normalizedAddress ||
                customer.CountryId != request.CountryId ||
                customer.CityId != request.CityId ||
                customer.DistrictId != request.DistrictId ||
                !customer.Latitude.HasValue ||
                !customer.Longitude.HasValue;

            customer.CustomerName = string.IsNullOrWhiteSpace(customer.CustomerName) ? requestedCompanyName : customer.CustomerName;

            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Address))
                customer.Address = normalizedAddress;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Website))
                customer.Website = normalizedWebsite;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Notes))
                customer.Notes = normalizedNotes;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Phone1))
                customer.Phone1 = normalizedPhone;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Phone2))
                customer.Phone2 = normalizedPhone2;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.Email))
                customer.Email = normalizedEmail;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.SalesRepCode))
                customer.SalesRepCode = normalizedSalesRepCode;
            if (overwriteExistingFields || string.IsNullOrWhiteSpace(customer.GroupCode))
                customer.GroupCode = normalizedGroupCode;
            if (overwriteExistingFields || !customer.CreditLimit.HasValue)
                customer.CreditLimit = request.CreditLimit;
            if (overwriteExistingFields || !customer.CountryId.HasValue)
                customer.CountryId = request.CountryId;
            if (overwriteExistingFields || !customer.CityId.HasValue)
                customer.CityId = request.CityId;
            if (overwriteExistingFields || !customer.DistrictId.HasValue)
                customer.DistrictId = request.DistrictId;
            if (overwriteExistingFields || !customer.CustomerTypeId.HasValue)
                customer.CustomerTypeId = request.CustomerTypeId;

            if (request.BranchCode.HasValue)
            {
                customer.BranchCode = request.BranchCode.Value;
            }
            else if (customer.BranchCode == 0)
            {
                customer.BranchCode = 1;
            }

            if (request.BusinessUnitCode.HasValue)
            {
                customer.BusinessUnitCode = request.BusinessUnitCode.Value;
            }
            else if (customer.BusinessUnitCode == 0)
            {
                customer.BusinessUnitCode = 1;
            }

            return shouldRefreshCoordinates;
        }

        private async Task TryRefreshBusinessCardCoordinatesAsync(Customer customer, CustomerCreateFromMobileDto request, bool shouldRefreshCoordinates)
        {
            if (!shouldRefreshCoordinates)
            {
                return;
            }

            try
            {
                var trackedCustomer = await _unitOfWork.Customers.GetByIdForUpdateAsync(customer.Id).ConfigureAwait(false);
                if (trackedCustomer == null)
                {
                    return;
                }

                trackedCustomer.Address = trackedCustomer.Address ?? BusinessCardOcrNormalizer.NormalizeNullable(request.Address);
                trackedCustomer.CountryId ??= request.CountryId;
                trackedCustomer.CityId ??= request.CityId;
                trackedCustomer.DistrictId ??= request.DistrictId;

                await TryFillCoordinatesFromAddressAsync(trackedCustomer, allowOverwriteExistingCoords: true).ConfigureAwait(false);
                await _unitOfWork.Customers.UpdateAsync(trackedCustomer).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Business card OCR could not refresh coordinates for customer {CustomerId}.", customer.Id);
            }
        }

        private async Task<(bool ImageUploaded, string? ImageUploadError)> TryUploadBusinessCardImageAsync(long customerId, CustomerCreateFromMobileDto request)
        {
            if (request.ImageFile == null || request.ImageFile.Length <= 0)
            {
                return (false, null);
            }

            try
            {
                var uploadResult = await _fileUploadService.UploadCustomerImageAsync(request.ImageFile, customerId).ConfigureAwait(false);
                if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Data))
                {
                    var uploadError = uploadResult.Message ??
                                      uploadResult.ExceptionMessage ??
                                      _localizationService.GetLocalizedString("CustomerService.ImageUploadFailed");
                    _logger.LogWarning("Business card OCR created customer {CustomerId} but image upload failed. Error: {Error}", customerId, uploadError);
                    return (false, uploadError);
                }

                var customerImage = new CustomerImage
                {
                    CustomerId = customerId,
                    ImageUrl = uploadResult.Data,
                    ImageDescription = BusinessCardOcrNormalizer.NormalizeNullable(request.ImageDescription)
                };

                await _unitOfWork.CustomerImages.AddAsync(customerImage).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Business card OCR image upload failed for customer {CustomerId}.", customerId);
                return (false, _localizationService.GetLocalizedString("CustomerService.ImageUploadFailed"));
            }
        }

        private static (string? FirstName, string? MiddleName, string? LastName) ResolveBusinessCardContactNames(CustomerCreateFromMobileDto dto)
        {
            var firstName = BusinessCardOcrNormalizer.NormalizeNullable(dto.ContactFirstName);
            var middleName = BusinessCardOcrNormalizer.NormalizeNullable(dto.ContactMiddleName);
            var lastName = BusinessCardOcrNormalizer.NormalizeNullable(dto.ContactLastName);

            if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
            {
                return (firstName, middleName, lastName);
            }

            var fullName = BusinessCardOcrNormalizer.NormalizeNullable(dto.ContactName);
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (firstName, middleName, lastName);
            }

            var tokens = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (tokens.Count == 0)
            {
                return (firstName, middleName, lastName);
            }

            if (tokens.Count == 1)
            {
                return (tokens[0], null, tokens[0]);
            }

            return (
                tokens.First(),
                tokens.Count > 2 ? string.Join(" ", tokens.Skip(1).Take(tokens.Count - 2)) : null,
                tokens.Last());
        }

        private static bool IsMobilePhone(string? value)
        {
            var normalized = BusinessCardOcrNormalizer.NormalizePhone(value);
            return normalized.Length == 10 && normalized.StartsWith("5", StringComparison.Ordinal);
        }

        private static string? SelectMobilePhone(string? phone1, string? phone2)
        {
            var candidate1 = BusinessCardOcrNormalizer.NormalizeNullable(phone1);
            var candidate2 = BusinessCardOcrNormalizer.NormalizeNullable(phone2);
            if (IsMobilePhone(candidate1))
            {
                return candidate1;
            }

            if (IsMobilePhone(candidate2))
            {
                return candidate2;
            }

            return null;
        }

        private static string? SelectLandlinePhone(string? phone1, string? phone2)
        {
            var candidate1 = BusinessCardOcrNormalizer.NormalizeNullable(phone1);
            var candidate2 = BusinessCardOcrNormalizer.NormalizeNullable(phone2);
            if (!string.IsNullOrWhiteSpace(candidate1) && !IsMobilePhone(candidate1))
            {
                return candidate1;
            }

            if (!string.IsNullOrWhiteSpace(candidate2) && !IsMobilePhone(candidate2))
            {
                return candidate2;
            }

            return null;
        }

        private static void MapBusinessCardContactFields(
            Contact contact,
            string firstName,
            string? middleName,
            string lastName,
            string fullName,
            CustomerCreateFromMobileDto request,
            string? landline,
            string? mobile,
            long titleId)
        {
            contact.Salutation = SalutationType.None;
            contact.FirstName = firstName;
            contact.MiddleName = middleName;
            contact.LastName = lastName;
            contact.FullName = fullName;
            contact.Email = BusinessCardOcrNormalizer.NormalizeNullable(request.Email);
            contact.Phone = BusinessCardOcrNormalizer.NormalizeNullable(landline);
            contact.Mobile = BusinessCardOcrNormalizer.NormalizeNullable(mobile);
            contact.Notes = BusinessCardOcrNormalizer.NormalizeNullable(request.Notes);
            contact.TitleId = titleId;
        }

        private string BuildBusinessCardOcrSuccessMessage(BusinessCardOcrProcessingState processingState)
        {
            var customerAction = processingState.CustomerAction;
            var contactAction = processingState.ContactAction;

            if (string.Equals(customerAction, BusinessCardOcrActions.Created, StringComparison.Ordinal) &&
                string.Equals(contactAction, BusinessCardOcrActions.Created, StringComparison.Ordinal))
            {
                return GetLocalizedBusinessCardMessage(
                    BusinessCardOcrMessageKeys.CustomerAndContactCreated,
                    "Müşteri ve kişi başarıyla oluşturuldu.");
            }

            if (string.Equals(customerAction, BusinessCardOcrActions.Reactivated, StringComparison.Ordinal))
            {
                return GetLocalizedBusinessCardMessage(
                    BusinessCardOcrMessageKeys.CustomerReactivatedAndContactResolved,
                    "Müşteri yeniden aktifleştirildi ve kişi bilgileri işlendi.");
            }

            if (string.Equals(contactAction, BusinessCardOcrActions.Created, StringComparison.Ordinal))
            {
                return GetLocalizedBusinessCardMessage(
                    BusinessCardOcrMessageKeys.ContactCreatedForExistingCustomer,
                    "Mevcut müşteriye yeni kişi eklendi.");
            }

            return GetLocalizedBusinessCardMessage(
                BusinessCardOcrMessageKeys.ExistingCustomerAndContactReused,
                "Müşteri ve kişi zaten sistemde mevcut; mevcut kayıtlar kullanıldı.");
        }

        private string GetLocalizedBusinessCardMessage(string key, string fallback)
        {
            var localized = _localizationService.GetLocalizedString(key);
            return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }

        private static List<Filter> ParseFiltersJson(string? filtersJson)
        {
            if (string.IsNullOrWhiteSpace(filtersJson))
                return new List<Filter>();

            try
            {
                var parsed = JsonSerializer.Deserialize<List<Filter>>(filtersJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return parsed ?? new List<Filter>();
            }
            catch
            {
                return new List<Filter>();
            }
        }

        private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double radiusOfEarthKm = 6371.0;
            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return radiusOfEarthKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        private static string BuildAddressDisplay(string? address, string? district, string? city, string? country)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(address)) parts.Add(address.Trim());
            if (!string.IsNullOrWhiteSpace(district)) parts.Add(district.Trim());
            if (!string.IsNullOrWhiteSpace(city)) parts.Add(city.Trim());
            if (!string.IsNullOrWhiteSpace(country)) parts.Add(country.Trim());
            return parts.Count == 0 ? "-" : string.Join(", ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(char.IsDigit).ToArray());
        }

        private static bool IsValidTaxNumber(string? value)
        {
            var normalized = NormalizeDigits(value);
            return string.IsNullOrWhiteSpace(normalized) || normalized.Length == 10;
        }

        private static bool IsValidTcknNumber(string? value)
        {
            var normalized = NormalizeDigits(value);
            return string.IsNullOrWhiteSpace(normalized) || normalized.Length == 11;
        }

        private static bool IsValidEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            try
            {
                _ = new MailAddress(value.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Müşteri adresinden enlem/boylam doldurur. Standard flow'da sadece boşsa doldurulur;
        /// adres değiştiğinde (allowOverwriteExistingCoords=true) mevcut koordinatlar da güncellenir.
        /// </summary>
        /// <param name="allowOverwriteExistingCoords">true ise mevcut lat/long olsa bile geocoding sonucuyla üzerine yazar.</param>
        private async Task TryFillCoordinatesFromAddressAsync(Customer customer, bool allowOverwriteExistingCoords = false)
        {
            if (!allowOverwriteExistingCoords && customer.Latitude.HasValue && customer.Longitude.HasValue)
                return;

            var fullAddress = await BuildFullAddressAsync(customer.Address, customer.CountryId, customer.CityId, customer.DistrictId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fullAddress))
                return;

            var coords = await _geocodingService.GeocodeAsync(fullAddress).ConfigureAwait(false);
            if (coords.HasValue)
            {
                customer.Latitude = coords.Value.Latitude;
                customer.Longitude = coords.Value.Longitude;
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
