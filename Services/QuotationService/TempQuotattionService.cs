using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using static crm_api.Models.SalesTypeEnum;

namespace crm_api.Services
{
    public class TempQuotattionService : ITempQuotattionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IUserService _userService;
        private readonly IDocumentSerialTypeService _documentSerialTypeService;

        public TempQuotattionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IUserService userService,
            IDocumentSerialTypeService documentSerialTypeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _userService = userService;
            _documentSerialTypeService = documentSerialTypeService;
        }

        public async Task<ApiResponse<PagedResponse<TempQuotattionGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var columnMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "customerName", "Customer.CustomerName" }
                };

                var query = _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Include(x => x.Customer)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(TempQuotattion.Id);
                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<TempQuotattionGetDto>(x)).ToList();
                var pagedResponse = new PagedResponse<TempQuotattionGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<TempQuotattionGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<TempQuotattionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted).ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                return ApiResponse<TempQuotattionGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionGetDto>> CreateAsync(TempQuotattionCreateDto dto)
        {
            try
            {
                var customerExists = await _unitOfWork.Customers.Query()
                    .AnyAsync(c => c.Id == dto.CustomerId && !c.IsDeleted).ConfigureAwait(false);
                if (!customerExists)
                {
                    return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entity = _mapper.Map<TempQuotattion>(dto);
                entity.CreatedDate = DateTime.UtcNow;
                entity.OfferDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattions.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var created = await _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted).ConfigureAwait(false);

                return ApiResponse<TempQuotattionGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionGetDto>(created ?? entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionGetDto>> UpdateAsync(long id, TempQuotattionUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var customerExists = await _unitOfWork.Customers.Query()
                    .AnyAsync(c => c.Id == dto.CustomerId && !c.IsDeleted).ConfigureAwait(false);
                if (!customerExists)
                {
                    return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattions.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updated = await _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted).ConfigureAwait(false);

                return ApiResponse<TempQuotattionGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionGetDto>(updated ?? entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionGetDto>> CreateRevisionAsync(long id)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                var source = await _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (source == null)
                {
                    return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var sourceLines = await _unitOfWork.TempQuotattionLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == id && !x.IsDeleted)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var sourceExchangeLines = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == id && !x.IsDeleted)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var revision = new TempQuotattion
                {
                    CustomerId = source.CustomerId,
                    RevisionId = source.Id,
                    OfferDate = DateTime.UtcNow,
                    CurrencyCode = source.CurrencyCode,
                    ExchangeRate = source.ExchangeRate,
                    DiscountRate1 = source.DiscountRate1,
                    DiscountRate2 = source.DiscountRate2,
                    DiscountRate3 = source.DiscountRate3,
                    Description = source.Description,
                    IsApproved = false,
                    ApprovedDate = null,
                    QuotationId = null,
                    QuotationNo = null,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.TempQuotattions.AddAsync(revision).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                if (sourceLines.Any())
                {
                    var revisionLines = sourceLines.Select(line => new TempQuotattionLine
                    {
                        TempQuotattionId = revision.Id,
                        ProductCode = line.ProductCode,
                        ProductName = line.ProductName,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        DiscountRate1 = line.DiscountRate1,
                        DiscountAmount1 = line.DiscountAmount1,
                        DiscountRate2 = line.DiscountRate2,
                        DiscountAmount2 = line.DiscountAmount2,
                        DiscountRate3 = line.DiscountRate3,
                        DiscountAmount3 = line.DiscountAmount3,
                        VatRate = line.VatRate,
                        VatAmount = line.VatAmount,
                        LineTotal = line.LineTotal,
                        LineGrandTotal = line.LineGrandTotal,
                        Description = line.Description,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    await _unitOfWork.TempQuotattionLines.AddAllAsync(revisionLines).ConfigureAwait(false);
                }

                if (sourceExchangeLines.Any())
                {
                    var revisionExchangeLines = sourceExchangeLines.Select(line => new TempQuotattionExchangeLine
                    {
                        TempQuotattionId = revision.Id,
                        Currency = line.Currency,
                        ExchangeRate = line.ExchangeRate,
                        ExchangeRateDate = line.ExchangeRateDate,
                        IsManual = line.IsManual,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    await _unitOfWork.TempQuotattionExchangeLines.AddAllAsync(revisionExchangeLines).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                var createdRevision = await _unitOfWork.TempQuotattions.Query()
                    .AsNoTracking()
                    .Include(x => x.Customer)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == revision.Id && !x.IsDeleted)
                    .ConfigureAwait(false);

                return ApiResponse<TempQuotattionGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionGetDto>(createdRevision ?? revision),
                    _localizationService.GetLocalizedString("TempQuotattionService.RevisionCreated"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<long>> ConvertToQuotationAsync(long id)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
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
                var entity = await _unitOfWork.TempQuotattions.Query(tracking: true)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                if (entity.QuotationId.HasValue)
                {
                    return ApiResponse<long>.SuccessResult(entity.QuotationId.Value, _localizationService.GetLocalizedString("TempQuotattionService.AlreadyConvertedToQuotation"));
                }

                var customer = await _unitOfWork.Customers.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == entity.CustomerId && !x.IsDeleted)
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        _localizationService.GetLocalizedString("CustomerService.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var lines = await _unitOfWork.TempQuotattionLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == id && !x.IsDeleted)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (!lines.Any())
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLinesNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLinesNotFound"),
                        StatusCodes.Status400BadRequest);
                }

                var exchangeLines = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == id && !x.IsDeleted)
                    .ToListAsync()
                    .ConfigureAwait(false);

                const string defaultOfferType = nameof(YURTICI);

                var salesTypeDefinition = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.SalesType == defaultOfferType)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (salesTypeDefinition == null)
                {
                    var msg = _localizationService.GetLocalizedString("TempQuotattionService.SalesTypeNotFoundForOfferType", defaultOfferType);
                    return ApiResponse<long>.ErrorResult(msg, msg, StatusCodes.Status404NotFound);
                }

                var customerTypeId = customer.CustomerTypeId ?? 0;

                var quotationDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.RuleType == PricingRuleType.Quotation)
                    .Where(x => x.CustomerTypeId == customerTypeId && x.SalesRepId == userId)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (quotationDocumentSerialType == null)
                {
                    quotationDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                        .AsNoTracking()
                        .Where(x => !x.IsDeleted && x.RuleType == PricingRuleType.Quotation)
                        .Where(x => x.CustomerTypeId == customerTypeId && x.SalesRepId == null)
                        .OrderBy(x => x.Id)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }

                if (quotationDocumentSerialType == null)
                {
                    quotationDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                        .AsNoTracking()
                        .Where(x => !x.IsDeleted && x.RuleType == PricingRuleType.Quotation)
                        .Where(x => x.CustomerTypeId == null && x.SalesRepId == userId)
                        .OrderBy(x => x.Id)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }

                if (quotationDocumentSerialType == null)
                {
                    quotationDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                        .AsNoTracking()
                        .Where(x => !x.IsDeleted && x.RuleType == PricingRuleType.Quotation)
                        .Where(x => x.CustomerTypeId == null && x.SalesRepId == null)
                        .OrderBy(x => x.Id)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                }

                if (quotationDocumentSerialType == null)
                {
                    var msg = _localizationService.GetLocalizedString("TempQuotattionService.QuotationDocumentSerialTypeNotFound");
                    return ApiResponse<long>.ErrorResult(msg, msg, StatusCodes.Status404NotFound);
                }

                var documentSerialResult = await _documentSerialTypeService.GenerateDocumentSerialAsync(quotationDocumentSerialType.Id).ConfigureAwait(false);
                if (!documentSerialResult.Success)
                {
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.QuotationNumberGenerationFailed"),
                        documentSerialResult.Message,
                        StatusCodes.Status500InternalServerError);
                }

                var quotation = new Quotation
                {
                    PotentialCustomerId = entity.CustomerId,
                    DocumentSerialTypeId = quotationDocumentSerialType.Id,
                    OfferType = defaultOfferType,
                    OfferDate = entity.OfferDate,
                    OfferNo = documentSerialResult.Data,
                    RevisionNo = documentSerialResult.Data,
                    Currency = entity.CurrencyCode,
                    Total = lines.Sum(x => x.LineTotal),
                    GrandTotal = lines.Sum(x => x.LineGrandTotal),
                    Description = entity.Description,
                    SalesTypeDefinitionId = salesTypeDefinition.Id,
                    Status = ApprovalStatus.HavenotStarted,
                    CreatedBy = userId,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.Quotations.AddAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var quotationLines = lines.Select(line => new QuotationLine
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
                    Description = line.Description
                }).ToList();

                await _unitOfWork.QuotationLines.AddAllAsync(quotationLines).ConfigureAwait(false);

                if (exchangeLines.Any())
                {
                    var quotationExchangeRates = exchangeLines.Select(rate => new QuotationExchangeRate
                    {
                        QuotationId = quotation.Id,
                        Currency = rate.Currency,
                        ExchangeRate = rate.ExchangeRate,
                        ExchangeRateDate = rate.ExchangeRateDate,
                        IsOfficial = !rate.IsManual
                    }).ToList();

                    await _unitOfWork.QuotationExchangeRates.AddAllAsync(quotationExchangeRates).ConfigureAwait(false);
                }

                entity.IsApproved = true;
                entity.ApprovedDate = DateTime.UtcNow;
                entity.QuotationId = quotation.Id;
                entity.QuotationNo = quotation.OfferNo;
                entity.UpdatedDate = DateTime.UtcNow;
                entity.UpdatedBy = userId;

                await _unitOfWork.TempQuotattions.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                return ApiResponse<long>.SuccessResult(quotation.Id, _localizationService.GetLocalizedString("TempQuotattionService.ConvertedToQuotationSuccess"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<long>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionGetDto>> SetApprovedAsync(long id)
        {
            var convertResult = await ConvertToQuotationAsync(id).ConfigureAwait(false);
            if (!convertResult.Success)
            {
                return ApiResponse<TempQuotattionGetDto>.ErrorResult(
                    convertResult.Message,
                    convertResult.ExceptionMessage,
                    convertResult.StatusCode,
                    convertResult.Errors);
            }

            return await GetByIdAsync(id).ConfigureAwait(false);
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.TempQuotattions.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TempQuotattionLineGetDto>>> GetLinesByHeaderIdAsync(long tempQuotattionId)
        {
            try
            {
                var exists = await _unitOfWork.TempQuotattions.Query().AnyAsync(x => x.Id == tempQuotattionId && !x.IsDeleted).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<List<TempQuotattionLineGetDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var lines = await _unitOfWork.TempQuotattionLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == tempQuotattionId && !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = lines.Select(_mapper.Map<TempQuotattionLineGetDto>).ToList();
                return ApiResponse<List<TempQuotattionLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TempQuotattionLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionLineGetDto>> GetLineByIdAsync(long lineId)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionLines.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == lineId && !x.IsDeleted).ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                return ApiResponse<TempQuotattionLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionLineGetDto>> CreateLineAsync(TempQuotattionLineCreateDto dto)
        {
            try
            {
                var exists = await _unitOfWork.TempQuotattions.Query().AnyAsync(x => x.Id == dto.TempQuotattionId && !x.IsDeleted).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entity = _mapper.Map<TempQuotattionLine>(dto);
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattionLines.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<TempQuotattionLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TempQuotattionLineGetDto>>> CreateLinesAsync(List<TempQuotattionLineCreateDto> dtos)
        {
            try
            {
                if (dtos == null || dtos.Count == 0)
                {
                    return ApiResponse<List<TempQuotattionLineGetDto>>.SuccessResult(
                        new List<TempQuotattionLineGetDto>(),
                        _localizationService.GetLocalizedString("TempQuotattionService.NoLineToCreate"));
                }

                var headerIds = dtos.Select(x => x.TempQuotattionId).Distinct().ToList();
                var existingHeaderIds = await _unitOfWork.TempQuotattions.Query()
                    .Where(x => headerIds.Contains(x.Id) && !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToListAsync().ConfigureAwait(false);

                if (existingHeaderIds.Count != headerIds.Count)
                {
                    return ApiResponse<List<TempQuotattionLineGetDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entities = _mapper.Map<List<TempQuotattionLine>>(dtos);
                foreach (var entity in entities)
                {
                    entity.CreatedDate = DateTime.UtcNow;
                }

                await _unitOfWork.TempQuotattionLines.AddAllAsync(entities).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var response = entities.Select(_mapper.Map<TempQuotattionLineGetDto>).ToList();
                return ApiResponse<List<TempQuotattionLineGetDto>>.SuccessResult(response, _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLinesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TempQuotattionLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionLineGetDto>> UpdateLineAsync(long lineId, TempQuotattionLineUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionLines.GetByIdAsync(lineId).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattionLines.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<TempQuotattionLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteLineAsync(long lineId)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionLines.GetByIdAsync(lineId).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.TempQuotattionLines.SoftDeleteAsync(lineId).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionLineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<TempQuotattionExchangeLineGetDto>>> GetExchangeLinesByHeaderIdAsync(long tempQuotattionId)
        {
            try
            {
                var exists = await _unitOfWork.TempQuotattions.Query().AnyAsync(x => x.Id == tempQuotattionId && !x.IsDeleted).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<List<TempQuotattionExchangeLineGetDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var rows = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .AsNoTracking()
                    .Where(x => x.TempQuotattionId == tempQuotattionId && !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = rows.Select(_mapper.Map<TempQuotattionExchangeLineGetDto>).ToList();
                return ApiResponse<List<TempQuotattionExchangeLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<TempQuotattionExchangeLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionExchangeLineGetDto>> GetExchangeLineByIdAsync(long exchangeLineId)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == exchangeLineId && !x.IsDeleted).ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                return ApiResponse<TempQuotattionExchangeLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionExchangeLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionExchangeLineGetDto>> CreateExchangeLineAsync(TempQuotattionExchangeLineCreateDto dto)
        {
            try
            {
                var exists = await _unitOfWork.TempQuotattions.Query().AnyAsync(x => x.Id == dto.TempQuotattionId && !x.IsDeleted).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var existingLine = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .FirstOrDefaultAsync(x => x.TempQuotattionId == dto.TempQuotattionId
                        && x.Currency == dto.Currency).ConfigureAwait(false);

                if (existingLine != null)
                {
                    if (!existingLine.IsDeleted)
                    {
                        return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                            _localizationService.GetLocalizedString("TempQuotattionService.ExchangeLineAlreadyExists"),
                            _localizationService.GetLocalizedString("TempQuotattionService.ExchangeLineAlreadyExists"),
                            StatusCodes.Status400BadRequest);
                    }

                    existingLine.IsDeleted = false;
                    existingLine.DeletedDate = null;
                    existingLine.DeletedBy = null;
                    existingLine.ExchangeRate = dto.ExchangeRate;
                    existingLine.ExchangeRateDate = dto.ExchangeRateDate;
                    existingLine.IsManual = dto.IsManual;
                    existingLine.UpdatedDate = DateTime.UtcNow;

                    await _unitOfWork.TempQuotattionExchangeLines.UpdateAsync(existingLine).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                    return ApiResponse<TempQuotattionExchangeLineGetDto>.SuccessResult(
                        _mapper.Map<TempQuotattionExchangeLineGetDto>(existingLine),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineCreated"));
                }

                var entity = _mapper.Map<TempQuotattionExchangeLine>(dto);
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattionExchangeLines.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<TempQuotattionExchangeLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionExchangeLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<TempQuotattionExchangeLineGetDto>> UpdateExchangeLineAsync(long exchangeLineId, TempQuotattionExchangeLineUpdateDto dto)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionExchangeLines.GetByIdAsync(exchangeLineId).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var currencyExists = await _unitOfWork.TempQuotattionExchangeLines.Query()
                    .AnyAsync(x => x.TempQuotattionId == entity.TempQuotattionId
                        && x.Currency == dto.Currency
                        && x.Id != exchangeLineId
                        && !x.IsDeleted).ConfigureAwait(false);
                if (currencyExists)
                {
                    return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.ExchangeLineAlreadyExists"),
                        _localizationService.GetLocalizedString("TempQuotattionService.ExchangeLineAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.TempQuotattionExchangeLines.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<TempQuotattionExchangeLineGetDto>.SuccessResult(
                    _mapper.Map<TempQuotattionExchangeLineGetDto>(entity),
                    _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<TempQuotattionExchangeLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteExchangeLineAsync(long exchangeLineId)
        {
            try
            {
                var entity = await _unitOfWork.TempQuotattionExchangeLines.GetByIdAsync(exchangeLineId).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.TempQuotattionExchangeLines.SoftDeleteAsync(exchangeLineId).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("TempQuotattionService.TempQuotattionExchangeLineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("TempQuotattionService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
