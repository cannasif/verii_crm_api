using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models.PowerBi;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using crm_api.DTOs.PowerBi;

namespace crm_api.Services
{
    public class PowerBIConfigurationService : IPowerBIConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PowerBIConfigurationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PowerBIConfigurationGetDto?>> GetAsync()
        {
            try
            {
                var entity = await _unitOfWork.PowerBIConfigurations
                    .Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<PowerBIConfigurationGetDto?>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<PowerBIConfigurationGetDto>(entity);
                return ApiResponse<PowerBIConfigurationGetDto?>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.Retrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIConfigurationGetDto?>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIConfigurationGetDto>> CreateAsync(CreatePowerBIConfigurationDto dto)
        {
            try
            {
                var existing = await _unitOfWork.PowerBIConfigurations
                    .Query()
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (existing != null)
                {
                    return ApiResponse<PowerBIConfigurationGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.AlreadyExists"),
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.AlreadyExists"),
                        StatusCodes.Status409Conflict);
                }

                var entity = _mapper.Map<PowerBIConfiguration>(dto);
                await _unitOfWork.PowerBIConfigurations.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var created = await _unitOfWork.PowerBIConfigurations.GetByIdAsync(entity.Id).ConfigureAwait(false);
                var resultDto = _mapper.Map<PowerBIConfigurationGetDto>(created!);
                return ApiResponse<PowerBIConfigurationGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.Created"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIConfigurationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIConfigurationGetDto>> UpdateAsync(long id, UpdatePowerBIConfigurationDto dto)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIConfigurations.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIConfigurationGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);
                await _unitOfWork.PowerBIConfigurations.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updated = await _unitOfWork.PowerBIConfigurations.GetByIdAsync(id).ConfigureAwait(false);
                var resultDto = _mapper.Map<PowerBIConfigurationGetDto>(updated!);
                return ApiResponse<PowerBIConfigurationGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.Updated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIConfigurationGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIConfigurations.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        _localizationService.GetLocalizedString("PowerBIConfigurationService.NotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PowerBIConfigurations.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.Deleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIConfigurationService.ExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
