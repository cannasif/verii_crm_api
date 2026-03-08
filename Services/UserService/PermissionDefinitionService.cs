using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models.UserPermissions;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class PermissionDefinitionService : IPermissionDefinitionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILocalizationService _localizationService;

        public PermissionDefinitionService(IUnitOfWork unitOfWork, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PermissionDefinitionDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.PermissionDefinitions.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(PermissionDefinition.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query.ApplyPagination(request.PageNumber, request.PageSize).ToListAsync().ConfigureAwait(false);

                var dtoItems = items.Select(x => new PermissionDefinitionDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    CreatedDate = x.CreatedDate,
                    UpdatedDate = x.UpdatedDate,
                    DeletedDate = x.DeletedDate,
                    IsDeleted = x.IsDeleted,
                    CreatedByFullUser = x.CreatedByUser != null ? $"{x.CreatedByUser.FirstName} {x.CreatedByUser.LastName}".Trim() : null,
                    UpdatedByFullUser = x.UpdatedByUser != null ? $"{x.UpdatedByUser.FirstName} {x.UpdatedByUser.LastName}".Trim() : null,
                    DeletedByFullUser = x.DeletedByUser != null ? $"{x.DeletedByUser.FirstName} {x.DeletedByUser.LastName}".Trim() : null
                }).ToList();

                return ApiResponse<PagedResponse<PermissionDefinitionDto>>.SuccessResult(
                    new PagedResponse<PermissionDefinitionDto>
                    {
                        Items = dtoItems,
                        TotalCount = totalCount,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize
                    },
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PermissionDefinitionDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PermissionDefinitionDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PermissionDefinitions.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted).ConfigureAwait(false);

                if (entity == null)
                {
                    return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status404NotFound);
                }

                return ApiResponse<PermissionDefinitionDto>.SuccessResult(
                    new PermissionDefinitionDto
                    {
                        Id = entity.Id,
                        Code = entity.Code,
                        Name = entity.Name,
                        Description = entity.Description,
                        IsActive = entity.IsActive,
                        CreatedDate = entity.CreatedDate,
                        UpdatedDate = entity.UpdatedDate,
                        DeletedDate = entity.DeletedDate,
                        IsDeleted = entity.IsDeleted,
                        CreatedByFullUser = entity.CreatedByUser != null ? $"{entity.CreatedByUser.FirstName} {entity.CreatedByUser.LastName}".Trim() : null,
                        UpdatedByFullUser = entity.UpdatedByUser != null ? $"{entity.UpdatedByUser.FirstName} {entity.UpdatedByUser.LastName}".Trim() : null,
                        DeletedByFullUser = entity.DeletedByUser != null ? $"{entity.DeletedByUser.FirstName} {entity.DeletedByUser.LastName}".Trim() : null
                    },
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PermissionDefinitionDto>> CreateAsync(CreatePermissionDefinitionDto dto)
        {
            try
            {
                var exists = await _unitOfWork.PermissionDefinitions.Query()
                    .AsNoTracking()
                    .AnyAsync(x => !x.IsDeleted && x.Code == dto.Code).ConfigureAwait(false);

                if (exists)
                {
                    return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = new PermissionDefinition
                {
                    Code = dto.Code.Trim(),
                    Name = dto.Name.Trim(),
                    Description = dto.Description?.Trim(),
                    IsActive = dto.IsActive
                };

                await _unitOfWork.PermissionDefinitions.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return await GetByIdAsync(entity.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PermissionDefinitionDto>> UpdateAsync(long id, UpdatePermissionDefinitionDto dto)
        {
            try
            {
                var entity = await _unitOfWork.PermissionDefinitions.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status404NotFound);
                }

                if (!string.IsNullOrWhiteSpace(dto.Code) && !dto.Code.Equals(entity.Code, StringComparison.OrdinalIgnoreCase))
                {
                    var duplicate = await _unitOfWork.PermissionDefinitions.Query()
                        .AsNoTracking()
                        .AnyAsync(x => !x.IsDeleted && x.Id != id && x.Code == dto.Code).ConfigureAwait(false);

                    if (duplicate)
                    {
                        return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                            _localizationService.GetLocalizedString("General.ValidationError"),
                            _localizationService.GetLocalizedString("General.ValidationError"),
                            StatusCodes.Status400BadRequest);
                    }

                    entity.Code = dto.Code.Trim();
                }

                if (!string.IsNullOrWhiteSpace(dto.Name))
                {
                    entity.Name = dto.Name.Trim();
                }

                if (dto.Description != null)
                {
                    entity.Description = dto.Description.Trim();
                }

                if (dto.IsActive.HasValue)
                {
                    entity.IsActive = dto.IsActive.Value;
                }

                await _unitOfWork.PermissionDefinitions.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return await GetByIdAsync(entity.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ApiResponse<PermissionDefinitionDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        

        public async Task<ApiResponse<PermissionDefinitionSyncResultDto>> SyncAsync(SyncPermissionDefinitionsDto dto)
        {
            try
            {
                dto ??= new SyncPermissionDefinitionsDto();
                dto.Items ??= new List<SyncPermissionDefinitionItemDto>();

                var normalized = dto.Items
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Code))
                    .Select(x => new SyncPermissionDefinitionItemDto
                    {
                        Code = x.Code.Trim(),
                        Name = string.IsNullOrWhiteSpace(x.Name) ? null : x.Name.Trim(),
                        Description = x.Description == null ? null : x.Description.Trim(),
                        IsActive = x.IsActive
                    })
                    .ToList();

                var distinctCodes = normalized
                    .Select(x => x.Code)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctCodes.Count == 0)
                {
                    return ApiResponse<PermissionDefinitionSyncResultDto>.SuccessResult(
                        new PermissionDefinitionSyncResultDto { CreatedCount = 0, UpdatedCount = 0, ReactivatedCount = 0, TotalProcessed = 0 },
                        _localizationService.GetLocalizedString("General.OperationSuccessful"));
                }

                var existingAll = await _unitOfWork.PermissionDefinitions.Query()
                    .IgnoreQueryFilters()
                    .Where(x => distinctCodes.Contains(x.Code))
                    .ToListAsync().ConfigureAwait(false);

                var existingByCode = existingAll
                    .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var created = 0;
                var updated = 0;
                var reactivated = 0;

                foreach (var group in normalized.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
                {
                    var item = group.First();

                    if (existingByCode.TryGetValue(item.Code, out var entity))
                    {
                        var reactivatedThisEntity = false;
                        if (dto.ReactivateSoftDeleted && entity.IsDeleted)
                        {
                            entity.IsDeleted = false;
                            entity.DeletedBy = null;
                            entity.DeletedDate = null;
                            reactivated++;
                            reactivatedThisEntity = true;
                        }

                        var changed = reactivatedThisEntity;

                        if (dto.UpdateExistingNames && !string.IsNullOrWhiteSpace(item.Name) && !string.Equals(entity.Name, item.Name, StringComparison.Ordinal))
                        {
                            entity.Name = item.Name;
                            changed = true;
                        }

                        if (dto.UpdateExistingDescriptions && item.Description != null && !string.Equals(entity.Description, item.Description, StringComparison.Ordinal))
                        {
                            entity.Description = item.Description;
                            changed = true;
                        }

                        if (dto.UpdateExistingIsActive && entity.IsActive != item.IsActive)
                        {
                            entity.IsActive = item.IsActive;
                            changed = true;
                        }

                        if (changed)
                        {
                            await _unitOfWork.PermissionDefinitions.UpdateAsync(entity).ConfigureAwait(false);
                            updated++;
                        }

                        continue;
                    }

                    var name = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.Code;

                    var newEntity = new PermissionDefinition
                    {
                        Code = item.Code,
                        Name = name,
                        Description = item.Description,
                        IsActive = item.IsActive
                    };

                    await _unitOfWork.PermissionDefinitions.AddAsync(newEntity).ConfigureAwait(false);
                    created++;
                }

if (created > 0 || updated > 0 || reactivated > 0)
                {
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                }

                return ApiResponse<PermissionDefinitionSyncResultDto>.SuccessResult(
                    new PermissionDefinitionSyncResultDto
                    {
                        CreatedCount = created,
                        UpdatedCount = updated,
                        ReactivatedCount = reactivated,
                        TotalProcessed = normalized.Count
                    },
                    _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PermissionDefinitionSyncResultDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<bool>> SoftDeleteAsync(long id)
        {
            try
            {
                var exists = await _unitOfWork.PermissionDefinitions.ExistsAsync(id).ConfigureAwait(false);
                if (!exists)
                {
                    return ApiResponse<bool>.ErrorResult(
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        _localizationService.GetLocalizedString("General.ValidationError"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PermissionDefinitions.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<bool>.SuccessResult(true, _localizationService.GetLocalizedString("General.OperationSuccessful"));
            }
            catch (Exception ex)
            {
                return ApiResponse<bool>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
