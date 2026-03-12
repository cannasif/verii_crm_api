using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using crm_api.Helpers;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class ProductPricingGroupByService : IProductPricingGroupByService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ProductPricingGroupByService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ProductPricingGroupByDto>>> GetAllProductPricingGroupBysAsync(PagedRequest request)
        {
            try
            {
                if (request == null)
                {
                    request = new PagedRequest();
                }

                if (request.Filters == null)
                {
                    request.Filters = new List<Filter>();
                }

                var query = _unitOfWork.ProductPricingGroupBys.Query()
                    .AsNoTracking()
                    .Where(ppgb => !ppgb.IsDeleted)
                    .Include(ppgb => ppgb.CreatedByUser)
                    .Include(ppgb => ppgb.UpdatedByUser)
                    .Include(ppgb => ppgb.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(ProductPricingGroupBy.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ProductPricingGroupByDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ProductPricingGroupByDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ProductPricingGroupByDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupBysRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ProductPricingGroupByDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.GetAllProductPricingGroupBysExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGroupByDto>> GetProductPricingGroupByByIdAsync(int id)
        {
            try
            {
                var productPricingGroupBy = await _unitOfWork.ProductPricingGroupBys.GetByIdAsync(id).ConfigureAwait(false);
                if (productPricingGroupBy == null)
                {
                    return ApiResponse<ProductPricingGroupByDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var productPricingGroupByWithNav = await _unitOfWork.ProductPricingGroupBys.Query()
                    .AsNoTracking()
                    .Include(ppgb => ppgb.CreatedByUser)
                    .Include(ppgb => ppgb.UpdatedByUser)
                    .Include(ppgb => ppgb.DeletedByUser)
                    .FirstOrDefaultAsync(ppgb => ppgb.Id == id && !ppgb.IsDeleted).ConfigureAwait(false);

                var productPricingGroupByDto = _mapper.Map<ProductPricingGroupByDto>(productPricingGroupByWithNav ?? productPricingGroupBy);
                return ApiResponse<ProductPricingGroupByDto>.SuccessResult(productPricingGroupByDto, _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGroupByDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.GetProductPricingGroupByByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGroupByDto>> CreateProductPricingGroupByAsync(CreateProductPricingGroupByDto createDto)
        {
            try
            {
                // Aynı ErpGroupCode ile mevcut kayıt var mı kontrol et (silinmiş dahil)
                var existing = await _unitOfWork.ProductPricingGroupBys.Query(tracking: true, ignoreQueryFilters: true)
                    .FirstOrDefaultAsync(ppgb => ppgb.ErpGroupCode == createDto.ErpGroupCode).ConfigureAwait(false);

                ProductPricingGroupBy productPricingGroupBy;

                if (existing != null)
                {
                    // Var olan kaydı geri yükle ve güncelle (upsert)
                    _mapper.Map(createDto, existing);
                    existing.IsDeleted = false;
                    existing.DeletedDate = null;
                    existing.DeletedBy = null;

                    await _unitOfWork.ProductPricingGroupBys.UpdateAsync(existing).ConfigureAwait(false);
                    productPricingGroupBy = existing;
                }
                else
                {
                    // Yeni kayıt oluştur
                    productPricingGroupBy = _mapper.Map<ProductPricingGroupBy>(createDto);
                    productPricingGroupBy.CreatedDate = DateTimeProvider.Now;
                    productPricingGroupBy.IsDeleted = false;

                    await _unitOfWork.ProductPricingGroupBys.AddAsync(productPricingGroupBy).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var productPricingGroupByWithNav = await _unitOfWork.ProductPricingGroupBys.Query()
                    .AsNoTracking()
                    .Include(ppgb => ppgb.CreatedByUser)
                    .Include(ppgb => ppgb.UpdatedByUser)
                    .Include(ppgb => ppgb.DeletedByUser)
                    .FirstOrDefaultAsync(ppgb => ppgb.Id == productPricingGroupBy.Id && !ppgb.IsDeleted).ConfigureAwait(false);

                var productPricingGroupByDto = _mapper.Map<ProductPricingGroupByDto>(productPricingGroupByWithNav ?? productPricingGroupBy);
                var messageKey = existing != null ? "ProductPricingGroupByService.ProductPricingGroupByUpdated" : "ProductPricingGroupByService.ProductPricingGroupByCreated";
                return ApiResponse<ProductPricingGroupByDto>.SuccessResult(productPricingGroupByDto, _localizationService.GetLocalizedString(messageKey));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGroupByDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.CreateProductPricingGroupByExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGroupByDto>> UpdateProductPricingGroupByAsync(int id, UpdateProductPricingGroupByDto updateDto)
        {
            try
            {
                var existingProductPricingGroupBy = await _unitOfWork.ProductPricingGroupBys.GetByIdAsync(id).ConfigureAwait(false);
                if (existingProductPricingGroupBy == null)
                {
                    return ApiResponse<ProductPricingGroupByDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, existingProductPricingGroupBy);
                existingProductPricingGroupBy.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.ProductPricingGroupBys.UpdateAsync(existingProductPricingGroupBy).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var productPricingGroupByWithNav = await _unitOfWork.ProductPricingGroupBys.Query()
                    .AsNoTracking()
                    .Include(ppgb => ppgb.CreatedByUser)
                    .Include(ppgb => ppgb.UpdatedByUser)
                    .Include(ppgb => ppgb.DeletedByUser)
                    .FirstOrDefaultAsync(ppgb => ppgb.Id == existingProductPricingGroupBy.Id && !ppgb.IsDeleted).ConfigureAwait(false);

                var productPricingGroupByDto = _mapper.Map<ProductPricingGroupByDto>(productPricingGroupByWithNav ?? existingProductPricingGroupBy);
                return ApiResponse<ProductPricingGroupByDto>.SuccessResult(productPricingGroupByDto, _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGroupByDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.UpdateProductPricingGroupByExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteProductPricingGroupByAsync(int id)
        {
            try
            {
                var productPricingGroupBy = await _unitOfWork.ProductPricingGroupBys.GetByIdAsync(id).ConfigureAwait(false);
                if (productPricingGroupBy == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByNotFound"),
                        StatusCodes.Status404NotFound);
                }

                productPricingGroupBy.IsDeleted = true;
                productPricingGroupBy.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.ProductPricingGroupBys.UpdateAsync(productPricingGroupBy).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ProductPricingGroupByService.ProductPricingGroupByDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingGroupByService.DeleteProductPricingGroupByExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
