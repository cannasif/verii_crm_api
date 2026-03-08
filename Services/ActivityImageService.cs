using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class ActivityImageService : IActivityImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IFileUploadService _fileUploadService;

        public ActivityImageService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService,
            IFileUploadService fileUploadService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<List<ActivityImageDto>>> AddImagesAsync(List<CreateActivityImageDto> request)
        {
            try
            {
                if (request == null || request.Count == 0)
                {
                    return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.AtLeastOneImageRequired"),
                        _localizationService.GetLocalizedString("ActivityImageService.AtLeastOneImageRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var activityIds = request.Select(x => x.ActivityId).Distinct().ToList();
                var existingActivityIds = await _unitOfWork.Activities.Query(tracking: false)
                    .Where(x => activityIds.Contains(x.Id) && !x.IsDeleted)
                    .Select(x => x.Id)
                    .ToListAsync();

                if (existingActivityIds.Count != activityIds.Count)
                {
                    return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivitiesNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivitiesNotFound"),
                        StatusCodes.Status400BadRequest);
                }

                var entities = new List<ActivityImage>();
                foreach (var dto in request)
                {
                    var entity = _mapper.Map<ActivityImage>(dto);
                    entity.CreatedDate = DateTime.UtcNow;
                    await _unitOfWork.ActivityImages.AddAsync(entity);
                    entities.Add(entity);
                }

                await _unitOfWork.SaveChangesAsync();

                var response = entities.Select(x => _mapper.Map<ActivityImageDto>(x)).ToList();
                return ApiResponse<List<ActivityImageDto>>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("ActivityImageService.ActivityImagesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ActivityImageDto>>> UploadImagesAsync(long activityId, List<IFormFile> files, List<string>? resimAciklamalar = null)
        {
            try
            {
                if (files == null || files.Count == 0)
                {
                    return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.AtLeastOneImageFileRequired"),
                        _localizationService.GetLocalizedString("ActivityImageService.AtLeastOneImageFileRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var activityExists = await _unitOfWork.Activities.Query(tracking: false)
                    .AnyAsync(x => x.Id == activityId && !x.IsDeleted);

                if (!activityExists)
                {
                    return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var uploaded = new List<ActivityImageDto>();

                await _unitOfWork.BeginTransactionAsync();

                for (var i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var aciklama = resimAciklamalar != null && i < resimAciklamalar.Count
                        ? resimAciklamalar[i]
                        : null;

                    var uploadResult = await _fileUploadService.UploadActivityImageAsync(file, activityId);
                    if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Data))
                    {
                        await _unitOfWork.RollbackTransactionAsync();
                        return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                            uploadResult.Message ?? _localizationService.GetLocalizedString("ActivityImageService.FileUploadFailed"),
                            uploadResult.ExceptionMessage,
                            uploadResult.StatusCode);
                    }

                    var entity = new ActivityImage
                    {
                        ActivityId = activityId,
                        ResimAciklama = aciklama,
                        ResimUrl = uploadResult.Data,
                        CreatedDate = DateTime.UtcNow
                    };

                    await _unitOfWork.ActivityImages.AddAsync(entity);
                    uploaded.Add(_mapper.Map<ActivityImageDto>(entity));
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                return ApiResponse<List<ActivityImageDto>>.SuccessResult(
                    uploaded,
                    _localizationService.GetLocalizedString("ActivityImageService.ActivityImagesUploaded"));
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ActivityImageDto>>> GetByActivityIdAsync(long activityId)
        {
            try
            {
                var activityExists = await _unitOfWork.Activities.Query(tracking: false)
                    .AnyAsync(x => x.Id == activityId && !x.IsDeleted);

                if (!activityExists)
                {
                    return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var items = await _unitOfWork.ActivityImages.Query()
                    .Where(x => x.ActivityId == activityId && !x.IsDeleted)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .AsNoTracking()
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                var response = items.Select(x => _mapper.Map<ActivityImageDto>(x)).ToList();
                return ApiResponse<List<ActivityImageDto>>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("ActivityImageService.ActivityImagesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ActivityImageDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ActivityImageDto>> UpdateAsync(long id, UpdateActivityImageDto request)
        {
            try
            {
                var entity = await _unitOfWork.ActivityImages.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<ActivityImageDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityImageNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityImageNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var activityExists = await _unitOfWork.Activities.Query(tracking: false)
                    .AnyAsync(x => x.Id == request.ActivityId && !x.IsDeleted);

                if (!activityExists)
                {
                    return ApiResponse<ActivityImageDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityNotFound"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(request, entity);
                entity.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.ActivityImages.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var response = _mapper.Map<ActivityImageDto>(entity);
                return ApiResponse<ActivityImageDto>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("ActivityImageService.ActivityImageUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ActivityImageDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.ActivityImages.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityImageNotFound"),
                        _localizationService.GetLocalizedString("ActivityImageService.ActivityImageNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.ActivityImages.SoftDeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(entity.ResimUrl))
                {
                    await _fileUploadService.DeleteActivityImageAsync(entity.ResimUrl);
                }

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("ActivityImageService.ActivityImageDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ActivityService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
