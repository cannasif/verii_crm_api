using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace crm_api.Services
{
    public class SmtpSettingsService : ISmtpSettingsService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IMemoryCache _cache;
        private readonly IDataProtector _protector;

        private const string CacheKey = "smtp_settings_runtime_v1";

        public SmtpSettingsService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IMemoryCache cache,
            IDataProtectionProvider dataProtectionProvider)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _cache = cache;
            _protector = dataProtectionProvider.CreateProtector("smtp-settings-v1");
        }

        public void InvalidateCache()
        {
            _cache.Remove(CacheKey);
        }

        public async Task<ApiResponse<SmtpSettingsDto>> GetAsync()
        {
            try
            {
                // Tek kayıt: Id=1
                var entity = await _unitOfWork.SmtpSettings
                    .Query()
                    .AsNoTracking()
                    .Where(x => x.Id == 1 && !x.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (entity == null)
                {
                    // Kayıt yoksa default DTO dön (front ekranı boş görmesin)
                    var dto = new SmtpSettingsDto
                    {
                        Host = "",
                        Port = 587,
                        EnableSsl = true,
                        Username = "",
                        FromEmail = "",
                        FromName = "V3RII CRM System",
                        Timeout = 30,
                        UpdatedAt = DateTime.UtcNow
                    };

                    return ApiResponse<SmtpSettingsDto>.SuccessResult(
                        dto,
                        _localizationService.GetLocalizedString("SmtpSettingsService.SmtpSettingsRetrievedDefault"));
                }

                var mapped = _mapper.Map<SmtpSettingsDto>(entity);

                return ApiResponse<SmtpSettingsDto>.SuccessResult(
                    mapped,
                    _localizationService.GetLocalizedString("SmtpSettingsService.SmtpSettingsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<SmtpSettingsDto>.ErrorResult(
                    _localizationService.GetLocalizedString("SmtpSettingsService.InternalServerError"),
                    _localizationService.GetLocalizedString("SmtpSettingsService.GetExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<ApiResponse<SmtpSettingsDto>> UpdateAsync(UpdateSmtpSettingsDto dto, long userId)
        {
            try
            {
                // Id=1 kayıt
                var entity = await _unitOfWork.SmtpSettings
                    .Query()
                    .Where(x => x.Id == 1 && !x.IsDeleted)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (entity == null)
                {
                    entity = new SmtpSetting
                    {
                        Id = 1,
                        IsDeleted = false,
                        CreatedDate = DateTimeProvider.Now,
                        CreatedBy = userId
                    };

                    // AutoMapper: dto -> entity (audit alanlar mapping profile'da ignore)
                    _mapper.Map(dto, entity);

                    // Password (varsa) şifrele
                    if (!string.IsNullOrWhiteSpace(dto.Password))
                        entity.PasswordEncrypted = _protector.Protect(dto.Password);

                    entity.UpdatedDate = DateTimeProvider.Now;
                    entity.UpdatedBy = userId;

                    await _unitOfWork.SmtpSettings.AddAsync(entity).ConfigureAwait(false);
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                    // cache invalidate (bu instance)
                    InvalidateCache();

                    var createdDto = _mapper.Map<SmtpSettingsDto>(entity);

                    return ApiResponse<SmtpSettingsDto>.SuccessResult(
                        createdDto,
                        _localizationService.GetLocalizedString("SmtpSettingsService.SmtpSettingsCreated"));
                }

                // mevcut kayıt update
                _mapper.Map(dto, entity);

                // Password boş ise eskisini koru
                if (!string.IsNullOrWhiteSpace(dto.Password))
                    entity.PasswordEncrypted = _protector.Protect(dto.Password);

                entity.UpdatedDate = DateTimeProvider.Now;
                entity.UpdatedBy = userId;

                await _unitOfWork.SmtpSettings.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // cache invalidate (bu instance)
                InvalidateCache();

                var updatedDto = _mapper.Map<SmtpSettingsDto>(entity);

                return ApiResponse<SmtpSettingsDto>.SuccessResult(
                    updatedDto,
                    _localizationService.GetLocalizedString("SmtpSettingsService.SmtpSettingsUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<SmtpSettingsDto>.ErrorResult(
                    _localizationService.GetLocalizedString("SmtpSettingsService.InternalServerError"),
                    _localizationService.GetLocalizedString("SmtpSettingsService.UpdateExceptionMessage", ex.Message, StatusCodes.Status500InternalServerError));
            }
        }

        public async Task<SmtpSettingsRuntimeDto> GetRuntimeAsync()
        {
            // DB kontrol yok: cache varsa direkt dön
            if (_cache.TryGetValue(CacheKey, out SmtpSettingsRuntimeDto? cached) && cached != null)
                return cached;

            // cache yoksa DB’den al
            var entity = await _unitOfWork.SmtpSettings
                .Query()
                .AsNoTracking()
                .Where(x => x.Id == 1 && !x.IsDeleted)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            if (entity == null)
                throw new InvalidOperationException(
                    _localizationService.GetLocalizedString("SmtpSettingsService.SmtpSettingsMissingInDatabase"));

            var password = string.IsNullOrWhiteSpace(entity.PasswordEncrypted)
                ? ""
                : _protector.Unprotect(entity.PasswordEncrypted);

            var runtime = new SmtpSettingsRuntimeDto
            {
                Host = entity.Host ?? "",
                Port = entity.Port,
                EnableSsl = entity.EnableSsl,
                Username = entity.Username ?? "",
                Password = password,
                FromEmail = entity.FromEmail ?? "",
                FromName = entity.FromName ?? "",
                Timeout = entity.Timeout
            };

            _cache.Set(CacheKey, runtime); // süresiz (instance-local)

            return runtime;
        }
    }
}
