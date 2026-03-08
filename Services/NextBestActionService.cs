using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class NextBestActionService : INextBestActionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NextBestActionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<RecommendedActionDto>> GetCustomerActionsAsync(long customerId, RevenueQualityDto revenueQuality)
        {
            var now = DateTime.UtcNow;
            var actions = new List<RecommendedActionDto>();

            var lastActivityDate = await _unitOfWork.Activities.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted)
                .Select(x => (DateTime?)x.StartDateTime)
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var openQuotationCount = await _unitOfWork.Quotations.Query(tracking: false)
                .CountAsync(x => x.PotentialCustomerId == customerId &&
                                 !x.IsDeleted &&
                                 (x.Status == null || x.Status != ApprovalStatus.Closed) &&
                                 x.Status != ApprovalStatus.Approved &&
                                 x.Status != ApprovalStatus.Rejected).ConfigureAwait(false);

            var openDemandCount = await _unitOfWork.Demands.Query(tracking: false)
                .CountAsync(x => x.PotentialCustomerId == customerId &&
                                 !x.IsDeleted &&
                                 (x.Status == null || x.Status != ApprovalStatus.Closed) &&
                                 x.Status != ApprovalStatus.Approved &&
                                 x.Status != ApprovalStatus.Rejected).ConfigureAwait(false);

            var lastOrderDate = await _unitOfWork.Orders.Query(tracking: false)
                .Where(x => x.PotentialCustomerId == customerId && !x.IsDeleted && (x.Status == null || x.Status != ApprovalStatus.Closed))
                .Select(x => (DateTime?)(x.OfferDate ?? x.CreatedDate))
                .DefaultIfEmpty()
                .MaxAsync().ConfigureAwait(false);

            var inactivityDays = lastActivityDate.HasValue ? (now.Date - lastActivityDate.Value.Date).Days : int.MaxValue;
            var daysSinceLastOrder = lastOrderDate.HasValue ? (now.Date - lastOrderDate.Value.Date).Days : int.MaxValue;

            if (inactivityDays >= 14)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.CustomerFollowUp,
                    "Customer follow-up call",
                    95,
                    $"No customer activity in the last {inactivityDays} days.",
                    now.AddDays(1),
                    "Customer",
                    customerId,
                    "RULE_INACTIVITY_14D"));
            }

            if (openQuotationCount > 0 && inactivityDays >= 7)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.QuotationFollowUp,
                    "Follow up open quotations",
                    90,
                    $"{openQuotationCount} open quotation(s) without recent activity.",
                    now.AddDays(1),
                    "Customer",
                    customerId,
                    "RULE_OPEN_QUOTATION_7D"));
            }

            if ((revenueQuality.ChurnRiskScore ?? 0m) >= 70m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.RetentionPlan,
                    "Run retention plan",
                    88,
                    $"Churn risk score is {(revenueQuality.ChurnRiskScore ?? 0m):0.##}.",
                    now.AddDays(2),
                    "Customer",
                    customerId,
                    "RULE_CHURN_RISK"));
            }

            if ((revenueQuality.UpsellPropensityScore ?? 0m) >= 70m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.UpsellOffer,
                    "Prepare upsell offer",
                    75,
                    $"Upsell propensity score is {(revenueQuality.UpsellPropensityScore ?? 0m):0.##}.",
                    now.AddDays(3),
                    "Customer",
                    customerId,
                    "RULE_UPSELL_PROPENSITY"));
            }

            if ((revenueQuality.PaymentBehaviorScore ?? 100m) <= 40m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.PaymentReview,
                    "Run payment risk review",
                    82,
                    $"Payment behavior score is {(revenueQuality.PaymentBehaviorScore ?? 0m):0.##}.",
                    now.AddDays(1),
                    "Customer",
                    customerId,
                    "RULE_PAYMENT_BEHAVIOR"));
            }

            if (daysSinceLastOrder >= 60 && openDemandCount > 0)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.DemandToQuotation,
                    "Convert open demands to quotations",
                    78,
                    $"No order in {daysSinceLastOrder} days and {openDemandCount} open demand(s).",
                    now.AddDays(2),
                    "Customer",
                    customerId,
                    "RULE_NO_ORDER_OPEN_DEMAND"));
            }

            return actions
                .OrderByDescending(x => x.Priority)
                .Take(5)
                .ToList();
        }

        public async Task<List<RecommendedActionDto>> GetSalesmanActionsAsync(long userId, RevenueQualityDto revenueQuality)
        {
            var now = DateTime.UtcNow;
            var actions = new List<RecommendedActionDto>();
            var since90 = now.AddDays(-90);
            var since7 = now.AddDays(-7);

            var openQuotationCount = await _unitOfWork.Quotations.Query(tracking: false)
                .CountAsync(x => x.RepresentativeId == userId &&
                                 !x.IsDeleted &&
                                 (x.Status == null || x.Status != ApprovalStatus.Closed) &&
                                 x.Status != ApprovalStatus.Approved &&
                                 x.Status != ApprovalStatus.Rejected).ConfigureAwait(false);

            var quotationCount90 = await _unitOfWork.Quotations.Query(tracking: false)
                .CountAsync(x => x.RepresentativeId == userId &&
                                 !x.IsDeleted &&
                                 (x.Status == null || x.Status != ApprovalStatus.Closed) &&
                                 (x.OfferDate ?? x.CreatedDate) >= since90).ConfigureAwait(false);

            var wonQuotationCount90 = await _unitOfWork.Quotations.Query(tracking: false)
                .CountAsync(x => x.RepresentativeId == userId &&
                                 !x.IsDeleted &&
                                 x.Status == ApprovalStatus.Approved &&
                                 (x.OfferDate ?? x.CreatedDate) >= since90).ConfigureAwait(false);

            var activityCount7 = await _unitOfWork.Activities.Query(tracking: false)
                .CountAsync(x => x.AssignedUserId == userId && !x.IsDeleted && x.StartDateTime >= since7).ConfigureAwait(false);

            if (openQuotationCount >= 15)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.PipelineCleanup,
                    "Clean open pipeline",
                    90,
                    $"{openQuotationCount} open quotations require prioritization.",
                    now.AddDays(1),
                    "User",
                    userId,
                    "RULE_OPEN_PIPELINE_VOLUME"));
            }

            if (activityCount7 < 5)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.ActivityBoost,
                    "Increase activity cadence",
                    80,
                    $"Only {activityCount7} activities in the last 7 days.",
                    now.AddDays(1),
                    "User",
                    userId,
                    "RULE_ACTIVITY_CADENCE"));
            }

            if ((revenueQuality.ChurnRiskScore ?? 0m) >= 70m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.PortfolioRetention,
                    "Review risky customer portfolio",
                    88,
                    $"Portfolio churn risk score is {(revenueQuality.ChurnRiskScore ?? 0m):0.##}.",
                    now.AddDays(2),
                    "User",
                    userId,
                    "RULE_PORTFOLIO_CHURN"));
            }

            if ((revenueQuality.UpsellPropensityScore ?? 0m) >= 70m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.UpsellCampaign,
                    "Run upsell campaign",
                    74,
                    $"Upsell propensity score is {(revenueQuality.UpsellPropensityScore ?? 0m):0.##}.",
                    now.AddDays(3),
                    "User",
                    userId,
                    "RULE_PORTFOLIO_UPSELL"));
            }

            var winRate90 = quotationCount90 == 0 ? 0m : (decimal)wonQuotationCount90 / quotationCount90;
            if (quotationCount90 >= 5 && winRate90 < 0.2m)
            {
                actions.Add(CreateAction(
                    NbaActionCatalog.WinrateImprovement,
                    "Review lost quotation reasons",
                    76,
                    $"90-day win rate is {(winRate90 * 100m):0.##}%.",
                    now.AddDays(2),
                    "User",
                    userId,
                    "RULE_LOW_WINRATE"));
            }

            return actions
                .OrderByDescending(x => x.Priority)
                .Take(5)
                .ToList();
        }

        private static RecommendedActionDto CreateAction(
            string actionCode,
            string title,
            int priority,
            string reason,
            DateTime dueDate,
            string targetEntityType,
            long targetEntityId,
            string ruleCode)
        {
            return new RecommendedActionDto
            {
                ActionCode = actionCode,
                Title = title,
                Priority = priority,
                Reason = reason,
                DueDate = dueDate,
                TargetEntityType = targetEntityType,
                TargetEntityId = targetEntityId,
                SourceRuleCode = ruleCode
            };
        }
    }
}
