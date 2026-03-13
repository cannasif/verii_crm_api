using crm_api.DTOs;
using crm_api.DTOs.CustomerDto;

namespace crm_api.Interfaces
{
    public interface ICustomer360Service
    {
        Task<ApiResponse<Customer360OverviewDto>> GetOverviewAsync(long customerId, string? currency = null);
        Task<ApiResponse<Customer360AnalyticsSummaryDto>> GetAnalyticsSummaryAsync(long customerId, string? currency = null);
        Task<ApiResponse<Customer360AnalyticsChartsDto>> GetAnalyticsChartsAsync(long customerId, int months = 12, string? currency = null);
        Task<ApiResponse<List<CohortRetentionDto>>> GetCohortRetentionAsync(long customerId, int months = 12);
        Task<ApiResponse<List<Customer360QuickQuotationDto>>> GetQuickQuotationsAsync(long customerId);
        Task<ApiResponse<List<Customer360ErpMovementDto>>> GetErpMovementsAsync(long customerId);
        Task<ApiResponse<Customer360ErpBalanceDto>> GetErpBalanceAsync(long customerId);
        Task<ApiResponse<ActivityDto>> ExecuteRecommendedActionAsync(long customerId, ExecuteRecommendedActionDto request);
    }
}
