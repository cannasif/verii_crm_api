using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using crm_api.DTOs;
using crm_api.Infrastructure;
using crm_api.Interfaces;
using crm_api.Models;

namespace crm_api.Controllers
{
    /// <summary>
    /// Legacy report template API. Prefer <see cref="PdfReportTemplateController"/> and /api/pdf-report-templates for new clients.
    /// </summary>
    [Obsolete("Use /api/pdf-report-templates (PdfReportTemplateController) instead. This endpoint remains for backward compatibility.")]
    [DeprecatedApi(Replacement = "/api/pdf-report-templates")]
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportTemplateController : ControllerBase
    {
        private readonly IReportTemplateService _reportTemplateService;
        private readonly ILocalizationService _localizationService;

        public ReportTemplateController(IReportTemplateService reportTemplateService, ILocalizationService localizationService)
        {
            _reportTemplateService = reportTemplateService;
            _localizationService = localizationService;
        }

        /// <summary>
        /// Get all report templates with pagination and filters
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DocumentRuleType? ruleType = null,
            [FromQuery] bool? isActive = null)
        {
            var request = new PagedRequest
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _reportTemplateService.GetAllAsync(request, ruleType, isActive);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Get report template by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _reportTemplateService.GetByIdAsync(id);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Create a new report template
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReportTemplateDto dto)
        {
            if (!long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                return Unauthorized(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("General.Unauthorized"),
                    _localizationService.GetLocalizedString("General.InvalidOrMissingUserClaim"),
                    401));

            var result = await _reportTemplateService.CreateAsync(dto, userId);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        /// <summary>
        /// Update an existing report template
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateReportTemplateDto dto)
        {
            if (!long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                return Unauthorized(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("General.Unauthorized"),
                    _localizationService.GetLocalizedString("General.InvalidOrMissingUserClaim"),
                    401));

            var result = await _reportTemplateService.UpdateAsync(id, dto, userId);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Delete a report template (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _reportTemplateService.DeleteAsync(id);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Generate PDF from template and entity data
        /// </summary>
        [HttpPost("generate-pdf")]
        public async Task<IActionResult> GeneratePdf([FromBody] GeneratePdfRequest request)
        {
            long? userId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) && uid > 0 ? uid : null;
            var result = await _reportTemplateService.GeneratePdfAsync(request.TemplateId, request.EntityId, userId);

            if (!result.Success)
            {
                return StatusCode(result.StatusCode, result);
            }

            return File(result.Data!, "application/pdf", $"report_{request.EntityId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        /// <summary>
        /// Get available fields for a document type (for template designer)
        /// </summary>
        [HttpGet("fields/{ruleType}")]
        public IActionResult GetAvailableFields(DocumentRuleType ruleType)
        {
            ReportTemplateFieldsDto fields = ruleType switch
            {
                DocumentRuleType.Demand => DemandFields.GetFields(),
                DocumentRuleType.Quotation => QuotationFields.GetFields(),
                DocumentRuleType.Order => OrderFields.GetFields(),
                DocumentRuleType.FastQuotation => FastQuotationFields.GetFields(),
                DocumentRuleType.Activity => ActivityFields.GetFields(),
                _ => new ReportTemplateFieldsDto()
            };

            return Ok(ApiResponse<ReportTemplateFieldsDto>.SuccessResult(
                fields,
                _localizationService.GetLocalizedString("ReportTemplateController.AvailableFieldsRetrieved")));
        }
    }
}
