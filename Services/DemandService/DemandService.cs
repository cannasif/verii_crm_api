using AutoMapper;
using crm_api.DTOs;
using crm_api.Models;
using crm_api.Interfaces;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using crm_api.Helpers;
using System;
using System.Security.Claims;
using System.Linq;
using Hangfire;
using Infrastructure.BackgroundJobs.Interfaces;
using Microsoft.Extensions.Configuration;
using crm_api.Models.Notification;
using crm_api.DTOs.NotificationDto;


namespace crm_api.Services
{
    public class DemandService : IDemandService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IErpService _erpService;
        private readonly IDocumentSerialTypeService _documentSerialTypeService;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;

        public DemandService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IHttpContextAccessor httpContextAccessor,
            IErpService erpService,
            IDocumentSerialTypeService documentSerialTypeService,
            IConfiguration configuration,
            IUserService userService,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _httpContextAccessor = httpContextAccessor;
            _erpService = erpService;
            _documentSerialTypeService = documentSerialTypeService;
            _configuration = configuration;
            _userService = userService;
            _notificationService = notificationService;
        }

        public async Task<ApiResponse<PagedResponse<DemandGetDto>>> GetAllDemandsAsync(PagedRequest request)
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
                    { "potentialCustomerName", "PotentialCustomer.CustomerName" },
                    { "documentSerialTypeName", "DocumentSerialType.SerialPrefix" },
                    { "salesTypeDefinitionName", "SalesTypeDefinition.Name" }
                };

                var query = _unitOfWork.Demands.Query()
                    .AsNoTracking()
                    .Where(q => !q.IsDeleted)
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(Demand.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<DemandGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DemandGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DemandGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DemandService.DemandsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DemandGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetAllDemandsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandGetDto>> GetDemandByIdAsync(long id)
        {
            try
            {
                var demand = await _unitOfWork.Demands.GetByIdAsync(id).ConfigureAwait(false);
                if (demand == null)
                {
                    return ApiResponse<DemandGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                    _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                    StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var demandWithNav = await _unitOfWork.Demands.Query()
                    .AsNoTracking()
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted).ConfigureAwait(false);

                var demandDto = _mapper.Map<DemandGetDto>(demandWithNav ?? demand);
                return ApiResponse<DemandGetDto>.SuccessResult(demandDto, _localizationService.GetLocalizedString("DemandService.DemandRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetDemandByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandDto>> CreateDemandAsync(CreateDemandDto createDemandDto)
        {
            try
            {
                var demand = _mapper.Map<Demand>(createDemandDto);
                demand.GeneralDiscountRate = createDemandDto.GeneralDiscountRate;
                demand.GeneralDiscountAmount = createDemandDto.GeneralDiscountAmount;
                demand.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.Demands.AddAsync(demand).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var demandDto = _mapper.Map<DemandDto>(demand);
                return ApiResponse<DemandDto>.SuccessResult(demandDto, _localizationService.GetLocalizedString("DemandService.DemandCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.CreateDemandExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<DemandDto>> UpdateDemandAsync(long id, UpdateDemandDto updateDemandDto)
        {
            try
            {
                // Get userId from HttpContext (should be set by middleware)
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    return ApiResponse<DemandDto>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var demand = await _unitOfWork.Demands.Query()
                    .Include(q => q.Lines)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted).ConfigureAwait(false);

                if (demand == null)
                {
                    return ApiResponse<DemandDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotFound"),
                        "Not found",
                        StatusCodes.Status404NotFound);
                }


                // 3. Güncelleme işlemi
                _mapper.Map(updateDemandDto, demand);
                demand.GeneralDiscountRate = updateDemandDto.GeneralDiscountRate;
                demand.GeneralDiscountAmount = updateDemandDto.GeneralDiscountAmount;
                demand.UpdatedDate = DateTime.UtcNow;
                demand.UpdatedBy = userId;

                // 4. Toplamları yeniden hesapla
                decimal total = 0m;
                decimal grandTotal = 0m;

                foreach (var line in demand.Lines.Where(l => !l.IsDeleted))
                {
                    total += line.LineTotal;
                    grandTotal += line.LineGrandTotal;
                }

                demand.Total = total;
                demand.GrandTotal = grandTotal;

                await _unitOfWork.Demands.UpdateAsync(demand).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var demandDto = _mapper.Map<DemandDto>(demand);
                return ApiResponse<DemandDto>.SuccessResult(demandDto, _localizationService.GetLocalizedString("DemandService.DemandUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.UpdateDemandExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<object>> DeleteDemandAsync(long id)
        {
            try
            {
                // Get userId from HttpContext (should be set by middleware)
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    return ApiResponse<object>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;


                var demand = await _unitOfWork.Demands.GetByIdAsync(id).ConfigureAwait(false);
                if (demand == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotFound"),
                        "Not found",
                        StatusCodes.Status404NotFound);
                }


                // 3. Soft delete
                await _unitOfWork.Demands.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("DemandService.DemandDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.DeleteDemandExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<DemandGetDto>>> GetDemandsByPotentialCustomerIdAsync(long potentialCustomerId)
        {
            try
            {
                var demands = await _unitOfWork.Demands.FindAsync(q => q.PotentialCustomerId == potentialCustomerId).ConfigureAwait(false);
                var demandDtos = _mapper.Map<List<DemandGetDto>>(demands.ToList());
                return ApiResponse<List<DemandGetDto>>.SuccessResult(demandDtos, _localizationService.GetLocalizedString("DemandService.DemandsByPotentialCustomerRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetDemandsByPotentialCustomerExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<DemandGetDto>>> GetDemandsByRepresentativeIdAsync(long representativeId)
        {
            try
            {
                var demands = await _unitOfWork.Demands.FindAsync(q => q.RepresentativeId == representativeId).ConfigureAwait(false);
                var demandDtos = _mapper.Map<List<DemandGetDto>>(demands.ToList());
                return ApiResponse<List<DemandGetDto>>.SuccessResult(demandDtos, _localizationService.GetLocalizedString("DemandService.DemandsByRepresentativeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetDemandsByRepresentativeExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<DemandGetDto>>> GetDemandsByStatusAsync(int status)
        {
            try
            {
                var demands = await _unitOfWork.Demands.FindAsync(q => (int?)q.Status == status).ConfigureAwait(false);
                var demandDtos = _mapper.Map<List<DemandGetDto>>(demands.ToList());
                return ApiResponse<List<DemandGetDto>>.SuccessResult(demandDtos, _localizationService.GetLocalizedString("DemandService.DemandsByStatusRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DemandGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetDemandsByStatusExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<bool>> DemandExistsAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.Demands.ExistsAsync(id).ConfigureAwait(false);
                return ApiResponse<bool>.SuccessResult(exists, exists ? _localizationService.GetLocalizedString("DemandService.DemandRetrieved") : _localizationService.GetLocalizedString("DemandService.DemandNotFound"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.DemandExistsExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<DemandGetDto>> CreateDemandBulkAsync(DemandBulkCreateDto bulkDto)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var documentSerialType = await _documentSerialTypeService.GenerateDocumentSerialAsync(bulkDto.Demand.DocumentSerialTypeId).ConfigureAwait(false);
                if (!documentSerialType.Success)
                {
                    return ApiResponse<DemandGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DocumentSerialTypeGenerationError"),
                        documentSerialType.Message,
                        StatusCodes.Status500InternalServerError);
                }
                bulkDto.Demand.OfferNo = documentSerialType.Data;
                bulkDto.Demand.RevisionNo = documentSerialType.Data;
                bulkDto.Demand.Status = ApprovalStatus.HavenotStarted;

                // 1. Header map
                var demand = _mapper.Map<Demand>(bulkDto.Demand);
                demand.GeneralDiscountRate = bulkDto.Demand.GeneralDiscountRate;
                demand.GeneralDiscountAmount = bulkDto.Demand.GeneralDiscountAmount;

                decimal total = 0m;
                decimal grandTotal = 0m;

                // 2. Header totals calculation
                foreach (var lineDto in bulkDto.Lines)
                {
                    var calc = CalculateLine(
                        lineDto.Quantity,
                        lineDto.UnitPrice,
                        lineDto.DiscountRate1,
                        lineDto.DiscountRate2,
                        lineDto.DiscountRate3,
                        lineDto.DiscountAmount1,
                        lineDto.DiscountAmount2,
                        lineDto.DiscountAmount3,
                        lineDto.VatRate
                    );

                    total += calc.NetTotal;
                    grandTotal += calc.GrandTotal;
                }

                demand.Total = total;
                demand.GrandTotal = grandTotal;

                // 3. Save header
                await _unitOfWork.Demands.AddAsync(demand).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // 4. Map & calculate lines
                var lines = new List<DemandLine>(bulkDto.Lines.Count);

                foreach (var lineDto in bulkDto.Lines)
                {
                    var line = _mapper.Map<DemandLine>(lineDto);
                    line.DemandId = demand.Id;

                    var calc = CalculateLine(
                        line.Quantity,
                        line.UnitPrice,
                        line.DiscountRate1,
                        line.DiscountRate2,
                        line.DiscountRate3,
                        line.DiscountAmount1,
                        line.DiscountAmount2,
                        line.DiscountAmount3,
                        line.VatRate
                    );

                    line.LineTotal = calc.NetTotal;
                    line.VatAmount = calc.VatAmount;
                    line.LineGrandTotal = calc.GrandTotal;

                    lines.Add(line);
                }

                await _unitOfWork.DemandLines.AddAllAsync(lines).ConfigureAwait(false);

                // 5. Demand notes
                if (bulkDto.DemandNotes != null)
                {
                    var demandNotes = _mapper.Map<DemandNotes>(bulkDto.DemandNotes);
                    demandNotes.DemandId = demand.Id;
                    await _unitOfWork.DemandNotes.AddAsync(demandNotes).ConfigureAwait(false);
                }

                // 6. Exchange rates
                if (bulkDto.ExchangeRates?.Any() == true)
                {
                    var rates = bulkDto.ExchangeRates
                        .Select(r =>
                        {
                            var rate = _mapper.Map<DemandExchangeRate>(r);
                            rate.DemandId = demand.Id;
                            return rate;
                        }).ToList();

                    await _unitOfWork.DemandExchangeRates.AddAllAsync(rates).ConfigureAwait(false);
                }

                // 6. Commit
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // 8. Reload
                var demandWithNav = await _unitOfWork.Demands
                    .Query()
                    .AsNoTracking()
                    .Include(q => q.Representative)
                    .Include(q => q.Lines)
                    .Include(q => q.PotentialCustomer)
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .FirstOrDefaultAsync(q => q.Id == demand.Id).ConfigureAwait(false);

                var dto = _mapper.Map<DemandGetDto>(demandWithNav);

                return ApiResponse<DemandGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("DemandService.DemandCreated"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);

                return ApiResponse<DemandGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.CreateDemandBulkExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        private static LineCalculationResult CalculateLine(decimal quantity, decimal unitPrice, decimal discountRate1, decimal discountRate2, decimal discountRate3, decimal discountAmount1, decimal discountAmount2, decimal discountAmount3, decimal vatRate)
        {
            decimal gross = quantity * unitPrice;

            // Sequential discount rates
            decimal netAfterRates = gross;
            netAfterRates *= (1 - discountRate1 / 100m);
            netAfterRates *= (1 - discountRate2 / 100m);
            netAfterRates *= (1 - discountRate3 / 100m);

            // Discount amounts
            decimal net = netAfterRates
                - discountAmount1
                - discountAmount2
                - discountAmount3;

            if (net < 0)
                net = 0;

            net = Math.Round(net, 2, MidpointRounding.AwayFromZero);

            decimal vat = Math.Round(net * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal grandTotal = net + vat;

            return new LineCalculationResult
            {
                NetTotal = net,
                VatAmount = vat,
                GrandTotal = grandTotal
            };
        }

        private sealed class LineCalculationResult
        {
            public decimal NetTotal { get; init; }
            public decimal VatAmount { get; init; }
            public decimal GrandTotal { get; init; }
        }


        public async Task<ApiResponse<DemandGetDto>> CreateRevisionOfDemandAsync(long demandId)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<DemandGetDto>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var demand = await _unitOfWork.Demands.GetByIdAsync(demandId).ConfigureAwait(false);
                if (demand == null)
                {
                    return ApiResponse<DemandGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var demandLines = await _unitOfWork.DemandLines.Query()
                .Where(x => !x.IsDeleted && x.DemandId == demandId).ToListAsync().ConfigureAwait(false);

                var DemandExchangeRates = await _unitOfWork.DemandExchangeRates.Query()
                .Where(x => !x.IsDeleted && x.DemandId == demandId).ToListAsync().ConfigureAwait(false);

                var demandNotes = await _unitOfWork.DemandNotes.Query()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.DemandId == demandId).ConfigureAwait(false);

                var documentSerialTypeWithRevision = await _documentSerialTypeService.GenerateDocumentSerialAsync(demand.DocumentSerialTypeId, false, demand.RevisionNo).ConfigureAwait(false);
                if (!documentSerialTypeWithRevision.Success)
                {
                    return ApiResponse<DemandGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DocumentSerialTypeGenerationError"),
                        documentSerialTypeWithRevision.Message,
                        StatusCodes.Status500InternalServerError);
                }

                var newDemand = new Demand();
                newDemand.OfferType = demand.OfferType;
                newDemand.RevisionId = demand.Id;
                newDemand.OfferDate = demand.OfferDate;
                newDemand.OfferNo = demand.OfferNo;
                newDemand.RevisionNo = documentSerialTypeWithRevision.Data;
                newDemand.OfferDate = demand.OfferDate;
                newDemand.Currency = demand.Currency;
                newDemand.GeneralDiscountRate = demand.GeneralDiscountRate;
                newDemand.GeneralDiscountAmount = demand.GeneralDiscountAmount;
                newDemand.Total = demand.Total;
                newDemand.GrandTotal = demand.GrandTotal;
                newDemand.CreatedBy = userId;
                newDemand.CreatedDate = DateTime.UtcNow;
                newDemand.PotentialCustomerId = demand.PotentialCustomerId;
                newDemand.ErpCustomerCode = demand.ErpCustomerCode;
                newDemand.ContactId = demand.ContactId;
                newDemand.ValidUntil = demand.ValidUntil;
                newDemand.DeliveryDate = demand.DeliveryDate;
                newDemand.ShippingAddressId = demand.ShippingAddressId;
                newDemand.RepresentativeId = demand.RepresentativeId;
                newDemand.ActivityId = demand.ActivityId;
                newDemand.Description = demand.Description;
                newDemand.PaymentTypeId = demand.PaymentTypeId;
                newDemand.HasCustomerSpecificDiscount = demand.HasCustomerSpecificDiscount;
                newDemand.SalesTypeDefinitionId = demand.SalesTypeDefinitionId;
                newDemand.ErpProjectCode = demand.ErpProjectCode;
                newDemand.Status = (int)ApprovalStatus.HavenotStarted;

                await _unitOfWork.Demands.AddAsync(newDemand).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var newDemandLines = new List<DemandLine>();
                foreach (var line in demandLines)
                {
                    var newLine = new DemandLine();
                    newLine.DemandId = newDemand.Id;
                    newLine.ProductCode = line.ProductCode;
                    newLine.Quantity = line.Quantity;
                    newLine.UnitPrice = line.UnitPrice;
                    newLine.DiscountRate1 = line.DiscountRate1;
                    newLine.DiscountRate2 = line.DiscountRate2;
                    newLine.DiscountRate3 = line.DiscountRate3;
                    newLine.DiscountAmount1 = line.DiscountAmount1;
                    newLine.DiscountAmount2 = line.DiscountAmount2;
                    newLine.DiscountAmount3 = line.DiscountAmount3;
                    newLine.VatRate = line.VatRate;
                    newLine.LineTotal = line.LineTotal;
                    newLine.VatAmount = line.VatAmount;
                    newLine.LineGrandTotal = line.LineGrandTotal;
                    newLine.Description = line.Description;
                    newLine.Description1 = line.Description1;
                    newLine.Description2 = line.Description2;
                    newLine.Description3 = line.Description3;
                    newLine.PricingRuleHeaderId = line.PricingRuleHeaderId;
                    newLine.RelatedStockId = line.RelatedStockId;
                    newLine.RelatedProductKey = line.RelatedProductKey;
                    newLine.IsMainRelatedProduct = line.IsMainRelatedProduct;
                    newLine.ErpProjectCode = line.ErpProjectCode;
                    newLine.ApprovalStatus = ApprovalStatus.HavenotStarted;
                    newDemandLines.Add(newLine);
                }
                await _unitOfWork.DemandLines.AddAllAsync(newDemandLines).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var newDemandExchangeRates = new List<DemandExchangeRate>();
                foreach (var exchangeRate in DemandExchangeRates)
                {
                    var newExchangeRate = new DemandExchangeRate();
                    newExchangeRate.DemandId = newDemand.Id;
                    newExchangeRate.Currency = exchangeRate.Currency;
                    newExchangeRate.ExchangeRate = exchangeRate.ExchangeRate;
                    newExchangeRate.ExchangeRateDate = exchangeRate.ExchangeRateDate;
                    newExchangeRate.IsOfficial = exchangeRate.IsOfficial;
                    newExchangeRate.CreatedDate = DateTime.UtcNow;
                    newExchangeRate.CreatedBy = userId;
                    newDemandExchangeRates.Add(newExchangeRate);
                }
                await _unitOfWork.DemandExchangeRates.AddAllAsync(newDemandExchangeRates).ConfigureAwait(false);

                if (demandNotes != null)
                {
                    var newDemandNotes = new DemandNotes
                    {
                        DemandId = newDemand.Id,
                        Note1 = demandNotes.Note1,
                        Note2 = demandNotes.Note2,
                        Note3 = demandNotes.Note3,
                        Note4 = demandNotes.Note4,
                        Note5 = demandNotes.Note5,
                        Note6 = demandNotes.Note6,
                        Note7 = demandNotes.Note7,
                        Note8 = demandNotes.Note8,
                        Note9 = demandNotes.Note9,
                        Note10 = demandNotes.Note10,
                        Note11 = demandNotes.Note11,
                        Note12 = demandNotes.Note12,
                        Note13 = demandNotes.Note13,
                        Note14 = demandNotes.Note14,
                        Note15 = demandNotes.Note15
                    };

                    await _unitOfWork.DemandNotes.AddAsync(newDemandNotes).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                var dto = _mapper.Map<DemandGetDto>(newDemand);
                return ApiResponse<DemandGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("DemandService.RevisionCreated"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<DemandGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.CreateRevisionExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<PricingRuleLineGetDto>>> GetPriceRuleOfDemandAsync(string customerCode, long salesmanId, DateTime demandDate)
        {
            try
            {
                var branchCodeRequest = await _erpService.GetBranchCodeFromContext().ConfigureAwait(false);
                if (!branchCodeRequest.Success)
                {
                    return ApiResponse<List<PricingRuleLineGetDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ErpService.BranchCodeRetrievalError"),
                        _localizationService.GetLocalizedString("ErpService.BranchCodeRetrievalErrorMessage"),
                        StatusCodes.Status500InternalServerError);
                }

                short branchCode = branchCodeRequest.Data;

                // 1️⃣ Ortak filtre (tek doğruluk kaynağı)
                var baseQuery = _unitOfWork.PricingRuleHeaders.Query()
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.RuleType == PricingRuleType.Demand &&
                        !x.IsDeleted &&
                        x.BranchCode == branchCode &&
                        x.ValidFrom <= demandDate &&
                        x.ValidTo >= demandDate
                    );

                // 2️⃣ İş kuralı öncelik sırası AÇIK
                PricingRuleHeader? header =
                    // 1. Satışçı + Cari
                    await baseQuery
                        .Where(x =>
                            x.ErpCustomerCode == customerCode &&
                            x.Salesmen.Any(s => s.SalesmanId == salesmanId && !s.IsDeleted))
                        .FirstOrDefaultAsync()

                    // 2. Cari var – Satışçı yok
                    ?? await baseQuery
                        .Where(x =>
                            x.ErpCustomerCode == customerCode &&
                            !x.Salesmen.Any(s => !s.IsDeleted))
                        .FirstOrDefaultAsync()

                    // 3. Satışçı var – Cari yok
                    ?? await baseQuery
                        .Where(x =>
                            string.IsNullOrEmpty(x.ErpCustomerCode) &&
                            x.Salesmen.Any(s => s.SalesmanId == salesmanId && !s.IsDeleted))
                        .FirstOrDefaultAsync()

                    // 4. Genel (Cari yok – Satışçı yok)
                    ?? await baseQuery
                        .Where(x =>
                            string.IsNullOrEmpty(x.ErpCustomerCode) &&
                            !x.Salesmen.Any(s => !s.IsDeleted))
                        .FirstOrDefaultAsync().ConfigureAwait(false);
                // 3️⃣ Kural yoksa → bilinçli boş dönüş
                if (header == null)
                {
                    return ApiResponse<List<PricingRuleLineGetDto>>.SuccessResult(
                        new List<PricingRuleLineGetDto>(),
                        _localizationService.GetLocalizedString(
                            "DemandService.PriceRuleNotFound"));
                }

                // 4️⃣ Line’ları getir
                var lines = await _unitOfWork.PricingRuleLines.Query()
                    .AsNoTracking()
                    .Where(x =>
                        x.PricingRuleHeaderId == header.Id &&
                        !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dto = _mapper.Map<List<PricingRuleLineGetDto>>(lines);

                return ApiResponse<List<PricingRuleLineGetDto>>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString(
                        "DemandService.PriceRuleOfDemandRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PricingRuleLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetPriceRuleOfDemandExceptionMessage", ex.Message
                    , StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<PriceOfProductDto>>> GetPriceOfProductAsync(List<PriceOfProductRequestDto> request)
        {
            try
            {
                var price = new List<PriceOfProductDto>();

                // `request` koleksiyonundaki `ProductCode` ve `GroupCode` değerlerini alıyoruz
                var productCodes = request.Select(y => y.ProductCode).ToList();

                // 1. `ProductCode`'a göre fiyat bilgisi almak
                var pricePricing = await _unitOfWork.ProductPricings.Query()
                    .Where(x => productCodes.Contains(x.ErpProductCode) && !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                // Eğer fiyatlar varsa, bunları ekleyelim
                if (pricePricing.Count > 0)
                {
                    foreach (var item in pricePricing)
                    {
                        price.Add(new PriceOfProductDto()
                        {
                            ProductCode = item.ErpProductCode,
                            GroupCode = item.ErpGroupCode,
                            Currency = item.Currency,
                            ListPrice = item.ListPrice,
                            CostPrice = item.CostPrice,
                            Discount1 = item.Discount1,
                            Discount2 = item.Discount2,
                            Discount3 = item.Discount3
                        });
                    }
                }

                // 2. Eğer `ProductCode` için fiyat bilgisi yoksa, `GroupCode`'a göre fiyatları alıyoruz
                var leftBehindProductCodesWithGroup = request
                    .Where(x => !pricePricing.Any(y => y.ErpProductCode == x.ProductCode))  // Fiyatı olmayanları filtrele
                    .Select(x => new { x.ProductCode, x.GroupCode })  // Hem `ProductCode` hem de `GroupCode`'u seç
                    .ToList();  // Belleğe alalım

                // 3. Eğer `GroupCode`'a göre fiyatlar varsa, onları da alıp ekleyelim
                if (leftBehindProductCodesWithGroup.Count > 0)
                {
                    var groupCodeValues = leftBehindProductCodesWithGroup.Select(x => x.GroupCode).ToList();
                    // 2. `ProductPricingGroupBys` tablosundan, sadece `GroupCode`'larına göre fiyatları alıyoruz
                    var priceGroupBy = await _unitOfWork.ProductPricingGroupBys.Query()
                    .Where(x => groupCodeValues.Contains(x.ErpGroupCode) && !x.IsDeleted)  // Grup kodlarıyla eşleşen fiyatları alıyoruz
                        .ToListAsync().ConfigureAwait(false);

                    foreach (var groupItem in priceGroupBy)
                    {
                        // `GroupCode` bazında fiyatları alıyoruz, fakat `ProductCode`'u ilişkili ürünlerle eşleştiriyoruz
                        foreach (var item in leftBehindProductCodesWithGroup.Where(x => x.GroupCode == groupItem.ErpGroupCode))
                        {
                            price.Add(new PriceOfProductDto()
                            {
                                ProductCode = item.ProductCode,  // Fiyat grup bazında alınacak, `ProductCode` grup koduna göre atanır
                                GroupCode = groupItem.ErpGroupCode,
                                Currency = groupItem.Currency,
                                ListPrice = groupItem.ListPrice,
                                CostPrice = groupItem.CostPrice,
                                Discount1 = groupItem.Discount1,
                                Discount2 = groupItem.Discount2,
                                Discount3 = groupItem.Discount3
                            });
                        }
                    }
                }

                return ApiResponse<List<PriceOfProductDto>>.SuccessResult(price, _localizationService.GetLocalizedString("DemandService.PriceOfProductRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PriceOfProductDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetPriceOfProductExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<bool>> StartApprovalFlowAsync(StartApprovalFlowDto request)
        {

            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                // Get userId from HttpContext
                var startedByUserIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!startedByUserIdResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        startedByUserIdResponse.Message,
                        startedByUserIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var startedByUserId = startedByUserIdResponse.Data;

                // 1️⃣ Daha önce başlatılmış mı?
                bool exists = await _unitOfWork.ApprovalRequests.Query()
                    .AnyAsync(x =>
                        x.EntityId == request.EntityId &&
                        x.DocumentType == request.DocumentType &&
                        x.Status == ApprovalStatus.Waiting &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (exists)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalFlowAlreadyExists"),
                        "Bu belge için zaten aktif bir onay süreci var.",
                        StatusCodes.Status400BadRequest);
                }

                // 2️⃣ Aktif flow bul
                var flow = await _unitOfWork.ApprovalFlows.Query()
                    .FirstOrDefaultAsync(x =>
                        x.DocumentType == request.DocumentType &&
                        x.IsActive &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (flow == null)
                {
                    // if there is no flow then Convert to quotation end return success
                    var quotationId = await ConvertToQuotationAsync(request.EntityId).ConfigureAwait(false);
                    if (quotationId.Success)
                    {
                        // Transaction'ı commit et
                        await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("DemandService.ApprovalFlowStarted"));
                    }
                    else
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.ErrorResult(quotationId.Message, quotationId.Message, StatusCodes.Status404NotFound);
                    }
                }

                // 3️⃣ Step'leri sırayla al
                var steps = await _unitOfWork.ApprovalFlowSteps.Query()
                    .Where(x =>
                        x.ApprovalFlowId == flow.Id &&
                        !x.IsDeleted)
                    .OrderBy(x => x.StepOrder)
                    .ToListAsync().ConfigureAwait(false);

                if (!steps.Any())
                {
                    const string stepsNotFoundMessage = "Flow'a ait step tanımı yok.";
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        stepsNotFoundMessage,
                        stepsNotFoundMessage,
                        StatusCodes.Status404NotFound);
                }

                // 4️⃣ Tutarı karşılayan ilk step'i bul
                ApprovalFlowStep? selectedStep = null;
                List<ApprovalRole>? validRoles = null;

                foreach (var step in steps)
                {
                    var roles = await _unitOfWork.ApprovalRoles.Query()
                        .Where(r =>
                            r.ApprovalRoleGroupId == step.ApprovalRoleGroupId &&
                            r.MaxAmount >= request.TotalAmount &&
                            !r.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);

                    if (roles.Any())
                    {
                        selectedStep = step;
                        validRoles = roles;
                        break;
                    }
                }

                if (selectedStep == null || validRoles == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalRoleNotFound"),
                        "Bu tutarı karşılayan onay yetkisi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // 5️⃣ ApprovalRequest oluştur
                var approvalRequest = new ApprovalRequest
                {
                    EntityId = request.EntityId,
                    DocumentType = request.DocumentType,
                    ApprovalFlowId = flow.Id,
                    CurrentStep = selectedStep.StepOrder,
                    Status = ApprovalStatus.Waiting,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = startedByUserId,
                    IsDeleted = false
                };

                await _unitOfWork.ApprovalRequests.AddAsync(approvalRequest).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // 6️⃣ Bu step için onaylayacak kullanıcıları bul
                var roleIds = validRoles.Select(r => r.Id).ToList();
                var userIds = await _unitOfWork.ApprovalUserRoles.Query()
                    .Where(x =>
                        roleIds.Contains(x.ApprovalRoleId) &&
                        !x.IsDeleted)
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToListAsync().ConfigureAwait(false);

                if (!userIds.Any())
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalUsersNotFound"),
                        "Bu step için onay yetkisi olan kullanıcı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // 7️⃣ ApprovalAction kayıtlarını oluştur ve onay maili gönderilecek kullanıcıları topla
                var actions = new List<ApprovalAction>();
                var usersToNotify = new List<(string Email, string FullName, long UserId)>();

                foreach (var userId in userIds)
                {
                    var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted).ConfigureAwait(false);
                    if (user == null)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.ErrorResult(
                            _localizationService.GetLocalizedString("DemandService.UserNotFound"),
                            "Kullanıcı bulunamadı.",
                            StatusCodes.Status404NotFound);
                    }

                    if (!string.IsNullOrWhiteSpace(user.Email))
                        usersToNotify.Add((user.Email, user.FullName, userId));

                    var action = new ApprovalAction
                    {
                        ApprovalRequestId = approvalRequest.Id,
                        StepOrder = selectedStep.StepOrder,
                        ApprovedByUserId = userId,
                        Status = ApprovalStatus.Waiting,
                        ActionDate = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = startedByUserId,
                        IsDeleted = false
                    };

                    actions.Add(action);
                }

                await _unitOfWork.ApprovalActions.AddAllAsync(actions).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var demand = await _unitOfWork.Demands.GetByIdAsync(request.EntityId).ConfigureAwait(false);
                if (demand == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        "Talep bulunamadı.",
                        StatusCodes.Status404NotFound);
                }
                demand.Status = ApprovalStatus.Waiting;

                await _unitOfWork.Demands.UpdateAsync(demand).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Transaction'ı commit et
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                var userIdToActionId = actions.ToDictionary(a => a.ApprovedByUserId, a => a.Id);
                var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                var approvalPath = _configuration["FrontendSettings:ApprovalPendingPath"]?.TrimStart('/') ?? "approvals/pending";
                var demandPath = _configuration["FrontendSettings:DemandDetailPath"]?.TrimStart('/') ?? "demands";

                // Send Notifications
                foreach (var user in usersToNotify)
                {
                    try
                    {
                        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = user.UserId,
                            TitleKey = "Notification.DemandApproval.Title", // "Onay Bekleyen Talep"
                            TitleArgs = new object[] { demand.Id },
                            MessageKey = "Notification.DemandApproval.Message", // "{0} numaralı talep onay beklemektedir."
                            MessageArgs = new object[] { demand.OfferNo ?? "" },
                            NotificationType = NotificationType.DemandApproval,
                            RelatedEntityName = "Demand",
                            RelatedEntityId = demand.Id
                        }).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                BackgroundJob.Enqueue<IMailJob>(job =>
                    job.SendDemandApprovalPendingEmailsAsync(
                        usersToNotify.ToList(),
                        userIdToActionId,
                        baseUrl,
                        approvalPath,
                        demandPath,
                        request.EntityId));

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("DemandService.ApprovalFlowStarted"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.StartApprovalFlowExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ApprovalActionGetDto>>> GetWaitingApprovalsAsync()
        {
            try
            {
                // Eğer userId verilmemişse HttpContext'ten al
                var targetUserIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!targetUserIdResponse.Success)
                {
                    return ApiResponse<List<ApprovalActionGetDto>>.ErrorResult(
                        targetUserIdResponse.Message,
                        targetUserIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var targetUserId = targetUserIdResponse.Data;

                var approvalActions = await _unitOfWork.ApprovalActions.Query()
                    .Where(x =>
                        x.ApprovalRequest.DocumentType == PricingRuleType.Demand &&
                        x.ApprovedByUserId == targetUserId &&
                        x.Status == ApprovalStatus.Waiting &&
                        !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = _mapper.Map<List<ApprovalActionGetDto>>(approvalActions);

                return ApiResponse<List<ApprovalActionGetDto>>.SuccessResult(
                    dtos,
                    _localizationService.GetLocalizedString("DemandService.WaitingApprovalsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ApprovalActionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetWaitingApprovalsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> ApproveAsync(ApproveActionDto request)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                // Onay kaydını bul
                var action = await _unitOfWork.ApprovalActions.Query()
                    .Include(a => a.ApprovalRequest)
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.ApprovalActionId &&
                        x.ApprovedByUserId == userId &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (action == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalActionNotFound"),
                        "Onay kaydı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Onay işlemini gerçekleştir
                action.Status = ApprovalStatus.Approved;
                action.ActionDate = DateTime.UtcNow;
                action.UpdatedDate = DateTime.UtcNow;
                action.UpdatedBy = userId;

                await _unitOfWork.ApprovalActions.UpdateAsync(action).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Aynı step'te bekleyen var mı?
                bool anyWaiting = await _unitOfWork.ApprovalActions.Query()
                    .AnyAsync(x =>
                        x.ApprovalRequestId == action.ApprovalRequestId &&
                        x.StepOrder == action.StepOrder &&
                        x.Status == ApprovalStatus.Waiting &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (anyWaiting)
                {
                    // Herkes onaylamadan ilerleme
                    await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.SuccessResult(
                        true,
                        _localizationService.GetLocalizedString("DemandService.ApprovalActionApproved"));
                }

                // Step tamamlandı → sonraki step'e geç
                var approvalRequest = await _unitOfWork.ApprovalRequests.Query()
                    .Include(ar => ar.ApprovalFlow)
                    .FirstOrDefaultAsync(x => x.Id == action.ApprovalRequestId && !x.IsDeleted).ConfigureAwait(false);

                if (approvalRequest == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalRequestNotFound"),
                        "Onay talebi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Demand'ı al (hem akış bittiğinde hem de sonraki step için gerekli)
                var demand = await _unitOfWork.Demands.Query()
                    .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);

                if (demand == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        "Talep bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                int nextStepOrder = approvalRequest.CurrentStep + 1;

                var nextStep = await _unitOfWork.ApprovalFlowSteps.Query()
                    .FirstOrDefaultAsync(x =>
                        x.ApprovalFlowId == approvalRequest.ApprovalFlowId &&
                        x.StepOrder == nextStepOrder &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (nextStep == null)
                {

                    // 🎉 AKIŞ BİTTİ
                    var now = DateTime.UtcNow;

                    demand.Status = ApprovalStatus.Approved;
                    demand.UpdatedDate = now;
                    demand.UpdatedBy = userId;
                    await _unitOfWork.Demands.UpdateAsync(demand).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(demand.OfferNo))
                    {
                        var siblingDemands = await _unitOfWork.Demands.Query()
                            .Where(d => !d.IsDeleted && d.Id != demand.Id && d.OfferNo == demand.OfferNo)
                            .ToListAsync().ConfigureAwait(false);

                        foreach (var siblingDemand in siblingDemands)
                        {
                            siblingDemand.Status = ApprovalStatus.Closed;
                            siblingDemand.UpdatedDate = now;
                            siblingDemand.UpdatedBy = userId;
                            await _unitOfWork.Demands.UpdateAsync(siblingDemand).ConfigureAwait(false);
                        }
                    }

                    // if there is no flow then Convert to quotation end return success
                    var quotationId = await ConvertToQuotationAsync(demand.Id).ConfigureAwait(false);
                    if (!quotationId.Success)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.ErrorResult(quotationId.Message, quotationId.Message, StatusCodes.Status404NotFound);
                    }

                    approvalRequest.Status = ApprovalStatus.Approved;
                    approvalRequest.UpdatedDate = now;
                    approvalRequest.UpdatedBy = userId;
                    await _unitOfWork.ApprovalRequests.UpdateAsync(approvalRequest).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                    // DemandLine'ların ApprovalStatus'unu Approved yap
                    var demandLines = await _unitOfWork.DemandLines.Query()
                        .Where(ql => ql.DemandId == demand.Id && !ql.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);

                    foreach (var line in demandLines)
                    {
                        line.ApprovalStatus = ApprovalStatus.Approved;
                        line.UpdatedDate = now;
                        line.UpdatedBy = userId;
                        await _unitOfWork.DemandLines.UpdateAsync(line).ConfigureAwait(false);
                    }

                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                    // Talep sahibine onaylandı bildirimi ve mail gönder (eğer onaylayan kişi talep sahibi değilse)
                    if (demand.CreatedBy > 0 && demand.CreatedBy != userId)
                    {
                        try
                        {
                            var demandForNotification = await _unitOfWork.Demands.Query()
                                .Include(d => d.CreatedByUser)
                                .FirstOrDefaultAsync(d => d.Id == demand.Id).ConfigureAwait(false);

                            if (demandForNotification != null && demandForNotification.CreatedByUser != null)
                            {
                                // Bildirim oluştur
                                try
                                {
                                    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                    {
                                        UserId = demandForNotification.CreatedBy ?? 0L,
                                        TitleKey = "Notification.DemandApproved.Title", // "Talep Onaylandı"
                                        TitleArgs = new object[] { demandForNotification.Id },
                                        MessageKey = "Notification.DemandApproved.Message", // "{0} numaralı talep onaylandı."
                                        MessageArgs = new object[] { demandForNotification.OfferNo ?? "" },
                                        NotificationType = NotificationType.DemandDetail,
                                        RelatedEntityName = "Demand",
                                        RelatedEntityId = demandForNotification.Id
                                    }).ConfigureAwait(false);
                                }
                                catch (Exception)
                                {
                                    // ignore
                                }

                                // Mail gönder
                                var approverUser = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted).ConfigureAwait(false);
                                if (approverUser != null && !string.IsNullOrWhiteSpace(demandForNotification.CreatedByUser.Email))
                                {
                                    var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                                    var demandPath = _configuration["FrontendSettings:DemandDetailPath"]?.TrimStart('/') ?? "demands";
                                    var demandLink = $"{baseUrl}/{demandPath}/{demandForNotification.Id}";

                                    var creatorFullName = $"{demandForNotification.CreatedByUser.FirstName} {demandForNotification.CreatedByUser.LastName}".Trim();
                                    if (string.IsNullOrWhiteSpace(creatorFullName)) creatorFullName = demandForNotification.CreatedByUser.Username;

                                    var approverFullName = $"{approverUser.FirstName} {approverUser.LastName}".Trim();
                                    if (string.IsNullOrWhiteSpace(approverFullName)) approverFullName = approverUser.Username;

                                    BackgroundJob.Enqueue<IMailJob>(job =>
                                        job.SendDemandApprovedEmailAsync(
                                            demandForNotification.CreatedByUser.Email,
                                            creatorFullName,
                                            approverFullName,
                                            demandForNotification.OfferNo ?? "",
                                            demandLink,
                                            demandForNotification.Id
                                        ));
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Bildirim ve mail gönderimi başarısız olsa bile işlem başarılı sayılmalı
                        }
                    }

                    return ApiResponse<bool>.SuccessResult(
                        true,
                        _localizationService.GetLocalizedString("DemandService.ApprovalFlowCompleted"));
                }

                // Yeni step için onaycıları oluştur
                approvalRequest.CurrentStep = nextStep.StepOrder;
                approvalRequest.UpdatedDate = DateTime.UtcNow;
                approvalRequest.UpdatedBy = userId;
                await _unitOfWork.ApprovalRequests.UpdateAsync(approvalRequest).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Yeni step için rolleri bul (StartApprovalFlow'daki mantık)
                var validRoles = await _unitOfWork.ApprovalRoles.Query()
                    .Where(r =>
                        r.ApprovalRoleGroupId == nextStep.ApprovalRoleGroupId &&
                        r.MaxAmount >= demand.GrandTotal &&
                        !r.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                if (!validRoles.Any())
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalRoleNotFound"),
                        "Yeni step için uygun onay yetkisi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Onaylayacak kullanıcıları bul
                var roleIds = validRoles.Select(r => r.Id).ToList();
                var userIds = await _unitOfWork.ApprovalUserRoles.Query()
                    .Where(x =>
                        roleIds.Contains(x.ApprovalRoleId) &&
                        !x.IsDeleted)
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToListAsync().ConfigureAwait(false);

                if (!userIds.Any())
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalUsersNotFound"),
                        "Yeni step için onay yetkisi olan kullanıcı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Yeni ApprovalAction kayıtlarını oluştur
                var newActions = new List<ApprovalAction>();
                foreach (var newUserId in userIds)
                {
                    var newAction = new ApprovalAction
                    {
                        ApprovalRequestId = approvalRequest.Id,
                        StepOrder = nextStep.StepOrder,
                        ApprovedByUserId = newUserId,
                        Status = ApprovalStatus.Waiting,
                        ActionDate = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = userId,
                        IsDeleted = false
                    };

                    newActions.Add(newAction);
                }

                await _unitOfWork.ApprovalActions.AddAllAsync(newActions).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // Yeni step için onaycılara bildirim ve mail gönder
                try
                {
                    var demandForNotification = await _unitOfWork.Demands.Query()
                        .FirstOrDefaultAsync(d => d.Id == demand.Id).ConfigureAwait(false);

                    if (demandForNotification != null)
                    {
                        // UserId -> ApprovalActionId eşlemesi (onay linkleri için)
                        var userIdToActionId = newActions.ToDictionary(a => a.ApprovedByUserId, a => a.Id);
                        var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                        var approvalPath = _configuration["FrontendSettings:ApprovalPendingPath"]?.TrimStart('/') ?? "approvals/pending";
                        var demandPath = _configuration["FrontendSettings:DemandDetailPath"]?.TrimStart('/') ?? "demands";

                        var usersToNotify = new List<(string Email, string FullName, long UserId)>();

                        foreach (var newUserId in userIds)
                        {
                            var user = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == newUserId && !x.IsDeleted).ConfigureAwait(false);
                            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                            {
                                usersToNotify.Add((user.Email, user.FullName, newUserId));
                            }
                        }

                        // Bildirim gönder
                        foreach (var user in usersToNotify)
                        {
                            try
                            {
                                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                {
                                    UserId = user.UserId,
                                    TitleKey = "Notification.DemandApproval.Title", // "Onay Bekleyen Talep"
                                    TitleArgs = new object[] { demandForNotification.Id },
                                    MessageKey = "Notification.DemandApproval.Message", // "{0} numaralı talep onay beklemektedir."
                                    MessageArgs = new object[] { demandForNotification.OfferNo ?? "" },
                                    NotificationType = NotificationType.DemandApproval,
                                    RelatedEntityName = "Demand",
                                    RelatedEntityId = demandForNotification.Id
                                }).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                // ignore
                            }
                        }

                        // Mail gönder
                        if (usersToNotify.Any())
                        {
                            BackgroundJob.Enqueue<IMailJob>(job =>
                                job.SendDemandApprovalPendingEmailsAsync(
                                    usersToNotify,
                                    userIdToActionId,
                                    baseUrl,
                                    approvalPath,
                                    demandPath,
                                    demandForNotification.Id));
                        }
                    }
                }
                catch (Exception)
                {
                    // Bildirim ve mail gönderimi başarısız olsa bile işlem başarılı sayılmalı
                }

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("DemandService.ApprovalActionApprovedAndNextStepStarted"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.ApproveExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> RejectAsync(RejectActionDto request)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                // Onay kaydını bul
                var action = await _unitOfWork.ApprovalActions.Query()
                    .Include(a => a.ApprovalRequest)
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.ApprovalActionId &&
                        x.ApprovedByUserId == userId &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (action == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalActionNotFound"),
                        "Onay kaydı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Red işlemini gerçekleştir
                action.Status = ApprovalStatus.Rejected;
                action.ActionDate = DateTime.UtcNow;
                action.UpdatedDate = DateTime.UtcNow;
                action.UpdatedBy = userId;

                await _unitOfWork.ApprovalActions.UpdateAsync(action).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // ApprovalRequest'i reddedildi olarak işaretle
                var approvalRequest = await _unitOfWork.ApprovalRequests.Query()
                    .FirstOrDefaultAsync(x => x.Id == action.ApprovalRequestId && !x.IsDeleted).ConfigureAwait(false);

                if (approvalRequest == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.ApprovalRequestNotFound"),
                        "Onay talebi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                approvalRequest.Status = ApprovalStatus.Rejected;
                approvalRequest.UpdatedDate = DateTime.UtcNow;
                approvalRequest.UpdatedBy = userId;

                await _unitOfWork.ApprovalRequests.UpdateAsync(approvalRequest).ConfigureAwait(false);

                // Talep durumunu ve red sebebini güncelle (raporlama için)
                var demandForReject = await _unitOfWork.Demands.Query(tracking: true)
                    .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);
                if (demandForReject != null)
                {
                    demandForReject.Status = ApprovalStatus.Rejected;
                    demandForReject.RejectedReason = request.RejectReason;
                    demandForReject.UpdatedDate = DateTime.UtcNow;
                    demandForReject.UpdatedBy = userId;
                    await _unitOfWork.Demands.UpdateAsync(demandForReject).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Eğer reddeden kullanıcı talebi oluşturan kullanıcıysa ve en alt aşamadaysa (CurrentStep == 1)
                // DemandLine'ların ApprovalStatus'unu Rejected yap
                if (approvalRequest.CurrentStep == 1)
                {
                    var demand = await _unitOfWork.Demands.Query()
                        .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);

                    if (demand != null && demand.CreatedBy == userId)
                    {
                        var demandLines = await _unitOfWork.DemandLines.Query()
                            .Where(ql => ql.DemandId == demand.Id && !ql.IsDeleted)
                            .ToListAsync().ConfigureAwait(false);

                        foreach (var line in demandLines)
                        {
                            line.ApprovalStatus = ApprovalStatus.Rejected;
                            line.UpdatedDate = DateTime.UtcNow;
                            line.UpdatedBy = userId;
                            await _unitOfWork.DemandLines.UpdateAsync(line).ConfigureAwait(false);
                        }

                        await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // Talep sahibine mail gönder (eğer reddeden kişi talep sahibi değilse)
                try
                {
                    var demandForMail = await _unitOfWork.Demands.Query()
                        .Include(q => q.CreatedByUser)
                        .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId).ConfigureAwait(false);

                    if (demandForMail != null && demandForMail.CreatedBy != userId)
                    {
                        // Bildirim oluştur
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                UserId = demandForMail.CreatedBy ?? 0L,
                                TitleKey = "Notification.DemandRejected.Title", // "Talep Reddedildi"
                                TitleArgs = new object[] { demandForMail.Id },
                                MessageKey = "Notification.DemandRejected.Message", // "{0} numaralı talep reddedildi."
                                MessageArgs = new object[] { demandForMail.OfferNo ?? "" },
                                NotificationType = NotificationType.DemandDetail,
                                RelatedEntityName = "Demand",
                                RelatedEntityId = demandForMail.Id
                            }).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // ignore
                        }

                        var rejectorUser = await _unitOfWork.Users.GetByIdAsync(userId).ConfigureAwait(false);
                        if (rejectorUser != null && demandForMail.CreatedByUser != null)
                        {
                            var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                            var demandPath = _configuration["FrontendSettings:DemandDetailPath"]?.TrimStart('/') ?? "demands";
                            var demandLink = $"{baseUrl}/{demandPath}/{demandForMail.Id}";

                            var creatorFullName = $"{demandForMail.CreatedByUser.FirstName} {demandForMail.CreatedByUser.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(creatorFullName)) creatorFullName = demandForMail.CreatedByUser.Username;

                            var rejectorFullName = $"{rejectorUser.FirstName} {rejectorUser.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(rejectorFullName)) rejectorFullName = rejectorUser.Username;

                            BackgroundJob.Enqueue<IMailJob>(job =>
                                job.SendDemandRejectedEmailAsync(
                                    demandForMail.CreatedByUser.Email,
                                    creatorFullName,
                                    rejectorFullName,
                                    demandForMail.OfferNo ?? "",
                                    request.RejectReason ?? "Belirtilmedi",
                                    demandLink,
                                    demandForMail.Id
                                ));
                        }
                    }
                }
                catch (Exception)
                {
                    // Mail gönderimi başarısız olsa bile işlem başarılı sayılmalı
                }

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("DemandService.ApprovalActionRejected"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.RejectExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagedResponse<DemandGetDto>>> GetRelatedDemands(PagedRequest request)
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

                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    return ApiResponse<PagedResponse<DemandGetDto>>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var avaibleUsersResponse = await GetDemandRelatedUsersAsync(userId).ConfigureAwait(false);
                if (!avaibleUsersResponse.Success)
                {
                    return ApiResponse<PagedResponse<DemandGetDto>>.ErrorResult(
                        avaibleUsersResponse.Message,
                        avaibleUsersResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var avaibleUsers = avaibleUsersResponse.Data ?? new List<ApprovalScopeUserDto>();
                var avaibleUsersIds = avaibleUsers.Select(x => x.UserId).ToList();


                var columnMapping2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "potentialCustomerName", "PotentialCustomer.CustomerName" },
                    { "documentSerialTypeName", "DocumentSerialType.SerialPrefix" },
                    { "salesTypeDefinitionName", "SalesTypeDefinition.Name" }
                };

                var query = _unitOfWork.Demands.Query()
                    .AsNoTracking()
                    .Where(q => !q.IsDeleted && (q.CreatedBy == userId || (q.RepresentativeId.HasValue && avaibleUsersIds.Contains(q.RepresentativeId.Value))))
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping2);

                var sortBy = request.SortBy ?? nameof(Demand.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping2);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<DemandGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DemandGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DemandGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DemandService.DemandsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DemandGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.GetAllDemandsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ApprovalScopeUserDto>>> GetDemandRelatedUsersAsync(long userId)
        {
            try
            {
                /* -------------------------------------------------------
                 * 1️ Kullanıcının bulunduğu flow + max step
                 * -------------------------------------------------------*/
                var myFlowSteps = await
                (
                    from ur in _unitOfWork.ApprovalUserRoles.Query()
                    join ar in _unitOfWork.ApprovalRoles.Query()
                        on ur.ApprovalRoleId equals ar.Id
                    join fs in _unitOfWork.ApprovalFlowSteps.Query()
                        on ar.ApprovalRoleGroupId equals fs.ApprovalRoleGroupId
                    join f in _unitOfWork.ApprovalFlows.Query()
                        on fs.ApprovalFlowId equals f.Id
                    where ur.UserId == userId
                          && !ur.IsDeleted
                          && !ar.IsDeleted
                          && !fs.IsDeleted
                          && !f.IsDeleted
                          && f.IsActive
                          && f.DocumentType == PricingRuleType.Demand
                    group fs by fs.ApprovalFlowId into g
                    select new MyFlowStepDto
                    {
                        ApprovalFlowId = g.Key,
                        MaxStepOrder = g.Max(x => x.StepOrder)
                    }
                ).ToListAsync().ConfigureAwait(false);

                if (!myFlowSteps.Any())
                {
                    var userData = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == userId).ConfigureAwait(false);
                    if (userData == null)
                    {
                        return ApiResponse<List<ApprovalScopeUserDto>>
                            .SuccessResult(
                                new List<ApprovalScopeUserDto>(),
                                _localizationService.GetLocalizedString("DemandService.ApprovalScopeUsersRetrieved"));
                    }
                    var approvalScopeUserDtos = new List<ApprovalScopeUserDto>();
                    approvalScopeUserDtos.Add(new ApprovalScopeUserDto
                    {
                        FlowId = 0,
                        UserId = userId,
                        FirstName = userData.FirstName ?? "",
                        LastName = userData.LastName ?? "",
                        RoleGroupName = "Talep Sahibi",
                        StepOrder = 0
                    });
                    return ApiResponse<List<ApprovalScopeUserDto>>
                        .SuccessResult(
                            approvalScopeUserDtos,
                            _localizationService.GetLocalizedString("DemandService.ApprovalScopeUsersRetrieved"));
                }

                var flowStepMap = myFlowSteps
                    .ToDictionary(x => x.ApprovalFlowId, x => x.MaxStepOrder);

                /* -------------------------------------------------------
                 * 2️ Flow altındaki kullanıcılar
                 * -------------------------------------------------------*/
                var rawUsers =
                    (
                        from fs in _unitOfWork.ApprovalFlowSteps.Query()
                        join ar in _unitOfWork.ApprovalRoles.Query()
                            on fs.ApprovalRoleGroupId equals ar.ApprovalRoleGroupId
                        join ur in _unitOfWork.ApprovalUserRoles.Query()
                            on ar.Id equals ur.ApprovalRoleId
                        join u in _unitOfWork.Users.Query()
                            on ur.UserId equals u.Id
                        join rg in _unitOfWork.ApprovalRoleGroups.Query()
                            on fs.ApprovalRoleGroupId equals rg.Id
                        where !fs.IsDeleted
                              && !ar.IsDeleted
                              && !ur.IsDeleted
                              && !u.IsDeleted
                        select new
                        {
                            fs.ApprovalFlowId,
                            fs.StepOrder,
                            u.Id,
                            u.FirstName,
                            u.LastName,
                            RoleGroupName = rg.Name
                        }
                    )
                    .AsEnumerable(); // SQL → Memory geçişi

                var usersUnderMe = rawUsers
                    .Where(x =>
                        flowStepMap.TryGetValue(x.ApprovalFlowId, out var maxStep)
                        && (
                            x.StepOrder < maxStep
                            || (x.StepOrder == maxStep && x.Id == userId)
                        )
                    )
                    .Select(x => new ApprovalScopeUserDto
                    {
                        FlowId = x.ApprovalFlowId,
                        UserId = x.Id,
                        FirstName = x.FirstName,
                        LastName = x.LastName,
                        RoleGroupName = x.RoleGroupName,
                        StepOrder = x.StepOrder
                    })
                    .Distinct()
                    .OrderBy(x => x.FlowId)
                    .ThenBy(x => x.StepOrder)
                    .ToList();

                return ApiResponse<List<ApprovalScopeUserDto>>
                    .SuccessResult(
                        usersUnderMe,
                        _localizationService.GetLocalizedString(
                            "DemandService.DemandApproverUsersRetrieved"
                        )
                    );
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ApprovalScopeUserDto>>
                    .ErrorResult(
                        _localizationService.GetLocalizedString(
                            "DemandService.InternalServerError"
                        ),
                        ex.Message,
                        StatusCodes.Status500InternalServerError
                    );
            }
        }

        public async Task<ApiResponse<long>> ConvertToQuotationAsync(long demandId)
        {
            try
            {
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    return ApiResponse<long>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var demand = await _unitOfWork.Demands.GetByIdForUpdateAsync(demandId).ConfigureAwait(false);
                if (demand == null)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        StatusCodes.Status404NotFound);
                }
                demand.Status = ApprovalStatus.Approved;
                demand.UpdatedDate = DateTime.UtcNow;
                demand.UpdatedBy = userId;

                var demandsForReject = await _unitOfWork.Demands.Query(tracking: true)
                    .Where(d => d.OfferNo == demand.OfferNo && !d.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);
                if (demandsForReject != null && demandsForReject.Any())
                {
                    foreach (var demandForReject in demandsForReject)
                    {
                        demandForReject.Status = ApprovalStatus.Rejected;
                        demandForReject.UpdatedDate = DateTime.UtcNow;
                        demandForReject.UpdatedBy = userId;
                    }
                }

                var demandLines = await _unitOfWork.DemandLines.Query()
                    .Where(dl => dl.DemandId == demandId && !dl.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);
                if (demandLines == null || !demandLines.Any())
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandLinesNotFound"),
                        _localizationService.GetLocalizedString("DemandService.DemandLinesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                    var demandExchangeRates = await _unitOfWork.DemandExchangeRates.Query()
                    .Where(der => der.DemandId == demandId && !der.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var quotationDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                    .Where(d => d.RuleType == PricingRuleType.Quotation && !d.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (quotationDocumentSerialType == null)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.QuotationDocumentSerialTypeNotFound"),
                        _localizationService.GetLocalizedString("DemandService.QuotationDocumentSerialTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var documentSerialResult = await _documentSerialTypeService.GenerateDocumentSerialAsync(quotationDocumentSerialType.Id).ConfigureAwait(false);
                if (!documentSerialResult.Success)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DocumentSerialTypeGenerationError"),
                        documentSerialResult.Message,
                        StatusCodes.Status500InternalServerError);
                }

                var quotation = new Quotation
                {
                    PotentialCustomerId = demand.PotentialCustomerId,
                    ErpCustomerCode = demand.ErpCustomerCode,
                    ContactId = demand.ContactId,
                    ValidUntil = demand.ValidUntil,
                    DeliveryDate = demand.DeliveryDate,
                    ShippingAddressId = demand.ShippingAddressId,
                    RepresentativeId = demand.RepresentativeId,
                    ActivityId = demand.ActivityId,
                    Description = demand.Description,
                    PaymentTypeId = demand.PaymentTypeId,
                    DocumentSerialTypeId = quotationDocumentSerialType.Id,
                    OfferType = demand.OfferType,
                    OfferDate = demand.OfferDate ?? DateTime.UtcNow,
                    OfferNo = documentSerialResult.Data,
                    RevisionNo = documentSerialResult.Data,
                    Currency = demand.Currency,
                    HasCustomerSpecificDiscount = demand.HasCustomerSpecificDiscount,
                    Total = demand.Total,
                    GrandTotal = demand.GrandTotal,
                    DemandId = demand.Id,
                    Status = ApprovalStatus.HavenotStarted,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.Quotations.AddAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var quotationLines = new List<QuotationLine>();
                foreach (var line in demandLines)
                {
                    quotationLines.Add(new QuotationLine
                    {
                        QuotationId = quotation.Id,
                        ProductCode = line.ProductCode,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        DiscountRate1 = line.DiscountRate1,
                        DiscountRate2 = line.DiscountRate2,
                        DiscountRate3 = line.DiscountRate3,
                        DiscountAmount1 = line.DiscountAmount1,
                        DiscountAmount2 = line.DiscountAmount2,
                        DiscountAmount3 = line.DiscountAmount3,
                        VatRate = line.VatRate,
                        VatAmount = line.VatAmount,
                        LineTotal = line.LineTotal,
                        LineGrandTotal = line.LineGrandTotal,
                        Description = line.Description,
                        Description1 = line.Description1,
                        Description2 = line.Description2,
                        Description3 = line.Description3,
                        PricingRuleHeaderId = line.PricingRuleHeaderId,
                        RelatedStockId = line.RelatedStockId,
                        RelatedProductKey = line.RelatedProductKey,
                        IsMainRelatedProduct = line.IsMainRelatedProduct,
                        ApprovalStatus = ApprovalStatus.HavenotStarted,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = userId
                    });
                }
                await _unitOfWork.QuotationLines.AddAllAsync(quotationLines).ConfigureAwait(false);

                if (demandExchangeRates != null && demandExchangeRates.Any())
                {
                    var quotationExchangeRates = demandExchangeRates.Select(rate => new QuotationExchangeRate
                    {
                        QuotationId = quotation.Id,
                        Currency = rate.Currency,
                        ExchangeRate = rate.ExchangeRate,
                        ExchangeRateDate = rate.ExchangeRateDate,
                        IsOfficial = rate.IsOfficial,
                        CreatedDate = DateTime.UtcNow,
                        CreatedBy = userId
                    }).ToList();
                    await _unitOfWork.QuotationExchangeRates.AddAllAsync(quotationExchangeRates).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<long>.SuccessResult(quotation.Id, _localizationService.GetLocalizedString("DemandService.QuotationConvertedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<long>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.ConvertToQuotationExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Talep onay akışı raporu - Aşamalar, kimler onayladı, kimler bekledi, kim reddetti ve ne yazdı
        /// </summary>
        /// <param name="demandId">Talep ID</param>
        /// <returns>Onay akışının aşama bazlı detaylı raporu</returns>
        public async Task<ApiResponse<DemandApprovalFlowReportDto>> GetApprovalFlowReportAsync(long demandId)
        {
            try
            {
                var demand = await _unitOfWork.Demands.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == demandId && !q.IsDeleted).ConfigureAwait(false);

                if (demand == null)
                {
                    return ApiResponse<DemandApprovalFlowReportDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandService.DemandNotFound"),
                        "Talep bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                var report = new DemandApprovalFlowReportDto
                {
                    DemandId = demand.Id,
                    DemandOfferNo = demand.RevisionNo ?? demand.OfferNo ?? demand.Id.ToString(),
                    HasApprovalRequest = false,
                    OverallStatus = (int?)demand.Status,
                    OverallStatusName = GetApprovalStatusName(demand.Status),
                    RejectedReason = demand.RejectedReason,
                    Steps = new List<ApprovalFlowStepReportDto>()
                };

                var approvalRequest = await _unitOfWork.ApprovalRequests.Query()
                    .AsNoTracking()
                    .Include(ar => ar.ApprovalFlow)
                    .FirstOrDefaultAsync(x =>
                        x.EntityId == demandId &&
                        x.DocumentType == PricingRuleType.Demand &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (approvalRequest == null)
                {
                    report.HasApprovalRequest = false;
                    return ApiResponse<DemandApprovalFlowReportDto>.SuccessResult(
                        report,
                        _localizationService.GetLocalizedString("DemandService.ApprovalFlowReportRetrieved"));
                }

                report.HasApprovalRequest = true;
                report.CurrentStep = approvalRequest.CurrentStep;
                report.OverallStatus = (int)approvalRequest.Status;
                report.OverallStatusName = GetApprovalStatusName(approvalRequest.Status);
                if (string.IsNullOrEmpty(report.RejectedReason))
                    report.RejectedReason = demand.RejectedReason;
                report.FlowDescription = approvalRequest.ApprovalFlow?.Description;

                var steps = await _unitOfWork.ApprovalFlowSteps.Query()
                    .AsNoTracking()
                    .Include(s => s.ApprovalRoleGroup)
                    .Where(x => x.ApprovalFlowId == approvalRequest.ApprovalFlowId && !x.IsDeleted)
                    .OrderBy(x => x.StepOrder)
                    .ToListAsync().ConfigureAwait(false);

                var allActions = await _unitOfWork.ApprovalActions.Query()
                    .AsNoTracking()
                    .Include(a => a.ApprovedByUser)
                    .Where(x => x.ApprovalRequestId == approvalRequest.Id && !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                foreach (var step in steps)
                {
                    var stepActions = allActions.Where(a => a.StepOrder == step.StepOrder).ToList();
                    var stepReport = new ApprovalFlowStepReportDto
                    {
                        StepOrder = step.StepOrder,
                        StepName = step.ApprovalRoleGroup?.Name ?? $"Adım {step.StepOrder}",
                        StepStatus = GetStepStatus(step, approvalRequest, stepActions),
                        Actions = stepActions.Select(a => new ApprovalActionDetailDto
                        {
                            UserId = a.ApprovedByUserId,
                            UserFullName = a.ApprovedByUser != null
                                ? $"{a.ApprovedByUser.FirstName} {a.ApprovedByUser.LastName}".Trim()
                                : null,
                            UserEmail = a.ApprovedByUser?.Email,
                            Status = (int)a.Status,
                            StatusName = GetApprovalStatusName(a.Status),
                            ActionDate = a.Status != ApprovalStatus.Waiting ? a.ActionDate : null,
                            RejectedReason = a.Status == ApprovalStatus.Rejected ? demand.RejectedReason : null
                        }).ToList()
                    };
                    report.Steps.Add(stepReport);
                }

                return ApiResponse<DemandApprovalFlowReportDto>.SuccessResult(
                    report,
                    _localizationService.GetLocalizedString("DemandService.ApprovalFlowReportRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandApprovalFlowReportDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    _localizationService.GetLocalizedString("DemandService.ApprovalFlowReportExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private static string GetApprovalStatusName(ApprovalStatus? status)
        {
            if (status == null) return "Belirsiz";
            return status switch
            {
                ApprovalStatus.HavenotStarted => "Başlamadı",
                ApprovalStatus.Waiting => "Beklemede",
                ApprovalStatus.Approved => "Onaylandı",
                ApprovalStatus.Rejected => "Reddedildi",
                ApprovalStatus.Closed => "Kapandı",
                _ => "Belirsiz"
            };
        }

        private static string GetStepStatus(ApprovalFlowStep step, ApprovalRequest request, List<ApprovalAction> stepActions)
        {
            if (request.Status == ApprovalStatus.Rejected)
            {
                var rejectedInStep = stepActions.Any(a => a.Status == ApprovalStatus.Rejected);
                return rejectedInStep ? "Rejected" : (step.StepOrder < request.CurrentStep ? "Completed" : "NotStarted");
            }

            if (step.StepOrder < request.CurrentStep)
                return "Completed";
            if (step.StepOrder == request.CurrentStep)
                return "InProgress";
            return "NotStarted";
        }
    }
}
