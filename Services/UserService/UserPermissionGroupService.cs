using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models.UserPermissions;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class UserPermissionGroupService : IUserPermissionGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILocalizationService _localizationService;

        public UserPermissionGroupService(IUnitOfWork unitOfWork, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<UserPermissionGroupDto>> GetByUserIdAsync(long userId)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId).ConfigureAwait(false);
                if (user == null)
                {
                    return ApiResponse<UserPermissionGroupDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var links = await _unitOfWork.UserPermissionGroups.Query()
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && !x.IsDeleted)
                    .Include(x => x.PermissionGroup)
                    .ToListAsync().ConfigureAwait(false);

                var dto = new UserPermissionGroupDto
                {
                    UserId = userId,
                    PermissionGroupIds = links.Select(x => x.PermissionGroupId).Distinct().OrderBy(x => x).ToList(),
                    PermissionGroupNames = links
                        .Where(x => x.PermissionGroup != null)
                        .Select(x => x.PermissionGroup.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList()
                };

                return ApiResponse<UserPermissionGroupDto>.SuccessResult(dto, _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserPermissionGroupDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserPermissionGroupDto>> SetUserGroupsAsync(long userId, SetUserPermissionGroupsDto dto)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId).ConfigureAwait(false);
                if (user == null)
                {
                    return ApiResponse<UserPermissionGroupDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var distinctGroupIds = dto.PermissionGroupIds.Distinct().ToList();
                if (distinctGroupIds.Count > 0)
                {
                    var validCount = await _unitOfWork.PermissionGroups.Query()
                        .AsNoTracking()
                        .CountAsync(x => !x.IsDeleted && distinctGroupIds.Contains(x.Id)).ConfigureAwait(false);

                    if (validCount != distinctGroupIds.Count)
                    {
                        return ApiResponse<UserPermissionGroupDto>.ErrorResult(
                            _localizationService.GetLocalizedString("General.ValidationError"),
                            _localizationService.GetLocalizedString("General.ValidationError"),
                            StatusCodes.Status400BadRequest);
                    }
                }

                var currentLinks = await _unitOfWork.UserPermissionGroups
                    .Query(tracking: true, ignoreQueryFilters: true)
                    .Where(x => x.UserId == userId)
                    .ToListAsync().ConfigureAwait(false);

                foreach (var link in currentLinks.Where(x => !x.IsDeleted && !distinctGroupIds.Contains(x.PermissionGroupId)))
                {
                    await _unitOfWork.UserPermissionGroups.SoftDeleteAsync(link.Id).ConfigureAwait(false);
                }

                foreach (var groupId in distinctGroupIds)
                {
                    var existing = currentLinks.FirstOrDefault(x => x.PermissionGroupId == groupId);
                    if (existing == null)
                    {
                        await _unitOfWork.UserPermissionGroups.AddAsync(new UserPermissionGroup
                        {
                            UserId = userId,
                            PermissionGroupId = groupId
                        }).ConfigureAwait(false);
                        continue;
                    }

                    if (existing.IsDeleted)
                    {
                        existing.IsDeleted = false;
                        existing.DeletedDate = null;
                        existing.DeletedBy = null;
                        await _unitOfWork.UserPermissionGroups.UpdateAsync(existing).ConfigureAwait(false);
                    }
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return await GetByUserIdAsync(userId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ApiResponse<UserPermissionGroupDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
