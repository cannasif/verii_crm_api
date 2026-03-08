using AutoMapper;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using crm_api.UnitOfWork;
using crm_api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class DocumentSerialTypeService : IDocumentSerialTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public DocumentSerialTypeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<DocumentSerialTypeGetDto>>> GetAllDocumentSerialTypesAsync(PagedRequest request)
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

                var query = _unitOfWork.DocumentSerialTypes
                    .Query()
                    .Where(d => !d.IsDeleted)
                    .Include(d => d.CustomerType)
                    .Include(d => d.SalesRep)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(DocumentSerialType.Id);
                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<DocumentSerialTypeGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<DocumentSerialTypeGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DocumentSerialTypeGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("DocumentSerialTypeService.TypesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DocumentSerialTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.GetAllTypesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DocumentSerialTypeGetDto>> GetDocumentSerialTypeByIdAsync(long id)
        {
            try
            {
                var documentSerialType = await _unitOfWork.DocumentSerialTypes
                    .Query()
                    .Include(d => d.CustomerType)
                    .Include(d => d.SalesRep)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted).ConfigureAwait(false);

                if (documentSerialType == null)
                {
                    return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var documentSerialTypeDto = _mapper.Map<DocumentSerialTypeGetDto>(documentSerialType);
                return ApiResponse<DocumentSerialTypeGetDto>.SuccessResult(documentSerialTypeDto, _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.GetTypeByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DocumentSerialTypeGetDto>> CreateDocumentSerialTypeAsync(DocumentSerialTypeCreateDto createDto)
        {
            try
            {
                var documentSerialType = _mapper.Map<DocumentSerialType>(createDto);
                await _unitOfWork.DocumentSerialTypes.AddAsync(documentSerialType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var documentSerialTypeWithNav = await _unitOfWork.DocumentSerialTypes
                    .Query()
                    .Include(d => d.CustomerType)
                    .Include(d => d.SalesRep)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .FirstOrDefaultAsync(d => d.Id == documentSerialType.Id && !d.IsDeleted).ConfigureAwait(false);

                if (documentSerialTypeWithNav == null)
                {
                    return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var documentSerialTypeDto = _mapper.Map<DocumentSerialTypeGetDto>(documentSerialTypeWithNav);
                return ApiResponse<DocumentSerialTypeGetDto>.SuccessResult(documentSerialTypeDto, _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.CreateTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DocumentSerialTypeGetDto>> UpdateDocumentSerialTypeAsync(long id, DocumentSerialTypeUpdateDto updateDto)
        {
            try
            {
                var documentSerialType = await _unitOfWork.DocumentSerialTypes.GetByIdForUpdateAsync(id).ConfigureAwait(false);
                if (documentSerialType == null)
                {
                    return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDto, documentSerialType);
                await _unitOfWork.DocumentSerialTypes.UpdateAsync(documentSerialType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties
                var documentSerialTypeWithNav = await _unitOfWork.DocumentSerialTypes
                    .Query()
                    .Include(d => d.CustomerType)
                    .Include(d => d.SalesRep)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.DeletedByUser)
                    .FirstOrDefaultAsync(d => d.Id == documentSerialType.Id && !d.IsDeleted).ConfigureAwait(false);

                if (documentSerialTypeWithNav == null)
                {
                    return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var documentSerialTypeDto = _mapper.Map<DocumentSerialTypeGetDto>(documentSerialTypeWithNav);
                return ApiResponse<DocumentSerialTypeGetDto>.SuccessResult(documentSerialTypeDto, _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DocumentSerialTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.UpdateTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteDocumentSerialTypeAsync(long id)
        {
            try
            {
                var documentSerialType = await _unitOfWork.DocumentSerialTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (documentSerialType == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.DocumentSerialTypes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("DocumentSerialTypeService.TypeDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.DeleteTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<List<DocumentSerialTypeGetDto>>> GetAvaibleDocumentSerialTypesAsync(long customerTypeId, long salesRepId, PricingRuleType ruleType)
        {
            try
            {
                var documentSerialTypeBaseQuery = _unitOfWork.DocumentSerialTypes.Query()
                    .Where(x => !x.IsDeleted);

                List<DocumentSerialType> documentSerialTypes = await documentSerialTypeBaseQuery
                    .Where(x => x.CustomerTypeId == customerTypeId && x.SalesRepId == salesRepId)
                    .ToListAsync().ConfigureAwait(false);

                if (!documentSerialTypes.Any())
                {
                    documentSerialTypes = await documentSerialTypeBaseQuery
                        .Where(x => x.CustomerTypeId == customerTypeId && x.SalesRepId == null)
                        .ToListAsync().ConfigureAwait(false);
                }

                if (!documentSerialTypes.Any())
                {
                    documentSerialTypes = await documentSerialTypeBaseQuery
                        .Where(x => x.CustomerTypeId == null && x.SalesRepId == salesRepId)
                        .ToListAsync().ConfigureAwait(false);
                }

                if (!documentSerialTypes.Any())
                {
                    documentSerialTypes = await documentSerialTypeBaseQuery
                        .Where(x => x.CustomerTypeId == null && x.SalesRepId == null)
                        .ToListAsync().ConfigureAwait(false);
                }

                var dtos = _mapper.Map<List<DocumentSerialTypeGetDto>>(documentSerialTypes);

                return ApiResponse<List<DocumentSerialTypeGetDto>>.SuccessResult(
                    dtos,
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.DocumentSerialTypesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<List<DocumentSerialTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString(
                        "DocumentSerialTypeService.GetAvaibleDocumentSerialTypesExceptionMessage",
                        ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }



        public async Task<ApiResponse<string>> GenerateDocumentSerialAsync(long id, bool isNewDocument = true, string? oldDocumentSerial = null)
        {
            try
            {
                var documentSerialType = await _unitOfWork.DocumentSerialTypes.Query(tracking:true)
                .Where(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync().ConfigureAwait(false);

                if (documentSerialType == null)
                {
                    return ApiResponse<string>.ErrorResult(
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.DocumentSerialTypeNotFound"),
                        _localizationService.GetLocalizedString("DocumentSerialTypeService.DocumentSerialTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }
                var returnDocumentSerial = "";
                if(isNewDocument)
                {
                    int length = documentSerialType.SerialLength ?? 0;
                    int current = documentSerialType.SerialCurrent ?? 0;
                    returnDocumentSerial = $"{documentSerialType.SerialPrefix}{DateTime.UtcNow.Year.ToString()}{current.ToString($"D{length}")}";
                    current++;
                    documentSerialType.SerialCurrent = current;
                    await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                }
                else
                {
                    if (string.IsNullOrWhiteSpace(oldDocumentSerial))
                    {
                        return ApiResponse<string>.ErrorResult(
                            _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                            _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                            StatusCodes.Status400BadRequest);
                    }

                    string beforeDash = oldDocumentSerial.Split('-')[0];
                    var QuotationDocumentSerialTypes = await _unitOfWork.Quotations.Query().Where(x => x.OfferNo == beforeDash).ToListAsync().ConfigureAwait(false);
                    int maxSerialNumber = QuotationDocumentSerialTypes
                                                            .Select(x =>
                                                            {
                                                                if (string.IsNullOrWhiteSpace(x.RevisionNo))
                                                                    return 0;

                                                                var index = x.RevisionNo.LastIndexOf('-');                                                                if (index == -1)
                                                                    return 0;

                                                                var numberPart = x.RevisionNo[(index + 1)..];
                                                                return int.TryParse(numberPart, out var rev) ? rev : 0;
                                                            })
                                                            .DefaultIfEmpty(0)
                                                            .Max();
                    returnDocumentSerial = $"{beforeDash}-{maxSerialNumber + 1}";
                }

                return ApiResponse<string>.SuccessResult(returnDocumentSerial, _localizationService.GetLocalizedString("DocumentSerialTypeService.DocumentSerialGenerated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.ErrorResult(
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("DocumentSerialTypeService.GenerateDocumentSerialExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
