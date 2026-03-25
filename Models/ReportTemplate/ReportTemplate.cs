using System;
namespace crm_api.Models
{
    /// <summary>
    /// Report template entity for storing JSON-based report designs
    /// </summary>
    public class ReportTemplate : BaseEntity
    {
        /// <summary>
        /// Document type: 0=Demand/Talep, 1=Quotation/Teklif, 2=Order/Sipariş, 3=FastQuotation/Hızlı Teklif, 4=Activity/Aktivite
        /// </summary>
        public DocumentRuleType RuleType { get; set; }

        /// <summary>
        /// Template title/description (Başlık)
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// JSON template data (page config + elements array)
        /// </summary>
        public string TemplateJson { get; set; } = string.Empty;

        /// <summary>
        /// Is this template active/enabled
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Her RuleType için tek bir şablon default olabilir; PDF oluştururken default şablon kullanılabilir.
        /// </summary>
        public bool Default { get; set; } = false;

        /// <summary>
        /// User ID who created this template
        /// </summary>
        public long? CreatedByUserId { get; set; }

        /// <summary>
        /// User ID who last updated this template
        /// </summary>
        public long? UpdatedByUserId { get; set; }
    }

    /// <summary>
    /// Document type enum for report templates
    /// </summary>
    public enum DocumentRuleType
    {
        /// <summary>
        /// Demand/Talep
        /// </summary>
        Demand = 0,

        /// <summary>
        /// Quotation/Teklif
        /// </summary>
        Quotation = 1,

        /// <summary>
        /// Order/Sipariş
        /// </summary>
        Order = 2,

        /// <summary>
        /// Fast quotation / hızlı teklif
        /// </summary>
        FastQuotation = 3,

        /// <summary>
        /// Activity / aktivite
        /// </summary>
        Activity = 4
    }
}
