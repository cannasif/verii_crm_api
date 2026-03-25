using System.Collections.Generic;

namespace crm_api.DTOs
{
    /// <summary>
    /// Frontend için kullanılabilir tüm field'ları içeren DTO
    /// Bu DTO template designer'da field palette'ini oluşturmak için kullanılır
    /// </summary>
    public class ReportTemplateFieldsDto
    {
        /// <summary>
        /// Header/Root seviye field'lar (Demand, Quotation, Order için ortak)
        /// </summary>
        public List<FieldDefinition> HeaderFields { get; set; } = new List<FieldDefinition>();

        /// <summary>
        /// Line (satır) seviye field'lar - Tablolarda kullanılır
        /// </summary>
        public List<FieldDefinition> LineFields { get; set; } = new List<FieldDefinition>();

        /// <summary>
        /// Döviz kuru tablosu field'ları - ExchangeRates tablosunda kullanılır
        /// </summary>
        public List<FieldDefinition> ExchangeRateFields { get; set; } = new List<FieldDefinition>();
    }

    /// <summary>
    /// Tek bir field tanımı
    /// </summary>
    public class FieldDefinition
    {
        /// <summary>
        /// Field'ın görünen adı (Türkçe)
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Field'ın DTO'daki path'i (örn: "OfferNo", "Lines.ProductName")
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Field'ın veri tipi (string, number, date, boolean)
        /// </summary>
        public string DataType { get; set; } = "string";

        /// <summary>
        /// Field açıklaması
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Örnek değer
        /// </summary>
        public string? ExampleValue { get; set; }
    }

    /// <summary>
    /// Demand için field'lar
    /// </summary>
    public static class DemandFields
    {
        public static ReportTemplateFieldsDto GetFields()
        {
            return new ReportTemplateFieldsDto
            {
                HeaderFields = new List<FieldDefinition>
                {
                    // Temel Bilgiler
                    new FieldDefinition { Label = "Talep No", Path = "OfferNo", DataType = "string", ExampleValue = "TLP-2024-001" },
                    new FieldDefinition { Label = "Talep Tarihi", Path = "OfferDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Talep Tipi", Path = "OfferType", DataType = "string", ExampleValue = "Yurtiçi" },
                    new FieldDefinition { Label = "Revizyon No", Path = "RevisionNo", DataType = "string", ExampleValue = "REV-001" },
                    
                    // Müşteri Bilgileri
                    new FieldDefinition { Label = "Müşteri Adı", Path = "CustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Potansiyel Müşteri Adı", Path = "PotentialCustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Müşteri Kodu (ERP)", Path = "ErpCustomerCode", DataType = "string", ExampleValue = "CUST001" },
                    
                    // Teslimat Bilgileri
                    new FieldDefinition { Label = "Teslimat Tarihi", Path = "DeliveryDate", DataType = "date", ExampleValue = "2024-02-15" },
                    new FieldDefinition { Label = "Teslimat Adresi", Path = "ShippingAddressText", DataType = "string", ExampleValue = "İstanbul, Türkiye" },
                    
                    // Diğer Bilgiler
                    new FieldDefinition { Label = "Satış Temsilcisi", Path = "RepresentativeName", DataType = "string", ExampleValue = "Ahmet Yılmaz" },
                    new FieldDefinition { Label = "Ödeme Tipi", Path = "PaymentTypeName", DataType = "string", ExampleValue = "Vadeli" },
                    new FieldDefinition { Label = "Para Birimi", Path = "Currency", DataType = "string", ExampleValue = "TRY" },
                    new FieldDefinition { Label = "Genel İndirim Oranı", Path = "GeneralDiscountRate", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "Genel İndirim Tutarı", Path = "GeneralDiscountAmount", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "Net Toplam", Path = "Total", DataType = "number", ExampleValue = "4750.00" },
                    new FieldDefinition { Label = "Genel Toplam", Path = "GrandTotal", DataType = "number", ExampleValue = "5605.00" },
                    new FieldDefinition { Label = "Açıklama", Path = "Description", DataType = "string", ExampleValue = "Acil talep" },
                    new FieldDefinition { Label = "Belge Seri Tipi", Path = "DocumentSerialTypeName", DataType = "string", ExampleValue = "A Serisi" },
                    
                    // Oluşturma Bilgileri
                    new FieldDefinition { Label = "Oluşturan", Path = "CreatedBy", DataType = "string", ExampleValue = "Mehmet Demir" },
                    new FieldDefinition { Label = "Güncelleyen", Path = "UpdatedBy", DataType = "string", ExampleValue = "Ayşe Kaya" },
                },
                LineFields = new List<FieldDefinition>
                {
                    // Ürün Bilgileri
                    new FieldDefinition { Label = "Ürün Kodu", Path = "Lines.ProductCode", DataType = "string", ExampleValue = "PRD-001" },
                    new FieldDefinition { Label = "Ürün Adı", Path = "Lines.ProductName", DataType = "string", ExampleValue = "Laptop" },
                    new FieldDefinition { Label = "Grup Kodu", Path = "Lines.GroupCode", DataType = "string", ExampleValue = "GRP-001" },
                    new FieldDefinition { Label = "Stok Kodu", Path = "Lines.StockCode", DataType = "string", ExampleValue = "STK-001" },
                    new FieldDefinition { Label = "Stok Birim", Path = "Lines.StockUnit", DataType = "string", ExampleValue = "Adet" },
                    new FieldDefinition { Label = "Üretici Kodu", Path = "Lines.StockManufacturerCode", DataType = "string", ExampleValue = "URT-001" },
                    new FieldDefinition { Label = "Stok Grup Adı", Path = "Lines.StockGroupName", DataType = "string", ExampleValue = "Elektronik" },
                    new FieldDefinition { Label = "Stok Kod 1", Path = "Lines.StockCode1", DataType = "string", ExampleValue = "K1" },
                    new FieldDefinition { Label = "Stok Kod 1 Adı", Path = "Lines.StockCode1Name", DataType = "string", ExampleValue = "Kod 1 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 2", Path = "Lines.StockCode2", DataType = "string", ExampleValue = "K2" },
                    new FieldDefinition { Label = "Stok Kod 2 Adı", Path = "Lines.StockCode2Name", DataType = "string", ExampleValue = "Kod 2 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 3", Path = "Lines.StockCode3", DataType = "string", ExampleValue = "K3" },
                    new FieldDefinition { Label = "Stok Kod 3 Adı", Path = "Lines.StockCode3Name", DataType = "string", ExampleValue = "Kod 3 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 4", Path = "Lines.StockCode4", DataType = "string", ExampleValue = "K4" },
                    new FieldDefinition { Label = "Stok Kod 4 Adı", Path = "Lines.StockCode4Name", DataType = "string", ExampleValue = "Kod 4 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 5", Path = "Lines.StockCode5", DataType = "string", ExampleValue = "K5" },
                    new FieldDefinition { Label = "Stok Kod 5 Adı", Path = "Lines.StockCode5Name", DataType = "string", ExampleValue = "Kod 5 Açıklama" },
                    
                    // Miktar ve Fiyat
                    new FieldDefinition { Label = "Miktar", Path = "Lines.Quantity", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "Birim Fiyat", Path = "Lines.UnitPrice", DataType = "number", ExampleValue = "5000.00" },
                    
                    // İndirimler
                    new FieldDefinition { Label = "İndirim Oranı 1 (%)", Path = "Lines.DiscountRate1", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "İndirim Tutarı 1", Path = "Lines.DiscountAmount1", DataType = "number", ExampleValue = "500.00" },
                    new FieldDefinition { Label = "İndirim Oranı 2 (%)", Path = "Lines.DiscountRate2", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "İndirim Tutarı 2", Path = "Lines.DiscountAmount2", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "İndirim Oranı 3 (%)", Path = "Lines.DiscountRate3", DataType = "number", ExampleValue = "2" },
                    new FieldDefinition { Label = "İndirim Tutarı 3", Path = "Lines.DiscountAmount3", DataType = "number", ExampleValue = "100.00" },
                    
                    // KDV
                    new FieldDefinition { Label = "KDV Oranı (%)", Path = "Lines.VatRate", DataType = "number", ExampleValue = "18" },
                    new FieldDefinition { Label = "KDV Tutarı", Path = "Lines.VatAmount", DataType = "number", ExampleValue = "900.00" },
                    
                    // Toplamlar
                    new FieldDefinition { Label = "Satır Toplamı (KDV Hariç)", Path = "Lines.LineTotal", DataType = "number", ExampleValue = "45000.00" },
                    new FieldDefinition { Label = "Satır Genel Toplamı (KDV Dahil)", Path = "Lines.LineGrandTotal", DataType = "number", ExampleValue = "53100.00" },
                    
                    // Diğer
                    new FieldDefinition { Label = "Açıklama", Path = "Lines.Description", DataType = "string", ExampleValue = "Özel not" },
                    new FieldDefinition { Label = "Stok HTML Açıklama", Path = "Lines.HtmlDescription", DataType = "string", Description = "Stok detayındaki HTML editör içeriği", ExampleValue = "<p>Ürün açıklaması</p>" },
                    new FieldDefinition { Label = "Stok Varsayılan Görsel", Path = "Lines.DefaultImagePath", DataType = "string", Description = "Stok ana görseli (FilePath)", ExampleValue = "uploads/stock/abc.jpg" },
                },
                ExchangeRateFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Para Birimi", Path = "ExchangeRates.Currency", DataType = "string", ExampleValue = "USD" },
                    new FieldDefinition { Label = "Kur", Path = "ExchangeRates.ExchangeRate", DataType = "number", ExampleValue = "34.50" },
                    new FieldDefinition { Label = "Kur Tarihi", Path = "ExchangeRates.ExchangeRateDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Resmi Kur", Path = "ExchangeRates.IsOfficial", DataType = "boolean", ExampleValue = "true" },
                }
            };
        }
    }

    /// <summary>
    /// Quotation için field'lar
    /// </summary>
    public static class QuotationFields
    {
        public static ReportTemplateFieldsDto GetFields()
        {
            return new ReportTemplateFieldsDto
            {
                HeaderFields = new List<FieldDefinition>
                {
                    // Temel Bilgiler
                    new FieldDefinition { Label = "Teklif No", Path = "OfferNo", DataType = "string", ExampleValue = "TKL-2024-001" },
                    new FieldDefinition { Label = "Teklif Tarihi", Path = "OfferDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Teklif Tipi", Path = "OfferType", DataType = "string", ExampleValue = "Yurtiçi" },
                    new FieldDefinition { Label = "Revizyon No", Path = "RevisionNo", DataType = "string", ExampleValue = "REV-001" },
                    
                    // Müşteri Bilgileri
                    new FieldDefinition { Label = "Müşteri Adı", Path = "CustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Potansiyel Müşteri Adı", Path = "PotentialCustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Müşteri Kodu (ERP)", Path = "ErpCustomerCode", DataType = "string", ExampleValue = "CUST001" },
                    
                    // Teslimat Bilgileri
                    new FieldDefinition { Label = "Teslimat Tarihi", Path = "DeliveryDate", DataType = "date", ExampleValue = "2024-02-15" },
                    new FieldDefinition { Label = "Teslimat Adresi", Path = "ShippingAddressText", DataType = "string", ExampleValue = "İstanbul, Türkiye" },
                    
                    // Diğer Bilgiler
                    new FieldDefinition { Label = "Satış Temsilcisi", Path = "RepresentativeName", DataType = "string", ExampleValue = "Ahmet Yılmaz" },
                    new FieldDefinition { Label = "Ödeme Tipi", Path = "PaymentTypeName", DataType = "string", ExampleValue = "Vadeli" },
                    new FieldDefinition { Label = "Para Birimi", Path = "Currency", DataType = "string", ExampleValue = "TRY" },
                    new FieldDefinition { Label = "Genel İndirim Oranı", Path = "GeneralDiscountRate", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "Genel İndirim Tutarı", Path = "GeneralDiscountAmount", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "Net Toplam", Path = "Total", DataType = "number", ExampleValue = "4750.00" },
                    new FieldDefinition { Label = "Genel Toplam", Path = "GrandTotal", DataType = "number", ExampleValue = "5605.00" },
                    new FieldDefinition { Label = "Açıklama", Path = "Description", DataType = "string", ExampleValue = "Özel teklif" },
                    new FieldDefinition { Label = "Belge Seri Tipi", Path = "DocumentSerialTypeName", DataType = "string", ExampleValue = "A Serisi" },
                    
                    // Oluşturma Bilgileri
                    new FieldDefinition { Label = "Oluşturan", Path = "CreatedBy", DataType = "string", ExampleValue = "Mehmet Demir" },
                    new FieldDefinition { Label = "Güncelleyen", Path = "UpdatedBy", DataType = "string", ExampleValue = "Ayşe Kaya" },
                },
                LineFields = new List<FieldDefinition>
                {
                    // Ürün Bilgileri
                    new FieldDefinition { Label = "Ürün Kodu", Path = "Lines.ProductCode", DataType = "string", ExampleValue = "PRD-001" },
                    new FieldDefinition { Label = "Ürün Adı", Path = "Lines.ProductName", DataType = "string", ExampleValue = "Laptop" },
                    new FieldDefinition { Label = "Grup Kodu", Path = "Lines.GroupCode", DataType = "string", ExampleValue = "GRP-001" },
                    new FieldDefinition { Label = "Stok Kodu", Path = "Lines.StockCode", DataType = "string", ExampleValue = "STK-001" },
                    new FieldDefinition { Label = "Stok Birim", Path = "Lines.StockUnit", DataType = "string", ExampleValue = "Adet" },
                    new FieldDefinition { Label = "Üretici Kodu", Path = "Lines.StockManufacturerCode", DataType = "string", ExampleValue = "URT-001" },
                    new FieldDefinition { Label = "Stok Grup Adı", Path = "Lines.StockGroupName", DataType = "string", ExampleValue = "Elektronik" },
                    new FieldDefinition { Label = "Stok Kod 1", Path = "Lines.StockCode1", DataType = "string", ExampleValue = "K1" },
                    new FieldDefinition { Label = "Stok Kod 1 Adı", Path = "Lines.StockCode1Name", DataType = "string", ExampleValue = "Kod 1 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 2", Path = "Lines.StockCode2", DataType = "string", ExampleValue = "K2" },
                    new FieldDefinition { Label = "Stok Kod 2 Adı", Path = "Lines.StockCode2Name", DataType = "string", ExampleValue = "Kod 2 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 3", Path = "Lines.StockCode3", DataType = "string", ExampleValue = "K3" },
                    new FieldDefinition { Label = "Stok Kod 3 Adı", Path = "Lines.StockCode3Name", DataType = "string", ExampleValue = "Kod 3 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 4", Path = "Lines.StockCode4", DataType = "string", ExampleValue = "K4" },
                    new FieldDefinition { Label = "Stok Kod 4 Adı", Path = "Lines.StockCode4Name", DataType = "string", ExampleValue = "Kod 4 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 5", Path = "Lines.StockCode5", DataType = "string", ExampleValue = "K5" },
                    new FieldDefinition { Label = "Stok Kod 5 Adı", Path = "Lines.StockCode5Name", DataType = "string", ExampleValue = "Kod 5 Açıklama" },
                    
                    // Miktar ve Fiyat
                    new FieldDefinition { Label = "Miktar", Path = "Lines.Quantity", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "Birim Fiyat", Path = "Lines.UnitPrice", DataType = "number", ExampleValue = "5000.00" },
                    
                    // İndirimler
                    new FieldDefinition { Label = "İndirim Oranı 1 (%)", Path = "Lines.DiscountRate1", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "İndirim Tutarı 1", Path = "Lines.DiscountAmount1", DataType = "number", ExampleValue = "500.00" },
                    new FieldDefinition { Label = "İndirim Oranı 2 (%)", Path = "Lines.DiscountRate2", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "İndirim Tutarı 2", Path = "Lines.DiscountAmount2", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "İndirim Oranı 3 (%)", Path = "Lines.DiscountRate3", DataType = "number", ExampleValue = "2" },
                    new FieldDefinition { Label = "İndirim Tutarı 3", Path = "Lines.DiscountAmount3", DataType = "number", ExampleValue = "100.00" },
                    
                    // KDV
                    new FieldDefinition { Label = "KDV Oranı (%)", Path = "Lines.VatRate", DataType = "number", ExampleValue = "18" },
                    new FieldDefinition { Label = "KDV Tutarı", Path = "Lines.VatAmount", DataType = "number", ExampleValue = "900.00" },
                    
                    // Toplamlar
                    new FieldDefinition { Label = "Satır Toplamı (KDV Hariç)", Path = "Lines.LineTotal", DataType = "number", ExampleValue = "45000.00" },
                    new FieldDefinition { Label = "Satır Genel Toplamı (KDV Dahil)", Path = "Lines.LineGrandTotal", DataType = "number", ExampleValue = "53100.00" },
                    
                    // Diğer
                    new FieldDefinition { Label = "Açıklama", Path = "Lines.Description", DataType = "string", ExampleValue = "Özel not" },
                    new FieldDefinition { Label = "Stok HTML Açıklama", Path = "Lines.HtmlDescription", DataType = "string", Description = "Stok detayındaki HTML editör içeriği", ExampleValue = "<p>Ürün açıklaması</p>" },
                    new FieldDefinition { Label = "Stok Varsayılan Görsel", Path = "Lines.DefaultImagePath", DataType = "string", Description = "Stok ana görseli (FilePath)", ExampleValue = "uploads/stock/abc.jpg" },
                },
                ExchangeRateFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Para Birimi", Path = "ExchangeRates.Currency", DataType = "string", ExampleValue = "USD" },
                    new FieldDefinition { Label = "Kur", Path = "ExchangeRates.ExchangeRate", DataType = "number", ExampleValue = "34.50" },
                    new FieldDefinition { Label = "Kur Tarihi", Path = "ExchangeRates.ExchangeRateDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Resmi Kur", Path = "ExchangeRates.IsOfficial", DataType = "boolean", ExampleValue = "true" },
                }
            };
        }
    }

    /// <summary>
    /// Order için field'lar
    /// </summary>
    public static class OrderFields
    {
        public static ReportTemplateFieldsDto GetFields()
        {
            return new ReportTemplateFieldsDto
            {
                HeaderFields = new List<FieldDefinition>
                {
                    // Temel Bilgiler
                    new FieldDefinition { Label = "Sipariş No", Path = "OfferNo", DataType = "string", ExampleValue = "SIP-2024-001" },
                    new FieldDefinition { Label = "Sipariş Tarihi", Path = "OfferDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Sipariş Tipi", Path = "OfferType", DataType = "string", ExampleValue = "Yurtiçi" },
                    new FieldDefinition { Label = "Revizyon No", Path = "RevisionNo", DataType = "string", ExampleValue = "REV-001" },
                    
                    // Müşteri Bilgileri
                    new FieldDefinition { Label = "Müşteri Adı", Path = "CustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Potansiyel Müşteri Adı", Path = "PotentialCustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Müşteri Kodu (ERP)", Path = "ErpCustomerCode", DataType = "string", ExampleValue = "CUST001" },
                    
                    // Teslimat Bilgileri
                    new FieldDefinition { Label = "Teslimat Tarihi", Path = "DeliveryDate", DataType = "date", ExampleValue = "2024-02-15" },
                    new FieldDefinition { Label = "Teslimat Adresi", Path = "ShippingAddressText", DataType = "string", ExampleValue = "İstanbul, Türkiye" },
                    
                    // Diğer Bilgiler
                    new FieldDefinition { Label = "Satış Temsilcisi", Path = "RepresentativeName", DataType = "string", ExampleValue = "Ahmet Yılmaz" },
                    new FieldDefinition { Label = "Ödeme Tipi", Path = "PaymentTypeName", DataType = "string", ExampleValue = "Vadeli" },
                    new FieldDefinition { Label = "Para Birimi", Path = "Currency", DataType = "string", ExampleValue = "TRY" },
                    new FieldDefinition { Label = "Genel İndirim Oranı", Path = "GeneralDiscountRate", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "Genel İndirim Tutarı", Path = "GeneralDiscountAmount", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "Net Toplam", Path = "Total", DataType = "number", ExampleValue = "4750.00" },
                    new FieldDefinition { Label = "Genel Toplam", Path = "GrandTotal", DataType = "number", ExampleValue = "5605.00" },
                    new FieldDefinition { Label = "Açıklama", Path = "Description", DataType = "string", ExampleValue = "Acil sipariş" },
                    new FieldDefinition { Label = "Belge Seri Tipi", Path = "DocumentSerialTypeName", DataType = "string", ExampleValue = "A Serisi" },
                    
                    // Oluşturma Bilgileri
                    new FieldDefinition { Label = "Oluşturan", Path = "CreatedBy", DataType = "string", ExampleValue = "Mehmet Demir" },
                    new FieldDefinition { Label = "Güncelleyen", Path = "UpdatedBy", DataType = "string", ExampleValue = "Ayşe Kaya" },
                },
                LineFields = new List<FieldDefinition>
                {
                    // Ürün Bilgileri
                    new FieldDefinition { Label = "Ürün Kodu", Path = "Lines.ProductCode", DataType = "string", ExampleValue = "PRD-001" },
                    new FieldDefinition { Label = "Ürün Adı", Path = "Lines.ProductName", DataType = "string", ExampleValue = "Laptop" },
                    new FieldDefinition { Label = "Grup Kodu", Path = "Lines.GroupCode", DataType = "string", ExampleValue = "GRP-001" },
                    new FieldDefinition { Label = "Stok Kodu", Path = "Lines.StockCode", DataType = "string", ExampleValue = "STK-001" },
                    new FieldDefinition { Label = "Stok Birim", Path = "Lines.StockUnit", DataType = "string", ExampleValue = "Adet" },
                    new FieldDefinition { Label = "Üretici Kodu", Path = "Lines.StockManufacturerCode", DataType = "string", ExampleValue = "URT-001" },
                    new FieldDefinition { Label = "Stok Grup Adı", Path = "Lines.StockGroupName", DataType = "string", ExampleValue = "Elektronik" },
                    new FieldDefinition { Label = "Stok Kod 1", Path = "Lines.StockCode1", DataType = "string", ExampleValue = "K1" },
                    new FieldDefinition { Label = "Stok Kod 1 Adı", Path = "Lines.StockCode1Name", DataType = "string", ExampleValue = "Kod 1 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 2", Path = "Lines.StockCode2", DataType = "string", ExampleValue = "K2" },
                    new FieldDefinition { Label = "Stok Kod 2 Adı", Path = "Lines.StockCode2Name", DataType = "string", ExampleValue = "Kod 2 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 3", Path = "Lines.StockCode3", DataType = "string", ExampleValue = "K3" },
                    new FieldDefinition { Label = "Stok Kod 3 Adı", Path = "Lines.StockCode3Name", DataType = "string", ExampleValue = "Kod 3 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 4", Path = "Lines.StockCode4", DataType = "string", ExampleValue = "K4" },
                    new FieldDefinition { Label = "Stok Kod 4 Adı", Path = "Lines.StockCode4Name", DataType = "string", ExampleValue = "Kod 4 Açıklama" },
                    new FieldDefinition { Label = "Stok Kod 5", Path = "Lines.StockCode5", DataType = "string", ExampleValue = "K5" },
                    new FieldDefinition { Label = "Stok Kod 5 Adı", Path = "Lines.StockCode5Name", DataType = "string", ExampleValue = "Kod 5 Açıklama" },
                    
                    // Miktar ve Fiyat
                    new FieldDefinition { Label = "Miktar", Path = "Lines.Quantity", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "Birim Fiyat", Path = "Lines.UnitPrice", DataType = "number", ExampleValue = "5000.00" },
                    
                    // İndirimler
                    new FieldDefinition { Label = "İndirim Oranı 1 (%)", Path = "Lines.DiscountRate1", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "İndirim Tutarı 1", Path = "Lines.DiscountAmount1", DataType = "number", ExampleValue = "500.00" },
                    new FieldDefinition { Label = "İndirim Oranı 2 (%)", Path = "Lines.DiscountRate2", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "İndirim Tutarı 2", Path = "Lines.DiscountAmount2", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "İndirim Oranı 3 (%)", Path = "Lines.DiscountRate3", DataType = "number", ExampleValue = "2" },
                    new FieldDefinition { Label = "İndirim Tutarı 3", Path = "Lines.DiscountAmount3", DataType = "number", ExampleValue = "100.00" },
                    
                    // KDV
                    new FieldDefinition { Label = "KDV Oranı (%)", Path = "Lines.VatRate", DataType = "number", ExampleValue = "18" },
                    new FieldDefinition { Label = "KDV Tutarı", Path = "Lines.VatAmount", DataType = "number", ExampleValue = "900.00" },
                    
                    // Toplamlar
                    new FieldDefinition { Label = "Satır Toplamı (KDV Hariç)", Path = "Lines.LineTotal", DataType = "number", ExampleValue = "45000.00" },
                    new FieldDefinition { Label = "Satır Genel Toplamı (KDV Dahil)", Path = "Lines.LineGrandTotal", DataType = "number", ExampleValue = "53100.00" },
                    
                    // Diğer
                    new FieldDefinition { Label = "Açıklama", Path = "Lines.Description", DataType = "string", ExampleValue = "Özel not" },
                    new FieldDefinition { Label = "Stok HTML Açıklama", Path = "Lines.HtmlDescription", DataType = "string", Description = "Stok detayındaki HTML editör içeriği", ExampleValue = "<p>Ürün açıklaması</p>" },
                    new FieldDefinition { Label = "Stok Varsayılan Görsel", Path = "Lines.DefaultImagePath", DataType = "string", Description = "Stok ana görseli (FilePath)", ExampleValue = "uploads/stock/abc.jpg" },
                },
                ExchangeRateFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Para Birimi", Path = "ExchangeRates.Currency", DataType = "string", ExampleValue = "USD" },
                    new FieldDefinition { Label = "Kur", Path = "ExchangeRates.ExchangeRate", DataType = "number", ExampleValue = "34.50" },
                    new FieldDefinition { Label = "Kur Tarihi", Path = "ExchangeRates.ExchangeRateDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Resmi Kur", Path = "ExchangeRates.IsOfficial", DataType = "boolean", ExampleValue = "true" },
                }
            };
        }
    }

    /// <summary>
    /// Fast quotation / hızlı teklif için field'lar
    /// </summary>
    public static class FastQuotationFields
    {
        public static ReportTemplateFieldsDto GetFields()
        {
            return new ReportTemplateFieldsDto
            {
                HeaderFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Hızlı Teklif No", Path = "OfferNo", DataType = "string", ExampleValue = "HT-42" },
                    new FieldDefinition { Label = "Bağlı Teklif No", Path = "QuotationNo", DataType = "string", ExampleValue = "TKL-2024-001" },
                    new FieldDefinition { Label = "Teklif Tarihi", Path = "OfferDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Teklif Tipi", Path = "OfferType", DataType = "string", ExampleValue = "Hızlı Teklif" },
                    new FieldDefinition { Label = "Revizyon", Path = "RevisionId", DataType = "number", ExampleValue = "3" },
                    new FieldDefinition { Label = "Revizyon No", Path = "RevisionNo", DataType = "string", ExampleValue = "REV-3" },
                    new FieldDefinition { Label = "Müşteri Adı", Path = "CustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Potansiyel Müşteri Adı", Path = "PotentialCustomerName", DataType = "string", ExampleValue = "ABC Şirketi" },
                    new FieldDefinition { Label = "Müşteri Adresi", Path = "ShippingAddressText", DataType = "string", ExampleValue = "Kazım Karabekir Mah. 8501 Sok. No:7 Buca / İzmir" },
                    new FieldDefinition { Label = "Para Birimi", Path = "Currency", DataType = "string", ExampleValue = "TRY" },
                    new FieldDefinition { Label = "Kur", Path = "ExchangeRate", DataType = "number", ExampleValue = "1.00" },
                    new FieldDefinition { Label = "İndirim Oranı 1 (%)", Path = "DiscountRate1", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "İndirim Oranı 2 (%)", Path = "DiscountRate2", DataType = "number", ExampleValue = "0" },
                    new FieldDefinition { Label = "İndirim Oranı 3 (%)", Path = "DiscountRate3", DataType = "number", ExampleValue = "0" },
                    new FieldDefinition { Label = "Genel İndirim Tutarı", Path = "GeneralDiscountAmount", DataType = "number", ExampleValue = "125.00" },
                    new FieldDefinition { Label = "Net Toplam", Path = "Total", DataType = "number", ExampleValue = "875.00" },
                    new FieldDefinition { Label = "Genel Toplam", Path = "GrandTotal", DataType = "number", ExampleValue = "1032.50" },
                    new FieldDefinition { Label = "Onaylandı", Path = "IsApproved", DataType = "boolean", ExampleValue = "false" },
                    new FieldDefinition { Label = "Onay Tarihi", Path = "ApprovedDate", DataType = "date", ExampleValue = "2024-01-16" },
                    new FieldDefinition { Label = "Açıklama", Path = "Description", DataType = "string", ExampleValue = "Hızlı teklif açıklaması" },
                },
                LineFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Kalem Görseli", Path = "Lines.ImagePath", DataType = "string", Description = "Hızlı teklif kalemine eklenen görsel yolu", ExampleValue = "/uploads/pdf-template-assets/1/line-image.jpg" },
                    new FieldDefinition { Label = "Ürün Kodu", Path = "Lines.ProductCode", DataType = "string", ExampleValue = "PRD-001" },
                    new FieldDefinition { Label = "Ürün Adı", Path = "Lines.ProductName", DataType = "string", ExampleValue = "Laptop" },
                    new FieldDefinition { Label = "Miktar", Path = "Lines.Quantity", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "Birim Fiyat", Path = "Lines.UnitPrice", DataType = "number", ExampleValue = "5000.00" },
                    new FieldDefinition { Label = "İndirim Oranı 1 (%)", Path = "Lines.DiscountRate1", DataType = "number", ExampleValue = "10" },
                    new FieldDefinition { Label = "İndirim Tutarı 1", Path = "Lines.DiscountAmount1", DataType = "number", ExampleValue = "500.00" },
                    new FieldDefinition { Label = "İndirim Oranı 2 (%)", Path = "Lines.DiscountRate2", DataType = "number", ExampleValue = "5" },
                    new FieldDefinition { Label = "İndirim Tutarı 2", Path = "Lines.DiscountAmount2", DataType = "number", ExampleValue = "250.00" },
                    new FieldDefinition { Label = "İndirim Oranı 3 (%)", Path = "Lines.DiscountRate3", DataType = "number", ExampleValue = "2" },
                    new FieldDefinition { Label = "İndirim Tutarı 3", Path = "Lines.DiscountAmount3", DataType = "number", ExampleValue = "100.00" },
                    new FieldDefinition { Label = "KDV Oranı (%)", Path = "Lines.VatRate", DataType = "number", ExampleValue = "20" },
                    new FieldDefinition { Label = "KDV Tutarı", Path = "Lines.VatAmount", DataType = "number", ExampleValue = "900.00" },
                    new FieldDefinition { Label = "Satır Toplamı (KDV Hariç)", Path = "Lines.LineTotal", DataType = "number", ExampleValue = "45000.00" },
                    new FieldDefinition { Label = "Satır Genel Toplamı (KDV Dahil)", Path = "Lines.LineGrandTotal", DataType = "number", ExampleValue = "53100.00" },
                    new FieldDefinition { Label = "Açıklama", Path = "Lines.Description", DataType = "string", ExampleValue = "Özel not" },
                },
                ExchangeRateFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Para Birimi", Path = "ExchangeRates.Currency", DataType = "string", ExampleValue = "USD" },
                    new FieldDefinition { Label = "Kur", Path = "ExchangeRates.ExchangeRate", DataType = "number", ExampleValue = "34.50" },
                    new FieldDefinition { Label = "Kur Tarihi", Path = "ExchangeRates.ExchangeRateDate", DataType = "date", ExampleValue = "2024-01-15" },
                    new FieldDefinition { Label = "Manuel Kur", Path = "ExchangeRates.IsManual", DataType = "boolean", ExampleValue = "true" },
                }
            };
        }
    }

    /// <summary>
    /// Activity / aktivite için field'lar
    /// </summary>
    public static class ActivityFields
    {
        public static ReportTemplateFieldsDto GetFields()
        {
            return new ReportTemplateFieldsDto
            {
                HeaderFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Aktivite Konusu", Path = "Subject", DataType = "string", ExampleValue = "Kazakistan fuar görüşmesi" },
                    new FieldDefinition { Label = "Açıklama", Path = "Description", DataType = "string", ExampleValue = "Müşteri ile fuar alanında görüşüldü." },
                    new FieldDefinition { Label = "Aktivite Tipi", Path = "ActivityTypeName", DataType = "string", ExampleValue = "Fuar Görüşmesi" },
                    new FieldDefinition { Label = "Ödeme", Path = "PaymentTypeName", DataType = "string", ExampleValue = "Vadeli" },
                    new FieldDefinition { Label = "Görüşme", Path = "ActivityMeetingTypeName", DataType = "string", ExampleValue = "Yüz yüze" },
                    new FieldDefinition { Label = "İlgilenilen Konular", Path = "ActivityTopicPurposeName", DataType = "string", ExampleValue = "PVC pencere sistemleri" },
                    new FieldDefinition { Label = "Teslimat", Path = "ActivityShippingName", DataType = "string", ExampleValue = "Depodan teslim" },
                    new FieldDefinition { Label = "Başlangıç Tarihi", Path = "StartDateTime", DataType = "date", ExampleValue = "2026-03-25 10:30" },
                    new FieldDefinition { Label = "Bitiş Tarihi", Path = "EndDateTime", DataType = "date", ExampleValue = "2026-03-25 11:30" },
                    new FieldDefinition { Label = "Tüm Gün", Path = "IsAllDay", DataType = "boolean", ExampleValue = "false" },
                    new FieldDefinition { Label = "Durum", Path = "Status", DataType = "string", ExampleValue = "Completed" },
                    new FieldDefinition { Label = "Öncelik", Path = "Priority", DataType = "string", ExampleValue = "High" },
                    new FieldDefinition { Label = "Atanan Kullanıcı", Path = "AssignedUserName", DataType = "string", ExampleValue = "Can Nasif" },
                    new FieldDefinition { Label = "Atanan Kullanıcı E-Posta", Path = "AssignedUserEmail", DataType = "string", ExampleValue = "can.nasif@v3rii.com" },
                    new FieldDefinition { Label = "İletişim Kişisi", Path = "ContactName", DataType = "string", ExampleValue = "Aidar Bekov" },
                    new FieldDefinition { Label = "İletişim E-Posta", Path = "ContactEmail", DataType = "string", ExampleValue = "aidar@example.com" },
                    new FieldDefinition { Label = "İletişim Telefon", Path = "ContactPhone", DataType = "string", ExampleValue = "+7 700 000 00 00" },
                    new FieldDefinition { Label = "Firma Adı", Path = "CustomerName", DataType = "string", ExampleValue = "BEM Kazakhstan" },
                    new FieldDefinition { Label = "Firma Adresi", Path = "CustomerAddress", DataType = "string", ExampleValue = "Almaty / Kazakhstan" },
                    new FieldDefinition { Label = "Müşteri Kodu", Path = "ErpCustomerCode", DataType = "string", ExampleValue = "CR-001" },
                    new FieldDefinition { Label = "Kartvizit Görseli", Path = "PrimaryImageUrl", DataType = "string", ExampleValue = "/uploads/activity/cardsample.jpg" },
                    new FieldDefinition { Label = "Oluşturulma Tarihi", Path = "CreatedDate", DataType = "date", ExampleValue = "2026-03-25 10:00" },
                    new FieldDefinition { Label = "Güncellenme Tarihi", Path = "UpdatedDate", DataType = "date", ExampleValue = "2026-03-25 12:15" },
                },
                LineFields = new List<FieldDefinition>
                {
                    new FieldDefinition { Label = "Görsel Açıklaması", Path = "Images.ResimAciklama", DataType = "string", ExampleValue = "Kartvizit ön yüz" },
                    new FieldDefinition { Label = "Görsel URL", Path = "Images.ResimUrl", DataType = "string", ExampleValue = "/uploads/activity/cardsample.jpg" },
                    new FieldDefinition { Label = "Hatırlatma Dakika", Path = "Reminders.OffsetMinutes", DataType = "number", ExampleValue = "30" },
                    new FieldDefinition { Label = "Hatırlatma Kanalı", Path = "Reminders.Channel", DataType = "string", ExampleValue = "Email" },
                    new FieldDefinition { Label = "Hatırlatma Durumu", Path = "Reminders.Status", DataType = "string", ExampleValue = "Pending" },
                    new FieldDefinition { Label = "Hatırlatma Gönderim Zamanı", Path = "Reminders.SentAt", DataType = "date", ExampleValue = "2026-03-25 09:30" },
                },
                ExchangeRateFields = new List<FieldDefinition>()
            };
        }
    }
}
