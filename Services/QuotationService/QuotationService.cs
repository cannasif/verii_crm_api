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
    public class QuotationService : IQuotationService
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

        public QuotationService(
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

        public async Task<ApiResponse<PagedResponse<QuotationGetDto>>> GetAllQuotationsAsync(PagedRequest request)
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

                var query = _unitOfWork.Quotations.Query()
                    .AsNoTracking()
                    .Where(q => !q.IsDeleted)
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping);

                var sortBy = request.SortBy ?? nameof(Quotation.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<QuotationGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<QuotationGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<QuotationGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("QuotationService.QuotationsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<QuotationGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetAllQuotationsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagedResponse<QuotationGetDto>>> GetRelatedQutotations(PagedRequest request)
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
                    return ApiResponse<PagedResponse<QuotationGetDto>>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var avaibleUsersResponse = await GetQuotationRelatedUsersAsync(userId).ConfigureAwait(false);
                if (!avaibleUsersResponse.Success)
                {
                    return ApiResponse<PagedResponse<QuotationGetDto>>.ErrorResult(
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

                var query = _unitOfWork.Quotations.Query()
                    .AsNoTracking()
                    .Where(q => !q.IsDeleted && (q.CreatedBy == userId || (q.RepresentativeId.HasValue && avaibleUsersIds.Contains(q.RepresentativeId.Value))))
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .ApplyFilters(request.Filters, request.FilterLogic, columnMapping2);

                var sortBy = request.SortBy ?? nameof(Quotation.Id);

                query = query.ApplySorting(sortBy, request.SortDirection, columnMapping2);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<QuotationGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<QuotationGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<QuotationGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("QuotationService.QuotationsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<QuotationGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetAllQuotationsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuotationGetDto>> GetQuotationByIdAsync(long id)
        {
            try
            {
                var quotation = await _unitOfWork.Quotations.GetByIdAsync(id).ConfigureAwait(false);
                if (quotation == null)
                {
                    return ApiResponse<QuotationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                    _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                    StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var quotationWithNav = await _unitOfWork.Quotations.Query()
                    .AsNoTracking()
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DeletedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted).ConfigureAwait(false);

                var quotationDto = _mapper.Map<QuotationGetDto>(quotationWithNav ?? quotation);
                return ApiResponse<QuotationGetDto>.SuccessResult(quotationDto, _localizationService.GetLocalizedString("QuotationService.QuotationRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetQuotationByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<QuotationDto>> CreateQuotationAsync(CreateQuotationDto createQuotationDto)
        {
            try
            {
                var quotation = _mapper.Map<Quotation>(createQuotationDto);
                quotation.GeneralDiscountRate = createQuotationDto.GeneralDiscountRate;
                quotation.GeneralDiscountAmount = createQuotationDto.GeneralDiscountAmount;
                quotation.CreatedDate = DateTimeProvider.Now;

                await _unitOfWork.Quotations.AddAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var quotationDto = _mapper.Map<QuotationDto>(quotation);
                return ApiResponse<QuotationDto>.SuccessResult(quotationDto, _localizationService.GetLocalizedString("QuotationService.QuotationCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.CreateQuotationExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<QuotationDto>> UpdateQuotationAsync(long id, UpdateQuotationDto updateQuotationDto)
        {
            try
            {
                // Get userId from HttpContext (should be set by middleware)
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    return ApiResponse<QuotationDto>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var quotation = await _unitOfWork.Quotations
                    .Query()
                    .Include(q => q.Lines)
                    .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted).ConfigureAwait(false);

                if (quotation == null)
                {
                    return ApiResponse<QuotationDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationNotFound"),
                        "Not found",
                        StatusCodes.Status404NotFound);
                }


                // 3. Güncelleme işlemi
                _mapper.Map(updateQuotationDto, quotation);
                quotation.GeneralDiscountRate = updateQuotationDto.GeneralDiscountRate;
                quotation.GeneralDiscountAmount = updateQuotationDto.GeneralDiscountAmount;
                quotation.UpdatedDate = DateTimeProvider.Now;
                quotation.UpdatedBy = userId;

                // 4. Toplamları yeniden hesapla
                decimal total = 0m;
                decimal grandTotal = 0m;

                foreach (var line in quotation.Lines.Where(l => !l.IsDeleted))
                {
                    total += line.LineTotal;
                    grandTotal += line.LineGrandTotal;
                }

                quotation.Total = total;
                quotation.GrandTotal = grandTotal;

                await _unitOfWork.Quotations.UpdateAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var quotationDto = _mapper.Map<QuotationDto>(quotation);
                return ApiResponse<QuotationDto>.SuccessResult(quotationDto, _localizationService.GetLocalizedString("QuotationService.QuotationUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.UpdateQuotationExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<object>> DeleteQuotationAsync(long id)
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


                var quotation = await _unitOfWork.Quotations.GetByIdAsync(id).ConfigureAwait(false);
                if (quotation == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationNotFound"),
                        "Not found",
                        StatusCodes.Status404NotFound);
                }


                // 3. Soft delete
                await _unitOfWork.Quotations.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("QuotationService.QuotationDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.DeleteQuotationExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<QuotationGetDto>>> GetQuotationsByPotentialCustomerIdAsync(long potentialCustomerId)
        {
            try
            {
                var quotations = await _unitOfWork.Quotations.FindAsync(q => q.PotentialCustomerId == potentialCustomerId).ConfigureAwait(false);
                var quotationDtos = _mapper.Map<List<QuotationGetDto>>(quotations.ToList());
                return ApiResponse<List<QuotationGetDto>>.SuccessResult(quotationDtos, _localizationService.GetLocalizedString("QuotationService.QuotationsByPotentialCustomerRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetQuotationsByPotentialCustomerExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<QuotationGetDto>>> GetQuotationsByRepresentativeIdAsync(long representativeId)
        {
            try
            {
                var quotations = await _unitOfWork.Quotations.FindAsync(q => q.RepresentativeId == representativeId).ConfigureAwait(false);
                var quotationDtos = _mapper.Map<List<QuotationGetDto>>(quotations.ToList());
                return ApiResponse<List<QuotationGetDto>>.SuccessResult(quotationDtos, _localizationService.GetLocalizedString("QuotationService.QuotationsByRepresentativeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetQuotationsByRepresentativeExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<QuotationGetDto>>> GetQuotationsByStatusAsync(int status)
        {
            try
            {
                var quotations = await _unitOfWork.Quotations.FindAsync(q => (int?)q.Status == status).ConfigureAwait(false);
                var quotationDtos = _mapper.Map<List<QuotationGetDto>>(quotations.ToList());
                return ApiResponse<List<QuotationGetDto>>.SuccessResult(quotationDtos, _localizationService.GetLocalizedString("QuotationService.QuotationsByStatusRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<QuotationGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetQuotationsByStatusExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<bool>> QuotationExistsAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.Quotations.ExistsAsync(id).ConfigureAwait(false);
                return ApiResponse<bool>.SuccessResult(exists, exists ? _localizationService.GetLocalizedString("QuotationService.QuotationRetrieved") : _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.QuotationExistsExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<QuotationGetDto>> CreateQuotationBulkAsync(QuotationBulkCreateDto bulkDto)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

            try
            {
                var documentSerialType = await _documentSerialTypeService.GenerateDocumentSerialAsync(bulkDto.Quotation.DocumentSerialTypeId).ConfigureAwait(false);
                if (!documentSerialType.Success)
                {
                    return ApiResponse<QuotationGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.DocumentSerialTypeGenerationError"),
                        documentSerialType.Message,
                        StatusCodes.Status500InternalServerError);
                }
                bulkDto.Quotation.OfferNo = documentSerialType.Data;
                bulkDto.Quotation.RevisionNo = documentSerialType.Data;
                bulkDto.Quotation.Status = ApprovalStatus.HavenotStarted;

                // 1. Header map
                var quotation = _mapper.Map<Quotation>(bulkDto.Quotation);
                quotation.GeneralDiscountRate = bulkDto.Quotation.GeneralDiscountRate;
                quotation.GeneralDiscountAmount = bulkDto.Quotation.GeneralDiscountAmount;

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

                quotation.Total = total;
                quotation.GrandTotal = grandTotal;

                // 3. Save header
                await _unitOfWork.Quotations.AddAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // 4. Map & calculate lines
                var lines = new List<QuotationLine>(bulkDto.Lines.Count);

                foreach (var lineDto in bulkDto.Lines)
                {
                    var line = _mapper.Map<QuotationLine>(lineDto);
                    line.QuotationId = quotation.Id;

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

                await _unitOfWork.QuotationLines.AddAllAsync(lines).ConfigureAwait(false);

                                // 5. Quotation notes
                if (bulkDto.QuotationNotes != null)
                {
                    var quotationNotes = _mapper.Map<QuotationNotes>(bulkDto.QuotationNotes);
                    quotationNotes.QuotationId = quotation.Id;
                    await _unitOfWork.QuotationNotes.AddAsync(quotationNotes).ConfigureAwait(false);
                }

                // 6. Exchange rates
                if (bulkDto.ExchangeRates?.Any() == true)
                {
                    var rates = bulkDto.ExchangeRates
                        .Select(r =>
                        {
                            var rate = _mapper.Map<QuotationExchangeRate>(r);
                            rate.QuotationId = quotation.Id;
                            return rate;
                        }).ToList();

                    await _unitOfWork.QuotationExchangeRates.AddAllAsync(rates).ConfigureAwait(false);
                }

                                // 7. Commit
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // 8. Reload
                var quotationWithNav = await _unitOfWork.Quotations
                    .Query()
                    .AsNoTracking()
                    .Include(q => q.Representative)
                    .Include(q => q.Lines)
                    .Include(q => q.PotentialCustomer)
                    .Include(q => q.CreatedByUser)
                    .Include(q => q.UpdatedByUser)
                    .Include(q => q.DocumentSerialType)
                    .Include(q => q.SalesTypeDefinition)
                    .FirstOrDefaultAsync(q => q.Id == quotation.Id).ConfigureAwait(false);

                var dto = _mapper.Map<QuotationGetDto>(quotationWithNav);

                return ApiResponse<QuotationGetDto>.SuccessResult(dto, _localizationService.GetLocalizedString("QuotationService.QuotationCreated"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);

                return ApiResponse<QuotationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.CreateQuotationBulkExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
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
        public async Task<ApiResponse<List<ApprovalScopeUserDto>>> GetQuotationRelatedUsersAsync(long userId)
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
                          && f.DocumentType == PricingRuleType.Quotation
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
                                _localizationService.GetLocalizedString("QuotationService.ApprovalScopeUsersRetrieved"));
                    }
                    var approvalScopeUserDtos = new List<ApprovalScopeUserDto>();
                    approvalScopeUserDtos.Add(new ApprovalScopeUserDto
                    {
                        FlowId = 0,
                        UserId = userId,
                        FirstName = userData.FirstName ?? "",
                        LastName = userData.LastName ?? "",
                        RoleGroupName = "Teklif Sahibi",
                        StepOrder = 0
                    });
                    return ApiResponse<List<ApprovalScopeUserDto>>
                        .SuccessResult(
                            approvalScopeUserDtos,
                            _localizationService.GetLocalizedString("QuotationService.ApprovalScopeUsersRetrieved"));
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
                            "QuotationService.QuotationApproverUsersRetrieved"
                        )
                    );
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ApprovalScopeUserDto>>
                    .ErrorResult(
                        _localizationService.GetLocalizedString(
                            "QuotationService.InternalServerError"
                        ),
                        ex.Message,
                        StatusCodes.Status500InternalServerError
                    );
            }
        }

        public async Task<ApiResponse<QuotationGetDto>> CreateRevisionOfQuotationAsync(long quotationId)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                var quotation = await _unitOfWork.Quotations.GetByIdAsync(quotationId).ConfigureAwait(false);
                if (quotation == null)
                {
                    return ApiResponse<QuotationGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var quotationLines = await _unitOfWork.QuotationLines.Query()
                .Where(x => !x.IsDeleted && x.QuotationId == quotationId).ToListAsync().ConfigureAwait(false);

                var QuotationExchangeRates = await _unitOfWork.QuotationExchangeRates.Query()
                .Where(x => !x.IsDeleted && x.QuotationId == quotationId).ToListAsync().ConfigureAwait(false);

                var quotationNotes = await _unitOfWork.QuotationNotes.Query()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.QuotationId == quotationId).ConfigureAwait(false);

                var documentSerialTypeWithRevision = await _documentSerialTypeService.GenerateDocumentSerialAsync(quotation.DocumentSerialTypeId, false, quotation.RevisionNo).ConfigureAwait(false);
                if (!documentSerialTypeWithRevision.Success)
                {
                    return ApiResponse<QuotationGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.DocumentSerialTypeGenerationError"),
                        documentSerialTypeWithRevision.Message,
                        StatusCodes.Status500InternalServerError);
                }

                var newQuotation = new Quotation();
                newQuotation.OfferType = quotation.OfferType;
                newQuotation.RevisionId = quotation.Id;
                newQuotation.OfferDate = quotation.OfferDate;
                newQuotation.OfferNo = quotation.OfferNo;
                newQuotation.RevisionNo = documentSerialTypeWithRevision.Data;
                newQuotation.OfferDate = quotation.OfferDate;
                newQuotation.Currency = quotation.Currency;
                newQuotation.GeneralDiscountRate = quotation.GeneralDiscountRate;
                newQuotation.GeneralDiscountAmount = quotation.GeneralDiscountAmount;
                newQuotation.Total = quotation.Total;
                newQuotation.GrandTotal = quotation.GrandTotal;
                newQuotation.CreatedBy = quotation.CreatedBy;
                newQuotation.CreatedDate = DateTimeProvider.Now;
                newQuotation.PotentialCustomerId = quotation.PotentialCustomerId;
                newQuotation.ErpCustomerCode = quotation.ErpCustomerCode;
                newQuotation.ContactId = quotation.ContactId;
                newQuotation.ValidUntil = quotation.ValidUntil;
                newQuotation.DeliveryDate = quotation.DeliveryDate;
                newQuotation.ShippingAddressId = quotation.ShippingAddressId;
                newQuotation.RepresentativeId = quotation.RepresentativeId;
                newQuotation.ActivityId = quotation.ActivityId;
                newQuotation.Description = quotation.Description;
                newQuotation.PaymentTypeId = quotation.PaymentTypeId;
                newQuotation.HasCustomerSpecificDiscount = quotation.HasCustomerSpecificDiscount;
                newQuotation.DemandId = quotation.DemandId;
                newQuotation.SalesTypeDefinitionId = quotation.SalesTypeDefinitionId;
                newQuotation.ErpProjectCode = quotation.ErpProjectCode;
                newQuotation.Status = (int)ApprovalStatus.HavenotStarted;

                await _unitOfWork.Quotations.AddAsync(newQuotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var newQuotationLines = new List<QuotationLine>();
                foreach (var line in quotationLines)
                {
                    var newLine = new QuotationLine();
                    newLine.QuotationId = newQuotation.Id;
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
                    newLine.ApprovalStatus = ApprovalStatus.HavenotStarted;
                    newQuotationLines.Add(newLine);
                }
                await _unitOfWork.QuotationLines.AddAllAsync(newQuotationLines).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var newQuotationExchangeRates = new List<QuotationExchangeRate>();
                foreach (var exchangeRate in QuotationExchangeRates)
                {
                    var newExchangeRate = new QuotationExchangeRate();
                    newExchangeRate.QuotationId = newQuotation.Id;
                    newExchangeRate.ExchangeRate = exchangeRate.ExchangeRate;
                    newExchangeRate.ExchangeRateDate = exchangeRate.ExchangeRateDate;
                    newExchangeRate.Currency = exchangeRate.Currency;
                    newExchangeRate.IsOfficial = exchangeRate.IsOfficial;
                    newQuotationExchangeRates.Add(newExchangeRate);
                }
                await _unitOfWork.QuotationExchangeRates.AddAllAsync(newQuotationExchangeRates).ConfigureAwait(false);

                if (quotationNotes != null)
                {
                    var newQuotationNotes = new QuotationNotes
                    {
                        QuotationId = newQuotation.Id,
                        Note1 = quotationNotes.Note1,
                        Note2 = quotationNotes.Note2,
                        Note3 = quotationNotes.Note3,
                        Note4 = quotationNotes.Note4,
                        Note5 = quotationNotes.Note5,
                        Note6 = quotationNotes.Note6,
                        Note7 = quotationNotes.Note7,
                        Note8 = quotationNotes.Note8,
                        Note9 = quotationNotes.Note9,
                        Note10 = quotationNotes.Note10,
                        Note11 = quotationNotes.Note11,
                        Note12 = quotationNotes.Note12,
                        Note13 = quotationNotes.Note13,
                        Note14 = quotationNotes.Note14,
                        Note15 = quotationNotes.Note15
                    };

                    await _unitOfWork.QuotationNotes.AddAsync(newQuotationNotes).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
                var dtos = _mapper.Map<QuotationGetDto>(newQuotation);
                return ApiResponse<QuotationGetDto>.SuccessResult(dtos, _localizationService.GetLocalizedString("QuotationService.QuotationRevisionCreated"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<QuotationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.CreateRevisionOfQuotationExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<List<PricingRuleLineGetDto>>> GetPriceRuleOfQuotationAsync(string customerCode, long salesmanId, DateTime quotationDate)
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
                        x.RuleType == PricingRuleType.Quotation &&
                        !x.IsDeleted &&
                        x.BranchCode == branchCode &&
                        x.ValidFrom <= quotationDate &&
                        x.ValidTo >= quotationDate
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
                            "QuotationService.PriceRuleNotFound"));
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
                        "QuotationService.PriceRuleOfQuotationRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PricingRuleLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetPriceRuleOfQuotationExceptionMessage", ex.Message
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

                return ApiResponse<List<PriceOfProductDto>>.SuccessResult(price, _localizationService.GetLocalizedString("QuotationService.PriceOfProductRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PriceOfProductDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetPriceOfProductExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalFlowAlreadyExists"),
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
                    var quotationId = await ConvertToOrderAsync(request.EntityId).ConfigureAwait(false);
                    if (!quotationId.Success)
                    {
                        await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.ErrorResult(quotationId.Message, quotationId.Message, StatusCodes.Status404NotFound);
                    }
                    else
                    {
                        await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
                        return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("QuotationService.ApprovalFlowStarted"));
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
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.ApprovalFlowStepsNotFound"),
                        "Flow'a ait step tanımı yok.",
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalRoleNotFound"),
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
                    CreatedDate = DateTimeProvider.Now,
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalUsersNotFound"),
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
                            _localizationService.GetLocalizedString("QuotationService.UserNotFound"),
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
                        ActionDate = DateTimeProvider.Now,
                        CreatedDate = DateTimeProvider.Now,
                        CreatedBy = startedByUserId,
                        IsDeleted = false
                    };

                    actions.Add(action);
                }

                await _unitOfWork.ApprovalActions.AddAllAsync(actions).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var quotation = await _unitOfWork.Quotations.GetByIdAsync(request.EntityId).ConfigureAwait(false);
                if (quotation == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        "Teklif bulunamadı.",
                        StatusCodes.Status404NotFound);
                }
                quotation.Status = ApprovalStatus.Waiting;

                await _unitOfWork.Quotations.UpdateAsync(quotation).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Transaction'ı commit et
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // UserId -> ApprovalActionId eşlemesi (onay linkleri için)
                var userIdToActionId = actions.ToDictionary(a => a.ApprovedByUserId, a => a.Id);
                var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                var approvalPath = _configuration["FrontendSettings:ApprovalPendingPath"]?.TrimStart('/') ?? "approvals/pending";
                var quotationPath = _configuration["FrontendSettings:QuotationDetailPath"]?.TrimStart('/') ?? "quotations";

                // Send Notifications
                foreach (var user in usersToNotify)
                {
                    try
                    {
                        var notificationResult = await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                        {
                            UserId = user.UserId,
                            TitleKey = "Notification.QuotationApproval.Title", // "Onay Bekleyen Teklif"
                            TitleArgs = new object[] { quotation.Id },
                            MessageKey = "Notification.QuotationApproval.Message", // "{0} numaralı teklif onay beklemektedir."
                            MessageArgs = new object[] { quotation.RevisionNo ?? "" },
                            NotificationType = NotificationType.QuotationApproval,
                            RelatedEntityName = "Quotation",
                            RelatedEntityId = quotation.Id
                        }).ConfigureAwait(false);


                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }

                BackgroundJob.Enqueue<IMailJob>(job =>
                    job.SendBulkQuotationApprovalPendingEmailsAsync(
                        usersToNotify.ToList(),
                        userIdToActionId,
                        baseUrl,
                        approvalPath,
                        quotationPath,
                        request.EntityId));

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("QuotationService.ApprovalFlowStarted"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.StartApprovalFlowExceptionMessage", ex.Message),
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
                        x.ApprovalRequest.DocumentType == PricingRuleType.Quotation &&
                        x.ApprovedByUserId == targetUserId &&
                        x.Status == ApprovalStatus.Waiting &&
                        !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = _mapper.Map<List<ApprovalActionGetDto>>(approvalActions);

                return ApiResponse<List<ApprovalActionGetDto>>.SuccessResult(
                    dtos,
                    _localizationService.GetLocalizedString("QuotationService.WaitingApprovalsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ApprovalActionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.GetWaitingApprovalsExceptionMessage", ex.Message),
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalActionNotFound"),
                        "Onay kaydı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Onay işlemini gerçekleştir
                action.Status = ApprovalStatus.Approved;
                action.ActionDate = DateTimeProvider.Now;
                action.UpdatedDate = DateTimeProvider.Now;
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalActionApproved"));
                }

                // Step tamamlandı → sonraki step'e geç
                var approvalRequest = await _unitOfWork.ApprovalRequests.Query()
                    .Include(ar => ar.ApprovalFlow)
                    .FirstOrDefaultAsync(x => x.Id == action.ApprovalRequestId && !x.IsDeleted).ConfigureAwait(false);

                if (approvalRequest == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.ApprovalRequestNotFound"),
                        "Onay talebi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Quotation'ı al (hem akış bittiğinde hem de sonraki step için gerekli)
                var quotation = await _unitOfWork.Quotations.Query()
                    .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);

                if (quotation == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        "Teklif bulunamadı.",
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

                    quotation.Status = ApprovalStatus.Approved;
                    quotation.UpdatedDate = now;
                    quotation.UpdatedBy = userId;
                    await _unitOfWork.Quotations.UpdateAsync(quotation).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(quotation.OfferNo))
                    {
                        var siblingQuotations = await _unitOfWork.Quotations.Query()
                            .Where(q => !q.IsDeleted && q.Id != quotation.Id && q.OfferNo == quotation.OfferNo)
                            .ToListAsync().ConfigureAwait(false);

                        foreach (var siblingQuotation in siblingQuotations)
                        {
                            siblingQuotation.Status = ApprovalStatus.Closed;
                            siblingQuotation.UpdatedDate = now;
                            siblingQuotation.UpdatedBy = userId;
                            await _unitOfWork.Quotations.UpdateAsync(siblingQuotation).ConfigureAwait(false);
                        }
                    }

                    var quotationId = await ConvertToOrderAsync(quotation.Id).ConfigureAwait(false);
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

                    // QuotationLine'ların ApprovalStatus'unu Approved yap
                    var quotationLines = await _unitOfWork.QuotationLines.Query()
                        .Where(ql => ql.QuotationId == quotation.Id && !ql.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);

                    foreach (var line in quotationLines)
                    {
                        line.ApprovalStatus = ApprovalStatus.Approved;
                        line.UpdatedDate = now;
                        line.UpdatedBy = userId;
                        await _unitOfWork.QuotationLines.UpdateAsync(line).ConfigureAwait(false);
                    }

                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                    // Teklif sahibine onaylandı bildirimi ve mail gönder (eğer onaylayan kişi teklif sahibi değilse)
                    if (quotation.CreatedBy > 0 && quotation.CreatedBy != userId)
                    {
                        try
                        {
                            var quotationForNotification = await _unitOfWork.Quotations.Query()
                                .Include(q => q.CreatedByUser)
                                .FirstOrDefaultAsync(q => q.Id == quotation.Id).ConfigureAwait(false);

                            if (quotationForNotification != null && quotationForNotification.CreatedByUser != null)
                            {
                                // Bildirim oluştur
                                try
                                {
                                    await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                                    {
                                        UserId = quotationForNotification.CreatedBy ?? 0L,
                                        TitleKey = "Notification.QuotationApproved.Title", // "Teklif Onaylandı"
                                        TitleArgs = new object[] { quotationForNotification.Id },
                                        MessageKey = "Notification.QuotationApproved.Message", // "{0} numaralı teklif onaylandı."
                                        MessageArgs = new object[] { quotationForNotification.RevisionNo ?? "" },
                                        NotificationType = NotificationType.QuotationDetail,
                                        RelatedEntityName = "Quotation",
                                        RelatedEntityId = quotationForNotification.Id
                                    }).ConfigureAwait(false);
                                }
                                catch (Exception)
                                {
                                    // ignore
                                }

                                // Mail gönder
                                var approverUser = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted).ConfigureAwait(false);
                                if (approverUser != null && !string.IsNullOrWhiteSpace(quotationForNotification.CreatedByUser.Email))
                                {
                                    var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                                    var quotationPath = _configuration["FrontendSettings:QuotationDetailPath"]?.TrimStart('/') ?? "quotations";
                                    var quotationLink = $"{baseUrl}/{quotationPath}/{quotationForNotification.Id}";

                                    var creatorFullName = $"{quotationForNotification.CreatedByUser.FirstName} {quotationForNotification.CreatedByUser.LastName}".Trim();
                                    if (string.IsNullOrWhiteSpace(creatorFullName)) creatorFullName = quotationForNotification.CreatedByUser.Username;

                                    var approverFullName = $"{approverUser.FirstName} {approverUser.LastName}".Trim();
                                    if (string.IsNullOrWhiteSpace(approverFullName)) approverFullName = approverUser.Username;

                                    BackgroundJob.Enqueue<IMailJob>(job =>
                                        job.SendQuotationApprovedEmailAsync(
                                            quotationForNotification.CreatedByUser.Email,
                                            creatorFullName,
                                            approverFullName,
                                            quotationForNotification.RevisionNo ?? "",
                                            quotationLink,
                                            quotationForNotification.Id
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalFlowCompleted"));
                }

                // Yeni step için onaycıları oluştur
                approvalRequest.CurrentStep = nextStep.StepOrder;
                approvalRequest.UpdatedDate = DateTimeProvider.Now;
                approvalRequest.UpdatedBy = userId;
                await _unitOfWork.ApprovalRequests.UpdateAsync(approvalRequest).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Yeni step için rolleri bul (StartApprovalFlow'daki mantık)
                var validRoles = await _unitOfWork.ApprovalRoles.Query()
                    .Where(r =>
                        r.ApprovalRoleGroupId == nextStep.ApprovalRoleGroupId &&
                        r.MaxAmount >= quotation.GrandTotal &&
                        !r.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                if (!validRoles.Any())
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.ApprovalRoleNotFound"),
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalUsersNotFound"),
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
                        ActionDate = DateTimeProvider.Now,
                        CreatedDate = DateTimeProvider.Now,
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
                    var quotationForNotification = await _unitOfWork.Quotations.Query()
                        .FirstOrDefaultAsync(q => q.Id == quotation.Id).ConfigureAwait(false);

                    if (quotationForNotification != null)
                    {
                        // UserId -> ApprovalActionId eşlemesi (onay linkleri için)
                        var userIdToActionId = newActions.ToDictionary(a => a.ApprovedByUserId, a => a.Id);
                        var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                        var approvalPath = _configuration["FrontendSettings:ApprovalPendingPath"]?.TrimStart('/') ?? "approvals/pending";
                        var quotationPath = _configuration["FrontendSettings:QuotationDetailPath"]?.TrimStart('/') ?? "quotations";

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
                                    TitleKey = "Notification.QuotationApproval.Title", // "Onay Bekleyen Teklif"
                                    TitleArgs = new object[] { quotationForNotification.Id },
                                    MessageKey = "Notification.QuotationApproval.Message", // "{0} numaralı teklif onay beklemektedir."
                                    MessageArgs = new object[] { quotationForNotification.RevisionNo ?? "" },
                                    NotificationType = NotificationType.QuotationApproval,
                                    RelatedEntityName = "Quotation",
                                    RelatedEntityId = quotationForNotification.Id
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
                                job.SendBulkQuotationApprovalPendingEmailsAsync(
                                    usersToNotify,
                                    userIdToActionId,
                                    baseUrl,
                                    approvalPath,
                                    quotationPath,
                                    quotationForNotification.Id));
                        }
                    }
                }
                catch (Exception)
                {
                    // Bildirim ve mail gönderimi başarısız olsa bile işlem başarılı sayılmalı
                }

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("QuotationService.ApprovalActionApprovedAndNextStepStarted"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.ApproveExceptionMessage", ex.Message),
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
                        _localizationService.GetLocalizedString("QuotationService.ApprovalActionNotFound"),
                        "Onay kaydı bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                // Red işlemini gerçekleştir
                action.Status = ApprovalStatus.Rejected;
                action.ActionDate = DateTimeProvider.Now;
                action.UpdatedDate = DateTimeProvider.Now;
                action.UpdatedBy = userId;

                await _unitOfWork.ApprovalActions.UpdateAsync(action).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // ApprovalRequest action ile birlikte yüklendi, ikinci kez query edip track çakışması oluşturmuyoruz.
                var approvalRequest = action.ApprovalRequest;

                if (approvalRequest == null || approvalRequest.IsDeleted)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.ApprovalRequestNotFound"),
                        "Onay talebi bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                approvalRequest.Status = ApprovalStatus.Rejected;
                approvalRequest.UpdatedDate = DateTimeProvider.Now;
                approvalRequest.UpdatedBy = userId;

                await _unitOfWork.ApprovalRequests.UpdateAsync(approvalRequest).ConfigureAwait(false);

                // Teklif durumunu ve red sebebini güncelle (raporlama için)
                var quotationForReject = await _unitOfWork.Quotations.Query(tracking: true)
                    .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);
                if (quotationForReject != null)
                {
                    quotationForReject.Status = ApprovalStatus.Rejected;
                    quotationForReject.RejectedReason = request.RejectReason;
                    quotationForReject.UpdatedDate = DateTimeProvider.Now;
                    quotationForReject.UpdatedBy = userId;
                    await _unitOfWork.Quotations.UpdateAsync(quotationForReject).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Eğer reddeden kullanıcı teklifi oluşturan kullanıcıysa ve en alt aşamadaysa (CurrentStep == 1)
                // QuotationLine'ların ApprovalStatus'unu Rejected yap
                if (approvalRequest.CurrentStep == 1)
                {
                    var quotation = await _unitOfWork.Quotations.Query()
                        .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId && !q.IsDeleted).ConfigureAwait(false);

                    if (quotation != null && quotation.CreatedBy == userId)
                    {
                        var quotationLines = await _unitOfWork.QuotationLines.Query()
                            .Where(ql => ql.QuotationId == quotation.Id && !ql.IsDeleted)
                            .ToListAsync().ConfigureAwait(false);

                        foreach (var line in quotationLines)
                        {
                            line.ApprovalStatus = ApprovalStatus.Rejected;
                            line.UpdatedDate = DateTimeProvider.Now;
                            line.UpdatedBy = userId;
                            await _unitOfWork.QuotationLines.UpdateAsync(line).ConfigureAwait(false);
                        }

                        await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                // Teklif sahibine mail gönder (eğer reddeden kişi teklif sahibi değilse)
                try
                {
                    var quotationForMail = await _unitOfWork.Quotations.Query()
                        .Include(q => q.CreatedByUser)
                        .FirstOrDefaultAsync(q => q.Id == approvalRequest.EntityId).ConfigureAwait(false);

                    if (quotationForMail != null && quotationForMail.CreatedBy != userId)
                    {
                        // Bildirim oluştur
                        try
                        {
                            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                            {
                                UserId = quotationForMail.CreatedBy ?? 0L,
                                TitleKey = "Notification.QuotationRejected.Title", // "Teklif Reddedildi"
                                TitleArgs = new object[] { quotationForMail.Id },
                                MessageKey = "Notification.QuotationRejected.Message", // "{0} numaralı teklif reddedildi."
                                MessageArgs = new object[] { quotationForMail.RevisionNo ?? "" },
                                NotificationType = NotificationType.QuotationDetail,
                                RelatedEntityName = "Quotation",
                                RelatedEntityId = quotationForMail.Id
                            }).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // ignore
                        }

                        var rejectorUser = await _unitOfWork.Users.Query().FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted).ConfigureAwait(false);
                        if (rejectorUser != null && quotationForMail.CreatedByUser != null)
                        {
                            var baseUrl = _configuration["FrontendSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                            var quotationPath = _configuration["FrontendSettings:QuotationDetailPath"]?.TrimStart('/') ?? "quotations";
                            var quotationLink = $"{baseUrl}/{quotationPath}/{quotationForMail.Id}";

                            var creatorFullName = $"{quotationForMail.CreatedByUser.FirstName} {quotationForMail.CreatedByUser.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(creatorFullName)) creatorFullName = quotationForMail.CreatedByUser.Username;

                            var rejectorFullName = $"{rejectorUser.FirstName} {rejectorUser.LastName}".Trim();
                            if (string.IsNullOrWhiteSpace(rejectorFullName)) rejectorFullName = rejectorUser.Username;

                            BackgroundJob.Enqueue<IMailJob>(job =>
                                job.SendQuotationRejectedEmailAsync(
                                    quotationForMail.CreatedByUser.Email,
                                    creatorFullName,
                                    rejectorFullName,
                                    quotationForMail.RevisionNo ?? "",
                                    request.RejectReason ?? _localizationService.GetLocalizedString("General.NotSpecified"),
                                    quotationLink,
                                    quotationForMail.Id
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
                    _localizationService.GetLocalizedString("QuotationService.ApprovalActionRejected"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.RejectExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<long>> ConvertToOrderAsync(long quotationId)
        {
            await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                var userIdResponse = await _userService.GetCurrentUserIdAsync().ConfigureAwait(false);
                if (!userIdResponse.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<long>.ErrorResult(
                        userIdResponse.Message,
                        userIdResponse.Message,
                        StatusCodes.Status401Unauthorized);
                }
                var userId = userIdResponse.Data;

                var quotation = await _unitOfWork.Quotations.GetByIdForUpdateAsync(quotationId).ConfigureAwait(false);
                if (quotation == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        StatusCodes.Status404NotFound);
                }
                quotation.Status = ApprovalStatus.Approved;
                quotation.UpdatedDate = DateTimeProvider.Now;
                quotation.UpdatedBy = userId;

                var quotationsForReject = await _unitOfWork.Quotations.Query(tracking: true)
                    .Where(q => q.OfferNo == quotation.OfferNo && !q.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);
                if (quotationsForReject != null && quotationsForReject.Any())
                {
                    foreach (var quotationForReject in quotationsForReject)
                    {
                        quotationForReject.Status = ApprovalStatus.Rejected;
                        quotationForReject.UpdatedDate = DateTimeProvider.Now;
                        quotationForReject.UpdatedBy = userId;
                    }
                }

                var quotationLines = await _unitOfWork.QuotationLines.Query()
                    .Where(ql => ql.QuotationId == quotationId && !ql.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);
                if (quotationLines == null || !quotationLines.Any())
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationLinesNotFound"),
                        _localizationService.GetLocalizedString("QuotationService.QuotationLinesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var quotationExchangeRates = await _unitOfWork.QuotationExchangeRates.Query()
                    .Where(qer => qer.QuotationId == quotationId && !qer.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var orderDocumentSerialType = await _unitOfWork.DocumentSerialTypes.Query()
                    .Where(d => d.RuleType == PricingRuleType.Order && !d.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (orderDocumentSerialType == null)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.OrderDocumentSerialTypeNotFound"),
                        _localizationService.GetLocalizedString("QuotationService.OrderDocumentSerialTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var documentSerialResult = await _documentSerialTypeService.GenerateDocumentSerialAsync(orderDocumentSerialType.Id).ConfigureAwait(false);
                if (!documentSerialResult.Success)
                {
                    await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                    return ApiResponse<long>.ErrorResult(
                        _localizationService.GetLocalizedString("OrderService.DocumentSerialTypeGenerationError"),
                        documentSerialResult.Message,
                        StatusCodes.Status500InternalServerError);
                }

                var order = new Order
                {
                    PotentialCustomerId = quotation.PotentialCustomerId,
                    ErpCustomerCode = quotation.ErpCustomerCode,
                    ContactId = quotation.ContactId,
                    ValidUntil = quotation.ValidUntil,
                    DeliveryDate = quotation.DeliveryDate,
                    ShippingAddressId = quotation.ShippingAddressId,
                    RepresentativeId = quotation.RepresentativeId,
                    ActivityId = quotation.ActivityId,
                    Description = quotation.Description,
                    PaymentTypeId = quotation.PaymentTypeId,
                    DocumentSerialTypeId = orderDocumentSerialType.Id,
                    OfferType = quotation.OfferType,
                    OfferDate = quotation.OfferDate ?? DateTime.UtcNow,
                    OfferNo = documentSerialResult.Data,
                    RevisionNo = documentSerialResult.Data,
                    Currency = quotation.Currency,
                    HasCustomerSpecificDiscount = quotation.HasCustomerSpecificDiscount,
                    GeneralDiscountRate = quotation.GeneralDiscountRate,
                    GeneralDiscountAmount = quotation.GeneralDiscountAmount,
                    Total = quotation.Total,
                    GrandTotal = quotation.GrandTotal,
                    QuotationId = quotation.Id,
                    Status = ApprovalStatus.HavenotStarted,
                    CreatedBy = userId,
                    CreatedDate = DateTimeProvider.Now
                };

                await _unitOfWork.Orders.AddAsync(order).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var orderLines = new List<OrderLine>();
                foreach (var line in quotationLines)
                {
                    orderLines.Add(new OrderLine
                    {
                        OrderId = order.Id,
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
                        CreatedDate = DateTimeProvider.Now,
                        CreatedBy = userId
                    });
                }
                await _unitOfWork.OrderLines.AddAllAsync(orderLines).ConfigureAwait(false);

                if (quotationExchangeRates != null && quotationExchangeRates.Any())
                {
                    var orderExchangeRates = quotationExchangeRates.Select(rate => new OrderExchangeRate
                    {
                        OrderId = order.Id,
                        Currency = rate.Currency,
                        ExchangeRate = rate.ExchangeRate,
                        ExchangeRateDate = rate.ExchangeRateDate,
                        IsOfficial = rate.IsOfficial,
                        CreatedDate = DateTimeProvider.Now,
                        CreatedBy = userId
                    }).ToList();
                    await _unitOfWork.OrderExchangeRates.AddAllAsync(orderExchangeRates).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);

                return ApiResponse<long>.SuccessResult(order.Id, _localizationService.GetLocalizedString("QuotationService.OrderConvertedSuccessfully"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                return ApiResponse<long>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.ConvertToOrderExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Teklif onay akışı raporu - Aşamalar, kimler onayladı, kimler bekledi, kim reddetti ve ne yazdı
        /// </summary>
        /// <param name="quotationId">Teklif ID</param>
        /// <returns>Onay akışının aşama bazlı detaylı raporu</returns>
        public async Task<ApiResponse<QuotationApprovalFlowReportDto>> GetApprovalFlowReportAsync(long quotationId)
        {
            try
            {
                var quotation = await _unitOfWork.Quotations.Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == quotationId && !q.IsDeleted).ConfigureAwait(false);

                if (quotation == null)
                {
                    return ApiResponse<QuotationApprovalFlowReportDto>.ErrorResult(
                        _localizationService.GetLocalizedString("QuotationService.QuotationNotFound"),
                        "Teklif bulunamadı.",
                        StatusCodes.Status404NotFound);
                }

                var report = new QuotationApprovalFlowReportDto
                {
                    QuotationId = quotation.Id,
                    QuotationOfferNo = quotation.RevisionNo ?? quotation.OfferNo ?? quotation.Id.ToString(),
                    HasApprovalRequest = false,
                    OverallStatus = (int?)quotation.Status,
                    OverallStatusName = GetApprovalStatusName(quotation.Status),
                    RejectedReason = quotation.RejectedReason,
                    Steps = new List<ApprovalFlowStepReportDto>()
                };

                var approvalRequest = await _unitOfWork.ApprovalRequests.Query()
                    .AsNoTracking()
                    .Include(ar => ar.ApprovalFlow)
                    .FirstOrDefaultAsync(x =>
                        x.EntityId == quotationId &&
                        x.DocumentType == PricingRuleType.Quotation &&
                        !x.IsDeleted).ConfigureAwait(false);

                if (approvalRequest == null)
                {
                    report.HasApprovalRequest = false;
                    return ApiResponse<QuotationApprovalFlowReportDto>.SuccessResult(
                        report,
                        _localizationService.GetLocalizedString("QuotationService.ApprovalFlowReportRetrieved"));
                }

                report.HasApprovalRequest = true;
                report.CurrentStep = approvalRequest.CurrentStep;
                report.OverallStatus = (int)approvalRequest.Status;
                report.OverallStatusName = GetApprovalStatusName(approvalRequest.Status);
                if (string.IsNullOrEmpty(report.RejectedReason))
                    report.RejectedReason = quotation.RejectedReason;
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
                            RejectedReason = a.Status == ApprovalStatus.Rejected ? quotation.RejectedReason : null
                        }).ToList()
                    };
                    report.Steps.Add(stepReport);
                }

                return ApiResponse<QuotationApprovalFlowReportDto>.SuccessResult(
                    report,
                    _localizationService.GetLocalizedString("QuotationService.ApprovalFlowReportRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<QuotationApprovalFlowReportDto>.ErrorResult(
                    _localizationService.GetLocalizedString("QuotationService.InternalServerError"),
                    _localizationService.GetLocalizedString("QuotationService.ApprovalFlowReportExceptionMessage", ex.Message),
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
