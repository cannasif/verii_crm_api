namespace depoWebAPI.Models
{
    public class RII_FN_CAHAR
    {
        public string CARI_KOD { get; set; } = string.Empty;
        public DateTime? TARIH { get; set; }
        public DateTime? VADE_TARIHI { get; set; }
        public string? BELGE_NO { get; set; }
        public string? ACIKLAMA { get; set; }
        public byte? DOVIZ_TURU { get; set; }
        public string? PARA_BIRIMI { get; set; }
        public decimal? BORC { get; set; }
        public decimal? ALACAK { get; set; }
        public decimal? TARIH_SIRALI_TL_BAKIYE { get; set; }
        public decimal? VADE_SIRALI_TL_BAKIYE { get; set; }
        public decimal? DOVIZ_BORC { get; set; }
        public decimal? DOVIZ_ALACAK { get; set; }
        public decimal? TARIH_SIRALI_DOVIZ_BAKIYE { get; set; }
        public decimal? VADE_SIRALI_DOVIZ_BAKIYE { get; set; }
    }
}
