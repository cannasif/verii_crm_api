using AutoMapper;
using depoWebAPI.Models;
using crm_api.DTOs.ErpDto;

namespace crm_api.Mappings
{
    public class ErpMappingProfile : Profile
    {
        public ErpMappingProfile()
        {


            // Cari mappings
            CreateMap<RII_FN_CARI, CariDto>()
                .ForMember(dest => dest.SubeKodu, opt => opt.MapFrom(src => src.SUBE_KODU))
                .ForMember(dest => dest.IsletmeKodu, opt => opt.MapFrom(src => src.ISLETME_KODU))
                .ForMember(dest => dest.CariKod, opt => opt.MapFrom(src => src.CARI_KOD))
                .ForMember(dest => dest.CariIsim, opt => opt.MapFrom(src => src.CARI_ISIM))
                .ForMember(dest => dest.CariTel, opt => opt.MapFrom(src => src.CARI_TEL))
                .ForMember(dest => dest.CariIl, opt => opt.MapFrom(src => src.CARI_IL))
                .ForMember(dest => dest.CariAdres, opt => opt.MapFrom(src => src.CARI_ADRES))
                .ForMember(dest => dest.CariIlce, opt => opt.MapFrom(src => src.CARI_ILCE))
                .ForMember(dest => dest.UlkeKodu, opt => opt.MapFrom(src => src.ULKE_KODU))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.EMAIL))
                .ForMember(dest => dest.Web, opt => opt.MapFrom(src => src.WEB))
                .ForMember(dest => dest.VergiNumarasi, opt => opt.MapFrom(src => src.VERGI_NUMARASI))
                .ForMember(dest => dest.VergiDairesi, opt => opt.MapFrom(src => src.VERGI_DAIRESI))
                .ForMember(dest => dest.TcknNumber, opt => opt.MapFrom(src => src.TCKIMLIKNO));

            CreateMap<RII_FN_CAHAR, ErpCariMovementDto>()
                .ForMember(dest => dest.CariKod, opt => opt.MapFrom(src => src.CARI_KOD))
                .ForMember(dest => dest.Tarih, opt => opt.MapFrom(src => src.TARIH))
                .ForMember(dest => dest.VadeTarihi, opt => opt.MapFrom(src => src.VADE_TARIHI))
                .ForMember(dest => dest.BelgeNo, opt => opt.MapFrom(src => src.BELGE_NO))
                .ForMember(dest => dest.Aciklama, opt => opt.MapFrom(src => src.ACIKLAMA))
                .ForMember(dest => dest.DovizTuru, opt => opt.MapFrom(src => src.DOVIZ_TURU))
                .ForMember(dest => dest.ParaBirimi, opt => opt.MapFrom(src => src.PARA_BIRIMI))
                .ForMember(dest => dest.Borc, opt => opt.MapFrom(src => src.BORC ?? 0m))
                .ForMember(dest => dest.Alacak, opt => opt.MapFrom(src => src.ALACAK ?? 0m))
                .ForMember(dest => dest.TarihSiraliTlBakiye, opt => opt.MapFrom(src => src.TARIH_SIRALI_TL_BAKIYE ?? 0m))
                .ForMember(dest => dest.VadeSiraliTlBakiye, opt => opt.MapFrom(src => src.VADE_SIRALI_TL_BAKIYE ?? 0m))
                .ForMember(dest => dest.DovizBorc, opt => opt.MapFrom(src => src.DOVIZ_BORC ?? 0m))
                .ForMember(dest => dest.DovizAlacak, opt => opt.MapFrom(src => src.DOVIZ_ALACAK ?? 0m))
                .ForMember(dest => dest.TarihSiraliDovizBakiye, opt => opt.MapFrom(src => src.TARIH_SIRALI_DOVIZ_BAKIYE ?? 0m))
                .ForMember(dest => dest.VadeSiraliDovizBakiye, opt => opt.MapFrom(src => src.VADE_SIRALI_DOVIZ_BAKIYE ?? 0m));

            // Stok mappings
            CreateMap<RII_VW_STOK, StokDto>()
                .ForMember(dest => dest.StokKodu, opt => opt.MapFrom(src => src.STOK_KODU))
                .ForMember(dest => dest.StokAdi, opt => opt.MapFrom(src => src.STOK_ADI))
                .ForMember(dest => dest.GrupKodu, opt => opt.MapFrom(src => src.GRUP_KODU))
                .ForMember(dest => dest.UreticiKodu, opt => opt.MapFrom(src => src.URETICI_KODU))
                .ForMember(dest => dest.OlcuBr1, opt => opt.MapFrom(src => src.OLCU_BR1))
                .ForMember(dest => dest.SatisFiat1, opt => opt.MapFrom(src => src.SATIS_FIAT1))
                .ForMember(dest => dest.SatisFiat2, opt => opt.MapFrom(src => src.SATIS_FIAT2))
                .ForMember(dest => dest.SatisFiat3, opt => opt.MapFrom(src => src.SATIS_FIAT3))
                .ForMember(dest => dest.SatisFiat4, opt => opt.MapFrom(src => src.SATIS_FIAT4))
                .ForMember(dest => dest.KdvOrani, opt => opt.MapFrom(src => src.KDV_ORANI))
                .ForMember(dest => dest.DepoKodu, opt => opt.MapFrom(src => src.DEPO_KODU));


            // Branch mappings
            CreateMap<RII_FN_BRANCHES, BranchDto>()
                .ForMember(dest => dest.SubeKodu, opt => opt.MapFrom(src => src.SUBE_KODU))
                .ForMember(dest => dest.Unvan, opt => opt.MapFrom(src => src.UNVAN));

            // RII_FN_PROJECTCODE (Proje kodu) mappings
            CreateMap<RII_FN_PROJECTCODE, ProjeDto>()
                .ForMember(dest => dest.ProjeKod, opt => opt.MapFrom(src => src.PROJE_KODU))
                .ForMember(dest => dest.ProjeAciklama, opt => opt.MapFrom(src => src.PROJE_ACIKLAMA));

            // Kur mappings
            CreateMap<RII_FN_KUR, KurDto>()
                .ForMember(dest => dest.DovizTipi, opt => opt.MapFrom(src => src.DOVIZ_TIPI))
                .ForMember(dest => dest.DovizIsmi, opt => opt.MapFrom(src => src.DOVIZ_ISMI))
                .ForMember(dest => dest.KurDegeri, opt => opt.MapFrom(src => src.KUR_DEGERI));

            // 2SHIPPING mappings
            CreateMap<RII_FN_2SHIPPING, ErpShippingAddressDto>()
                .ForMember(dest => dest.CustomerCode, opt => opt.MapFrom(src => src.CARI_KOD))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.CARI_ISIM))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => src.CARI_ADRES))
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.CARI_IL))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.CARI_ILCE));

            // Stok group mappings
            CreateMap<RII_STGROUP, StokGroupDto>()
                .ForMember(dest => dest.IsletmeKodu, opt => opt.MapFrom(src => src.ISLETME_KODU))
                .ForMember(dest => dest.SubeKodu, opt => opt.MapFrom(src => src.SUBE_KODU))
                .ForMember(dest => dest.GrupKodu, opt => opt.MapFrom(src => src.GRUP_KOD))
                .ForMember(dest => dest.GrupAdi, opt => opt.MapFrom(src => src.GRUP_ISIM));

            // Stok function mappings
            CreateMap<RII_FN_STOK, StokFunctionDto>()
                .ForMember(dest => dest.SubeKodu, opt => opt.MapFrom(src => src.SUBE_KODU))
                .ForMember(dest => dest.IsletmeKodu, opt => opt.MapFrom(src => src.ISLETME_KODU))
                .ForMember(dest => dest.StokKodu, opt => opt.MapFrom(src => src.STOK_KODU))
                .ForMember(dest => dest.UreticiKodu, opt => opt.MapFrom(src => src.URETICI_KODU))
                .ForMember(dest => dest.StokAdi, opt => opt.MapFrom(src => src.STOK_ADI))
                .ForMember(dest => dest.OlcuBr1, opt => opt.MapFrom(src => src.OLCU_BR1))
                .ForMember(dest => dest.GrupKodu, opt => opt.MapFrom(src => src.GRUP_KODU))
                .ForMember(dest => dest.GrupIsim, opt => opt.MapFrom(src => src.GRUP_ISIM))
                .ForMember(dest => dest.Kod1, opt => opt.MapFrom(src => src.KOD_1))
                .ForMember(dest => dest.Kod1Adi, opt => opt.MapFrom(src => src.KOD1_ADI))
                .ForMember(dest => dest.Kod2, opt => opt.MapFrom(src => src.KOD_2))
                .ForMember(dest => dest.Kod2Adi, opt => opt.MapFrom(src => src.KOD2_ADI))
                .ForMember(dest => dest.Kod3, opt => opt.MapFrom(src => src.KOD_3))
                .ForMember(dest => dest.Kod3Adi, opt => opt.MapFrom(src => src.KOD3_ADI))
                .ForMember(dest => dest.Kod4, opt => opt.MapFrom(src => src.KOD_4))
                .ForMember(dest => dest.Kod4Adi, opt => opt.MapFrom(src => src.KOD4_ADI))
                .ForMember(dest => dest.Kod5, opt => opt.MapFrom(src => src.KOD_5))
                .ForMember(dest => dest.Kod5Adi, opt => opt.MapFrom(src => src.KOD5_ADI))
                .ForMember(dest => dest.IngIsim, opt => opt.MapFrom(src => src.INGISIM));
        }
    }
}
