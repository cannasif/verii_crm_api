using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class Salesmen360Service : ISalesmen360Service
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILocalizationService _localizationService;
        private readonly IErpService _erpService;
        private readonly IRevenueQualityService _revenueQualityService;
        private readonly INextBestActionService _nextBestActionService;

        public Salesmen360Service(
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

        public async Task<ApiResponse<Salesmen360OverviewDto>> GetOverviewAsync(long userId, string? currency = null)
        {
            try
            {
                var user = await GetUserAsync(userId).ConfigureAwait(false);

                if (user == null)
                {
                    return ApiResponse<Salesmen360OverviewDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var totalDemands = await _unitOfWork.Demands.Query(tracking: false)
                    .CountAsync(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed)).ConfigureAwait(false);

                var totalQuotations = await _unitOfWork.Quotations.Query(tracking: false)
                    .CountAsync(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed)).ConfigureAwait(false);

                var totalOrders = await _unitOfWork.Orders.Query(tracking: false)
                    .CountAsync(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed)).ConfigureAwait(false);

                var totalActivities = await _unitOfWork.Activities.Query(tracking: false)
                    .CountAsync(a => a.AssignedUserId == userId && !a.IsDeleted).ConfigureAwait(false);

                var currencyNameMap = await GetCurrencyNameMapAsync().ConfigureAwait(false);
                var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : NormalizeCurrency(currency);
                var currencyFilterValues = normalizedCurrency == null
                    ? null
                    : BuildCurrencyFilterValues(normalizedCurrency, currencyNameMap);
                decimal totalDemandAmount = 0m;
                decimal totalQuotationAmount = 0m;
                decimal totalOrderAmount = 0m;

                if (currencyFilterValues != null)
                {
                    totalDemandAmount = await _unitOfWork.Demands.Query(tracking: false)
                        .Where(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed) &&
                            currencyFilterValues.Contains((d.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(d => (decimal?)d.GrandTotal).ConfigureAwait(false) ?? 0m;

                    totalQuotationAmount = await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                            currencyFilterValues.Contains((q.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(q => (decimal?)q.GrandTotal).ConfigureAwait(false) ?? 0m;

                    totalOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;
                }

                var totalsByCurrency = await GetTotalsByCurrencyAsync(userId).ConfigureAwait(false);
                totalsByCurrency = MergeCurrencyAmountRows(totalsByCurrency, currencyNameMap);

                var response = new Salesmen360OverviewDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Kpis = new Salesmen360KpiDto
                    {
                        Currency = normalizedCurrency == null ? null : ResolveCurrencyName(normalizedCurrency, currencyNameMap),
                        TotalDemands = totalDemands,
                        TotalQuotations = totalQuotations,
                        TotalOrders = totalOrders,
                        TotalActivities = totalActivities,
                        TotalDemandAmount = totalDemandAmount,
                        TotalQuotationAmount = totalQuotationAmount,
                        TotalOrderAmount = totalOrderAmount,
                        TotalsByCurrency = totalsByCurrency
                    },
                    RevenueQuality = await _revenueQualityService.CalculateSalesmanRevenueQualityAsync(userId).ConfigureAwait(false),
                };
                response.RecommendedActions = await _nextBestActionService.GetSalesmanActionsAsync(userId, response.RevenueQuality).ConfigureAwait(false);

                return ApiResponse<Salesmen360OverviewDto>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Salesmen360OverviewDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Salesmen360AnalyticsSummaryDto>> GetAnalyticsSummaryAsync(long userId, string? currency = null)
        {
            try
            {
                var user = await GetUserAsync(userId).ConfigureAwait(false);

                if (user == null)
                {
                    return ApiResponse<Salesmen360AnalyticsSummaryDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
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
                        .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            (o.OfferDate ?? o.CreatedDate) >= sinceDate &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openQuotationAmount = await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                            q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((q.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(q => (decimal?)q.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;
                }

                var activityCount = await _unitOfWork.Activities.Query(tracking: false)
                    .CountAsync(a => a.AssignedUserId == userId && !a.IsDeleted).ConfigureAwait(false);
                var totalsByCurrency = await GetTotalsByCurrencyAsync(userId).ConfigureAwait(false);
                totalsByCurrency = MergeCurrencyAmountRows(totalsByCurrency, currencyNameMap);

                var summary = new Salesmen360AnalyticsSummaryDto
                {
                    Currency = normalizedCurrency == null ? null : ResolveCurrencyName(normalizedCurrency, currencyNameMap),
                    Last12MonthsOrderAmount = last12MonthsOrderAmount,
                    OpenQuotationAmount = openQuotationAmount,
                    OpenOrderAmount = openOrderAmount,
                    ActivityCount = activityCount,
                    LastActivityDate = await GetLastActivityDateAsync(userId).ConfigureAwait(false),
                    TotalsByCurrency = totalsByCurrency
                };

                return ApiResponse<Salesmen360AnalyticsSummaryDto>.SuccessResult(
                    summary,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Salesmen360AnalyticsSummaryDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<Salesmen360AnalyticsChartsDto>> GetAnalyticsChartsAsync(long userId, int months = 12, string? currency = null)
        {
            try
            {
                var user = await GetUserAsync(userId).ConfigureAwait(false);

                if (user == null)
                {
                    return ApiResponse<Salesmen360AnalyticsChartsDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
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
                    .Where(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                    .Select(d => d.OfferDate ?? d.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync();

                var quotationDates = await _unitOfWork.Quotations.Query(tracking: false)
                    .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                    .Select(q => q.OfferDate ?? q.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync();

                var orderDates = await _unitOfWork.Orders.Query(tracking: false)
                    .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                    .Select(o => o.OfferDate ?? o.CreatedDate)
                    .Where(d => d >= startMonth && d < endExclusive)
                    .ToListAsync();

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
                    .Select(label => new Salesmen360MonthlyTrendItemDto
                    {
                        Month = label,
                        DemandCount = demandByMonth.TryGetValue(label, out var dCount) ? dCount : 0,
                        QuotationCount = quotationByMonth.TryGetValue(label, out var qCount) ? qCount : 0,
                        OrderCount = orderByMonth.TryGetValue(label, out var oCount) ? oCount : 0
                    })
                    .ToList();

                var distribution = new Salesmen360DistributionDto
                {
                    DemandCount = await _unitOfWork.Demands.Query(tracking: false)
                        .CountAsync(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed)).ConfigureAwait(false),
                    QuotationCount = await _unitOfWork.Quotations.Query(tracking: false)
                        .CountAsync(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed)).ConfigureAwait(false),
                    OrderCount = await _unitOfWork.Orders.Query(tracking: false)
                        .CountAsync(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed)).ConfigureAwait(false)
                };

                var last12MonthsStart = currentMonth.AddMonths(-11);

                decimal last12OrderAmount = 0m;
                decimal openQuotationAmount = 0m;
                decimal openOrderAmount = 0m;

                if (currencyFilterValues != null)
                {
                    last12OrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            (o.OfferDate ?? o.CreatedDate) >= last12MonthsStart &&
                            (o.OfferDate ?? o.CreatedDate) < endExclusive &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openQuotationAmount = await _unitOfWork.Quotations.Query(tracking: false)
                        .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                            q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((q.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(q => (decimal?)q.GrandTotal).ConfigureAwait(false) ?? 0m;

                    openOrderAmount = await _unitOfWork.Orders.Query(tracking: false)
                        .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected &&
                            currencyFilterValues.Contains((o.Currency ?? "UNKNOWN").ToUpper()))
                        .SumAsync(o => (decimal?)o.GrandTotal).ConfigureAwait(false) ?? 0m;
                }

                var amountComparison = new Salesmen360AmountComparisonDto
                {
                    Currency = normalizedCurrency == null ? null : ResolveCurrencyName(normalizedCurrency, currencyNameMap),
                    Last12MonthsOrderAmount = last12OrderAmount,
                    OpenQuotationAmount = openQuotationAmount,
                    OpenOrderAmount = openOrderAmount
                };

                var amountComparisonByCurrency = await GetAmountComparisonByCurrencyAsync(userId, last12MonthsStart, endExclusive).ConfigureAwait(false);
                amountComparisonByCurrency = MergeAmountComparisonRows(amountComparisonByCurrency, currencyNameMap);

                var charts = new Salesmen360AnalyticsChartsDto
                {
                    MonthlyTrend = monthlyTrend,
                    Distribution = distribution,
                    AmountComparison = amountComparison,
                    AmountComparisonByCurrency = amountComparisonByCurrency
                };

                return ApiResponse<Salesmen360AnalyticsChartsDto>.SuccessResult(
                    charts,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<Salesmen360AnalyticsChartsDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<CohortRetentionDto>>> GetCohortRetentionAsync(long userId, int months = 12)
        {
            try
            {
                var user = await GetUserAsync(userId).ConfigureAwait(false);
                if (user == null)
                {
                    return ApiResponse<List<CohortRetentionDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var safeMonths = months <= 0 ? 12 : Math.Min(months, 24);

                var orderEntries = await _unitOfWork.Orders.Query(tracking: false)
                    .Where(o => o.RepresentativeId == userId &&
                                o.PotentialCustomerId.HasValue &&
                                !o.IsDeleted &&
                                (o.Status == null || o.Status != ApprovalStatus.Closed))
                    .Select(o => new
                    {
                        CustomerId = o.PotentialCustomerId!.Value,
                        OrderMonth = new DateTime((o.OfferDate ?? o.CreatedDate).Year, (o.OfferDate ?? o.CreatedDate).Month, 1)
                    })
                    .ToListAsync().ConfigureAwait(false);

                if (orderEntries.Count == 0)
                {
                    return ApiResponse<List<CohortRetentionDto>>.SuccessResult(new List<CohortRetentionDto>(), _localizationService.GetLocalizedString("General.OperationSuccessful"));
                }

                var firstOrderByCustomer = orderEntries
                    .GroupBy(x => x.CustomerId)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.OrderMonth));

                var monthsByCustomer = orderEntries
                    .GroupBy(x => x.CustomerId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.OrderMonth.ToString("yyyy-MM")).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase));

                var customersByCohort = firstOrderByCustomer
                    .GroupBy(x => x.Value.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

                var cohorts = new List<CohortRetentionDto>();

                foreach (var cohortItem in customersByCohort.OrderBy(x => x.Key))
                {
                    var cohortCustomerIds = cohortItem.Value;
                    var cohortSize = cohortCustomerIds.Count;

                    var cohort = new CohortRetentionDto
                    {
                        CohortKey = cohortItem.Key,
                        CohortSize = cohortSize
                    };

                    var cohortStart = DateTime.Parse($"{cohortItem.Key}-01");
                    for (var period = 0; period < safeMonths; period++)
                    {
                        var monthKey = cohortStart.AddMonths(period).ToString("yyyy-MM");
                        var retainedCount = cohortCustomerIds.Count(customerId => monthsByCustomer[customerId].Contains(monthKey));
                        var retentionRate = cohortSize == 0 ? 0m : Math.Round((decimal)retainedCount / cohortSize * 100m, 2);

                        cohort.Points.Add(new CohortRetentionPointDto
                        {
                            PeriodIndex = period,
                            PeriodMonth = monthKey,
                            RetainedCount = retainedCount,
                            RetentionRate = retentionRate
                        });
                    }

                    cohorts.Add(cohort);
                }

                return ApiResponse<List<CohortRetentionDto>>.SuccessResult(cohorts, _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CohortRetentionDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityDto>> ExecuteRecommendedActionAsync(long userId, ExecuteRecommendedActionDto request)
        {
            try
            {
                var user = await GetUserAsync(userId).ConfigureAwait(false);
                if (user == null)
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
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
                    !string.Equals(template.TargetEntityType, "User", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<ActivityDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        $"Unsupported action code for Salesmen360: {actionCode}",
                        StatusCodes.Status400BadRequest);
                }

                var dueInDays = request.DueInDays.GetValueOrDefault(template.DefaultDueInDays);
                if (dueInDays < 0) dueInDays = 0;
                if (dueInDays > 30) dueInDays = 30;

                var subject = string.IsNullOrWhiteSpace(request.Title)
                    ? $"[{actionCode}] {template.Title} - {user.FullName}"
                    : request.Title.Trim();

                var description = request.Reason ?? $"Auto-created from Salesmen 360 recommended action: {actionCode}.";
                var priority = string.IsNullOrWhiteSpace(request.Priority) ? template.DefaultPriority : request.Priority.Trim();
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
                    Status = ActivityStatus.Scheduled,
                    Priority = ParseActivityPriority(priority),
                    AssignedUserId = request.AssignedUserId ?? userId,
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
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<User?> GetUserAsync(long userId)
        {
            return await _unitOfWork.Users.Query(tracking: false)
                .Where(u => u.Id == userId && !u.IsDeleted)
                .FirstOrDefaultAsync().ConfigureAwait(false);
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

        private static List<Salesmen360CurrencyAmountDto> MergeCurrencyAmountRows(
            List<Salesmen360CurrencyAmountDto> rows,
            Dictionary<string, string> currencyNameMap)
        {
            return rows
                .GroupBy(x => ResolveCurrencyName(x.Currency, currencyNameMap), StringComparer.OrdinalIgnoreCase)
                .Select(g => new Salesmen360CurrencyAmountDto
                {
                    Currency = g.Key,
                    DemandAmount = g.Sum(x => x.DemandAmount),
                    QuotationAmount = g.Sum(x => x.QuotationAmount),
                    OrderAmount = g.Sum(x => x.OrderAmount)
                })
                .OrderBy(x => x.Currency)
                .ToList();
        }

        private static List<Salesmen360AmountComparisonDto> MergeAmountComparisonRows(
            List<Salesmen360AmountComparisonDto> rows,
            Dictionary<string, string> currencyNameMap)
        {
            return rows
                .GroupBy(x => ResolveCurrencyName(x.Currency, currencyNameMap), StringComparer.OrdinalIgnoreCase)
                .Select(g => new Salesmen360AmountComparisonDto
                {
                    Currency = g.Key,
                    Last12MonthsOrderAmount = g.Sum(x => x.Last12MonthsOrderAmount),
                    OpenQuotationAmount = g.Sum(x => x.OpenQuotationAmount),
                    OpenOrderAmount = g.Sum(x => x.OpenOrderAmount)
                })
                .OrderBy(x => x.Currency)
                .ToList();
        }

        private async Task<List<Salesmen360CurrencyAmountDto>> GetTotalsByCurrencyAsync(long userId)
        {
            var demandTotals = await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .GroupBy(d => d.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var quotationTotals = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .GroupBy(q => q.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderTotals = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var result = new Dictionary<string, Salesmen360CurrencyAmountDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in demandTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.DemandAmount = item.Amount;
            }

            foreach (var item in quotationTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.QuotationAmount = item.Amount;
            }

            foreach (var item in orderTotals)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360CurrencyAmountDto { Currency = key };
                    result[key] = dto;
                }
                dto.OrderAmount = item.Amount;
            }

            return result.Values.OrderBy(x => x.Currency).ToList();
        }

        private async Task<List<Salesmen360AmountComparisonDto>> GetAmountComparisonByCurrencyAsync(
            long userId,
            DateTime last12MonthsStart,
            DateTime endExclusive)
        {
            var quotationOpenByCurrency = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed) &&
                    q.Status != ApprovalStatus.Approved && q.Status != ApprovalStatus.Rejected)
                .GroupBy(q => q.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderOpenByCurrency = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                    o.Status != ApprovalStatus.Approved && o.Status != ApprovalStatus.Rejected)
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var orderLast12ByCurrency = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                    (o.OfferDate ?? o.CreatedDate) >= last12MonthsStart &&
                    (o.OfferDate ?? o.CreatedDate) < endExclusive)
                .GroupBy(o => o.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(x => x.GrandTotal) })
                .ToListAsync().ConfigureAwait(false);

            var result = new Dictionary<string, Salesmen360AmountComparisonDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in orderLast12ByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.Last12MonthsOrderAmount = item.Amount;
            }

            foreach (var item in quotationOpenByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.OpenQuotationAmount = item.Amount;
            }

            foreach (var item in orderOpenByCurrency)
            {
                var key = NormalizeCurrency(item.Currency);
                if (!result.TryGetValue(key, out var dto))
                {
                    dto = new Salesmen360AmountComparisonDto { Currency = key };
                    result[key] = dto;
                }
                dto.OpenOrderAmount = item.Amount;
            }

            return result.Values.OrderBy(x => x.Currency).ToList();
        }

        private async Task<DateTime?> GetLastActivityDateAsync(long userId)
        {
            var demandDate = await _unitOfWork.Demands.Query(tracking: false)
                .Where(d => d.RepresentativeId == userId && !d.IsDeleted && (d.Status == null || d.Status != ApprovalStatus.Closed))
                .Select(d => (DateTime?)(d.OfferDate ?? d.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var quotationDate = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(q => q.RepresentativeId == userId && !q.IsDeleted && (q.Status == null || q.Status != ApprovalStatus.Closed))
                .Select(q => (DateTime?)(q.OfferDate ?? q.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var orderDate = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId && !o.IsDeleted && (o.Status == null || o.Status != ApprovalStatus.Closed))
                .Select(o => (DateTime?)(o.OfferDate ?? o.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var activityDate = await _unitOfWork.Activities.Query(tracking: false)
                .Where(a => a.AssignedUserId == userId && !a.IsDeleted)
                .Select(a => (DateTime?)a.StartDateTime)
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var dates = new[] { demandDate, quotationDate, orderDate, activityDate }
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            return dates.Count > 0 ? dates.Max() : null;
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
    }
}
