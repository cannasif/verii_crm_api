namespace crm_api.DTOs.CustomerDto
{
    public class Customer360ErpMovementDto
    {
        public string CariKod { get; set; } = string.Empty;
        public DateTime? Tarih { get; set; }
        public DateTime? VadeTarihi { get; set; }
        public string? BelgeNo { get; set; }
        public string? Aciklama { get; set; }
        public byte? DovizTuru { get; set; }
        public string? ParaBirimi { get; set; }
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        public decimal TarihSiraliTlBakiye { get; set; }
        public decimal VadeSiraliTlBakiye { get; set; }
        public decimal DovizBorc { get; set; }
        public decimal DovizAlacak { get; set; }
        public decimal TarihSiraliDovizBakiye { get; set; }
        public decimal VadeSiraliDovizBakiye { get; set; }
    }
}
