using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;

namespace crm_api.Services
{
    /// <summary>
    /// PDF report template service: CRUD and generate with validation, default invariant fix, and access control.
    /// </summary>
    public class PdfReportTemplateService : IPdfReportTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PdfReportTemplateService> _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IPdfReportDocumentGeneratorService _pdfGenerator;
        private readonly IPdfReportTemplateValidator _validator;

        public PdfReportTemplateService(
            IUnitOfWork unitOfWork,
            ILogger<PdfReportTemplateService> logger,
            ILocalizationService localizationService,
            IPdfReportDocumentGeneratorService pdfGenerator,
            IPdfReportTemplateValidator validator)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _localizationService = localizationService;
            _pdfGenerator = pdfGenerator;
            _validator = validator;
        }

        public async Task<ApiResponse<PagedResponse<PdfReportTemplateDto>>> GetAllAsync(PdfReportTemplateListRequest request)
        {
            try
            {
                var query = _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => !rt.IsDeleted)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    var search = request.Search.Trim();
                    query = query.Where(rt => rt.Title.Contains(search));
                }
                if (request.RuleType.HasValue)
                    query = query.Where(rt => rt.RuleType == request.RuleType.Value);
                if (request.IsActive.HasValue)
                    query = query.Where(rt => rt.IsActive == request.IsActive.Value);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var templates = await query
                    .OrderByDescending(rt => rt.CreatedDate)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var items = templates.Select(t => MapToPdfDto(t)).ToList();
                var paged = new PagedResponse<PdfReportTemplateDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PdfReportTemplateDto>>.SuccessResult(
                    paged,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplatesRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplates"));
                return ApiResponse<PagedResponse<PdfReportTemplateDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplates"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<PdfReportTemplateDto>> GetByIdAsync(long id)
        {
            try
            {
                var template = await _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => rt.Id == id && !rt.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (template == null)
                {
                    return ApiResponse<PdfReportTemplateDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFound"),
                        null,
                        404);
                }

                return ApiResponse<PdfReportTemplateDto>.SuccessResult(
                    MapToPdfDto(template),
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report template with ID {Id}", id);
                return ApiResponse<PdfReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<PdfReportTemplateDto>> CreateAsync(CreatePdfReportTemplateDto dto, long userId)
        {
            dto.TemplateData ??= new ReportTemplateData();
            if (dto.TemplateData.SchemaVersion <= 0)
                dto.TemplateData.SchemaVersion = 1;

            var validationErrors = _validator.ValidateTemplateData(dto.TemplateData, dto.RuleType);
            if (validationErrors.Count > 0)
            {
                var response = ApiResponse<PdfReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    string.Join("; ", validationErrors),
                    400);
                response.Errors = validationErrors.ToList();
                return response;
            }

            try
            {
                var templateJson = JsonSerializer.Serialize(dto.TemplateData, PdfReportTemplateJsonOptions.CamelCase);

                var sameTypeCount = await _unitOfWork.Repository<ReportTemplate>().Query().CountAsync(rt => rt.RuleType == dto.RuleType && !rt.IsDeleted).ConfigureAwait(false);
                var isFirstForType = sameTypeCount == 0;
                var setAsDefault = dto.Default || isFirstForType;

                if (setAsDefault && !isFirstForType)
                {
                    var others = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == dto.RuleType && !rt.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);
                    var changed = false;
                    foreach (var o in others)
                    {
                        if (!o.Default)
                            continue;
                        o.Default = false;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(o).ConfigureAwait(false);
                        changed = true;
                    }
                    if (changed)
                        await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                }

                var template = new ReportTemplate
                {
                    RuleType = dto.RuleType,
                    Title = dto.Title,
                    TemplateJson = templateJson,
                    IsActive = dto.IsActive,
                    Default = setAsDefault,
                    CreatedByUserId = userId,
                    CreatedDate = DateTimeProvider.Now
                };

                await _unitOfWork.Repository<ReportTemplate>().AddAsync(template).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var templateDto = new PdfReportTemplateDto
                {
                    Id = template.Id,
                    RuleType = template.RuleType,
                    Title = template.Title,
                    TemplateData = dto.TemplateData,
                    IsActive = template.IsActive,
                    Default = template.Default,
                    CreatedByUserId = template.CreatedByUserId,
                    CreatedDate = template.CreatedDate
                };

                return ApiResponse<PdfReportTemplateDto>.SuccessResult(
                    templateDto,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateCreated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizationService.GetLocalizedString("ReportTemplateService.ErrorCreatingReportTemplate"));
                return ApiResponse<PdfReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorCreatingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<PdfReportTemplateDto>> UpdateAsync(long id, UpdatePdfReportTemplateDto dto, long userId)
        {
            dto.TemplateData ??= new ReportTemplateData();
            if (dto.TemplateData.SchemaVersion <= 0)
                dto.TemplateData.SchemaVersion = 1;

            var validationErrors = _validator.ValidateTemplateData(dto.TemplateData, dto.RuleType);
            if (validationErrors.Count > 0)
            {
                var response = ApiResponse<PdfReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    string.Join("; ", validationErrors),
                    400);
                response.Errors = validationErrors.ToList();
                return response;
            }

            try
            {
                var template = await _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => rt.Id == id && !rt.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (template == null)
                {
                    return ApiResponse<PdfReportTemplateDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFound"),
                        null,
                        404);
                }

                // Capture current state before mutating, because default reassignment depends on the old rule type.
                var currentWasDefault = template.Default;
                var originalRuleType = template.RuleType;

                var templateJson = JsonSerializer.Serialize(dto.TemplateData, PdfReportTemplateJsonOptions.CamelCase);

                if (dto.Default)
                {
                    var others = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == dto.RuleType && rt.Id != id && !rt.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);
                    var changed = false;
                    foreach (var o in others)
                    {
                        if (!o.Default)
                            continue;
                        o.Default = false;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(o).ConfigureAwait(false);
                        changed = true;
                    }
                    if (changed)
                        await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                }

                template.RuleType = dto.RuleType;
                template.Title = dto.Title;
                template.TemplateJson = templateJson;
                template.IsActive = dto.IsActive;
                template.Default = dto.Default;
                template.UpdatedByUserId = userId;
                template.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(template).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                if (currentWasDefault && (!dto.Default || originalRuleType != dto.RuleType))
                {
                    // If the previous default is being unset or moved away, keep the old rule type with a fallback default.
                    var newDefault = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == originalRuleType && rt.Id != id && !rt.IsDeleted)
                        .OrderBy(rt => rt.Id)
                        .FirstOrDefaultAsync().ConfigureAwait(false);
                    if (newDefault != null)
                    {
                        newDefault.Default = true;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(newDefault).ConfigureAwait(false);
                        await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                    }
                }

                var templateDto = new PdfReportTemplateDto
                {
                    Id = template.Id,
                    RuleType = template.RuleType,
                    Title = template.Title,
                    TemplateData = dto.TemplateData,
                    IsActive = template.IsActive,
                    Default = template.Default,
                    CreatedByUserId = template.CreatedByUserId,
                    UpdatedByUserId = template.UpdatedByUserId,
                    CreatedDate = template.CreatedDate,
                    UpdatedDate = template.UpdatedDate
                };

                return ApiResponse<PdfReportTemplateDto>.SuccessResult(
                    templateDto,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateUpdated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report template with ID {Id}", id);
                return ApiResponse<PdfReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorUpdatingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(long id)
        {
            try
            {
                var template = await _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => rt.Id == id && !rt.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (template == null)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFound"),
                        null,
                        404);
                }

                template.IsDeleted = true;
                template.DeletedDate = DateTimeProvider.Now;
                await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(template).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(
                    true,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateDeleted"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report template with ID {Id}", id);
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorDeletingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<byte[]>> GeneratePdfAsync(long templateId, long entityId, long? requestingUserId)
        {
            // Erişim kontrolü: en azından kimlik doğrulaması gerekli
            if (!requestingUserId.HasValue || requestingUserId.Value <= 0)
            {
                return ApiResponse<byte[]>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.UnauthorizedGenerate"),
                    null,
                    401);
            }

            try
            {
                var template = await _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => rt.Id == templateId && !rt.IsDeleted && rt.IsActive)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (template == null)
                {
                    return ApiResponse<byte[]>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFoundOrInactive"),
                        null,
                        404);
                }

                ReportTemplateData? templateData;
                try
                {
                    templateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson, PdfReportTemplateJsonOptions.CamelCase);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid template JSON for template {TemplateId}", templateId);
                    return ApiResponse<byte[]>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.InvalidTemplateData"),
                        ex.Message,
                        400);
                }

                if (templateData == null)
                {
                    return ApiResponse<byte[]>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.InvalidTemplateData"),
                        null,
                        400);
                }

                var genErrors = _validator.ValidateForGenerate(templateData);
                if (genErrors.Count > 0)
                {
                    var response = ApiResponse<byte[]>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        string.Join("; ", genErrors),
                        400);
                    response.Errors = genErrors.ToList();
                    return response;
                }

                var pdfBytes = await _pdfGenerator.GeneratePdfAsync(template.RuleType, entityId, templateData).ConfigureAwait(false);

                return ApiResponse<byte[]>.SuccessResult(
                    pdfBytes,
                    _localizationService.GetLocalizedString("ReportTemplateService.PdfGenerated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for template {TemplateId} and entity {EntityId}", templateId, entityId);
                return ApiResponse<byte[]>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorGeneratingPdf"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        private static PdfReportTemplateDto MapToPdfDto(ReportTemplate template)
        {
            var templateData = (ReportTemplateData?)null;
            if (!string.IsNullOrEmpty(template.TemplateJson))
            {
                try
                {
                    templateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson, PdfReportTemplateJsonOptions.CamelCase);
                }
                catch { /* leave null on invalid json */ }
            }

            return new PdfReportTemplateDto
            {
                Id = template.Id,
                RuleType = template.RuleType,
                Title = template.Title,
                TemplateData = templateData,
                IsActive = template.IsActive,
                Default = template.Default,
                CreatedByUserId = template.CreatedByUserId,
                UpdatedByUserId = template.UpdatedByUserId,
                CreatedDate = template.CreatedDate,
                UpdatedDate = template.UpdatedDate
            };
        }
    }
}
