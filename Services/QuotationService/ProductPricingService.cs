using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace crm_api.Services
{
    public class ProductPricingService : IProductPricingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public ProductPricingService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<ProductPricingGetDto>>> GetAllProductPricingsAsync(PagedRequest request)
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

                var query = _unitOfWork.ProductPricings.Query()
                    .AsNoTracking()
                    .Where(pp => !pp.IsDeleted)
                    .Include(pp => pp.CreatedByUser)
                    .Include(pp => pp.UpdatedByUser)
                    .Include(pp => pp.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(ProductPricing.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<ProductPricingGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<ProductPricingGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<ProductPricingGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("ProductPricingService.ProductPricingsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<ProductPricingGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingService.GetAllProductPricingsExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGetDto>> GetProductPricingByIdAsync(long id)
        {
            try
            {
                var productPricing = await _unitOfWork.ProductPricings.GetByIdAsync(id).ConfigureAwait(false);
                if (productPricing == null)
                {
                    return ApiResponse<ProductPricingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var productPricingWithNav = await _unitOfWork.ProductPricings.Query()
                    .AsNoTracking()
                    .Include(pp => pp.CreatedByUser)
                    .Include(pp => pp.UpdatedByUser)
                    .Include(pp => pp.DeletedByUser)
                    .FirstOrDefaultAsync(pp => pp.Id == id && !pp.IsDeleted).ConfigureAwait(false);

                var productPricingDto = _mapper.Map<ProductPricingGetDto>(productPricingWithNav ?? productPricing);
                return ApiResponse<ProductPricingGetDto>.SuccessResult(productPricingDto, _localizationService.GetLocalizedString("ProductPricingService.ProductPricingRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingService.GetProductPricingByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGetDto>> CreateProductPricingAsync(ProductPricingCreateDto createProductPricingDto)
        {
            try
            {
                // Aynı ErpProductCode + ErpGroupCode ile mevcut kayıt var mı kontrol et (silinmiş dahil)
                var existing = await _unitOfWork.ProductPricings.Query(tracking: true, ignoreQueryFilters: true)
                    .FirstOrDefaultAsync(pp => pp.ErpProductCode == createProductPricingDto.ErpProductCode && pp.ErpGroupCode == createProductPricingDto.ErpGroupCode).ConfigureAwait(false);

                ProductPricing productPricing;

                if (existing != null)
                {
                    // Var olan kaydı geri yükle ve güncelle (upsert)
                    _mapper.Map(createProductPricingDto, existing);
                    existing.IsDeleted = false;
                    existing.DeletedDate = null;
                    existing.DeletedBy = null;

                    await _unitOfWork.ProductPricings.UpdateAsync(existing).ConfigureAwait(false);
                    productPricing = existing;
                }
                else
                {
                    // Yeni kayıt oluştur
                    productPricing = _mapper.Map<ProductPricing>(createProductPricingDto);
                    productPricing.CreatedDate = DateTimeProvider.Now;
                    productPricing.IsDeleted = false;

                    await _unitOfWork.ProductPricings.AddAsync(productPricing).ConfigureAwait(false);
                }

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var productPricingWithNav = await _unitOfWork.ProductPricings.Query()
                    .AsNoTracking()
                    .Include(pp => pp.CreatedByUser)
                    .Include(pp => pp.UpdatedByUser)
                    .Include(pp => pp.DeletedByUser)
                    .FirstOrDefaultAsync(pp => pp.Id == productPricing.Id && !pp.IsDeleted).ConfigureAwait(false);

                var productPricingDto = _mapper.Map<ProductPricingGetDto>(productPricingWithNav ?? productPricing);
                var messageKey = existing != null ? "ProductPricingService.ProductPricingUpdated" : "ProductPricingService.ProductPricingCreated";
                return ApiResponse<ProductPricingGetDto>.SuccessResult(productPricingDto, _localizationService.GetLocalizedString(messageKey));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingService.CreateProductPricingExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<ProductPricingGetDto>> UpdateProductPricingAsync(long id, ProductPricingUpdateDto updateProductPricingDto)
        {
            try
            {
                var existingProductPricing = await _unitOfWork.ProductPricings.GetByIdAsync(id).ConfigureAwait(false);
                if (existingProductPricing == null)
                {
                    return ApiResponse<ProductPricingGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateProductPricingDto, existingProductPricing);
                existingProductPricing.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.ProductPricings.UpdateAsync(existingProductPricing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var productPricingWithNav = await _unitOfWork.ProductPricings.Query()
                    .AsNoTracking()
                    .Include(pp => pp.CreatedByUser)
                    .Include(pp => pp.UpdatedByUser)
                    .Include(pp => pp.DeletedByUser)
                    .FirstOrDefaultAsync(pp => pp.Id == existingProductPricing.Id && !pp.IsDeleted).ConfigureAwait(false);

                var productPricingDto = _mapper.Map<ProductPricingGetDto>(productPricingWithNav ?? existingProductPricing);
                return ApiResponse<ProductPricingGetDto>.SuccessResult(productPricingDto, _localizationService.GetLocalizedString("ProductPricingService.ProductPricingUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductPricingGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingService.UpdateProductPricingExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteProductPricingAsync(long id)
        {
            try
            {
                var productPricing = await _unitOfWork.ProductPricings.GetByIdAsync(id).ConfigureAwait(false);
                if (productPricing == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        _localizationService.GetLocalizedString("ProductPricingService.ProductPricingNotFound"),
                        StatusCodes.Status404NotFound);
                }

                productPricing.IsDeleted = true;
                productPricing.DeletedDate = DateTimeProvider.Now;

                await _unitOfWork.ProductPricings.UpdateAsync(productPricing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("ProductPricingService.ProductPricingDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ProductPricingService.InternalServerError"),
                    _localizationService.GetLocalizedString("ProductPricingService.DeleteProductPricingExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
