using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;

namespace crm_api.Controllers
{
    /// <summary>
    /// PDF report template API (report-builder discipline). Use this for /report-designer frontend.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/pdf-report-templates")]
    public class PdfReportTemplateController : ControllerBase
    {
        private readonly IPdfReportTemplateService _pdfReportTemplateService;
        private readonly IPdfTemplateAssetService _pdfTemplateAssetService;
        private readonly ILocalizationService _localizationService;

        public PdfReportTemplateController(
            IPdfReportTemplateService pdfReportTemplateService,
            IPdfTemplateAssetService pdfTemplateAssetService,
            ILocalizationService localizationService)
        {
            _pdfReportTemplateService = pdfReportTemplateService;
            _pdfTemplateAssetService = pdfTemplateAssetService;
            _localizationService = localizationService;
        }

        /// <summary>Get all PDF report templates with pagination and filters.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] DocumentRuleType? ruleType = null,
            [FromQuery] bool? isActive = null)
        {
            var request = new PdfReportTemplateListRequest
            {
                Search = search,
                PageNumber = pageNumber,
                PageSize = pageSize,
                RuleType = ruleType,
                IsActive = isActive
            };
            var result = await _pdfReportTemplateService.GetAllAsync(request);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return Ok(result);
        }

        /// <summary>Get PDF report template by ID.</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _pdfReportTemplateService.GetByIdAsync(id);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return Ok(result);
        }

        /// <summary>Create a new PDF report template.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePdfReportTemplateDto dto)
        {
            if (!long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                return Unauthorized(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.UnauthorizedGenerate"),
                    "Invalid or missing user claim",
                    401));

            var result = await _pdfReportTemplateService.CreateAsync(dto, userId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPost("assets/upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadAsset(IFormFile file, [FromForm] long? templateId = null)
        {
            if (!long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                return Unauthorized(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.UnauthorizedGenerate"),
                    "Invalid or missing user claim",
                    401));

            var result = await _pdfTemplateAssetService.UploadAsync(file, userId, templateId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return Ok(result);
        }

        /// <summary>Update an existing PDF report template.</summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] UpdatePdfReportTemplateDto dto)
        {
            if (!long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
                return Unauthorized(ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.UnauthorizedGenerate"),
                    "Invalid or missing user claim",
                    401));

            var result = await _pdfReportTemplateService.UpdateAsync(id, dto, userId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return Ok(result);
        }

        /// <summary>Delete a PDF report template (soft delete).</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _pdfReportTemplateService.DeleteAsync(id);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return Ok(result);
        }

        /// <summary>Generate PDF document from template and entity.</summary>
        [HttpPost("generate-document")]
        public async Task<IActionResult> GenerateDocument([FromBody] GeneratePdfRequest request)
        {
            long? userId = long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) && uid > 0 ? uid : null;
            var result = await _pdfReportTemplateService.GeneratePdfAsync(request.TemplateId, request.EntityId, userId);
            if (!result.Success)
                return StatusCode(result.StatusCode, result);
            return File(result.Data!, "application/pdf", $"report_{request.EntityId}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
        }

        /// <summary>Get available fields for a document type (for template designer).</summary>
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
