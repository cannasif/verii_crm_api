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

                var query = _unitOfWork.Customers
                    .Query()
                    .Where(c => !c.IsDeleted)
                    .Include(c => c.Country)
                    .Include(c => c.City)
                    .Include(c => c.District)
                    .Include(c => c.CustomerType)
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .Include(c => c.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(Customer.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync();

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync();

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
                var customer = await _unitOfWork.Customers.GetByIdAsync(id);
                if (customer == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

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
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

                var customerDto = _mapper.Map<CustomerGetDto>(customerWithNav ?? customer);
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

                var customerQuery = _unitOfWork.Customers
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
                        .ThenInclude(sa => sa.District)
                    .ApplyFilters(parsedFilters, query.FilterLogic, columnMapping);

                var branchCodeStr = _httpContextAccessor.HttpContext?.Items["BranchCode"]?.ToString();
                if (short.TryParse(branchCodeStr, out var branchCode) && branchCode > 0)
                {
                    customerQuery = customerQuery.Where(c => c.BranchCode == branchCode);
                }

                var customers = await customerQuery.ToListAsync();
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

        public async Task<ApiResponse<CustomerGetDto>> CreateCustomerAsync(CustomerCreateDto customerCreateDto)
        {
            try
            {
                NormalizeCustomerDto(customerCreateDto);
                var governanceError = await ValidateCustomerGovernanceAsync(customerCreateDto, null);
                if (governanceError != null)
                {
                    return governanceError;
                }

                var customer = _mapper.Map<Customer>(customerCreateDto);
                await TryFillCoordinatesFromAddressAsync(customer);
                await _unitOfWork.Customers.AddAsync(customer);
                await _unitOfWork.SaveChangesAsync();

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
                    .FirstOrDefaultAsync(c => c.Id == customer.Id && !c.IsDeleted);

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
                string? normalizeNullable(string? value)
                {
                    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                }

                string normalizeTitleDisplay(string? value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return string.Empty;

                    var parts = value
                        .Trim()
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    return string.Join(" ", parts);
                }

                string normalizeTitleKey(string? value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return string.Empty;

                    return normalizeTitleDisplay(value)
                        .ToUpperInvariant();
                }

                bool isMobile(string? value)
                {
                    var digits = NormalizeDigits(value);
                    if (string.IsNullOrWhiteSpace(digits)) return false;
                    if (digits.Length == 12 && digits.StartsWith("90")) return digits.Substring(2).StartsWith("5");
                    if (digits.Length == 11 && digits.StartsWith("0")) return digits.Substring(1).StartsWith("5");
                    if (digits.Length == 10) return digits.StartsWith("5");
                    return false;
                }

                string? selectMobile(string? phone1, string? phone2)
                {
                    var candidate1 = normalizeNullable(phone1);
                    var candidate2 = normalizeNullable(phone2);
                    if (isMobile(candidate1)) return candidate1;
                    if (isMobile(candidate2)) return candidate2;
                    return null;
                }

                string? selectLandline(string? phone1, string? phone2)
                {
                    var candidate1 = normalizeNullable(phone1);
                    var candidate2 = normalizeNullable(phone2);
                    if (!string.IsNullOrWhiteSpace(candidate1) && !isMobile(candidate1)) return candidate1;
                    if (!string.IsNullOrWhiteSpace(candidate2) && !isMobile(candidate2)) return candidate2;
                    return null;
                }

                (string? FirstName, string? MiddleName, string? LastName) resolveContactNames(CustomerCreateFromMobileDto dto)
                {
                    var firstName = normalizeNullable(dto.ContactFirstName);
                    var middleName = normalizeNullable(dto.ContactMiddleName);
                    var lastName = normalizeNullable(dto.ContactLastName);

                    if (!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName))
                        return (firstName, middleName, lastName);

                    var fullName = normalizeNullable(dto.ContactName);
                    if (string.IsNullOrWhiteSpace(fullName))
                        return (firstName, middleName, lastName);

                    var tokens = fullName
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList();

                    if (tokens.Count == 0)
                        return (firstName, middleName, lastName);

                    if (tokens.Count == 1)
                        return (tokens[0], null, tokens[0]);

                    var fallbackFirstName = tokens.First();
                    var fallbackLastName = tokens.Last();
                    var middleTokens = tokens.Skip(1).Take(tokens.Count - 2).ToList();
                    var fallbackMiddleName = middleTokens.Count > 0 ? string.Join(" ", middleTokens) : null;

                    return (fallbackFirstName, fallbackMiddleName, fallbackLastName);
                }

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

                var requestEmail = request.Email;
                var requestPhone = request.Phone;
                var requestPhone2 = request.Phone2;

                var customerQuery = _unitOfWork.Customers
                    .Query(tracking: false, ignoreQueryFilters: true);

                if (request.BranchCode.HasValue)
                {
                    customerQuery = customerQuery.Where(c => c.BranchCode == request.BranchCode.Value);
                }

                var emailMatchedIds = new HashSet<long>();
                if (!string.IsNullOrWhiteSpace(requestEmail))
                {
                    var ids = await customerQuery
                        .Where(c => c.Email == requestEmail)
                        .Select(c => c.Id)
                        .ToListAsync();
                    foreach (var id in ids)
                        emailMatchedIds.Add(id);
                }

                var phoneMatchedIds = new HashSet<long>();
                if (!string.IsNullOrWhiteSpace(requestPhone) || !string.IsNullOrWhiteSpace(requestPhone2))
                {
                    var ids = await customerQuery
                        .Where(c =>
                            (!string.IsNullOrWhiteSpace(requestPhone) && (c.Phone1 == requestPhone || c.Phone2 == requestPhone)) ||
                            (!string.IsNullOrWhiteSpace(requestPhone2) && (c.Phone1 == requestPhone2 || c.Phone2 == requestPhone2)))
                        .Select(c => c.Id)
                        .ToListAsync();
                    foreach (var id in ids)
                        phoneMatchedIds.Add(id);
                }

                var matchedIds = new HashSet<long>();
                if (emailMatchedIds.Count > 0) foreach (var id in emailMatchedIds) matchedIds.Add(id);
                if (phoneMatchedIds.Count > 0) foreach (var id in phoneMatchedIds) matchedIds.Add(id);

                if (matchedIds.Count > 1)
                {
                    var matchedDetails = new List<string>();
                    if (emailMatchedIds.Count > 0)
                        matchedDetails.Add($"email=>[{string.Join(", ", emailMatchedIds.OrderBy(x => x))}]");
                    if (phoneMatchedIds.Count > 0)
                        matchedDetails.Add($"telefon=>[{string.Join(", ", phoneMatchedIds.OrderBy(x => x))}]");

                    return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                        "2 farklı müşteri bulundu. Önce eşleşmeleri düzelt, sonra kayıt at.",
                        $"Şirket email/telefon alanlarında farklı müşteri ID'leri bulundu. Bulunan müşteriler: {string.Join(" | ", matchedDetails)}",
                        StatusCodes.Status409Conflict);
                }

                var existingCustomerId = matchedIds.FirstOrDefault();

                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    Customer customer;
                    var customerCreated = false;

                    if (existingCustomerId > 0)
                    {
                        var existingCustomer = await _unitOfWork.Customers
                            .Query(tracking: true, ignoreQueryFilters: true)
                            .FirstOrDefaultAsync(c => c.Id == existingCustomerId);

                        if (existingCustomer == null)
                        {
                            return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                                _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                                _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                                StatusCodes.Status404NotFound);
                        }

                        if (existingCustomer.IsDeleted)
                        {
                            existingCustomer.IsDeleted = false;
                            existingCustomer.DeletedDate = null;
                            existingCustomer.DeletedBy = null;
                            await _unitOfWork.Customers.UpdateAsync(existingCustomer);
                            await _unitOfWork.SaveChangesAsync();
                        }

                        customer = existingCustomer;
                    }
                    else
                    {
                        // Geocoding transaction dışında yapılır; ağ gecikmesi DB lock süresini uzatmasın.
                        var mobileFullAddress = await BuildFullAddressAsync(request.Address, request.CountryId, request.CityId, request.DistrictId);
                        var mobileCoords = !string.IsNullOrWhiteSpace(mobileFullAddress)
                            ? await _geocodingService.GeocodeAsync(mobileFullAddress)
                            : null;

                        customer = new Customer
                        {
                            CustomerName = request.Name.Trim(),
                            Email = normalizeNullable(request.Email),
                            Phone1 = normalizeNullable(request.Phone),
                            Phone2 = normalizeNullable(request.Phone2),
                            Address = normalizeNullable(request.Address),
                            Website = normalizeNullable(request.Website),
                            Notes = normalizeNullable(request.Notes),
                            CountryId = request.CountryId,
                            CityId = request.CityId,
                            DistrictId = request.DistrictId,
                            CustomerTypeId = request.CustomerTypeId,
                            SalesRepCode = normalizeNullable(request.SalesRepCode),
                            GroupCode = normalizeNullable(request.GroupCode),
                            CreditLimit = request.CreditLimit,
                            BranchCode = request.BranchCode ?? 1,
                            BusinessUnitCode = request.BusinessUnitCode ?? 1,
                            Latitude = mobileCoords?.Latitude,
                            Longitude = mobileCoords?.Longitude
                        };

                        await _unitOfWork.Customers.AddAsync(customer);
                        await _unitOfWork.SaveChangesAsync();
                        customerCreated = true;
                    }

                    var imageUploaded = false;
                    string? imageUploadError = null;

                    if (request.ImageFile != null && request.ImageFile.Length > 0)
                    {
                        var uploadResult = await _fileUploadService.UploadCustomerImageAsync(request.ImageFile, customer.Id);
                        if (uploadResult.Success && !string.IsNullOrWhiteSpace(uploadResult.Data))
                        {
                            var customerImage = new CustomerImage
                            {
                                CustomerId = customer.Id,
                                ImageUrl = uploadResult.Data,
                                ImageDescription = normalizeNullable(request.ImageDescription)
                            };
                            await _unitOfWork.CustomerImages.AddAsync(customerImage);
                            await _unitOfWork.SaveChangesAsync();
                            imageUploaded = true;
                        }
                        else
                        {
                            imageUploadError = uploadResult.Message ?? uploadResult.ExceptionMessage ?? "Customer image upload failed.";
                            _logger.LogWarning("mobileCreate: customer {CustomerId} created but image upload failed. Error: {Error}", customer.Id, imageUploadError);
                        }
                    }

                    const string fallbackUnknownTitleName = "Bilinmeyen";
                    long? titleId = null;
                    var titleCreated = false;
                    var (contactFirstName, contactMiddleName, contactLastName) = resolveContactNames(request);
                    var hasContact = !string.IsNullOrWhiteSpace(contactFirstName) && !string.IsNullOrWhiteSpace(contactLastName);
                    var requestedTitle = normalizeTitleDisplay(request.Title);
                    var resolvedTitleName = string.IsNullOrWhiteSpace(requestedTitle)
                        ? fallbackUnknownTitleName
                        : requestedTitle;
                    var resolvedTitleKey = normalizeTitleKey(resolvedTitleName);

                    var existingTitle = await _unitOfWork.Titles
                        .Query(tracking: true, ignoreQueryFilters: true)
                        .ToListAsync();

                    var matchedTitle = existingTitle.FirstOrDefault(t => normalizeTitleKey(t.TitleName) == resolvedTitleKey);

                    if (matchedTitle == null)
                    {
                        matchedTitle = new Title
                        {
                            TitleName = resolvedTitleName
                        };
                        await _unitOfWork.Titles.AddAsync(matchedTitle);
                        await _unitOfWork.SaveChangesAsync();
                        titleCreated = true;
                    }
                    else
                    {
                        if (matchedTitle.IsDeleted)
                        {
                            matchedTitle.IsDeleted = false;
                            matchedTitle.DeletedDate = null;
                            matchedTitle.DeletedBy = null;
                        }

                        if (!string.Equals(normalizeTitleDisplay(matchedTitle.TitleName), resolvedTitleName, StringComparison.Ordinal))
                        {
                            matchedTitle.TitleName = resolvedTitleName;
                        }

                        await _unitOfWork.Titles.UpdateAsync(matchedTitle);
                        await _unitOfWork.SaveChangesAsync();
                    }

                    titleId = matchedTitle.Id;

                    long? contactId = null;
                    var contactCreated = false;
                    if (hasContact)
                    {
                        var contactPhone = selectLandline(request.Phone, request.Phone2);
                        var contactMobile = selectMobile(request.Phone, request.Phone2);
                        var fullName = string.Join(" ", new[] { contactFirstName, contactMiddleName, contactLastName }
                            .Where(x => !string.IsNullOrWhiteSpace(x)))
                            .Trim();
                        var normalizedContactFullName = normalizeNullable(fullName)?.ToUpperInvariant();
                        var normalizedContactEmail = normalizeNullable(request.Email)?.ToUpperInvariant();
                        var normalizedContactPhone = NormalizeDigits(contactPhone);
                        var normalizedContactMobile = NormalizeDigits(contactMobile);
                        var normalizedContactNumbers = new HashSet<string>(
                            new[] { normalizedContactPhone, normalizedContactMobile }
                                .Where(x => !string.IsNullOrWhiteSpace(x))!
                        );
                        var hasComparableIdentifier =
                            !string.IsNullOrWhiteSpace(normalizedContactFullName) ||
                            !string.IsNullOrWhiteSpace(normalizedContactEmail) ||
                            normalizedContactNumbers.Count > 0;

                        var sameContacts = await _unitOfWork.Contacts
                            .Query(tracking: true, ignoreQueryFilters: true)
                            .ToListAsync();

                        bool isSameContact(Contact c)
                        {
                            if (!hasComparableIdentifier)
                                return false;

                            var fullNameMatched =
                                !string.IsNullOrWhiteSpace(normalizedContactFullName) &&
                                string.Equals(
                                    normalizeNullable(c.FullName)?.ToUpperInvariant(),
                                    normalizedContactFullName,
                                    StringComparison.Ordinal);

                            var emailMatched =
                                !string.IsNullOrWhiteSpace(normalizedContactEmail) &&
                                string.Equals(
                                    normalizeNullable(c.Email)?.ToUpperInvariant(),
                                    normalizedContactEmail,
                                    StringComparison.Ordinal);

                            var phoneMatched = normalizedContactNumbers.Count > 0 &&
                                               (
                                                   normalizedContactNumbers.Contains(NormalizeDigits(c.Phone)) ||
                                                   normalizedContactNumbers.Contains(NormalizeDigits(c.Mobile))
                                               );

                            return fullNameMatched || emailMatched || phoneMatched;
                        }

                        var matchedExistingActiveContact = sameContacts.FirstOrDefault(c => !c.IsDeleted && isSameContact(c));
                        if (matchedExistingActiveContact != null)
                        {
                            await _unitOfWork.RollbackTransactionAsync();
                            var duplicateContactMessage = _localizationService.GetLocalizedString("CustomerService.MobileOcrDuplicateContact");
                            return ApiResponse<CustomerCreateFromMobileResultDto>.ErrorResult(
                                duplicateContactMessage,
                                duplicateContactMessage,
                                StatusCodes.Status409Conflict);
                        }

                        var matchedDeletedContact = sameContacts.FirstOrDefault(c => c.IsDeleted && isSameContact(c));
                        if (matchedDeletedContact != null)
                        {
                            matchedDeletedContact.IsDeleted = false;
                            matchedDeletedContact.DeletedDate = null;
                            matchedDeletedContact.DeletedBy = null;
                            matchedDeletedContact.Salutation = SalutationType.None;
                            matchedDeletedContact.FirstName = contactFirstName!;
                            matchedDeletedContact.MiddleName = contactMiddleName;
                            matchedDeletedContact.LastName = contactLastName!;
                            matchedDeletedContact.FullName = fullName;
                            matchedDeletedContact.Email = normalizeNullable(request.Email);
                            matchedDeletedContact.Phone = normalizeNullable(contactPhone);
                            matchedDeletedContact.Mobile = normalizeNullable(contactMobile);
                            matchedDeletedContact.Notes = normalizeNullable(request.Notes);
                            matchedDeletedContact.CustomerId = customer.Id;
                            matchedDeletedContact.TitleId = titleId;

                            await _unitOfWork.Contacts.UpdateAsync(matchedDeletedContact);
                            await _unitOfWork.SaveChangesAsync();
                            contactId = matchedDeletedContact.Id;
                        }
                        else
                        {
                            var contact = new Contact
                            {
                                Salutation = SalutationType.None,
                                FirstName = contactFirstName!,
                                MiddleName = contactMiddleName,
                                LastName = contactLastName!,
                                FullName = fullName,
                                Email = normalizeNullable(request.Email),
                                Phone = normalizeNullable(contactPhone),
                                Mobile = normalizeNullable(contactMobile),
                                Notes = normalizeNullable(request.Notes),
                                CustomerId = customer.Id,
                                TitleId = titleId
                            };

                            await _unitOfWork.Contacts.AddAsync(contact);
                            contactCreated = true;

                            await _unitOfWork.SaveChangesAsync();
                            contactId = contact.Id;
                        }
                    }

                    await _unitOfWork.CommitTransactionAsync();

                    var response = new CustomerCreateFromMobileResultDto
                    {
                        CustomerId = customer.Id,
                        CustomerCreated = customerCreated,
                        ContactId = contactId,
                        ContactCreated = contactCreated,
                        TitleId = titleId,
                        TitleCreated = titleCreated,
                        ImageUploaded = imageUploaded,
                        ImageUploadError = imageUploadError
                    };

                    return ApiResponse<CustomerCreateFromMobileResultDto>.SuccessResult(
                        response,
                        _localizationService.GetLocalizedString("CustomerService.CustomerCreated"));
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
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
                var customer = await _unitOfWork.Customers.GetByIdForUpdateAsync(id);
                if (customer == null)
                {
                    return ApiResponse<CustomerGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var governanceError = await ValidateCustomerGovernanceAsync(customerUpdateDto, id);
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
                    await TryFillCoordinatesFromAddressAsync(customer, allowOverwriteExistingCoords: true);
                }
                else
                {
                    _logger.LogDebug("Customer {CustomerId}: address unchanged, keeping existing coordinates.", id);
                }

                await _unitOfWork.Customers.UpdateAsync(customer);
                await _unitOfWork.SaveChangesAsync();

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
                    .FirstOrDefaultAsync(c => c.Id == customer.Id && !c.IsDeleted);

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
                var customer = await _unitOfWork.Customers.GetByIdAsync(id);
                if (customer == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.Customers.SoftDeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

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
            var erpResponse = await _erpService.GetCarisAsync(null);

            if (erpResponse?.Data == null || erpResponse.Data.Count == 0)
            {
                _logger.LogInformation("Customer sync skipped: no ERP records returned.");
                return;
            }

            _logger.LogInformation("Customer sync fetched {Count} ERP records.", erpResponse.Data.Count);

            var existingCustomers = await _unitOfWork.Customers
                .Query(tracking: true, ignoreQueryFilters: true)
                .ToListAsync();

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

                if (!customerByCode.TryGetValue(code, out var customer))
                {
                    var name = string.IsNullOrWhiteSpace(erpCustomer.CariIsim) ? code : erpCustomer.CariIsim!.Trim();
                    newCustomers.Add(new Customer
                    {
                        CustomerCode = code,
                        CustomerName = name,
                        TaxOffice = erpCustomer.VergiDairesi,
                        TaxNumber = erpCustomer.VergiNumarasi,
                        TcknNumber = erpCustomer.TcknNumber,
                        Email = erpCustomer.Email,
                        Website = erpCustomer.Web,
                        Phone1 = erpCustomer.CariTel,
                        Address = erpCustomer.CariAdres,
                        BranchCode = erpCustomer.SubeKodu,
                        BusinessUnitCode = erpCustomer.IsletmeKodu,
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
                var newName = string.IsNullOrWhiteSpace(erpCustomer.CariIsim) ? code : erpCustomer.CariIsim!.Trim();

                if (customer.CustomerName != newName) { customer.CustomerName = newName; updated = true; }
                if (customer.TaxOffice != erpCustomer.VergiDairesi) { customer.TaxOffice = erpCustomer.VergiDairesi; updated = true; }
                if (customer.TaxNumber != erpCustomer.VergiNumarasi) { customer.TaxNumber = erpCustomer.VergiNumarasi; updated = true; }
                if (customer.TcknNumber != erpCustomer.TcknNumber) { customer.TcknNumber = erpCustomer.TcknNumber; updated = true; }
                if (customer.Email != erpCustomer.Email) { customer.Email = erpCustomer.Email; updated = true; }
                if (customer.Website != erpCustomer.Web) { customer.Website = erpCustomer.Web; updated = true; }
                if (customer.Phone1 != erpCustomer.CariTel) { customer.Phone1 = erpCustomer.CariTel; updated = true; }
                if (customer.Address != erpCustomer.CariAdres) { customer.Address = erpCustomer.CariAdres; updated = true; }
                if (customer.BranchCode != erpCustomer.SubeKodu) { customer.BranchCode = erpCustomer.SubeKodu; updated = true; }
                if (customer.BusinessUnitCode != erpCustomer.IsletmeKodu) { customer.BusinessUnitCode = erpCustomer.IsletmeKodu; updated = true; }

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
                await _unitOfWork.BeginTransactionAsync();

                if (newCustomers.Count > 0)
                    await _unitOfWork.Customers.AddAllAsync(newCustomers);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation(
                    "Customer sync completed: created={Created}, updated={Updated}, reactivated={Reactivated}.",
                    createdCount,
                    updatedCount,
                    reactivatedCount);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
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
                    .ToListAsync();

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

                var master = await _unitOfWork.Customers.GetByIdForUpdateAsync(request.MasterCustomerId);
                var duplicate = await _unitOfWork.Customers.GetByIdForUpdateAsync(request.DuplicateCustomerId);
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
                    .ToListAsync();

                var masterContactKeys = await _unitOfWork.Contacts
                    .Query()
                    .Where(x => !x.IsDeleted && x.CustomerId == master.Id)
                    .Select(x => BuildContactKey(x.FullName, x.Email, x.Mobile, x.Phone))
                    .ToListAsync();

                var masterContactSet = masterContactKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var contact in duplicateContacts)
                {
                    var key = BuildContactKey(contact.FullName, contact.Email, contact.Mobile, contact.Phone);
                    if (masterContactSet.Contains(key))
                    {
                        await _unitOfWork.Contacts.SoftDeleteAsync(contact.Id);
                        continue;
                    }

                    contact.CustomerId = master.Id;
                    await _unitOfWork.Contacts.UpdateAsync(contact);
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

                await _unitOfWork.Customers.UpdateAsync(master);
                await _unitOfWork.Customers.SoftDeleteAsync(duplicate.Id);
                await _unitOfWork.SaveChangesAsync();

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
                    .FirstOrDefaultAsync(c => c.Id == master.Id && !c.IsDeleted);

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
                    c.TaxNumber,
                    c.TcknNumber,
                    c.CustomerCode,
                    c.BranchCode
                })
                .ToListAsync();

            var isDuplicate = duplicateCandidates.Any(c =>
                (!string.IsNullOrWhiteSpace(tax) && NormalizeDigits(c.TaxNumber) == tax) ||
                (!string.IsNullOrWhiteSpace(tckn) && NormalizeDigits(c.TcknNumber) == tckn) ||
                (!string.IsNullOrWhiteSpace(customerCode) && c.BranchCode == branchCode && NormalizeText(c.CustomerCode) == customerCode));

            if (isDuplicate)
            {
                return ApiResponse<CustomerGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCustomer"),
                    _localizationService.GetLocalizedString("CustomerService.DuplicateCustomer"),
                    StatusCodes.Status409Conflict);
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

            return await ValidateCustomerGovernanceAsync(createLike, excludedId);
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

            var fullAddress = await BuildFullAddressAsync(customer.Address, customer.CountryId, customer.CityId, customer.DistrictId);
            if (string.IsNullOrWhiteSpace(fullAddress))
                return;

            var coords = await _geocodingService.GeocodeAsync(fullAddress);
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
                var district = await _unitOfWork.Districts.GetByIdAsync(districtId.Value);
                if (district?.Name != null)
                    parts.Add(district.Name.Trim());
            }
            if (cityId.HasValue)
            {
                var city = await _unitOfWork.Cities.GetByIdAsync(cityId.Value);
                if (city?.Name != null)
                    parts.Add(city.Name.Trim());
            }
            if (countryId.HasValue)
            {
                var country = await _unitOfWork.Countries.GetByIdAsync(countryId.Value);
                if (country?.Name != null)
                    parts.Add(country.Name.Trim());
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
