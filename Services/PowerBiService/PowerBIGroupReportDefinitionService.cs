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
    public class PowerBIGroupReportDefinitionService : IPowerBIGroupReportDefinitionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PowerBIGroupReportDefinitionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PowerBIGroupReportDefinitionGetDto>>> GetAllAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.PowerBIGroupReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Group)
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PowerBIGroupReportDefinition.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PowerBIGroupReportDefinitionGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PowerBIGroupReportDefinitionGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PowerBIGroupReportDefinitionGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PowerBIGroupReportDefinitionGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.GetAllExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupReportDefinitionGetDto>> GetByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroupReportDefinitions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIGroupReportDefinitionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entityWithNav = await _unitOfWork.PowerBIGroupReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Group)
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var dto = _mapper.Map<PowerBIGroupReportDefinitionGetDto>(entityWithNav ?? entity);

                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.GetByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupReportDefinitionGetDto>> CreateAsync(CreatePowerBIGroupReportDefinitionDto dto)
        {
            try
            {
                var entity = _mapper.Map<PowerBIGroupReportDefinition>(dto);

                await _unitOfWork.PowerBIGroupReportDefinitions.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var createdEntity = await _unitOfWork.PowerBIGroupReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Group)
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIGroupReportDefinitionGetDto>(createdEntity ?? entity);

                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.CreateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PowerBIGroupReportDefinitionGetDto>> UpdateAsync(long id, UpdatePowerBIGroupReportDefinitionDto dto)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroupReportDefinitions.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<PowerBIGroupReportDefinitionGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(dto, entity);

                await _unitOfWork.PowerBIGroupReportDefinitions.UpdateAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                var updatedEntity = await _unitOfWork.PowerBIGroupReportDefinitions
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Group)
                    .Include(x => x.ReportDefinition)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);

                var resultDto = _mapper.Map<PowerBIGroupReportDefinitionGetDto>(updatedEntity ?? entity);

                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.SuccessResult(
                    resultDto,
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PowerBIGroupReportDefinitionGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.UpdateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.PowerBIGroupReportDefinitions.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PowerBIGroupReportDefinitions.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.ItemDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.InternalServerError"),
                    _localizationService.GetLocalizedString("PowerBIGroupReportDefinitionService.DeleteExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
