using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class RevenueQualityService : IRevenueQualityService
    {
        private readonly IUnitOfWork _unitOfWork;

        public RevenueQualityService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<RevenueQualityDto> CalculateCustomerRevenueQualityAsync(long customerId)
        {
            var now = DateTime.UtcNow;
            var orders = await _unitOfWork.Orders.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => new
                {
                    Date = x.OfferDate ?? x.CreatedDate,
                    x.GrandTotal,
                    x.Status
                })
                .ToListAsync().ConfigureAwait(false);

            var quotations = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => new
                {
                    Date = x.OfferDate ?? x.CreatedDate,
                    x.GrandTotal,
                    x.Status
                })
                .ToListAsync().ConfigureAwait(false);

            var demands = await _unitOfWork.Demands.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => x.OfferDate ?? x.CreatedDate)
                .ToListAsync().ConfigureAwait(false);

            var lastActivityDate = await _unitOfWork.Activities.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted)
                .Select(x => (DateTime?)x.StartDateTime)
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var firstTouchDate = orders.Select(x => (DateTime?)x.Date)
                .Concat(quotations.Select(x => (DateTime?)x.Date))
                .Concat(demands.Select(x => (DateTime?)x))
                .DefaultIfEmpty(null)
                .Min();

            var lastOrderDate = orders.Count > 0 ? orders.Max(x => x.Date) : (DateTime?)null;
            var daysSinceLastOrder = lastOrderDate.HasValue ? (now.Date - lastOrderDate.Value.Date).Days : (int?)null;
            var daysSinceLastActivity = lastActivityDate.HasValue ? (now.Date - lastActivityDate.Value.Date).Days : (int?)null;
            var since12Months = now.AddMonths(-12);

            var orders12 = orders.Where(x => x.Date >= since12Months).ToList();
            var quotations12 = quotations.Where(x => x.Date >= since12Months).ToList();
            var orderCount12 = orders12.Count;
            var quotationCount12 = quotations12.Count;
            var approvedQuotation12 = quotations12.Count(x => x.Status == ApprovalStatus.Approved);
            var rejectedQuotation12 = quotations12.Count(x => x.Status == ApprovalStatus.Rejected);

            var retentionRate = CalculateRetentionRate(firstTouchDate, orders.Select(x => x.Date), now);
            var ltv = orders.Sum(x => x.GrandTotal);
            var rfmSegment = CalculateRfmSegment(daysSinceLastOrder, orderCount12, orders12.Sum(x => x.GrandTotal));
            var churnRisk = CalculateCustomerChurnRisk(daysSinceLastOrder, daysSinceLastActivity, orderCount12);
            var upsellPropensity = CalculateCustomerUpsellPropensity(
                daysSinceLastOrder,
                orderCount12,
                quotationCount12,
                approvedQuotation12,
                orders12.Select(x => x.GrandTotal).ToList());
            var paymentBehaviorScore = CalculatePaymentBehaviorScore(orderCount12, quotationCount12, rejectedQuotation12, daysSinceLastOrder);

            return new RevenueQualityDto
            {
                CohortKey = firstTouchDate.HasValue ? firstTouchDate.Value.ToString("yyyy-MM") : null,
                RetentionRate = retentionRate,
                RfmSegment = rfmSegment,
                Ltv = ltv,
                ChurnRiskScore = churnRisk,
                UpsellPropensityScore = upsellPropensity,
                PaymentBehaviorScore = paymentBehaviorScore,
                DataQualityNote = "Payment behavior score is currently proxy-based and does not use ERP payment collection history."
            };
        }

        public async Task<RevenueQualityDto> CalculateSalesmanRevenueQualityAsync(long userId)
        {
            var now = DateTime.UtcNow;
            var since12Months = now.AddMonths(-12);

            var orders = await _unitOfWork.Orders.Query(tracking: false)
                .Where(x => x.RepresentativeId == userId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => new
                {
                    Date = x.OfferDate ?? x.CreatedDate,
                    x.GrandTotal
                })
                .ToListAsync().ConfigureAwait(false);

            var quotations = await _unitOfWork.Quotations.Query(tracking: false)
                .Where(x => x.RepresentativeId == userId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => new
                {
                    Date = x.OfferDate ?? x.CreatedDate,
                    x.Status
                })
                .ToListAsync().ConfigureAwait(false);

            var activities = await _unitOfWork.Activities.Query(tracking: false)
                .Where(x => x.AssignedUserId == userId && !x.IsDeleted)
                .Select(x => x.StartDateTime)
                .ToListAsync().ConfigureAwait(false);

            var firstTouchDate = orders.Select(x => (DateTime?)x.Date)
                .Concat(quotations.Select(x => (DateTime?)x.Date))
                .Concat(activities.Select(x => (DateTime?)x))
                .DefaultIfEmpty(null)
                .Min();

            var lastOrderDate = orders.Count > 0 ? orders.Max(x => x.Date) : (DateTime?)null;
            var daysSinceLastOrder = lastOrderDate.HasValue ? (now.Date - lastOrderDate.Value.Date).Days : (int?)null;

            var orders12 = orders.Where(x => x.Date >= since12Months).ToList();
            var quotations12 = quotations.Where(x => x.Date >= since12Months).ToList();
            var activity12 = activities.Where(x => x >= since12Months).ToList();

            var activeCustomers12 = await _unitOfWork.Orders.Query(tracking: false)
                .Where(x => x.RepresentativeId == userId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed) && (x.OfferDate ?? x.CreatedDate) >= since12Months)
                .Select(x => x.PotentialCustomerId)
                .Distinct()
                .CountAsync(x => x.HasValue).ConfigureAwait(false);

            var allCustomerIds = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId &&
                            o.PotentialCustomerId.HasValue &&
                            !o.IsDeleted &&
                            (o.Status == null || o.Status != ApprovalStatus.Closed))
                .Select(o => o.PotentialCustomerId!.Value)
                .Distinct()
                .ToListAsync().ConfigureAwait(false);

            var activeCustomerIds90 = await _unitOfWork.Orders.Query(tracking: false)
                .Where(o => o.RepresentativeId == userId &&
                            o.PotentialCustomerId.HasValue &&
                            !o.IsDeleted &&
                            (o.Status == null || o.Status != ApprovalStatus.Closed) &&
                            (o.OfferDate ?? o.CreatedDate) >= now.AddDays(-90))
                .Select(o => o.PotentialCustomerId!.Value)
                .Distinct()
                .ToListAsync().ConfigureAwait(false);

            var dormantCustomers90 = allCustomerIds.Except(activeCustomerIds90).Count();

            var retentionRate = CalculateRetentionRate(firstTouchDate, orders.Select(x => x.Date), now);
            var ltv = orders.Sum(x => x.GrandTotal);
            var rfmSegment = CalculateRfmSegment(daysSinceLastOrder, orders12.Count, orders12.Sum(x => x.GrandTotal));

            var churnRisk = Math.Clamp(activeCustomers12 == 0
                ? 50m
                : (decimal)dormantCustomers90 / activeCustomers12 * 100m, 0m, 100m);

            var approvedQuotation12 = quotations12.Count(x => x.Status == ApprovalStatus.Approved);
            var upsellPropensity = Math.Clamp(
                25m +
                (orders12.Count >= 10 ? 20m : orders12.Count >= 5 ? 10m : 0m) +
                (approvedQuotation12 >= 5 ? 20m : approvedQuotation12 >= 2 ? 10m : 0m) +
                (activity12.Count >= 20 ? 15m : activity12.Count >= 8 ? 5m : 0m),
                0m, 100m);

            var rejectedQuotation12 = quotations12.Count(x => x.Status == ApprovalStatus.Rejected);
            var paymentBehaviorScore = CalculatePaymentBehaviorScore(
                orders12.Count,
                quotations12.Count,
                rejectedQuotation12,
                daysSinceLastOrder);

            return new RevenueQualityDto
            {
                CohortKey = firstTouchDate.HasValue ? firstTouchDate.Value.ToString("yyyy-MM") : null,
                RetentionRate = retentionRate,
                RfmSegment = rfmSegment,
                Ltv = ltv,
                ChurnRiskScore = churnRisk,
                UpsellPropensityScore = upsellPropensity,
                PaymentBehaviorScore = paymentBehaviorScore,
                DataQualityNote = "Scores are deterministic v1 and can be enhanced with ERP collection data + ML features."
            };
        }

        private static decimal? CalculateRetentionRate(DateTime? firstTouchDate, IEnumerable<DateTime> orderDates, DateTime now)
        {
            if (!firstTouchDate.HasValue)
            {
                return null;
            }

            var monthsSinceFirstTouch = ((now.Year - firstTouchDate.Value.Year) * 12) + now.Month - firstTouchDate.Value.Month + 1;
            monthsSinceFirstTouch = Math.Max(monthsSinceFirstTouch, 1);

            var activeMonths = orderDates
                .Select(x => x.ToString("yyyy-MM"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return Math.Round((decimal)activeMonths / monthsSinceFirstTouch * 100m, 2);
        }

        private static string CalculateRfmSegment(int? daysSinceLastOrder, int frequency12, decimal monetary12)
        {
            var recency = daysSinceLastOrder ?? 9999;

            if (recency <= 30 && frequency12 >= 6 && monetary12 >= 100000m)
            {
                return "Champions";
            }

            if (recency <= 60 && frequency12 >= 3)
            {
                return "Loyal";
            }

            if (recency > 120 && frequency12 <= 1)
            {
                return "AtRisk";
            }

            return "Potential";
        }

        private static decimal CalculateCustomerChurnRisk(int? daysSinceLastOrder, int? daysSinceLastActivity, int orderCount12)
        {
            decimal score = 0m;
            var orderRecency = daysSinceLastOrder ?? 9999;
            var activityRecency = daysSinceLastActivity ?? 9999;

            if (orderRecency > 180) score += 45m;
            else if (orderRecency > 120) score += 35m;
            else if (orderRecency > 60) score += 20m;
            else if (orderRecency > 30) score += 10m;

            if (activityRecency > 30) score += 20m;
            else if (activityRecency > 14) score += 10m;

            if (orderCount12 == 0) score += 25m;
            else if (orderCount12 <= 2) score += 10m;

            return Math.Clamp(score, 0m, 100m);
        }

        private static decimal CalculateCustomerUpsellPropensity(
            int? daysSinceLastOrder,
            int orderCount12,
            int quotationCount12,
            int approvedQuotation12,
            List<decimal> orders12Amounts)
        {
            decimal score = 20m;

            if (orderCount12 >= 6) score += 20m;
            else if (orderCount12 >= 3) score += 10m;

            if (quotationCount12 > 0)
            {
                var conversion = (decimal)approvedQuotation12 / quotationCount12;
                if (conversion >= 0.5m) score += 25m;
                else if (conversion >= 0.3m) score += 15m;
            }

            var averageOrderAmount = orderCount12 > 0
                ? orders12Amounts.Sum() / orderCount12
                : 0m;
            if (averageOrderAmount >= 50000m) score += 15m;
            else if (averageOrderAmount >= 20000m) score += 8m;

            if ((daysSinceLastOrder ?? 9999) <= 45) score += 10m;

            return Math.Clamp(score, 0m, 100m);
        }

        private static decimal CalculatePaymentBehaviorScore(
            int orderCount12,
            int quotationCount12,
            int rejectedQuotation12,
            int? daysSinceLastOrder)
        {
            decimal score = 50m;

            if (orderCount12 >= 4) score += 15m;
            else if (orderCount12 >= 2) score += 8m;

            if ((daysSinceLastOrder ?? 9999) <= 45) score += 10m;

            if (quotationCount12 > 0)
            {
                var rejectionRate = (decimal)rejectedQuotation12 / quotationCount12;
                if (rejectionRate >= 0.5m) score -= 20m;
                else if (rejectionRate >= 0.25m) score -= 10m;
            }

            return Math.Clamp(score, 0m, 100m);
        }
    }
}
