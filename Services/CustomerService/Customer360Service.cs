using crm_api.DTOs;
using crm_api.DTOs.CustomerDto;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class Customer360Service : ICustomer360Service
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILocalizationService _localizationService;
        private readonly IErpService _erpService;
        private readonly IRevenueQualityService _revenueQualityService;
        private readonly INextBestActionService _nextBestActionService;

        public Customer360Service(
            IUnitOfWork unitOfWork,
            ILocalizationService localizationService,
            IErpService erpService,
            IRevenueQualityService revenueQualityService,
            INextBestActionService nextBestActionService)
        {
            _unitOfWork = unitOfWork;
            _localizationService = localizationService;
            _erpService = erpService;
            _revenueQualityService = revenueQualityService;
            _nextBestActionService = nextBestActionService;
        }

        public async Task<ApiResponse<Customer360OverviewDto>> GetOverviewAsync(long customerId, string? currency = null)
        {
            try
            {
                var customerQuery = _unitOfWork.Customers.Query(tracking: false).Where(c => c.Id == customerId && !c.IsDeleted);
                var customer = await customerQuery.FirstOrDefaultAsync().ConfigureAwait(false);

                if (customer == null)
                {
                    return ApiResponse<Customer360OverviewDto>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var profile = MapProfile(customer);

                var contacts = await GetContactsAsync(customerId).ConfigureAwait(false);
                var shippingAddresses = await GetShippingAddressesAsync(customerId).ConfigureAwait(false);
                var recentDemands = await GetRecentDemandsAsync(customerId).ConfigureAwait(false);
                var recentQuotations = await GetRecentQuotationsAsync(customerId).ConfigureAwait(false);
                var recentOrders = await GetRecentOrdersAsync(customerId).ConfigureAwait(false);
                var recentActivities = await GetRecentActivitiesAsync(customerId).ConfigureAwait(false);

                var totalDemands = await _unitOfWork.Demands.Query(tracking: false)
                    .CountAsync(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed)).ConfigureAwait(false);
                var totalQuotations = await _unitOfWork.Quotations.Query(tracking: false)
                    .CountAsync(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed)).ConfigureAwait(false);
                var totalOrders = await _unitOfWork.Orders.Query(tracking: false)
                    .CountAsync(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed)).ConfigureAwait(false);

                var openQuotations = await _unitOfWork.Quotations.Query(tracking: false)
                    .CountAsync(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                        q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected).ConfigureAwait(false);
                var openOrders = await _unitOfWork.Orders.Query(tracking: false)
                    .CountAsync(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                        o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected).ConfigureAwait(false);

                var lastActivityDate = await GetLastActivityDateAsync(customerId).ConfigureAwait(false);

                var kpis = new Customer360KpiDto
                {
                    TotalDemands = totalDemands,
                    TotalQuotations = totalQuotations,
                    TotalOrders = totalOrders,
                    OpenQuotations = openQuotations,
                    OpenOrders = openOrders,
                    LastActivityDate = lastActivityDate
                };

                var timeline = await BuildTimelineAsync(customerId).ConfigureAwait(false);
                var revenueQuality = await _revenueQualityService.CalculateCustomerRevenueQualityAsync(customerId).ConfigureAwait(false);
                var recommendedActions = await _nextBestActionService.GetCustomerActionsAsync(customerId, revenueQuality).ConfigureAwait(false);

                var overview = new Customer360OverviewDto
                {
                    Profile = profile,
                    Kpis = kpis,
                    Contacts = contacts,
                    ShippingAddresses = shippingAddresses,
                    RecentDemands = recentDemands,
                    RecentQuotations = recentQuotations,
                    RecentOrders = recentOrders,
                    RecentActivities = recentActivities,
                    Timeline = timeline,
                    RevenueQuality = revenueQuality,
                    RecommendedActions = recommendedActions
                };

                return ApiResponse<Customer360OverviewDto>.SuccessResult(
                    overview,
                    _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer360OverviewDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Customer360AnalyticsSummaryDto>> GetAnalyticsSummaryAsync(long customerId, string? currency = null)
        {
            try
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(c => c.Id == customerId && !c.IsDeleted).ConfigureAwait(false);

                if (!customerExists)
                {
                    return ApiResponse<Customer360AnalyticsSummaryDto>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var sinceDate = DateTime.UtcNow.AddMonths(-12);
                var currencyNameMap = await GetCurrencyNameMapAsync().ConfigureAwait(false);
                var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : NormalizeCurrency(currency);
                var currencyFilterValues = normalizedCurrency == null
                    ? null
                    : BuildCurrencyFilterValues(normalizedCurrency, currencyNameMap);

                decimal last12MonthsOrderAmount = 0m;
                decimal openQuotationAmount = 0m;
                decimal openOrderAmount = 0m;

                if (currencyFilterValues != null)
                {
                    last12MonthsOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            (o.OfferDate ?? o.CreatedDate) >= sinceDate &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openQuotationAmount = await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                            q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((q.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(q => (decimal?)q.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;
                }

                var activityCount = await _unitOfWork.Activities.Query(tracking: false)
                    .CountAsync(a => a.PotentialCustomerId == customerId && !a.IsDeleted).ConfigureAwait(false);

                var lastActivityDate = await GetLastActivityDateAsync(customerId).ConfigureAwait(false);

                var totalsByCurrency = await GetTotalsByCurrencyAsync(customerId).ConfigureAwait(false);
                totalsByCurrency = MergeCurrencyAmountRows(totalsByCurrency, currencyNameMap);

                var summary = new Customer360AnalyticsSummaryDto
                {
                    Currency = normalizedCurrency == null ? null : ResolveCurrencyName(normalizedCurrency, currencyNameMap),
                    Last12MonthsOrderAmount = last12MonthsOrderAmount,
                    OpenQuotationAmount = openQuotationAmount,
                    OpenOrderAmount = openOrderAmount,
                    ActivityCount = activityCount,
                    LastActivityDate = lastActivityDate,
                    TotalsByCurrency = totalsByCurrency
                };

                return ApiResponse<Customer360AnalyticsSummaryDto>.SuccessResult(
                    summary,
                    _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer360AnalyticsSummaryDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Customer360AnalyticsChartsDto>> GetAnalyticsChartsAsync(long customerId, int months = 12, string? currency = null)
        {
            try
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(c => c.Id == customerId && !c.IsDeleted).ConfigureAwait(false);

                if (!customerExists)
                {
                    return ApiResponse<Customer360AnalyticsChartsDto>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var safeMonths = months <= 0 ? 12 : Math.Min(months, 36);
                var currencyNameMap = await GetCurrencyNameMapAsync().ConfigureAwait(false);
                var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : NormalizeCurrency(currency);
                var currencyFilterValues = normalizedCurrency == null
                    ? null
                    : BuildCurrencyFilterValues(normalizedCurrency, currencyNameMap);
                var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var startMonth = currentMonth.AddMonths(-(safeMonths - 1));
                var endExclusive = currentMonth.AddMonths(1);
                var monthLabels = Enumerable.Range(0, safeMonths)
                    .Select(i => startMonth.AddMonths(i).ToString("yyyy-MM"))
                    .ToList();

                var demandDates = await _unitOfWork.Demands.Query(tracking: false)
                    .Where(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                    .Select(d => d.OfferDate ?? d.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync().ConfigureAwait(false);

                var quotationDates = await _unitOfWork.Quotations.Query(tracking: false)
                    .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                    .Select(q => q.OfferDate ?? q.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync().ConfigureAwait(false);

                var orderDates = await _unitOfWork.Orders.Query(tracking: false)
                    .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                    .Select(o => o.OfferDate ?? o.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync().ConfigureAwait(false);

                var demandByMonth = demandDates
                    .GroupBy(d => d.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                var quotationByMonth = quotationDates
                    .GroupBy(d => d.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                var orderByMonth = orderDates
                    .GroupBy(d => d.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                var monthlyTrend = monthLabels
                    .Select(label => new Customer360MonthlyTrendItemDto
                    {
                        Month = label,
                        DemandCount = demandByMonth.TryGetValue(label, out var dCount) ? dCount : 0,
                        QuotationCount = quotationByMonth.TryGetValue(label, out var qCount) ? qCount : 0,
                        OrderCount = orderByMonth.TryGetValue(label, out var oCount) ? oCount : 0
                    })
                    .ToList();

                var distribution = new Customer360DistributionDto
                {
                    DemandCount = await _unitOfWork.Demands.Query(tracking: false)
                        .CountAsync(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed)).ConfigureAwait(false),
                    QuotationCount = await _unitOfWork.Quotations.Query(tracking: false)
                        .CountAsync(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed)).ConfigureAwait(false),
                    OrderCount = await _unitOfWork.Orders.Query(tracking: false)
                        .CountAsync(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed)).ConfigureAwait(false)
                };

                var last12MonthsStart = currentMonth.AddMonths(-11);

                decimal last12MonthsOrderAmount = 0m;
                decimal openQuotationAmount = 0m;
                decimal openOrderAmount = 0m;

                if (currencyFilterValues != null)
                {
                    last12MonthsOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            (o.OfferDate ?? o.CreatedDate) >= last12MonthsStart &&
                            (o.OfferDate ?? o.CreatedDate) < endExclusive &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openQuotationAmount = await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                            q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((q.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(q => (decimal?)q.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;
                }

                var amountComparison = new Customer360AmountComparisonDto
                {
                    Currency = normalizedCurrency == null ? null : ResolveCurrencyName(normalizedCurrency, currencyNameMap),
                    Last12MonthsOrderAmount = last12MonthsOrderAmount,
                    OpenQuotationAmount = openQuotationAmount,
                    OpenOrderAmount = openOrderAmount
                };

                var amountComparisonByCurrency = await GetAmountComparisonByCurrencyAsync(customerId, last12MonthsStart, endExclusive).ConfigureAwait(false);
                amountComparisonByCurrency = MergeAmountComparisonRows(amountComparisonByCurrency, currencyNameMap);

                var charts = new Customer360AnalyticsChartsDto
                {
                    MonthlyTrend = monthlyTrend,
                    Distribution = distribution,
                    AmountComparison = amountComparison,
                    AmountComparisonByCurrency = amountComparisonByCurrency
                };

                return ApiResponse<Customer360AnalyticsChartsDto>.SuccessResult(
                    charts,
                    _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer360AnalyticsChartsDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<CohortRetentionDto>>> GetCohortRetentionAsync(long customerId, int months = 12)
        {
            try
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(c => c.Id == customerId && !c.IsDeleted).ConfigureAwait(false);

                if (!customerExists)
                {
                    return ApiResponse<List<CohortRetentionDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var safeMonths = months <= 0 ? 12 : Math.Min(months, 24);
                var orderDates = await _unitOfWork.Orders.Query(tracking: false)
                    .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                    .Select(o => o.OfferDate ?? o.CreatedDate)
                    .OrderBy(x => x)
                    .ToListAsync().ConfigureAwait(false);

                if (orderDates.Count == 0)
                {
                    return ApiResponse<List<CohortRetentionDto>>.SuccessResult(new List<CohortRetentionDto>(), _localizationService.GetLocalizedString("General.OperationSuccessful"));
                }

                var cohortStart = new DateTime(orderDates.Min().Year, orderDates.Min().Month, 1);
                var activeMonthSet = orderDates
                    .Select(x => new DateTime(x.Year, x.Month, 1).ToString("yyyy-MM"))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var cohort = new CohortRetentionDto
                {
                    CohortKey = cohortStart.ToString("yyyy-MM"),
                    CohortSize = 1
                };

                for (var period = 0; period < safeMonths; period++)
                {
                    var month = cohortStart.AddMonths(period).ToString("yyyy-MM");
                    var retained = activeMonthSet.Contains(month) ? 1 : 0;

                    cohort.Points.Add(new CohortRetentionPointDto
                    {
                        PeriodIndex = period,
                        PeriodMonth = month,
                        RetainedCount = retained,
                        RetentionRate = retained == 1 ? 100m : 0m
                    });
                }

                return ApiResponse<List<CohortRetentionDto>>.SuccessResult(
                    new List<CohortRetentionDto> { cohort },
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CohortRetentionDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<Customer360QuickQuotationDto>>> GetQuickQuotationsAsync(long customerId)
        {
            try
            {
                var customerExists = await _unitOfWork.Customers.Query(tracking: false)
                    .AnyAsync(c => c.Id == customerId && !c.IsDeleted).ConfigureAwait(false);

                if (!customerExists)
                {
                    return ApiResponse<List<Customer360QuickQuotationDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var tempHeaders = await _unitOfWork.TempQuotattions.Query(tracking: false)
                    .Where(x => x.CustomerId == customerId && !x.IsDeleted)
                    .Select(x => new
                    {
                        x.Id,
                        x.OfferDate,
                        x.CurrencyCode,
                        x.Description,
                        x.IsApproved,
                        x.ApprovedDate,
                        x.QuotationId,
                        x.QuotationNo
                    })
                    .OrderByDescending(x => x.OfferDate)
                    .Take(100)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (tempHeaders.Count == 0)
                {
                    return ApiResponse<List<Customer360QuickQuotationDto>>.SuccessResult(
                        new List<Customer360QuickQuotationDto>(),
                        _localizationService.GetLocalizedString("General.OperationSuccessful"));
                }

                var tempIds = tempHeaders.Select(x => x.Id).ToList();
                var tempTotals = await _unitOfWork.TempQuotattionLines.Query(tracking: false)
                    .Where(x => tempIds.Contains(x.TempQuotattionId) && !x.IsDeleted)
                    .GroupBy(x => x.TempQuotattionId)
                    .Select(g => new
                    {
                        TempQuotationId = g.Key,
                        TotalAmount = g.Sum(x => x.LineGrandTotal)
                    })
                    .ToDictionaryAsync(x => x.TempQuotationId, x => x.TotalAmount)
                    .ConfigureAwait(false);

                var quotationIds = tempHeaders
                    .Where(x => x.QuotationId.HasValue)
                    .Select(x => x.QuotationId!.Value)
                    .Distinct()
                    .ToList();

                var quotationInfo = quotationIds.Count == 0
                    ? new Dictionary<long, (ApprovalStatus? Status, string? OfferNo)>()
                    : await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(x => quotationIds.Contains(x.Id) && !x.IsDeleted)
                        .Select(x => new
                        {
                            x.Id,
                            x.Status,
                            OfferNo = x.OfferNo ?? x.RevisionNo
                        })
                        .ToDictionaryAsync(
                            x => x.Id,
                            x => (x.Status, x.OfferNo))
                        .ConfigureAwait(false);

                var approvalInfo = quotationIds.Count == 0
                    ? new Dictionary<long, (ApprovalStatus Status, int CurrentStep, string? FlowDescription)>()
                    : await _unitOfWork.ApprovalRequests.Query(tracking: false)
                        .Include(x => x.ApprovalFlow)
                        .Where(x =>
                            quotationIds.Contains(x.EntityId) &&
                            x.DocumentType == PricingRuleType.Quotation &&
                            !x.IsDeleted)
                        .ToDictionaryAsync(
                            x => x.EntityId,
                            x => (
                                Status: x.Status,
                                CurrentStep: x.CurrentStep,
                                FlowDescription: x.ApprovalFlow != null ? x.ApprovalFlow.Description : null))
                        .ConfigureAwait(false);

                var rows = tempHeaders.Select(x =>
                {
                    quotationInfo.TryGetValue(x.QuotationId ?? 0, out var quotation);
                    approvalInfo.TryGetValue(x.QuotationId ?? 0, out var approval);

                    return new Customer360QuickQuotationDto
                    {
                        Id = x.Id,
                        OfferDate = x.OfferDate,
                        CurrencyCode = x.CurrencyCode,
                        TotalAmount = tempTotals.TryGetValue(x.Id, out var total) ? total : 0m,
                        Description = x.Description,
                        IsApproved = x.IsApproved,
                        ApprovedDate = x.ApprovedDate,
                        QuotationId = x.QuotationId,
                        QuotationNo = x.QuotationNo ?? quotation.OfferNo,
                        HasConvertedQuotation = x.QuotationId.HasValue,
                        QuotationStatus = quotation.Status.HasValue ? (int)quotation.Status.Value : null,
                        QuotationStatusName = quotation.Status.HasValue ? GetApprovalStatusName(quotation.Status.Value) : null,
                        HasApprovalRequest = x.QuotationId.HasValue && approvalInfo.ContainsKey(x.QuotationId.Value),
                        ApprovalStatus = x.QuotationId.HasValue && approvalInfo.ContainsKey(x.QuotationId.Value)
                            ? (int)approval.Status
                            : null,
                        ApprovalStatusName = x.QuotationId.HasValue && approvalInfo.ContainsKey(x.QuotationId.Value)
                            ? GetApprovalStatusName(approval.Status)
                            : null,
                        ApprovalCurrentStep = x.QuotationId.HasValue && approvalInfo.ContainsKey(x.QuotationId.Value)
                            ? approval.CurrentStep
                            : null,
                        ApprovalFlowDescription = x.QuotationId.HasValue && approvalInfo.ContainsKey(x.QuotationId.Value)
                            ? approval.FlowDescription
                            : null
                    };
                }).ToList();

                return ApiResponse<List<Customer360QuickQuotationDto>>.SuccessResult(
                    rows,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Customer360QuickQuotationDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<Customer360ErpMovementDto>>> GetErpMovementsAsync(long customerId)
        {
            try
            {
                var customer = await _unitOfWork.Customers.Query(tracking: false)
                    .Where(c => c.Id == customerId && !c.IsDeleted)
                    .Select(c => new { c.CustomerCode })
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (customer == null)
                {
                    return ApiResponse<List<Customer360ErpMovementDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    return ApiResponse<List<Customer360ErpMovementDto>>.SuccessResult(
                        new List<Customer360ErpMovementDto>(),
                        _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
                }

                var erpResult = await _erpService.GetCariMovementsAsync(customer.CustomerCode).ConfigureAwait(false);
                if (!erpResult.Success || erpResult.Data == null)
                {
                    return ApiResponse<List<Customer360ErpMovementDto>>.ErrorResult(
                        erpResult.Message ?? _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                        erpResult.ExceptionMessage ?? erpResult.Message ?? _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                        erpResult.StatusCode);
                }

                var rows = erpResult.Data
                    .OrderByDescending(x => x.Tarih ?? DateTime.MinValue)
                    .ThenByDescending(x => x.VadeTarihi ?? DateTime.MinValue)
                    .Select(x => new Customer360ErpMovementDto
                    {
                        CariKod = x.CariKod,
                        Tarih = x.Tarih,
                        VadeTarihi = x.VadeTarihi,
                        BelgeNo = x.BelgeNo,
                        Aciklama = x.Aciklama,
                        DovizTuru = x.DovizTuru,
                        ParaBirimi = x.ParaBirimi,
                        Borc = x.Borc,
                        Alacak = x.Alacak,
                        TarihSiraliTlBakiye = x.TarihSiraliTlBakiye,
                        VadeSiraliTlBakiye = x.VadeSiraliTlBakiye,
                        DovizBorc = x.DovizBorc,
                        DovizAlacak = x.DovizAlacak,
                        TarihSiraliDovizBakiye = x.TarihSiraliDovizBakiye,
                        VadeSiraliDovizBakiye = x.VadeSiraliDovizBakiye
                    })
                    .ToList();

                return ApiResponse<List<Customer360ErpMovementDto>>.SuccessResult(
                    rows,
                    _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<Customer360ErpMovementDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Customer360ErpBalanceDto>> GetErpBalanceAsync(long customerId)
        {
            try
            {
                var movementsResult = await GetErpMovementsAsync(customerId).ConfigureAwait(false);
                if (!movementsResult.Success || movementsResult.Data == null)
                {
                    return ApiResponse<Customer360ErpBalanceDto>.ErrorResult(
                        movementsResult.Message ?? _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                        movementsResult.ExceptionMessage ?? movementsResult.Message ?? _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                        movementsResult.StatusCode);
                }

                var movements = movementsResult.Data;
                var totalBorc = movements.Sum(x => x.Borc);
                var totalAlacak = movements.Sum(x => x.Alacak);
                var lastBalance = movements
                    .OrderByDescending(x => x.Tarih ?? DateTime.MinValue)
                    .ThenByDescending(x => x.VadeTarihi ?? DateTime.MinValue)
                    .Select(x => x.TarihSiraliTlBakiye)
                    .FirstOrDefault();
                var netBakiye = movements.Count > 0 ? lastBalance : totalBorc - totalAlacak;
                var bakiyeDurumu = netBakiye > 0 ? "Borç" : netBakiye < 0 ? "Alacak" : "Kapalı";

                var summary = new Customer360ErpBalanceDto
                {
                    CariKod = movements.FirstOrDefault()?.CariKod ?? string.Empty,
                    NetBakiye = netBakiye,
                    BakiyeDurumu = bakiyeDurumu,
                    BakiyeTutari = Math.Abs(netBakiye),
                    ToplamBorc = totalBorc,
                    ToplamAlacak = totalAlacak
                };

                return ApiResponse<Customer360ErpBalanceDto>.SuccessResult(
                    summary,
                    _localizationService.GetLocalizedString("Customer360Service.OverviewRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Customer360ErpBalanceDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityDto>> ExecuteRecommendedActionAsync(long customerId, ExecuteRecommendedActionDto request)
        {
            try
            {
                var customer = await _unitOfWork.Customers.Query(tracking: false)
                    .FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted).ConfigureAwait(false);

                if (customer == null)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        _localizationService.GetLocalizedString("Customer360Service.CustomerNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var actionCode = (request.ActionCode ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(actionCode))
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                if (!NbaActionCatalog.TryGet(actionCode, out var template) ||
                    !string.Equals(template.TargetEntityType, "Customer", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        $"Unsupported action code for Customer360: {actionCode}",
                        StatusCodes.Status400BadRequest);
                }

                var dueInDays = request.DueInDays.GetValueOrDefault(template.DefaultDueInDays);
                if (dueInDays < 0) dueInDays = 0;
                if (dueInDays > 30) dueInDays = 30;

                var subject = string.IsNullOrWhiteSpace(request.Title)
                    ? $"[{actionCode}] {template.Title} - {customer.CustomerName}"
                    : request.Title.Trim();

                var description = request.Reason ?? $"Auto-created from Customer 360 recommended action: {actionCode}.";
                var priority = string.IsNullOrWhiteSpace(request.Priority) ? template.DefaultPriority : request.Priority.Trim();
                if (!request.AssignedUserId.HasValue)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                var activityTypeId = await GetDefaultActivityTypeIdAsync().ConfigureAwait(false);
                if (!activityTypeId.HasValue)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                var activity = new Activity
                {
                    Subject = subject,
                    Description = description,
                    ActivityTypeId = activityTypeId.Value,
                    PotentialCustomerId = customerId,
                    ErpCustomerCode = customer.CustomerCode,
                    Status = ActivityStatus.Scheduled,
                    Priority = ParseActivityPriority(priority),
                    AssignedUserId = request.AssignedUserId.Value,
                    StartDateTime = DateTime.UtcNow.AddDays(dueInDays)
                };

                await _unitOfWork.Activities.AddAsync(activity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var entity = await _unitOfWork.Activities.Query(tracking: false)
                    .Include(a => a.CreatedByUser)
                    .Include(a => a.UpdatedByUser)
                    .Include(a => a.DeletedByUser)
                    .FirstOrDefaultAsync(a => a.Id == activity.Id).ConfigureAwait(false) ?? activity;

                var dto = new ActivityDto
                {
                    Id = entity.Id,
                    CreatedDate = entity.CreatedDate,
                    UpdatedDate = entity.UpdatedDate,
                    DeletedDate = entity.DeletedDate,
                    IsDeleted = entity.IsDeleted,
                    CreatedByFullUser = entity.CreatedByUser?.FullName,
                    UpdatedByFullUser = entity.UpdatedByUser?.FullName,
                    DeletedByFullUser = entity.DeletedByUser?.FullName,
                    Subject = entity.Subject,
                    Description = entity.Description,
                    ActivityTypeId = entity.ActivityTypeId,
                    PotentialCustomerId = entity.PotentialCustomerId,
                    ErpCustomerCode = entity.ErpCustomerCode,
                    Status = entity.Status,
                    Priority = entity.Priority,
                    ContactId = entity.ContactId,
                    AssignedUserId = entity.AssignedUserId,
                    StartDateTime = entity.StartDateTime,
                    EndDateTime = entity.EndDateTime,
                    IsAllDay = entity.IsAllDay,
                    Reminders = new List<ActivityReminderDto>()
                };

                return ApiResponse<ActivityDto>.SuccessResult(dto, _localizationService.GetLocalizedString("ActivityService.ActivityCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityDto>.ErrorResult(
                    _localizationService.GetLocalizedString("Customer360Service.InternalServerError"),
                    _localizationService.GetLocalizedString("Customer360Service.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        private static Customer360ProfileDto MapProfile(Customer c)
        {
            return new Customer360ProfileDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                Name = c.CustomerName,
                TaxNumber = c.TaxNumber,
                TaxOffice = c.TaxOffice,
                TcknNumber = c.TcknNumber,
                Email = c.Email,
                Phone = c.Phone1,
                Phone2 = c.Phone2,
                Website = c.Website,
                Address = c.Address,
                SalesRepCode = c.SalesRepCode,
                GroupCode = c.GroupCode,
                CreditLimit = c.CreditLimit,
                IsERPIntegrated = c.IsERPIntegrated,
                LastSyncDate = c.LastSyncDate
            };
        }

        private static string NormalizeCurrency(string? currency)
        {
            return string.IsNullOrWhiteSpace(currency) ? "UNKNOWN" : currency.Trim().ToUpperInvariant();
        }

        private async Task<Dictionary<string, string>> GetCurrencyNameMapAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rates = await _erpService.GetExchangeRateAsync(DateTime.Now, 1).ConfigureAwait(false);

            if (!rates.Success || rates.Data == null)
            {
                return result;
            }

            foreach (var item in rates.Data)
            {
                if (item.DovizTipi <= 0 || string.IsNullOrWhiteSpace(item.DovizIsmi))
                {
                    continue;
                }

                var idKey = item.DovizTipi.ToString();
                var nameValue = NormalizeCurrency(item.DovizIsmi);

                if (!result.ContainsKey(idKey))
                {
                    result[idKey] = nameValue;
                }

                if (!result.ContainsKey(nameValue))
                {
                    result[nameValue] = nameValue;
                }
            }

            return result;
        }

        private static List<string> BuildCurrencyFilterValues(string normalizedCurrency, Dictionary<string, string> currencyNameMap)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedCurrency };

            if (currencyNameMap.TryGetValue(normalizedCurrency, out var mappedName))
            {
                values.Add(mappedName);
            }

            foreach (var item in currencyNameMap.Where(x => string.Equals(x.Value, normalizedCurrency, StringComparison.OrdinalIgnoreCase)))
            {
                values.Add(item.Key);
            }

            return values.Select(NormalizeCurrency).Distinct().ToList();
        }

        private static string ResolveCurrencyName(string? currency, Dictionary<string, string> currencyNameMap)
        {
            var normalized = NormalizeCurrency(currency);
            return currencyNameMap.TryGetValue(normalized, out var mappedName) ? mappedName : normalized;
        }

        private async Task<List<Customer360CurrencyAmountDto>> GetTotalsByCurrencyAsync(long customerId)
        {
            var demandTotals = await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .GroupBy(d => d.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var quotationTotals = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .GroupBy(q => q.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderTotals = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var result = new Dictionary<string, Customer360CurrencyAmountDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in demandTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.DemandAmount = item.Amount;
            }

            foreach (var item in quotationTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.QuotationAmount = item.Amount;
            }

            foreach (var item in orderTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.OrderAmount = item.Amount;
            }

            return result.Values.OrderBy(x => x.Currency).ToList();
        }

        private async Task<List<Customer360AmountComparisonDto>> GetAmountComparisonByCurrencyAsync(
            long customerId,
            DateTime last12MonthsStart,
            DateTime endExclusive)
        {
            var quotationOpenByCurrency = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                    q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected)
                .GroupBy(q => q.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderOpenByCurrency = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                    o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected)
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderLast12ByCurrency = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                    (o.OfferDate ?? o.CreatedDate) >= last12MonthsStart &&
                    (o.OfferDate ?? o.CreatedDate) < endExclusive)
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var result = new Dictionary<string, Customer360AmountComparisonDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in orderLast12ByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.Last12MonthsOrderAmount = item.Amount;
            }

            foreach (var item in quotationOpenByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.OpenQuotationAmount = item.Amount;
            }

            foreach (var item in orderOpenByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Customer360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.OpenOrderAmount = item.Amount;
            }

            return result.Values.OrderBy(x => x.Currency).ToList();
        }

        private static List<Customer360CurrencyAmountDto> MergeCurrencyAmountRows(
            List<Customer360CurrencyAmountDto> rows,
            Dictionary<string, string> currencyNameMap)
        {
            return rows
                .GroupBy(x => ResolveCurrencyName(x.Currency, currencyNameMap), StringComparer.OrdinalIgnoreCase)
                .Select(g => new Customer360CurrencyAmountDto
                {
                    Currency = g.Key,
                    DemandAmount = g.Sum(x => x.DemandAmount),
                    QuotationAmount = g.Sum(x => x.QuotationAmount),
                    OrderAmount = g.Sum(x => x.OrderAmount)
                })
                .OrderBy(x => x.Currency)
                .ToList();
        }

        private static List<Customer360AmountComparisonDto> MergeAmountComparisonRows(
            List<Customer360AmountComparisonDto> rows,
            Dictionary<string, string> currencyNameMap)
        {
            return rows
                .GroupBy(x => ResolveCurrencyName(x.Currency, currencyNameMap), StringComparer.OrdinalIgnoreCase)
                .Select(g => new Customer360AmountComparisonDto
                {
                    Currency = g.Key,
                    Last12MonthsOrderAmount = g.Sum(x => x.Last12MonthsOrderAmount),
                    OpenQuotationAmount = g.Sum(x => x.OpenQuotationAmount),
                    OpenOrderAmount = g.Sum(x => x.OpenOrderAmount)
                })
                .OrderBy(x => x.Currency)
                .ToList();
        }

        private async Task<List<Customer360SimpleItemDto>> GetContactsAsync(long customerId)
        {
            return await _unitOfWork.Contacts.Query(tracking: false)
                .Where(c => c.CustomerId == customerId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedDate)
                .Take(10)
                .Select(c => new Customer360SimpleItemDto
                {
                    Id = c.Id,
                    Title = c.FullName,
                    Subtitle = c.Email ?? c.Phone,
                    Status = null,
                    Amount = null,
                    Date = c.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<List<Customer360SimpleItemDto>> GetShippingAddressesAsync(long customerId)
        {
            return await _unitOfWork.ShippingAddresses.Query(tracking: false)
                .Where(s => s.CustomerId == customerId && !s.IsDeleted)
                .OrderByDescending(s => s.CreatedDate)
                .Take(10)
                .Select(s => new Customer360SimpleItemDto
                {
                    Id = s.Id,
                    Title = s.Address,
                    Subtitle = s.ContactPerson ?? s.Phone,
                    Status = null,
                    Amount = null,
                    Date = s.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<List<Customer360SimpleItemDto>> GetRecentDemandsAsync(long customerId)
        {
            return await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .OrderByDescending(d => d.OfferDate ?? d.CreatedDate)
                .Take(10)
                .Select(d => new Customer360SimpleItemDto
                {
                    Id = d.Id,
                    Title = d.OfferNo ?? d.Id.ToString(),
                    Subtitle = d.Description,
                    Status = d.Status.HasValue ? d.Status.ToString() : null,
                    Amount = d.GrandTotal,
                    Date = d.OfferDate ?? d.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<List<Customer360SimpleItemDto>> GetRecentQuotationsAsync(long customerId)
        {
            return await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .OrderByDescending(q => q.OfferDate ?? q.CreatedDate)
                .Take(10)
                .Select(q => new Customer360SimpleItemDto
                {
                    Id = q.Id,
                    Title = q.OfferNo ?? q.Id.ToString(),
                    Subtitle = q.Description,
                    Status = q.Status.HasValue ? q.Status.ToString() : null,
                    Amount = q.GrandTotal,
                    Date = q.OfferDate ?? q.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<List<Customer360SimpleItemDto>> GetRecentOrdersAsync(long customerId)
        {
            return await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .OrderByDescending(o => o.OfferDate ?? o.CreatedDate)
                .Take(10)
                .Select(o => new Customer360SimpleItemDto
                {
                    Id = o.Id,
                    Title = o.OfferNo ?? o.Id.ToString(),
                    Subtitle = o.Description,
                    Status = o.Status.HasValue ? o.Status.ToString() : null,
                    Amount = o.GrandTotal,
                    Date = o.OfferDate ?? o.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<List<Customer360SimpleItemDto>> GetRecentActivitiesAsync(long customerId)
        {
            return await _unitOfWork.Activities.Query(tracking: false)
                .Where(a => a.PotentialCustomerId == customerId && !a.IsDeleted)
                .OrderByDescending(a => a.StartDateTime)
                .Take(10)
                .Select(a => new Customer360SimpleItemDto
                {
                    Id = a.Id,
                    Title = a.Subject,
                    Subtitle = a.Description,
                    Status = a.Status.ToString(),
                    Amount = null,
                    Date = a.StartDateTime
                })
                .ToListAsync().ConfigureAwait(false);
        }

        private async Task<DateTime?> GetLastActivityDateAsync(long customerId)
        {
            var demandDate = await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .Select(d => (DateTime?)(d.OfferDate ?? d.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var quotationDate = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .Select(q => (DateTime?)(q.OfferDate ?? q.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var orderDate = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .Select(o => (DateTime?)(o.OfferDate ?? o.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var activityDate = await _unitOfWork.Activities.Query(tracking: false)
                .Where(a => a.PotentialCustomerId == customerId && !a.IsDeleted)
                .Select(a => (DateTime?)a.StartDateTime)
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var dates = new[] { demandDate, quotationDate, orderDate, activityDate }.Where(d => d.HasValue).Select(d => d!.Value).ToList();
            return dates.Count > 0 ? dates.Max() : null;
        }

        private async Task<List<Customer360TimelineItemDto>> BuildTimelineAsync(long customerId)
        {
            var demands = await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.PotentialCustomerId == customerId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .Select(d => new Customer360TimelineItemDto
                {
                    Type = "Demand",
                    ItemId = d.Id,
                    Title = d.OfferNo ?? d.Id.ToString(),
                    Status = d.Status.HasValue ? d.Status.ToString() : null,
                    Amount = d.GrandTotal,
                    Date = d.OfferDate ?? d.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);

            var quotations = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.PotentialCustomerId == customerId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .Select(q => new Customer360TimelineItemDto
                {
                    Type = "Quotation",
                    ItemId = q.Id,
                    Title = q.OfferNo ?? q.Id.ToString(),
                    Status = q.Status.HasValue ? q.Status.ToString() : null,
                    Amount = q.GrandTotal,
                    Date = q.OfferDate ?? q.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);

            var orders = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.PotentialCustomerId == customerId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .Select(o => new Customer360TimelineItemDto
                {
                    Type = "Order",
                    ItemId = o.Id,
                    Title = o.OfferNo ?? o.Id.ToString(),
                    Status = o.Status.HasValue ? o.Status.ToString() : null,
                    Amount = o.GrandTotal,
                    Date = o.OfferDate ?? o.CreatedDate
                })
                .ToListAsync().ConfigureAwait(false);

            var activities = await _unitOfWork.Activities.Query(tracking: false)
                .Where(a => a.PotentialCustomerId == customerId && !a.IsDeleted)
                .Select(a => new Customer360TimelineItemDto
                {
                    Type = "Activity",
                    ItemId = a.Id,
                    Title = a.Subject,
                    Status = a.Status.ToString(),
                    Amount = null,
                    Date = a.StartDateTime
                })
                .ToListAsync().ConfigureAwait(false);

            var timeline = demands.Concat(quotations).Concat(orders).Concat(activities)
                .OrderByDescending(t => t.Date)
                .Take(50)
                .ToList();

            return timeline;
        }

        private async Task<long?> GetDefaultActivityTypeIdAsync()
        {
            return await _unitOfWork.ActivityTypes.Query(tracking: false)
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }

        private static ActivityPriority ParseActivityPriority(string? priority)
        {
            if (string.IsNullOrWhiteSpace(priority))
            {
                return ActivityPriority.Medium;
            }

            return priority.Trim().ToLowerInvariant() switch
            {
                "low" => ActivityPriority.Low,
                "high" => ActivityPriority.High,
                _ => ActivityPriority.Medium
            };
        }

        private static string GetApprovalStatusName(ApprovalStatus status)
        {
            return status switch
            {
                ApprovalStatus.HavenotStarted => "Başlamadı",
                ApprovalStatus.Waiting => "Beklemede",
                ApprovalStatus.Approved => "Onaylandı",
                ApprovalStatus.Rejected => "Reddedildi",
                ApprovalStatus.Closed => "Kapandı",
                _ => status.ToString()
            };
        }
    }
}
