using AutoMapper;
using crm_api.Data;
using crm_api.DTOs;
using crm_api.DTOs.ErpDto;
using crm_api.Interfaces;
using crm_api.UnitOfWork;
using depoWebAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;

namespace crm_api.Services
{
    public class ErpService : IErpService
    {
        private readonly ErpCmsDbContext _erpContext;
        private readonly CmsDbContext _cmsContext;
        private readonly ILogger<ErpService> _logger;
        private readonly ILocalizationService _localizationService;
        private readonly IMapper _mapper;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ErpService(ErpCmsDbContext erpContext, CmsDbContext cmsContext, ILogger<ErpService> logger, ILocalizationService localizationService, IMapper mapper, IHttpContextAccessor httpContextAccessor)
        {
            _erpContext = erpContext;
            _cmsContext = cmsContext;
            _logger = logger;
            _localizationService = localizationService;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<ApiResponse<short>> GetBranchCodeFromContext()
        {
            var branchCodeStr = _httpContextAccessor.HttpContext?.Items["BranchCode"]?.ToString();

            if (!short.TryParse(branchCodeStr, out var branchCode))
                return Task.FromResult(ApiResponse<short>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.BranchCodeRetrievalError"),
                    _localizationService.GetLocalizedString("ErpService.BranchCodeRetrievalErrorMessage"),
                    StatusCodes.Status500InternalServerError));

            return Task.FromResult(ApiResponse<short>.SuccessResult(
                branchCode,
                _localizationService.GetLocalizedString("ErpService.BranchCodeRetrieved")));
        }



        
        // Cari işlemleri - DTO dönen versiyon
        public async Task<ApiResponse<List<CariDto>>> GetCarisAsync(string? cariKodu)
        {
            try
            {
                var subeFromContext = _httpContextAccessor.HttpContext?.Items["BranchCode"] as string;
                var subeKodu = string.IsNullOrWhiteSpace(subeFromContext) ? null : subeFromContext;

                var result = await _erpContext.RII_FN_CARI
                    .FromSqlRaw(
                        "SELECT * FROM dbo.RII_FN_CARI({0}, {1})",
                        string.IsNullOrWhiteSpace(cariKodu) ? DBNull.Value : cariKodu,
                        string.IsNullOrWhiteSpace(subeKodu) ? DBNull.Value : subeKodu)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var mappedResult = _mapper.Map<List<CariDto>>(result);
                return ApiResponse<List<CariDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.CariRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CariDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllCariExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<CariDto>>> GetCarisByCodesAsync(IEnumerable<string> cariKodlari)
        {
            try
            {
                var codes = (cariKodlari ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct()
                    .ToList();

                var cariParam = codes.Count == 0 ? null : string.Join(",", codes);

                var subeFromContext = _httpContextAccessor.HttpContext?.Items["BranchCode"] as string;
                var subeCsv = string.IsNullOrWhiteSpace(subeFromContext)
                    ? null
                    : string.Join(",", subeFromContext.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)));

                var result = await _erpContext.RII_FN_CARI
                    .FromSqlRaw(
                        "SELECT * FROM dbo.RII_FN_CARI({0}, {1})",
                        string.IsNullOrWhiteSpace(cariParam) ? DBNull.Value : cariParam,
                        string.IsNullOrWhiteSpace(subeCsv) ? DBNull.Value : subeCsv)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                var mappedResult = _mapper.Map<List<CariDto>>(result);
                return ApiResponse<List<CariDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.CariRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<CariDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllCariExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        // Stok işlemleri - DTO dönen versiyon
        public async Task<ApiResponse<List<StokFunctionDto>>> GetStoksAsync(string? stokKodu)
        {
            try
            {
                var subeFromContext = _httpContextAccessor.HttpContext?.Items["BranchCode"] as string;
                var subeKodu = string.IsNullOrWhiteSpace(subeFromContext) ? null : subeFromContext;

                var result = await _cmsContext.Set<RII_FN_STOK>()
                    .FromSqlRaw(
                        "SELECT * FROM dbo.RII_FN_STOK({0}, {1})",
                        string.IsNullOrWhiteSpace(stokKodu) ? DBNull.Value : stokKodu,
                        string.IsNullOrWhiteSpace(subeKodu) ? DBNull.Value : subeKodu)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);
                var mappedResult = _mapper.Map<List<StokFunctionDto>>(result);

                return ApiResponse<List<StokFunctionDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.StokRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<StokFunctionDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllStokExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        // Branch işlemleri
        public async Task<ApiResponse<List<BranchDto>>> GetBranchesAsync(int? branchNo = null)
        {
            try
            {
                var connectionString = _erpContext.Database.GetConnectionString();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogWarning("GetBranchesAsync called but ErpConnection is not configured.");
                    return ApiResponse<List<BranchDto>>.ErrorResult(
                        _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                        "ErpConnection is not configured.",
                        StatusCodes.Status503ServiceUnavailable);
                }

                _logger.LogInformation(
                    "ERP branch list requested. BranchNo: {BranchNo}, ConnectionStringPresent: {HasConnectionString}",
                    branchNo,
                    !string.IsNullOrWhiteSpace(_erpContext.Database.GetConnectionString()));

                var rows = await _erpContext.Set<RII_FN_BRANCHES>()
                    .FromSqlRaw(
                        "SELECT * FROM dbo.RII_FN_BRANCHES({0})",
                        branchNo.HasValue ? branchNo.Value : DBNull.Value)
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);

                _logger.LogInformation("ERP branch list retrieved successfully. Count: {Count}", rows.Count);

                var mappedList = _mapper.Map<List<BranchDto>>(rows);
                return ApiResponse<List<BranchDto>>.SuccessResult(mappedList, _localizationService.GetLocalizedString("ErpService.BranchesRetrievedSuccessfully"));
            }
            catch (Exception ex)
            {
                try
                {
                    var conn = _erpContext.Database.GetDbConnection();
                    _logger.LogError(ex,
                        "ERP branch list retrieval failed. BranchNo: {BranchNo}, ConnectionState: {ConnectionState}, DataSource: {DataSource}, Database: {Database}, InnerException: {InnerException}",
                        branchNo, conn?.State.ToString(), conn?.DataSource, conn?.Database, ex.InnerException?.Message);
                }
                catch { _logger.LogError(ex, "ERP branch list retrieval failed. BranchNo: {BranchNo}", branchNo); }

                return ApiResponse<List<BranchDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.BranchesRetrievalError", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<KurDto>>> GetExchangeRateAsync(DateTime tarih, int fiyatTipi)
        {
            try
            {
                string resultDate = tarih.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var result = await _cmsContext.Set<RII_FN_KUR>()
                .FromSqlRaw("SELECT * FROM dbo.RII_FN_KUR({0}, {1})", resultDate, fiyatTipi)
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
                var mappedResult = _mapper.Map<List<KurDto>>(result);
                return ApiResponse<List<KurDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.ExchangeRateRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<KurDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllExchangeRateExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    
        public async Task<ApiResponse<List<ErpShippingAddressDto>>> GetErpShippingAddressAsync(string customerCode)
        {
            try
            {   
                var result = await _cmsContext.Set<RII_FN_2SHIPPING>()
                .FromSqlRaw("SELECT * FROM dbo.RII_FN_2SHIPPING({0})", customerCode)
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
                var mappedResult = _mapper.Map<List<ErpShippingAddressDto>>(result);
                return ApiResponse<List<ErpShippingAddressDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.ExchangeRateRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ErpShippingAddressDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllErpShippingAddressExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<StokGroupDto>>> GetStokGroupAsync(string? grupKodu)
        {
            try
            {
                var subeFromContext = _httpContextAccessor.HttpContext?.Items["BranchCode"] as string;
                var subeKodu = string.IsNullOrWhiteSpace(subeFromContext) ? null : subeFromContext;
                var result = await _cmsContext.Set<RII_STGROUP>()
                .FromSqlRaw(
                    "SELECT * FROM dbo.RII_FN_STGRUP({0}, {1})",
                    string.IsNullOrWhiteSpace(grupKodu) ? DBNull.Value : grupKodu,
                    string.IsNullOrWhiteSpace(subeKodu) ? DBNull.Value : subeKodu)
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
                var mappedResult = _mapper.Map<List<StokGroupDto>>(result);

                return ApiResponse<List<StokGroupDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.StokGroupRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<StokGroupDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetAllStokGroupExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<ProjeDto>>> GetProjectCodesAsync()
        {
            try
            {
                var result = await _erpContext.Set<RII_FN_PROJECTCODE>()
                    .FromSqlRaw("SELECT * FROM dbo.RII_FN_PROJECTCODE()")
                    .AsNoTracking()
                    .ToListAsync().ConfigureAwait(false);
                var mappedResult = _mapper.Map<List<ProjeDto>>(result);
                return ApiResponse<List<ProjeDto>>.SuccessResult(mappedResult, _localizationService.GetLocalizedString("ErpService.ProjeRecordsRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ProjeDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.InternalServerError"),
                    _localizationService.GetLocalizedString("ErpService.GetProjectCodesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        // Health Check
        public async Task<ApiResponse<object>> HealthCheckAsync()
        {
            try
            {
                // ERP veritabanı bağlantısını test et
                await _erpContext.Database.CanConnectAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(new { Status = "Healthy", Timestamp = DateTime.UtcNow }, _localizationService.GetLocalizedString("ErpService.ErpConnectionSuccessful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERP Health check failed");
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("ErpService.ErpConnectionFailed"),
                    _localizationService.GetLocalizedString("ErpService.HealthCheckExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
