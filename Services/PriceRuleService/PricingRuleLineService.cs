using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class PricingRuleLineService : IPricingRuleLineService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PricingRuleLineService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PricingRuleLineGetDto>>> GetAllPricingRuleLinesAsync(PagedRequest request)
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

                var query = _unitOfWork.PricingRuleLines
                    .Query()
                    .Where(l => !l.IsDeleted)
                    .Include(l => l.PricingRuleHeader)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.UpdatedByUser)
                    .Include(l => l.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PricingRuleLine.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PricingRuleLineGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PricingRuleLineGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PricingRuleLineGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("PricingRuleLineService.LinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PricingRuleLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.GetAllLinesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleLineGetDto>> GetPricingRuleLineByIdAsync(long id)
        {
            try
            {
                var line = await _unitOfWork.PricingRuleLines
                    .Query()
                    .Include(l => l.PricingRuleHeader)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.UpdatedByUser)
                    .Include(l => l.DeletedByUser)
                    .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted).ConfigureAwait(false);

                if (line == null)
                {
                    return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var lineDto = _mapper.Map<PricingRuleLineGetDto>(line);
                return ApiResponse<PricingRuleLineGetDto>.SuccessResult(lineDto, _localizationService.GetLocalizedString("PricingRuleLineService.LineRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.GetLineByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleLineGetDto>> CreatePricingRuleLineAsync(PricingRuleLineCreateDto createDto)
        {
            try
            {
                var line = _mapper.Map<PricingRuleLine>(createDto);
                var lineCheck = await _unitOfWork.PricingRuleLines.Query(tracking: false, ignoreQueryFilters: true)
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(l => l.PricingRuleHeaderId == createDto.PricingRuleHeaderId && l.StokCode == createDto.StokCode).ConfigureAwait(false);
                if (lineCheck != null)
                {
                    lineCheck.IsDeleted = false;
                    lineCheck.MinQuantity = createDto.MinQuantity;
                    lineCheck.MaxQuantity = createDto.MaxQuantity;
                    lineCheck.FixedUnitPrice = createDto.FixedUnitPrice;
                    lineCheck.CurrencyCode = createDto.CurrencyCode;
                    lineCheck.DiscountRate1 = createDto.DiscountRate1;
                    lineCheck.DiscountAmount1 = createDto.DiscountAmount1;
                    lineCheck.DiscountRate2 = createDto.DiscountRate2;
                    lineCheck.DiscountAmount2 = createDto.DiscountAmount2;
                    lineCheck.DiscountRate3 = createDto.DiscountRate3;
                    lineCheck.DiscountAmount3 = createDto.DiscountAmount3;
                    lineCheck.DeletedBy = null;
                    lineCheck.DeletedDate = null;
                    await _unitOfWork.PricingRuleLines.UpdateAsync(lineCheck).ConfigureAwait(false);
                    line = lineCheck;
                }else{
                await _unitOfWork.PricingRuleLines.AddAsync(line).ConfigureAwait(false);
                }
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var lineWithNav = await _unitOfWork.PricingRuleLines
                    .Query()
                    .Include(l => l.PricingRuleHeader)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.UpdatedByUser)
                    .Include(l => l.DeletedByUser)
                    .FirstOrDefaultAsync(l => l.Id == line.Id && !l.IsDeleted).ConfigureAwait(false);

                if (lineWithNav == null)
                {
                    return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var lineDto = _mapper.Map<PricingRuleLineGetDto>(lineWithNav);
                return ApiResponse<PricingRuleLineGetDto>.SuccessResult(lineDto, _localizationService.GetLocalizedString("PricingRuleLineService.LineCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.CreateLineExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PricingRuleLineGetDto>> UpdatePricingRuleLineAsync(long id, PricingRuleLineUpdateDto updateDto)
        {
            try
            {
                var line = await _unitOfWork.PricingRuleLines.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (line == null)
                {
                    return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, line);
                await _unitOfWork.PricingRuleLines.UpdateAsync(line).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var lineWithNav = await _unitOfWork.PricingRuleLines
                    .Query()
                    .Include(l => l.PricingRuleHeader)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.UpdatedByUser)
                    .Include(l => l.DeletedByUser)
                    .FirstOrDefaultAsync(l => l.Id == line.Id && !l.IsDeleted).ConfigureAwait(false);

                if (lineWithNav == null)
                {
                    return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var lineDto = _mapper.Map<PricingRuleLineGetDto>(lineWithNav);
                return ApiResponse<PricingRuleLineGetDto>.SuccessResult(lineDto, _localizationService.GetLocalizedString("PricingRuleLineService.LineUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PricingRuleLineGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.UpdateLineExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeletePricingRuleLineAsync(long id)
        {
            try
            {
                var line = await _unitOfWork.PricingRuleLines.GetByIdAsync(id).ConfigureAwait(false);
                if (line == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        _localizationService.GetLocalizedString("PricingRuleLineService.LineNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PricingRuleLines.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("PricingRuleLineService.LineDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.DeleteLineExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<PricingRuleLineGetDto>>> GetPricingRuleLinesByHeaderIdAsync(long headerId)
        {
            try
            {
                var lines = await _unitOfWork.PricingRuleLines
                    .Query()
                    .Where(l => l.PricingRuleHeaderId == headerId && !l.IsDeleted)
                    .Include(l => l.PricingRuleHeader)
                    .Include(l => l.CreatedByUser)
                    .Include(l => l.UpdatedByUser)
                    .Include(l => l.DeletedByUser)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = lines.Select(x => _mapper.Map<PricingRuleLineGetDto>(x)).ToList();
                return ApiResponse<List<PricingRuleLineGetDto>>.SuccessResult(dtos, _localizationService.GetLocalizedString("PricingRuleLineService.LinesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<PricingRuleLineGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PricingRuleLineService.InternalServerError"),
                    _localizationService.GetLocalizedString("PricingRuleLineService.GetLinesByHeaderIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
