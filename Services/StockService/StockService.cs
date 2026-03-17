using AutoMapper;
using crm_api.DTOs;
using crm_api.Helpers;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace crm_api.Services
{
    public class StockService : IStockService
    {
        private static readonly string[] SearchableColumns =
        [
            nameof(Stock.ErpStockCode),
            nameof(Stock.StockName),
            nameof(Stock.Unit),
            nameof(Stock.UreticiKodu),
            nameof(Stock.GrupKodu),
            nameof(Stock.GrupAdi),
            nameof(Stock.Kod1),
            nameof(Stock.Kod1Adi),
            nameof(Stock.Kod2),
            nameof(Stock.Kod2Adi),
            nameof(Stock.Kod3),
            nameof(Stock.Kod3Adi),
            nameof(Stock.Kod4),
            nameof(Stock.Kod4Adi),
            nameof(Stock.Kod5),
            nameof(Stock.Kod5Adi)
        ];

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;
        private readonly IErpService _erpService;

        public StockService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService, IErpService erpService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
            _erpService = erpService;
        }

        public async Task<ApiResponse<PagedResponse<StockGetDto>>> GetAllStocksAsync(PagedRequest request)
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

                var query = _unitOfWork.Stocks
                    .Query()
                    .Where(s => !s.IsDeleted)
                    .Include(s => s.StockDetail)
                    .Include(s => s.StockImages.Where(i => !i.IsDeleted))
                    .Include(s => s.ParentRelations.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.RelatedStock)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .ApplySearch(request.Search, SearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(Stock.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<StockGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<StockGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<StockGetDto>>.SuccessResult(
                    pagedResponse, 
                    _localizationService.GetLocalizedString("StockService.StocksRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<StockGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.GetAllStocksExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PagedResponse<StockGetWithMainImageDto>>> GetAllStocksWithImagesAsync(PagedRequest request)
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

                var query = _unitOfWork.Stocks
                    .Query()
                    .Where(s => !s.IsDeleted)
                    .Include(s => s.StockDetail)
                    .Include(s => s.StockImages.Where(i => !i.IsDeleted))
                    .Include(s => s.ParentRelations.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.RelatedStock)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .ApplySearch(request.Search, SearchableColumns)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(Stock.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var baseDtos = items.Select(x => _mapper.Map<StockGetDto>(x)).ToList();
                
                var dtos = baseDtos.Select(stockDto =>
                {
                    var stockWithMainImage = _mapper.Map<StockGetWithMainImageDto>(stockDto);
                    // Main image'ı bul (IsPrimary = true olan)
                    var mainImage = stockDto.StockImages?.FirstOrDefault(img => img.IsPrimary);
                    stockWithMainImage.MainImage = mainImage;
                    return stockWithMainImage;
                }).ToList();

                var pagedResponse = new PagedResponse<StockGetWithMainImageDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<StockGetWithMainImageDto>>.SuccessResult(
                    pagedResponse, 
                    _localizationService.GetLocalizedString("StockService.StocksRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<StockGetWithMainImageDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.GetAllStocksExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StockGetDto>> GetStockByIdAsync(long id)
        {
            try
            {
                var stock = await _unitOfWork.Stocks
                    .Query()
                    .Include(s => s.StockDetail)
                    .Include(s => s.StockImages.Where(i => !i.IsDeleted))
                    .Include(s => s.ParentRelations.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.RelatedStock)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted).ConfigureAwait(false);

                if (stock == null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var stockDto = _mapper.Map<StockGetDto>(stock);

                return ApiResponse<StockGetDto>.SuccessResult(
                    stockDto, 
                    _localizationService.GetLocalizedString("StockService.StockRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<StockGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.GetStockByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StockGetDto>> CreateStockAsync(StockCreateDto stockCreateDto)
        {
            try
            {
                // Business Rule: Check if ErpStockCode already exists
                var existingStock = await _unitOfWork.Stocks
                    .Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ErpStockCode == stockCreateDto.ErpStockCode && !s.IsDeleted).ConfigureAwait(false);

                if (existingStock != null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.ErpStockCodeAlreadyExists"),
                        _localizationService.GetLocalizedString("StockService.ErpStockCodeAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                var stock = _mapper.Map<Stock>(stockCreateDto);
                await _unitOfWork.Stocks.AddAsync(stock).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var stockWithNav = await _unitOfWork.Stocks
                    .Query()
                    .Include(s => s.StockDetail)
                    .Include(s => s.StockImages.Where(i => !i.IsDeleted))
                    .Include(s => s.ParentRelations.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.RelatedStock)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == stock.Id && !s.IsDeleted).ConfigureAwait(false);

                if (stockWithNav == null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var stockDto = _mapper.Map<StockGetDto>(stockWithNav);

                return ApiResponse<StockGetDto>.SuccessResult(
                    stockDto, 
                    _localizationService.GetLocalizedString("StockService.StockCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<StockGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.CreateStockExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<StockGetDto>> UpdateStockAsync(long id, StockUpdateDto stockUpdateDto)
        {
            try
            {
                var stock = await _unitOfWork.Stocks.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (stock == null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Business Rule: Check if ErpStockCode already exists (excluding current stock)
                var existingStock = await _unitOfWork.Stocks
                    .Query()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.ErpStockCode == stockUpdateDto.ErpStockCode && s.Id != id && !s.IsDeleted).ConfigureAwait(false);

                if (existingStock != null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.ErpStockCodeAlreadyExists"),
                        _localizationService.GetLocalizedString("StockService.ErpStockCodeAlreadyExists"),
                        StatusCodes.Status400BadRequest);
                }

                _mapper.Map(stockUpdateDto, stock);
                await _unitOfWork.Stocks.UpdateAsync(stock).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var stockWithNav = await _unitOfWork.Stocks
                    .Query()
                    .Include(s => s.StockDetail)
                    .Include(s => s.StockImages.Where(i => !i.IsDeleted))
                    .Include(s => s.ParentRelations.Where(r => !r.IsDeleted))
                        .ThenInclude(r => r.RelatedStock)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == stock.Id && !s.IsDeleted).ConfigureAwait(false);

                if (stockWithNav == null)
                {
                    return ApiResponse<StockGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var stockDto = _mapper.Map<StockGetDto>(stockWithNav);

                return ApiResponse<StockGetDto>.SuccessResult(
                    stockDto, 
                    _localizationService.GetLocalizedString("StockService.StockUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<StockGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.UpdateStockExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteStockAsync(long id)
        {
            try
            {
                var stock = await _unitOfWork.Stocks.GetByIdAsync(id).ConfigureAwait(false);
                if (stock == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        _localizationService.GetLocalizedString("StockService.StockNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.Stocks.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(
                    null, 
                    _localizationService.GetLocalizedString("StockService.StockDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("StockService.InternalServerError"),
                    _localizationService.GetLocalizedString("StockService.DeleteStockExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
                
        public async Task SyncStocksFromErpAsync()
        {
            var erpResponse = await _erpService.GetStoksAsync(null).ConfigureAwait(false);

            if (erpResponse?.Data == null || erpResponse.Data.Count == 0)
                return;

            // 🔹 ERP sync için TRACKING açık olmalı (update yapacağız)
            var existingStocks = await _unitOfWork.Stocks
                .Query(tracking:true)
                .Where(x => !x.IsDeleted)
                .ToDictionaryAsync(x => x.ErpStockCode).ConfigureAwait(false);

            var newStocks = new List<Stock>();
            var hasAnyChange = false;

            foreach (var erpStock in erpResponse.Data)
            {
                var code = erpStock.StokKodu?.Trim();
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                // RII_STOCK için null dönmemesi gereken alanlar: ERP null gelirse '' veya 0 atanır (customer ile aynı mantık)
                var stockName = erpStock.StokAdi ?? string.Empty;
                var unit = erpStock.OlcuBr1 ?? string.Empty;
                var ureticiKodu = erpStock.UreticiKodu ?? string.Empty;
                var grupKodu = erpStock.GrupKodu ?? string.Empty;
                var grupAdi = erpStock.GrupIsim ?? string.Empty;
                var kod1 = erpStock.Kod1 ?? string.Empty;
                var kod1Adi = erpStock.Kod1Adi ?? string.Empty;
                var kod2 = erpStock.Kod2 ?? string.Empty;
                var kod2Adi = erpStock.Kod2Adi ?? string.Empty;
                var kod3 = erpStock.Kod3 ?? string.Empty;
                var kod3Adi = erpStock.Kod3Adi ?? string.Empty;
                var kod4 = erpStock.Kod4 ?? string.Empty;
                var kod4Adi = erpStock.Kod4Adi ?? string.Empty;
                var kod5 = erpStock.Kod5 ?? string.Empty;
                var kod5Adi = erpStock.Kod5Adi ?? string.Empty;
                var branchCode = (int)erpStock.SubeKodu;

                // 🔹 INSERT
                if (!existingStocks.TryGetValue(code, out var stock))
                {
                    newStocks.Add(new Stock
                    {
                        ErpStockCode = code,
                        StockName = string.IsNullOrWhiteSpace(stockName) ? code : stockName,
                        Unit = unit,
                        UreticiKodu = ureticiKodu,
                        GrupKodu = grupKodu,
                        GrupAdi = grupAdi,
                        Kod1 = kod1,
                        Kod1Adi = kod1Adi,
                        Kod2 = kod2,
                        Kod2Adi = kod2Adi,
                        Kod3 = kod3,
                        Kod3Adi = kod3Adi,
                        Kod4 = kod4,
                        Kod4Adi = kod4Adi,
                        Kod5 = kod5,
                        Kod5Adi = kod5Adi,
                        BranchCode = branchCode,
                        IsDeleted = false
                    });

                    hasAnyChange = true;
                    continue;
                }

                // 🔹 UPDATE (ANY FIELD CHANGED)
                var newStockName = string.IsNullOrWhiteSpace(stockName) ? code : stockName;
                if (
                    stock.StockName != newStockName ||
                    stock.Unit != unit ||
                    stock.UreticiKodu != ureticiKodu ||
                    stock.GrupKodu != grupKodu ||
                    stock.GrupAdi != grupAdi ||
                    stock.Kod1 != kod1 ||
                    stock.Kod1Adi != kod1Adi ||
                    stock.Kod2 != kod2 ||
                    stock.Kod2Adi != kod2Adi ||
                    stock.Kod3 != kod3 ||
                    stock.Kod3Adi != kod3Adi ||
                    stock.Kod4 != kod4 ||
                    stock.Kod4Adi != kod4Adi ||
                    stock.Kod5 != kod5 ||
                    stock.Kod5Adi != kod5Adi ||
                    stock.BranchCode != branchCode
                )
                {
                    stock.StockName = newStockName;
                    stock.Unit = unit;
                    stock.UreticiKodu = ureticiKodu;
                    stock.GrupKodu = grupKodu;
                    stock.GrupAdi = grupAdi;
                    stock.Kod1 = kod1;
                    stock.Kod1Adi = kod1Adi;
                    stock.Kod2 = kod2;
                    stock.Kod2Adi = kod2Adi;
                    stock.Kod3 = kod3;
                    stock.Kod3Adi = kod3Adi;
                    stock.Kod4 = kod4;
                    stock.Kod4Adi = kod4Adi;
                    stock.Kod5 = kod5;
                    stock.Kod5Adi = kod5Adi;
                    stock.BranchCode = branchCode;
                    stock.UpdatedDate = DateTimeProvider.Now;
                    stock.UpdatedBy = null; // ERP sync: background job

                    hasAnyChange = true;
                }
            }

            // 🔴 GERÇEKTEN HİÇ DEĞİŞİKLİK YOKSA
            if (!hasAnyChange)
                return;

            try
            {
                await _unitOfWork.BeginTransactionAsync().ConfigureAwait(false);

                if (newStocks.Count > 0)
                    await _unitOfWork.Stocks.AddAllAsync(newStocks).ConfigureAwait(false);

                // Update için ayrıca çağrı yok
                // EF ChangeTracker zaten değişenleri takip ediyor

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                await _unitOfWork.CommitTransactionAsync().ConfigureAwait(false);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync().ConfigureAwait(false);
                throw;
            }
        }

    }
}
