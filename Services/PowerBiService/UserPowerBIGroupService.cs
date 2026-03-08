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
    public class UserPowerBIGroupService : IUserPowerBIGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public UserPowerBIGroupService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<UserPowerBIGroupGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.UserPowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.Group)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(UserPowerBIGroup.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<UserPowerBIGroupGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<UserPowerBIGroupGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<UserPowerBIGroupGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<UserPowerBIGroupGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserPowerBIGroupGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.UserPowerBIGroups.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<UserPowerBIGroupGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entityWithNav = await _unitOfWork.UserPowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.Group)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var dto = _mapper.Map<UserPowerBIGroupGetDto>(entityWithNav ?? entity);

                return ApiResponse<UserPowerBIGroupGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserPowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserPowerBIGroupGetDto>> CreateAsync(CreateUserPowerBIGroupDto dto)
        {
            try
            {
                var entity = _mapper.Map<UserPowerBIGroup>(dto);

                await _unitOfWork.UserPowerBIGroups.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var createdEntity = await _unitOfWork.UserPowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.Group)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);

                var resultDto = _mapper.Map<UserPowerBIGroupGetDto>(createdEntity ?? entity);

                return ApiResponse<UserPowerBIGroupGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserPowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<UserPowerBIGroupGetDto>> UpdateAsync(long id, UpdateUserPowerBIGroupDto dto)
        {
            try
            {
                var entity = await _unitOfWork.UserPowerBIGroups.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<UserPowerBIGroupGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);

                await _unitOfWork.UserPowerBIGroups.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updatedEntity = await _unitOfWork.UserPowerBIGroups
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.Group)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var resultDto = _mapper.Map<UserPowerBIGroupGetDto>(updatedEntity ?? entity);

                return ApiResponse<UserPowerBIGroupGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<UserPowerBIGroupGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.UserPowerBIGroups.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.UserPowerBIGroups.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.ItemDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.InternalServerError"),
                    _localizationService.GetLocalizedString("UserPowerBIGroupService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
