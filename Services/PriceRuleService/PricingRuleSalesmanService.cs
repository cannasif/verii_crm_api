using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class PricingRuleSalesmanService : IPricingRuleSalesmanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PricingRuleSalesmanService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PricingRuleSalesmanGetDto>>> GetAllPricingRuleSalesmenAsync(PagedRequest request)
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

                var query = _unitOfWork.PricingRuleSalesmen
                    .Query()
                    .Where(s => !s.IsDeleted)
                    .Include(s => s.PricingRuleHeader)
                    .Include(s => s.Salesman)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PricingRuleSalesman.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PricingRuleSalesmanGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PricingRuleSalesmanGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PricingRuleSalesmanGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmenRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PricingRuleSalesmanGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.GetAllSalesmenExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleSalesmanGetDto>> GetPricingRuleSalesmanByIdAsync(long id)
        {
            try
            {
                var salesman = await _unitOfWork.PricingRuleSalesmen
                    .Query()
                    .Include(s => s.PricingRuleHeader)
                    .Include(s => s.Salesman)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted).ConfigureAwait(false);

                if (salesman == null)
                {
                    return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var salesmanDto = _mapper.Map<PricingRuleSalesmanGetDto>(salesman);
                return ApiResponse<PricingRuleSalesmanGetDto>.SuccessResult(salesmanDto, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.GetSalesmanByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleSalesmanGetDto>> CreatePricingRuleSalesmanAsync(PricingRuleSalesmanCreateDto createDto)
        {
            try
            {
                var salesman = _mapper.Map<PricingRuleSalesman>(createDto);
                var salesmanCheck =  await _unitOfWork.PricingRuleSalesmen
                                    .Query(tracking: false, ignoreQueryFilters: true)
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(s =>
                                        s.PricingRuleHeaderId == createDto.PricingRuleHeaderId &&
                                        s.SalesmanId == createDto.SalesmanId).ConfigureAwait(false);
                if (salesmanCheck != null)
                {
                    salesmanCheck.IsDeleted = false;
                    salesmanCheck.DeletedBy = null;
                    salesmanCheck.DeletedDate = null;
                    await _unitOfWork.PricingRuleSalesmen.UpdateAsync(salesmanCheck).ConfigureAwait(false);
                    salesman = salesmanCheck;
                }else{
                await _unitOfWork.PricingRuleSalesmen.AddAsync(salesman).ConfigureAwait(false);
                }
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var salesmanWithNav = await _unitOfWork.PricingRuleSalesmen
                    .Query()
                    .Include(s => s.PricingRuleHeader)
                    .Include(s => s.Salesman)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .FirstOrDefaultAsync(s => s.Id == salesman.Id && !s.IsDeleted).ConfigureAwait(false);

                if (salesmanWithNav == null)
                {
                    return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var salesmanDto = _mapper.Map<PricingRuleSalesmanGetDto>(salesmanWithNav);
                return ApiResponse<PricingRuleSalesmanGetDto>.SuccessResult(salesmanDto, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.CreateSalesmanExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleSalesmanGetDto>> UpdatePricingRuleSalesmanAsync(long id, PricingRuleSalesmanUpdateDto updateDto)
        {
            try
            {
                var salesman = await _unitOfWork.PricingRuleSalesmen.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (salesman == null)
                {
                    return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, salesman);
                await _unitOfWork.PricingRuleSalesmen.UpdateAsync(salesman).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var salesmanWithNav = await _unitOfWork.PricingRuleSalesmen
                    .Query()
                    .Include(s => s.PricingRuleHeader)
                    .Include(s => s.Salesman)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .FirstOrDefaultAsync(s => s.Id == salesman.Id && !s.IsDeleted).ConfigureAwait(false);

                if (salesmanWithNav == null)
                {
                    return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var salesmanDto = _mapper.Map<PricingRuleSalesmanGetDto>(salesmanWithNav);
                return ApiResponse<PricingRuleSalesmanGetDto>.SuccessResult(salesmanDto, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleSalesmanGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.UpdateSalesmanExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeletePricingRuleSalesmanAsync(long id)
        {
            try
            {
                var salesman = await _unitOfWork.PricingRuleSalesmen.GetByIdAsync(id).ConfigureAwait(false);
                if (salesman == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PricingRuleSalesmen.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmanDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.DeleteSalesmanExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<PricingRuleSalesmanGetDto>>> GetPricingRuleSalesmenByHeaderIdAsync(long headerId)
        {
            try
            {
                var salesmen = await _unitOfWork.PricingRuleSalesmen
                    .Query()
                    .Where(s => s.PricingRuleHeaderId == headerId && !s.IsDeleted)
                    .Include(s => s.PricingRuleHeader)
                    .Include(s => s.Salesman)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.UpdatedByUser)
                    .Include(s => s.DeletedByUser)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = salesmen.Select(x => _mapper.Map<PricingRuleSalesmanGetDto>(x)).ToList();
                return ApiResponse<List<PricingRuleSalesmanGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("PricingRuleSalesmanService.SalesmenRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PricingRuleSalesmanGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleSalesmanService.GetSalesmenByHeaderIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
