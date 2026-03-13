using Microsoft.EntityFrameworkCore;
using depoWebAPI.Models;

namespace crm_api.Data
{
    /// <summary>
    /// ERP veritabanı bağlantısı için DbContext
    /// SQL View'lar ve Function'lar için kullanılır
    /// </summary>
    public class ErpCmsDbContext : DbContext
    {
        public ErpCmsDbContext(DbContextOptions<ErpCmsDbContext> options) : base(options)
        {
        }

        // ERP DbSet'leri
        public DbSet<RII_FN_CARI> RII_FN_CARI { get; set; }
        public DbSet<RII_VW_STOK> RII_VW_STOK { get; set; }
        public DbSet<RII_FN_BRANCHES> Branches { get; set; }
        public DbSet<RII_FN_PROJECTCODE> RII_FN_PROJECTCODE { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cari view yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_CARI>(entity =>
            {
                entity.HasNoKey();
                entity.ToFunction("RII_FN_CARI");
                entity.Property(e => e.CARI_KOD).HasMaxLength(25);
                entity.Property(e => e.CARI_ISIM).HasMaxLength(100);
                entity.Property(e => e.CARI_TEL).HasMaxLength(20);
                entity.Property(e => e.CARI_IL).HasMaxLength(50);
                entity.Property(e => e.CARI_ADRES).HasMaxLength(500);
            });

            // Stok view yapılandırması - Key yok
            modelBuilder.Entity<RII_VW_STOK>(entity =>
            {
                entity.HasNoKey();
                entity.ToFunction("RII_VW_STOK");
                entity.Property(e => e.STOK_KODU).HasMaxLength(25);
                entity.Property(e => e.STOK_ADI).HasMaxLength(50);
                entity.Property(e => e.GRUP_KODU).HasMaxLength(10);
                entity.Property(e => e.URETICI_KODU).HasMaxLength(25);
            });


            // Branches function yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_BRANCHES>(entity =>
            {
                entity.HasNoKey();
                entity.ToFunction("RII_FN_BRANCHES");
                entity.Property(e => e.UNVAN).HasMaxLength(150);
            });

            // RII_FN_PROJECTCODE function yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_PROJECTCODE>(entity =>
            {
                entity.HasNoKey();
                entity.ToFunction("RII_FN_PROJECTCODE");
                entity.Property(e => e.PROJE_KODU).HasMaxLength(15);
                entity.Property(e => e.PROJE_ACIKLAMA).HasMaxLength(50);
            });
        }
    }
}
