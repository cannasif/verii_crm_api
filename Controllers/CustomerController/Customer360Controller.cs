using crm_api.Interfaces;
using crm_api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace crm_api.Controllers
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class Customer360Controller : ControllerBase
    {
        private readonly ICustomer360Service _customer360Service;

        public Customer360Controller(ICustomer360Service customer360Service)
        {
            _customer360Service = customer360Service;
        }

        [HttpGet("{id}/overview")]
        public async Task<IActionResult> GetOverview(long id, [FromQuery] string? currency = null)
        {
            var effectiveCurrency = ResolveCurrency(currency);
            var result = await _customer360Service.GetOverviewAsync(id, effectiveCurrency);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/analytics/summary")]
        public async Task<IActionResult> GetAnalyticsSummary(long id, [FromQuery] string? currency = null)
        {
            var effectiveCurrency = ResolveCurrency(currency);
            var result = await _customer360Service.GetAnalyticsSummaryAsync(id, effectiveCurrency);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/analytics/charts")]
        public async Task<IActionResult> GetAnalyticsCharts(long id, [FromQuery] int months = 12, [FromQuery] string? currency = null)
        {
            var effectiveCurrency = ResolveCurrency(currency);
            var result = await _customer360Service.GetAnalyticsChartsAsync(id, months, effectiveCurrency);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/analytics/cohort")]
        public async Task<IActionResult> GetCohortRetention(long id, [FromQuery] int months = 12)
        {
            var result = await _customer360Service.GetCohortRetentionAsync(id, months);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/quick-quotations")]
        public async Task<IActionResult> GetQuickQuotations(long id)
        {
            var result = await _customer360Service.GetQuickQuotationsAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/erp-movements")]
        public async Task<IActionResult> GetErpMovements(long id)
        {
            var result = await _customer360Service.GetErpMovementsAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id}/erp-balance")]
        public async Task<IActionResult> GetErpBalance(long id)
        {
            var result = await _customer360Service.GetErpBalanceAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{id}/recommended-actions/execute")]
        public async Task<IActionResult> ExecuteRecommendedAction(long id, [FromBody] ExecuteRecommendedActionDto request)
        {
            var result = await _customer360Service.ExecuteRecommendedActionAsync(id, request);
            return StatusCode(result.StatusCode, result);
        }

        private string? ResolveCurrency(string? currency)
        {
            if (!string.IsNullOrWhiteSpace(currency))
            {
                return currency;
            }

            if (Request.Headers.TryGetValue("X-Currency", out var headerCurrency) && !string.IsNullOrWhiteSpace(headerCurrency))
            {
                return headerCurrency.ToString();
            }

            if (Request.Headers.TryGetValue("Currency", out var plainHeaderCurrency) && !string.IsNullOrWhiteSpace(plainHeaderCurrency))
            {
                return plainHeaderCurrency.ToString();
            }

            return null;
        }
    }
}
