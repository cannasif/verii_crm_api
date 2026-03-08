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
    public class DemandNotesService : IDemandNotesService
    {
        private const int MaxNoteCount = 15;
        private const int MaxNoteLength = 100;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public DemandNotesService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<DemandNotesGetDto>>> GetAllDemandNotesAsync(PagedRequest request)
        {
            try
            {
                request ??= new PagedRequest();
                request.Filters ??= new List<Filter>();

                var query = _unitOfWork.DemandNotes.Query()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .ApplyFilters(request.Filters, request.FilterLogic)
                    .ApplySorting(request.SortBy ?? nameof(DemandNotes.Id), request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);
                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .Select(x => _mapper.Map<DemandNotesGetDto>(x))
                    .ToListAsync().ConfigureAwait(false);

                var pagedResponse = new PagedResponse<DemandNotesGetDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<DemandNotesGetDto>>.SuccessResult(
                    pagedResponse,
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<DemandNotesGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandNotesGetDto>> GetDemandNotesByIdAsync(long id)
        {
            try
            {
                var entity = await _unitOfWork.DemandNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (entity == null || entity.IsDeleted)
                {
                    return ApiResponse<DemandNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var dto = _mapper.Map<DemandNotesGetDto>(entity);
                return ApiResponse<DemandNotesGetDto>.SuccessResult(
                    dto,
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandNotesGetDto>> GetNotesByDemandIdAsync(long demandId)
        {
            try
            {
                var demandExists = await _unitOfWork.Demands.Query().AnyAsync(x => x.Id == demandId && !x.IsDeleted).ConfigureAwait(false);
                if (!demandExists)
                {
                    return ApiResponse<DemandNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var entity = await _unitOfWork.DemandNotes.Query().AsNoTracking().FirstOrDefaultAsync(x => x.DemandId == demandId && !x.IsDeleted).ConfigureAwait(false);
                if (entity == null)
                {
                    return ApiResponse<DemandNotesGetDto>.SuccessResult(
                        new DemandNotesGetDto { DemandId = demandId },
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesRetrieved"));
                }

                return ApiResponse<DemandNotesGetDto>.SuccessResult(
                    _mapper.Map<DemandNotesGetDto>(entity),
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandNotesDto>> CreateDemandNotesAsync(CreateDemandNotesDto createDemandNotesDto)
        {
            try
            {
                var demandExists = await _unitOfWork.Demands.Query().AnyAsync(x => x.Id == createDemandNotesDto.DemandId && !x.IsDeleted).ConfigureAwait(false);
                if (!demandExists)
                {
                    return ApiResponse<DemandNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var exists = await _unitOfWork.DemandNotes.Query().AnyAsync(x => x.DemandId == createDemandNotesDto.DemandId && !x.IsDeleted).ConfigureAwait(false);
                if (exists)
                {
                    return ApiResponse<DemandNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesAlreadyExist"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesAlreadyExist"),
                        StatusCodes.Status400BadRequest);
                }

                var entity = _mapper.Map<DemandNotes>(createDemandNotesDto);
                entity.CreatedDate = DateTime.UtcNow;
                await _unitOfWork.DemandNotes.AddAsync(entity).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<DemandNotesDto>.SuccessResult(
                    _mapper.Map<DemandNotesDto>(entity),
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandNotesDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandNotesDto>> UpdateDemandNotesAsync(long id, UpdateDemandNotesDto updateDemandNotesDto)
        {
            try
            {
                var existing = await _unitOfWork.DemandNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null || existing.IsDeleted)
                {
                    return ApiResponse<DemandNotesDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updateDemandNotesDto, existing);
                existing.UpdatedDate = DateTime.UtcNow;
                await _unitOfWork.DemandNotes.UpdateAsync(existing).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<DemandNotesDto>.SuccessResult(
                    _mapper.Map<DemandNotesDto>(existing),
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandNotesDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<DemandNotesGetDto>> UpdateNotesListByDemandIdAsync(long demandId, UpdateDemandNotesListDto request)
        {
            try
            {
                var demandExists = await _unitOfWork.Demands.Query().AnyAsync(x => x.Id == demandId && !x.IsDeleted).ConfigureAwait(false);
                if (!demandExists)
                {
                    return ApiResponse<DemandNotesGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotFound"),
                        StatusCodes.Status404NotFound);
                }

                var notes = (request?.Notes ?? new List<string>())
                    .Where(x => x != null)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (notes.Count > MaxNoteCount)
                {
                    var maxCountMessage = _localizationService.GetLocalizedString("DemandNotesService.MaxNoteCountExceeded", MaxNoteCount);
                    return ApiResponse<DemandNotesGetDto>.ErrorResult(maxCountMessage, maxCountMessage, StatusCodes.Status400BadRequest);
                }

                if (notes.Any(x => x.Length > MaxNoteLength))
                {
                    var maxLengthMessage = _localizationService.GetLocalizedString("DemandNotesService.MaxNoteLengthExceeded", MaxNoteLength);
                    return ApiResponse<DemandNotesGetDto>.ErrorResult(maxLengthMessage, maxLengthMessage, StatusCodes.Status400BadRequest);
                }

                var entity = await _unitOfWork.DemandNotes.Query().FirstOrDefaultAsync(x => x.DemandId == demandId && !x.IsDeleted).ConfigureAwait(false);
                if (entity == null)
                {
                    entity = new DemandNotes { DemandId = demandId, CreatedDate = DateTime.UtcNow };
                    await _unitOfWork.DemandNotes.AddAsync(entity).ConfigureAwait(false);
                }
                else
                {
                    entity.UpdatedDate = DateTime.UtcNow;
                    await _unitOfWork.DemandNotes.UpdateAsync(entity).ConfigureAwait(false);
                }

                entity.Note1 = notes.ElementAtOrDefault(0);
                entity.Note2 = notes.ElementAtOrDefault(1);
                entity.Note3 = notes.ElementAtOrDefault(2);
                entity.Note4 = notes.ElementAtOrDefault(3);
                entity.Note5 = notes.ElementAtOrDefault(4);
                entity.Note6 = notes.ElementAtOrDefault(5);
                entity.Note7 = notes.ElementAtOrDefault(6);
                entity.Note8 = notes.ElementAtOrDefault(7);
                entity.Note9 = notes.ElementAtOrDefault(8);
                entity.Note10 = notes.ElementAtOrDefault(9);
                entity.Note11 = notes.ElementAtOrDefault(10);
                entity.Note12 = notes.ElementAtOrDefault(11);
                entity.Note13 = notes.ElementAtOrDefault(12);
                entity.Note14 = notes.ElementAtOrDefault(13);
                entity.Note15 = notes.ElementAtOrDefault(14);

                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<DemandNotesGetDto>.SuccessResult(
                    _mapper.Map<DemandNotesGetDto>(entity),
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<DemandNotesGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeleteDemandNotesAsync(long id)
        {
            try
            {
                var existing = await _unitOfWork.DemandNotes.GetByIdAsync(id).ConfigureAwait(false);
                if (existing == null || existing.IsDeleted)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        _localizationService.GetLocalizedString("DemandNotesService.DemandNotesNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.DemandNotes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);
                return ApiResponse<object>.SuccessResult(
                    null,
                    _localizationService.GetLocalizedString("DemandNotesService.DemandNotesDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("DemandService.InternalServerError"),
                    ex.Message,
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
