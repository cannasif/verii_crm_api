namespace crm_api.DTOs.CustomerDto
{
    public class Customer360ErpBalanceDto
    {
        public string CariKod { get; set; } = string.Empty;
        public decimal NetBakiye { get; set; }
        public string BakiyeDurumu { get; set; } = string.Empty;
        public decimal BakiyeTutari { get; set; }
        public decimal ToplamBorc { get; set; }
        public decimal ToplamAlacak { get; set; }
    }
}
