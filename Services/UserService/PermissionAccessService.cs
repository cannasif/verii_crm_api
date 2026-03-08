using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace crm_api.Services
{
    public class PermissionAccessService : IPermissionAccessService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionAccessService(
            IUnitOfWork unitOfWork,
            ILocalizationService localizationService,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _localizationService = localizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResponse<MyPermissionsDto>> GetMyPermissionsAsync()
        {
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return ApiResponse<MyPermissionsDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.Unauthorized"),
                        _localizationService.GetLocalizedString("General.Unauthorized"),
                        StatusCodes.Status401Unauthorized);
                }

                var user = await _unitOfWork.Users.Query()
                    .AsNoTracking()
                    .Include(x => x.RoleNavigation)
                    .FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted).ConfigureAwait(false);

                if (user == null)
                {
                    return ApiResponse<MyPermissionsDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        _localizationService.GetLocalizedString("UserService.UserNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var userGroupLinks = await _unitOfWork.UserPermissionGroups.Query()
                    .AsNoTracking()
                    .Where(x => x.UserId == userId && !x.IsDeleted)
                    .Include(x => x.PermissionGroup)
                    .ThenInclude(x => x.GroupPermissions.Where(gp => !gp.IsDeleted))
                    .ThenInclude(x => x.PermissionDefinition)
                    .ToListAsync().ConfigureAwait(false);

                var roleTitle = user.RoleNavigation?.Title ?? "User";
                var isSystemAdmin = userGroupLinks.Any(x => x.PermissionGroup.IsSystemAdmin);

                if (!isSystemAdmin && userGroupLinks.Count == 0 &&
                    roleTitle.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    isSystemAdmin = true;
                }

                var permissionCodes = isSystemAdmin
                    ? new List<string>()
                    : userGroupLinks
                        .SelectMany(x => x.PermissionGroup.GroupPermissions)
                        .Where(x => !x.IsDeleted && x.PermissionDefinition != null && !x.PermissionDefinition.IsDeleted && x.PermissionDefinition.IsActive)
                        .Select(x => x.PermissionDefinition.Code)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                var response = new MyPermissionsDto
                {
                    UserId = userId,
                    RoleTitle = roleTitle,
                    IsSystemAdmin = isSystemAdmin,
                    PermissionGroups = userGroupLinks
                        .Select(x => x.PermissionGroup.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList(),
                    PermissionCodes = permissionCodes
                };

                return ApiResponse<MyPermissionsDto>.SuccessResult(
                    response,
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<MyPermissionsDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
