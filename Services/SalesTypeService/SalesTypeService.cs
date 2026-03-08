using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class SalesTypeService : ISalesTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public SalesTypeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<SalesTypeGetDto>>> GetAllSalesTypesAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.SalesTypeDefinitions.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(SalesTypeDefinition.Id), request.SortDirection);

                var totalCount = await query.CountAsync();
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync();

                var dtos = items.Select(x => _mapper.Map<SalesTypeGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<SalesTypeGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<SalesTypeGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("SalesTypeService.SalesTypesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<SalesTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SalesTypeGetDto>> GetSalesTypeByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.SalesTypeDefinitions.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var withNav = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                return ApiResponse<SalesTypeGetDto>.SuccessResult(
                    _mapper.Map<SalesTypeGetDto>(withNav ?? entity),
                    _localizationService.GetLocalizedString("SalesTypeService.SalesTypeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<SalesTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SalesTypeGetDto>> CreateSalesTypeAsync(SalesTypeCreateDto createSalesTypeDto)
        {
            try
            {
                var normalizedSalesType = NormalizeSalesType(createSalesTypeDto.SalesType);
                if (normalizedSalesType == null)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeInvalid"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeInvalid"),
                        StatusCodes.Status400BadRequest);
                }

                var normalizedName = (createSalesTypeDto.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.NameRequired"),
                        _localizationService.GetLocalizedString("General.NameRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var exists = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AnyAsync(x => !x.IsDeleted && x.SalesType == normalizedSalesType && x.Name == normalizedName);
                if (exists)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeAlreadyExists"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = _mapper.Map<SalesTypeDefinition>(createSalesTypeDto);
                entity.SalesType = normalizedSalesType;
                entity.Name = normalizedName;
                entity.CreatedDate = DateTime.UtcNow;

                await _unitOfWork.SalesTypeDefinitions.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var created = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDeleted);

                return ApiResponse<SalesTypeGetDto>.SuccessResult(
                    _mapper.Map<SalesTypeGetDto>(created ?? entity),
                    _localizationService.GetLocalizedString("SalesTypeService.SalesTypeCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<SalesTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<SalesTypeGetDto>> UpdateSalesTypeAsync(long id, SalesTypeUpdateDto updateSalesTypeDto)
        {
            try
            {
                var entity = await _unitOfWork.SalesTypeDefinitions.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var normalizedSalesType = NormalizeSalesType(updateSalesTypeDto.SalesType);
                if (normalizedSalesType == null)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeInvalid"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeInvalid"),
                        StatusCodes.Status400BadRequest);
                }

                var normalizedName = (updateSalesTypeDto.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("General.NameRequired"),
                        _localizationService.GetLocalizedString("General.NameRequired"),
                        StatusCodes.Status400BadRequest);
                }

                var exists = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AnyAsync(x => !x.IsDeleted && x.Id != id && x.SalesType == normalizedSalesType && x.Name == normalizedName);
                if (exists)
                {
                    return ApiResponse<SalesTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeAlreadyExists"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                entity.SalesType = normalizedSalesType;
                entity.Name = normalizedName;
                entity.UpdatedDate = DateTime.UtcNow;

                await _unitOfWork.SalesTypeDefinitions.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var updated = await _unitOfWork.SalesTypeDefinitions.Query()
                    .AsNoTracking()
                    .Include(x => x.CreatedByUser)
                    .Include(x => x.UpdatedByUser)
                    .Include(x => x.DeletedByUser)
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                return ApiResponse<SalesTypeGetDto>.SuccessResult(
                    _mapper.Map<SalesTypeGetDto>(updated ?? entity),
                    _localizationService.GetLocalizedString("SalesTypeService.SalesTypeUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<SalesTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteSalesTypeAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.SalesTypeDefinitions.GetByIdAsync(id);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        _localizationService.GetLocalizedString("SalesTypeService.SalesTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.SalesTypeDefinitions.SoftDeleteAsync(id);
                await _unitOfWork.SaveChangesAsync();

                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("SalesTypeService.SalesTypeDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("General.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        private static string? NormalizeSalesType(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var normalized = input.Trim().ToUpperInvariant();
            return Enum.TryParse<SalesTypeEnum>(normalized, ignoreCase: true, out _) ? normalized : null;
        }
    }
}
