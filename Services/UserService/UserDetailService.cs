using AutoMapper;
using Microsoft.EntityFrameworkCore;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography.X509Certificates;

namespace crm_api.Services
{
    public class UserDetailService : IUserDetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IFileUploadService _fileUploadService;

        public UserDetailService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IFileUploadService fileUploadService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _fileUploadService = fileUploadService;
        }

        public async Task<ApiResponse<UserDetailDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.UserDetails.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserDetails.Query()
                    .Where(x => x.Id == id && !x.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                var dto = _mapper.Map<UserDetailDto>(entityWithNav ?? entity);
                return ApiResponse<UserDetailDto>.SuccessResult(dto, _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDetailDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDetailDto>> GetByUserIdAsync(long userId)
        {
            try
            {
                var entity = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && !x.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync().ConfigureAwait(false);

                var dto = _mapper.Map<UserDetailDto>(entity);
                return ApiResponse<UserDetailDto>.SuccessResult(dto, _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDetailDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<IEnumerable<UserDetailDto>>> GetAllAsync()
        {
            try
            {
                var entities = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = _mapper.Map<IEnumerable<UserDetailDto>>(entities);
                return ApiResponse<IEnumerable<UserDetailDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<IEnumerable<UserDetailDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagedResponse<UserDetailDto>>> GetPagedAsync(PagedRequest request)
        {
            try
            {
                if (request.PageNumber < 1) request.PageNumber = 1;
                if (request.PageSize < 1) request.PageSize = 20;

                var query = _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Where(u => !u.IsDeleted)
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(UserDetail.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = _mapper.Map<List<UserDetailDto>>(items);
                var result = new PagedResponse<UserDetailDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<UserDetailDto>>.SuccessResult(result, _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<UserDetailDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailRetrievalExceptionMessage", ex.Message ?? string.Empty),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDetailDto>> CreateAsync(CreateUserDetailDto dto)
        {
            try
            {
                // Check if user exists
                var user = await _unitOfWork.Users.GetByIdAsync(dto.UserId).ConfigureAwait(false);
                if (user == null || user.IsDeleted)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserDetailService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Check if user detail already exists
                var existingDetail = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .FirstOrDefaultAsync(x => x.UserId == dto.UserId && !x.IsDeleted).ConfigureAwait(false);

                if (existingDetail != null)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailAlreadyExists"),
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = _mapper.Map<UserDetail>(dto);
                await _unitOfWork.UserDetails.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == entity.Id && !u.IsDeleted).ConfigureAwait(false);

                var result = _mapper.Map<UserDetailDto>(entityWithNav ?? entity);
                return ApiResponse<UserDetailDto>.SuccessResult(result, _localizationService.GetLocalizedString("UserDetailService.UserDetailCreatedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDetailDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailCreationError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailCreationExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDetailDto>> UpdateAsync(long id, UpdateUserDetailDto dto)
        {
            try
            {
                var entity = await _unitOfWork.UserDetails.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // If profile picture exists in entity and is being changed or removed, delete old one
                if (!string.IsNullOrEmpty(entity.ProfilePictureUrl) && 
                    (string.IsNullOrEmpty(dto.ProfilePictureUrl) || entity.ProfilePictureUrl != dto.ProfilePictureUrl))
                {
                    // Delete old profile picture before updating
                    await _fileUploadService.DeleteProfilePictureAsync(entity.ProfilePictureUrl).ConfigureAwait(false);
                }

                _mapper.Map(dto, entity);
                entity.UpdatedDate = DateTime.UtcNow;
                
                await _unitOfWork.UserDetails.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var entityWithNav = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Include(u => u.CreatedByUser)
                    .Include(u => u.UpdatedByUser)
                    .Include(u => u.DeletedByUser)
                    .FirstOrDefaultAsync(u => u.Id == entity.Id && !u.IsDeleted).ConfigureAwait(false);

                var result = _mapper.Map<UserDetailDto>(entityWithNav ?? entity);
                return ApiResponse<UserDetailDto>.SuccessResult(result, _localizationService.GetLocalizedString("UserDetailService.UserDetailUpdatedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDetailDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailUpdateError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailUpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.UserDetails.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        _localizationService.GetLocalizedString("UserDetailService.UserDetailNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Delete profile picture if exists
                if (!string.IsNullOrEmpty(entity.ProfilePictureUrl))
                {
                    await _fileUploadService.DeleteProfilePictureAsync(entity.ProfilePictureUrl).ConfigureAwait(false);
                }

                entity.IsDeleted = true;
                entity.DeletedDate = DateTime.UtcNow;
                await _unitOfWork.UserDetails.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("UserDetailService.UserDetailDeletedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailDeletionError"),
                    _localizationService.GetLocalizedString("UserDetailService.UserDetailDeletionExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserDetailDto>> UploadProfilePictureAsync(long userId, IFormFile file)
        {
            try
            {
                // Check if user exists
                var user = await _unitOfWork.Users.GetByIdAsync(userId).ConfigureAwait(false);
                if (user == null || user.IsDeleted)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserDetailService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserDetailService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Upload the file
                var uploadResult = await _fileUploadService.UploadProfilePictureAsync(file, userId).ConfigureAwait(false);
                if (!uploadResult.Success)
                {
                    return ApiResponse<UserDetailDto>.ErrorResult(
                        uploadResult.Message,
                        uploadResult.ExceptionMessage,
                        uploadResult.StatusCode);
                }

                // Get or create user detail
                var userDetail = await _unitOfWork.UserDetails.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted).ConfigureAwait(false);

                if (userDetail == null)
                {
                    // Create new user detail
                    userDetail = new UserDetail
                    {
                        UserId = userId,
                        ProfilePictureUrl = uploadResult.Data,
                        CreatedDate = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    await _unitOfWork.UserDetails.AddAsync(userDetail).ConfigureAwait(false);
                }
                else
                {
                    // Delete old profile picture if exists
                    if (!string.IsNullOrEmpty(userDetail.ProfilePictureUrl))
                    {
                        await _fileUploadService.DeleteProfilePictureAsync(userDetail.ProfilePictureUrl).ConfigureAwait(false);
                    }

                    // Update existing user detail
                    userDetail.ProfilePictureUrl = uploadResult.Data;
                    userDetail.UpdatedDate = DateTime.UtcNow;
                    await _unitOfWork.UserDetails.UpdateAsync(userDetail).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var result = _mapper.Map<UserDetailDto>(userDetail);
                return ApiResponse<UserDetailDto>.SuccessResult(result, _localizationService.GetLocalizedString("UserDetailService.ProfilePictureUploadedSuccessfully"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserDetailDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserDetailService.ProfilePictureUploadError"),
                    _localizationService.GetLocalizedString("UserDetailService.ProfilePictureUploadExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
