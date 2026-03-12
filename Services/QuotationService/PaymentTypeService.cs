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
    public class PaymentTypeService : IPaymentTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILocalizationService _localizationService;

        public PaymentTypeService(IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _localizationService = localizationService;
        }

        public async Task<ApiResponse<PagedResponse<PaymentTypeGetDto>>> GetAllPaymentTypesAsync(PagedRequest request)
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

                var query = _unitOfWork.PaymentTypes.Query()
                    .AsNoTracking()
                    .Where(pt => !pt.IsDeleted)
                    .Include(pt => pt.CreatedByUser)
                    .Include(pt => pt.UpdatedByUser)
                    .Include(pt => pt.DeletedByUser)
                    .ApplyFilters(request.Filters, request.FilterLogic);

                var sortBy = request.SortBy ?? nameof(PaymentType.Id);
                var isDesc = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

                query = query.ApplySorting(sortBy, request.SortDirection);

                var totalCount = await query.CountAsync().ConfigureAwait(false);

                var items = await query
                    .ApplyPagination(request.PageNumber, request.PageSize)
                    .ToListAsync().ConfigureAwait(false);

                var dtos = items.Select(x => _mapper.Map<PaymentTypeGetDto>(x)).ToList();

                var pagedResponse = new PagedResponse<PaymentTypeGetDto>
                {
                    Items = dtos,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                };

                return ApiResponse<PagedResponse<PaymentTypeGetDto>>.SuccessResult(pagedResponse, _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypesRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PagedResponse<PaymentTypeGetDto>>.ErrorResult(
                    _localizationService.GetLocalizedString("PaymentTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("PaymentTypeService.GetAllPaymentTypesExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PaymentTypeGetDto>> GetPaymentTypeByIdAsync(long id)
        {
            try
            {
                var paymentType = await _unitOfWork.PaymentTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (paymentType == null)
                {
                    return ApiResponse<PaymentTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                // Reload with navigation properties for mapping
                var paymentTypeWithNav = await _unitOfWork.PaymentTypes.Query()
                    .AsNoTracking()
                    .Include(pt => pt.CreatedByUser)
                    .Include(pt => pt.UpdatedByUser)
                    .Include(pt => pt.DeletedByUser)
                    .FirstOrDefaultAsync(pt => pt.Id == id && !pt.IsDeleted).ConfigureAwait(false);

                var paymentTypeDto = _mapper.Map<PaymentTypeGetDto>(paymentTypeWithNav ?? paymentType);

                return ApiResponse<PaymentTypeGetDto>.SuccessResult(paymentTypeDto, _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeRetrieved"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PaymentTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PaymentTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("PaymentTypeService.GetPaymentTypeByIdExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PaymentTypeGetDto>> CreatePaymentTypeAsync(PaymentTypeCreateDto createPaymentTypeDto)
        {
            try
            {
                var paymentType = _mapper.Map<PaymentType>(createPaymentTypeDto);
                paymentType.CreatedDate = DateTimeProvider.Now;

                await _unitOfWork.PaymentTypes.AddAsync(paymentType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var paymentTypeWithNav = await _unitOfWork.PaymentTypes.Query()
                    .AsNoTracking()
                    .Include(pt => pt.CreatedByUser)
                    .Include(pt => pt.UpdatedByUser)
                    .Include(pt => pt.DeletedByUser)
                    .FirstOrDefaultAsync(pt => pt.Id == paymentType.Id && !pt.IsDeleted).ConfigureAwait(false);

                var paymentTypeDto = _mapper.Map<PaymentTypeGetDto>(paymentTypeWithNav ?? paymentType);

                return ApiResponse<PaymentTypeGetDto>.SuccessResult(paymentTypeDto, _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeCreated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PaymentTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PaymentTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("PaymentTypeService.CreatePaymentTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<PaymentTypeGetDto>> UpdatePaymentTypeAsync(long id, PaymentTypeUpdateDto updatePaymentTypeDto)
        {
            try
            {
                var existingPaymentType = await _unitOfWork.PaymentTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (existingPaymentType == null)
                {
                    return ApiResponse<PaymentTypeGetDto>.ErrorResult(
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                _mapper.Map(updatePaymentTypeDto, existingPaymentType);
                existingPaymentType.UpdatedDate = DateTimeProvider.Now;

                await _unitOfWork.PaymentTypes.UpdateAsync(existingPaymentType).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                // Reload with navigation properties for mapping
                var paymentTypeWithNav = await _unitOfWork.PaymentTypes.Query()
                    .AsNoTracking()
                    .Include(pt => pt.CreatedByUser)
                    .Include(pt => pt.UpdatedByUser)
                    .Include(pt => pt.DeletedByUser)
                    .FirstOrDefaultAsync(pt => pt.Id == existingPaymentType.Id && !pt.IsDeleted).ConfigureAwait(false);

                var paymentTypeDto = _mapper.Map<PaymentTypeGetDto>(paymentTypeWithNav ?? existingPaymentType);

                return ApiResponse<PaymentTypeGetDto>.SuccessResult(paymentTypeDto, _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeUpdated"));
            }
            catch (Exception ex)
            {
                return ApiResponse<PaymentTypeGetDto>.ErrorResult(
                    _localizationService.GetLocalizedString("PaymentTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("PaymentTypeService.UpdatePaymentTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }

        public async Task<ApiResponse<object>> DeletePaymentTypeAsync(long id)
        {
            try
            {
                var paymentType = await _unitOfWork.PaymentTypes.GetByIdAsync(id).ConfigureAwait(false);
                if (paymentType == null)
                {
                    return ApiResponse<object>.ErrorResult(
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeNotFound"),
                        StatusCodes.Status404NotFound);
                }

                await _unitOfWork.PaymentTypes.SoftDeleteAsync(id).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync().ConfigureAwait(false);

                return ApiResponse<object>.SuccessResult(null, _localizationService.GetLocalizedString("PaymentTypeService.PaymentTypeDeleted"));
            }
            catch (Exception ex)
            {
                return ApiResponse<object>.ErrorResult(
                    _localizationService.GetLocalizedString("PaymentTypeService.InternalServerError"),
                    _localizationService.GetLocalizedString("PaymentTypeService.DeletePaymentTypeExceptionMessage", ex.Message),
                    StatusCodes.Status500InternalServerError);
            }
        }
    }
}
