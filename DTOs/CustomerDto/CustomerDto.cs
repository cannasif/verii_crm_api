using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace crm_api.DTOs
{
    public class CustomerGetDto : BaseHeaderEntityDto
    {
        public string? CustomerCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? TaxNumber { get; set; }
        public string? TaxOffice { get; set; }
        public string? TcknNumber { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Phone { get; set; }
        public string? Phone2 { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Notes { get; set; }
        public long? CountryId { get; set; }
        public string? CountryName { get; set; }
        public long? CityId { get; set; }
        public string? CityName { get; set; }
        public long? DistrictId { get; set; }
        public string? DistrictName { get; set; }
        public long? CustomerTypeId { get; set; }
        public string? CustomerTypeName { get; set; }
        public string? SalesRepCode { get; set; }
        public string? GroupCode { get; set; }
        public decimal? CreditLimit { get; set; }
        public short BranchCode { get; set; }
        public short BusinessUnitCode { get; set; }
        public long? DefaultShippingAddressId { get; set; }
    }

    public class CustomerCreateDto
    {
        [MaxLength(100)]
        public string? CustomerCode { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? TaxNumber { get; set; }

        [MaxLength(100)]
        public string? TaxOffice { get; set; }

        [MaxLength(11)]
        public string? TcknNumber { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        [MaxLength(100)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Phone2 { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? Website { get; set; }

        [MaxLength(250)]
        public string? Notes { get; set; }

        public long? CountryId { get; set; }

        public long? CityId { get; set; }

        public long? DistrictId { get; set; }

        public long? CustomerTypeId { get; set; }

        [MaxLength(50)]
        public string? SalesRepCode { get; set; }

        [MaxLength(50)]
        public string? GroupCode { get; set; }

        public decimal? CreditLimit { get; set; }

        public short BranchCode { get; set; }

        public short BusinessUnitCode { get; set; }

        public long? DefaultShippingAddressId { get; set; }

        public bool IsCompleted { get; set; } = false;
    }

    public class CustomerUpdateDto
    {
        [MaxLength(100)]
        public string? CustomerCode { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? TaxNumber { get; set; }

        [MaxLength(100)]
        public string? TaxOffice { get; set; }

        [MaxLength(11)]
        public string? TcknNumber { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        [MaxLength(100)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Phone2 { get; set; }

        [MaxLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? Website { get; set; }

        [MaxLength(250)]
        public string? Notes { get; set; }

        public long? CountryId { get; set; }

        public long? CityId { get; set; }

        public long? DistrictId { get; set; }

        public long? CustomerTypeId { get; set; }

        [MaxLength(50)]
        public string? SalesRepCode { get; set; }

        [MaxLength(50)]
        public string? GroupCode { get; set; }

        public decimal? CreditLimit { get; set; }

        public short BranchCode { get; set; }

        public short BusinessUnitCode { get; set; }

        public long? DefaultShippingAddressId { get; set; }

        public DateTime? CompletedDate { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class CustomerDuplicateCandidateDto
    {
        public long MasterCustomerId { get; set; }
        public string MasterCustomerName { get; set; } = string.Empty;
        public long DuplicateCustomerId { get; set; }
        public string DuplicateCustomerName { get; set; } = string.Empty;
        public string MatchType { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }

    public class CustomerMergeRequestDto
    {
        [Required]
        public long MasterCustomerId { get; set; }

        [Required]
        public long DuplicateCustomerId { get; set; }

        public bool PreferMasterValues { get; set; } = true;
    }

    public class CustomerDuplicateConflictDto
    {
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public short? BranchCode { get; set; }
    }

    public class CustomerDuplicateConflictListDto
    {
        public List<CustomerDuplicateConflictDto> Conflicts { get; set; } = new();
    }
}
