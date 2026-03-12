using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;

namespace crm_api.Services
{
    public class ReportTemplateService : IReportTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<ReportTemplateService> _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IReportPdfGeneratorService _pdfGenerator;
        private readonly IPdfReportTemplateValidator _validator;

        public ReportTemplateService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ReportTemplateService> logger,
            ILocalizationService localizationService,
            IReportPdfGeneratorService pdfGenerator,
            IPdfReportTemplateValidator validator)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _localizationService = localizationService;
            _pdfGenerator = pdfGenerator;
            _validator = validator;
        }

        public async Task<ApiResponse<PagedResponse<ReportTemplateDto>>> GetAllAsync(
            PagedRequest request,
            DocumentRuleType? ruleType = null,
            bool? isActive = null)
        {
            try
            {
                var query = _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => !rt.IsDeleted)
                    .AsQueryable();

                // Apply filters
                if (ruleType.HasValue)
                {
                    query = query.Where(rt => rt.RuleType == ruleType.Value);
                }

                if (isActive.HasValue)
                {
                    query = query.Where(rt => rt.IsActive == isActive.Value);
                }

                // Get total count
                var totalCount = await query.CountAsync().ConfigureAwait(false);

                // Apply pagination
                var templates = await query
                    .OrderByDescending(rt => rt.CreatedDate)
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                // Map to DTOs
                var templateDtos = templates.Select(template => new ReportTemplateDto
                {
                    Id = template.Id,
                    RuleType = template.RuleType,
                    Title = template.Title,
                    TemplateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson),
                    IsActive = template.IsActive,
                    Default = template.Default,
                    CreatedByUserId = template.CreatedByUserId,
                    UpdatedByUserId = template.UpdatedByUserId,
                    CreatedDate = template.CreatedDate,
                    UpdatedDate = template.UpdatedDate
                }).ToList();

                var pagedResponse = new PagedResponse<ReportTemplateDto>
                {
                    Items = templateDtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ReportTemplateDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplatesRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplates"));
                return ApiResponse<PagedResponse<ReportTemplateDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplates"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<ReportTemplateDto>> GetByIdAsync(long id)
        {
            try
            {
                var template = await _unitOfWork.Repository<ReportTemplate>().Query()
                    .Where(rt => rt.Id == id && !rt.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (template == null)
                {
                    return ApiResponse<ReportTemplateDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFound"),
                        null,
                        404);
                }

                var templateDto = new ReportTemplateDto
                {
                    Id = template.Id,
                    RuleType = template.RuleType,
                    Title = template.Title,
                    TemplateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson),
                    IsActive = template.IsActive,
                    Default = template.Default,
                    CreatedByUserId = template.CreatedByUserId,
                    UpdatedByUserId = template.UpdatedByUserId,
                    CreatedDate = template.CreatedDate,
                    UpdatedDate = template.UpdatedDate
                };

                return ApiResponse<ReportTemplateDto>.SuccessResult(
                    templateDto,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateRetrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report template with ID {Id}", id);
                return ApiResponse<ReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorRetrievingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<ReportTemplateDto>> CreateAsync(CreateReportTemplateDto dto, long userId)
        {
            dto.TemplateData ??= new ReportTemplateData();
            if (dto.TemplateData.SchemaVersion <= 0)
                dto.TemplateData.SchemaVersion = 1;

            var validationErrors = _validator.ValidateTemplateData(dto.TemplateData, dto.RuleType);
            if (validationErrors.Count > 0)
            {
                var response = ApiResponse<ReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.ValidationError"),
                    string.Join("; ", validationErrors),
                    400);
                response.Errors = validationErrors.ToList();
                return response;
            }

            try
            {
                // Serialize template data to JSON
                var templateJson = JsonSerializer.Serialize(dto.TemplateData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Her RuleType için tek default: yeni Default=true ise diğerlerini false yap; ilk şablon ise Default=true yap
                var sameTypeCount = await _unitOfWork.Repository<ReportTemplate>().Query().CountAsync(rt => rt.RuleType == dto.RuleType && !rt.IsDeleted).ConfigureAwait(false);
                var isFirstForType = sameTypeCount == 0;
                var setAsDefault = dto.Default || isFirstForType;

                if (setAsDefault && !isFirstForType)
                {
                    var others = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == dto.RuleType && !rt.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);
                    foreach (var o in others)
                    {
                        o.Default = false;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(o).ConfigureAwait(false);
                    }
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

                var templateDto = new ReportTemplateDto
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

                return ApiResponse<ReportTemplateDto>.SuccessResult(
                    templateDto,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateCreated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizationService.GetLocalizedString("ReportTemplateService.ErrorCreatingReportTemplate"));
                return ApiResponse<ReportTemplateDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ReportTemplateService.ErrorCreatingReportTemplate"),
                    _localizationService.GetLocalizedString("ReportTemplateService.InternalServerError"),
                    500);
            }
        }

        public async Task<ApiResponse<ReportTemplateDto>> UpdateAsync(long id, UpdateReportTemplateDto dto, long userId)
        {
            dto.TemplateData ??= new ReportTemplateData();
            if (dto.TemplateData.SchemaVersion <= 0)
                dto.TemplateData.SchemaVersion = 1;

            var validationErrors = _validator.ValidateTemplateData(dto.TemplateData, dto.RuleType);
            if (validationErrors.Count > 0)
            {
                var response = ApiResponse<ReportTemplateDto>.ErrorResult(
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
                    return ApiResponse<ReportTemplateDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateNotFound"),
                        null,
                        404);
                }

                // Capture current default state BEFORE updating (default invariant bug fix)
                var currentWasDefault = template.Default;

                // Serialize template data to JSON
                var templateJson = JsonSerializer.Serialize(dto.TemplateData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                template.RuleType = dto.RuleType;
                template.Title = dto.Title;
                template.TemplateJson = templateJson;
                template.IsActive = dto.IsActive;
                template.Default = dto.Default;
                template.UpdatedByUserId = userId;
                template.UpdatedDate = DateTimeProvider.Now;

                if (dto.Default)
                {
                    var others = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == dto.RuleType && rt.Id != id && !rt.IsDeleted)
                        .ToListAsync().ConfigureAwait(false);
                    foreach (var o in others)
                    {
                        o.Default = false;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(o).ConfigureAwait(false);
                    }
                }
                else if (currentWasDefault)
                {
                    var newDefault = await _unitOfWork.Repository<ReportTemplate>().Query()
                        .Where(rt => rt.RuleType == dto.RuleType && rt.Id != id && !rt.IsDeleted)
                        .OrderBy(rt => rt.Id)
                        .FirstOrDefaultAsync().ConfigureAwait(false);
                    if (newDefault != null)
                    {
                        newDefault.Default = true;
                        await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(newDefault).ConfigureAwait(false);
                    }
                }

                await _unitOfWork.Repository<ReportTemplate>().UpdateAsync(template).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var templateDto = new ReportTemplateDto
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

                return ApiResponse<ReportTemplateDto>.SuccessResult(
                    templateDto,
                    _localizationService.GetLocalizedString("ReportTemplateService.ReportTemplateUpdated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report template with ID {Id}", id);
                return ApiResponse<ReportTemplateDto>.ErrorResult(
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

        public async Task<ApiResponse<byte[]>> GeneratePdfAsync(long templateId, long entityId, long? requestingUserId = null)
        {
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

                // Deserialize template data
                var templateData = JsonSerializer.Deserialize<ReportTemplateData>(template.TemplateJson);
                if (templateData == null)
                {
                    return ApiResponse<byte[]>.ErrorResult(
                        _localizationService.GetLocalizedString("ReportTemplateService.InvalidTemplateData"),
                        null,
                        400);
                }

                // Generate PDF using the PDF generator service
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
    }
}
