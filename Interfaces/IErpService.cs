using depoWebAPI.Models;
using crm_api.DTOs;
using crm_api.DTOs.ErpDto;
using crm_api.Data;

namespace crm_api.Interfaces
{
    public interface IErpService
    {
        Task<ApiResponse<short>> GetBranchCodeFromContext();
        Task<ApiResponse<List<CariDto>>> GetCarisAsync(string? cariKodu);
        Task<ApiResponse<List<CariDto>>> GetCarisByCodesAsync(IEnumerable<string> cariKodlari);
        Task<ApiResponse<List<StokFunctionDto>>> GetStoksAsync(string? stokKodu);
        Task<ApiResponse<List<BranchDto>>> GetBranchesAsync(int? branchNo = null);
        Task<ApiResponse<List<KurDto>>> GetExchangeRateAsync(DateTime tarih, int fiyatTipi);
        Task<ApiResponse<List<ErpCariMovementDto>>> GetCariMovementsAsync(string customerCode);
        Task<ApiResponse<List<ErpShippingAddressDto>>> GetErpShippingAddressAsync(string customerCode);
        Task<ApiResponse<List<StokGroupDto>>> GetStokGroupAsync(string? grupKodu);
        Task<ApiResponse<List<ProjeDto>>> GetProjectCodesAsync();

        // Health Check
        Task<ApiResponse<object>> HealthCheckAsync();
    }
}
