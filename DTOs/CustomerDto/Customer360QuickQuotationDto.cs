namespace crm_api.DTOs.CustomerDto
{
    public class Customer360QuickQuotationDto
    {
        public long Id { get; set; }
        public DateTime OfferDate { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? Description { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public long? QuotationId { get; set; }
        public string? QuotationNo { get; set; }
        public bool HasConvertedQuotation { get; set; }
        public int? QuotationStatus { get; set; }
        public string? QuotationStatusName { get; set; }
        public bool HasApprovalRequest { get; set; }
        public int? ApprovalStatus { get; set; }
        public string? ApprovalStatusName { get; set; }
        public int? ApprovalCurrentStep { get; set; }
        public string? ApprovalFlowDescription { get; set; }
    }
}
