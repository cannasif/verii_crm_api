using Microsoft.EntityFrameworkCore;
using crm_api.Models;
using crm_api.Models.Notification;
using crm_api.Models.ReportBuilder;
using crm_api.Data.Configurations;
using depoWebAPI.Models;
using crm_api.Models.PowerBi;
using crm_api.Models.UserPermissions;


namespace crm_api.Data
{
    public class CmsDbContext : DbContext
    {
        public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options)
        {
        }

        public DbSet<City> Cities { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerImage> CustomerImages { get; set; }
        public DbSet<CustomerType> CustomerTypes { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Title> Titles { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityReminder> ActivityReminders { get; set; }
        public DbSet<ActivityImage> ActivityImages { get; set; }
        public DbSet<ActivityType> ActivityTypes { get; set; }
        public DbSet<ActivityMeetingType> ActivityMeetingTypes { get; set; }
        public DbSet<ActivityTopicPurpose> ActivityTopicPurposes { get; set; }
        public DbSet<ActivityShipping> ActivityShippings { get; set; }
        public DbSet<ProductPricing> ProductPricings { get; set; }
        public DbSet<ProductPricingGroupBy> ProductPricingGroupBys { get; set; }
        public DbSet<UserDiscountLimit> UserDiscountLimits { get; set; }
        public DbSet<PaymentType> PaymentTypes { get; set; }
        public DbSet<SalesTypeDefinition> SalesTypeDefinitions { get; set; }
        public DbSet<ShippingAddress> ShippingAddresses { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<TempQuotattion> TempQuotattions { get; set; }
        public DbSet<TempQuotattionLine> TempQuotattionLines { get; set; }
        public DbSet<TempQuotattionExchangeLine> TempQuotattionExchangeLines { get; set; }
        public DbSet<QuotationLine> QuotationLines { get; set; }
        public DbSet<QuotationExchangeRate> QuotationExchangeRates { get; set; }
        public DbSet<QuotationNotes> QuotationNotes { get; set; }
        public DbSet<Demand> Demands { get; set; }
        public DbSet<DemandLine> DemandLines { get; set; }
        public DbSet<DemandExchangeRate> DemandExchangeRates { get; set; }
        public DbSet<DemandNotes> DemandNotes { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderLine> OrderLines { get; set; }
        public DbSet<OrderExchangeRate> OrderExchangeRates { get; set; }
        public DbSet<OrderNotes> OrderNotes { get; set; }
        public DbSet<UserAuthority> UserAuthorities { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<UserDetail> UserDetails { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<PricingRuleHeader> PricingRuleHeaders { get; set; }
        public DbSet<PricingRuleLine> PricingRuleLines { get; set; }
        public DbSet<PricingRuleSalesman> PricingRuleSalesmen { get; set; }
        public DbSet<DocumentSerialType> DocumentSerialTypes { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<StockDetail> StockDetails { get; set; }
        public DbSet<StockImage> StockImages { get; set; }
        public DbSet<StockRelation> StockRelations { get; set; }
        public DbSet<ApprovalAction> ApprovalActions { get; set; }
        public DbSet<ApprovalFlow> ApprovalFlows { get; set; }
        public DbSet<ApprovalFlowStep> ApprovalFlowSteps { get; set; }
        public DbSet<ApprovalRequest> ApprovalRequests { get; set; }
        public DbSet<ApprovalRoleGroup> ApprovalRoleGroups { get; set; }
        public DbSet<ApprovalRole> ApprovalRoles { get; set; }
        public DbSet<ApprovalUserRole> ApprovalUserRoles { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ReportTemplate> ReportTemplates { get; set; }
        public DbSet<PdfTemplateAsset> PdfTemplateAssets { get; set; }
        public DbSet<PdfTablePreset> PdfTablePresets { get; set; }
        public DbSet<ReportDefinition> ReportDefinitions { get; set; }
        public DbSet<ReportAssignment> ReportAssignments { get; set; }
        public DbSet<SmtpSetting> SmtpSettings { get; set; }
        public DbSet<RII_FN_CAHAR> RII_FN_CAHAR { get; set; }
        public DbSet<RII_FN_CARIBAKIYE> RII_FN_CARIBAKIYE { get; set; }

        //Power BI DbSet'leri
        public DbSet<PowerBIReportDefinition> PowerBIReportDefinitions { get; set; }
        public DbSet<PowerBIGroup> PowerBIGroups { get; set; }
        public DbSet<UserPowerBIGroup> UserPowerBIGroups { get; set; }
        public DbSet<PowerBIGroupReportDefinition> PowerBIGroupReportDefinitions { get; set; }
        public DbSet<PowerBIConfiguration> PowerBIConfigurations { get; set; }
        public DbSet<PowerBIReportRoleMapping> PowerBIReportRoleMappings { get; set; }
        public DbSet<PermissionDefinition> PermissionDefinitions { get; set; }
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<PermissionGroupPermission> PermissionGroupPermissions { get; set; }
        public DbSet<UserPermissionGroup> UserPermissionGroups { get; set; }
        public DbSet<UserGoogleAccount> UserGoogleAccounts { get; set; }
        public DbSet<TenantGoogleOAuthSettings> TenantGoogleOAuthSettings { get; set; }
        public DbSet<GoogleIntegrationLog> GoogleIntegrationLogs { get; set; }
        public DbSet<GoogleCustomerMailLog> GoogleCustomerMailLogs { get; set; }
        public DbSet<UserOutlookAccount> UserOutlookAccounts { get; set; }
        public DbSet<OutlookIntegrationLog> OutlookIntegrationLogs { get; set; }
        public DbSet<OutlookCustomerMailLog> OutlookCustomerMailLogs { get; set; }
        public DbSet<JobFailureLog> JobFailureLogs { get; set; }





        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                    {
                        property.SetColumnType("decimal(18,6)");
                    }
                }
            }

            // Kur function yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_KUR>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_FN_KUR", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_FN_KUR");
            });

            // 2SHIPPING function yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_2SHIPPING>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_FN_2SHIPPING", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_FN_2SHIPPING");
            });

            // Stok group function yapılandırması - Key yok
            modelBuilder.Entity<RII_STGROUP>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_STGROUP", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_STGROUP");
            });

            // Stok function yapılandırması - Key yok
            modelBuilder.Entity<RII_FN_STOK>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_FN_STOK", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_FN_STOK");

            });

            modelBuilder.Entity<RII_FN_CAHAR>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_FN_CAHAR", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_FN_CAHAR");
                entity.Property(e => e.CARI_KOD).HasMaxLength(15);
                entity.Property(e => e.BELGE_NO).HasMaxLength(15);
                entity.Property(e => e.ACIKLAMA).HasMaxLength(50);
                entity.Property(e => e.PARA_BIRIMI).HasMaxLength(30);
            });

            modelBuilder.Entity<RII_FN_CARIBAKIYE>(entity =>
            {
                entity.HasNoKey();
                entity.ToTable("__EFMigrationsHistory_FN_CARIBAKIYE", t => t.ExcludeFromMigrations());
                entity.ToFunction("RII_FN_CARIBAKIYE");
                entity.Property(e => e.CARI_KOD).HasMaxLength(35);
                entity.Property(e => e.BAKIYE_DURUMU).HasMaxLength(14);
            });

            // Apply all configurations from the Configurations folder
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmsDbContext).Assembly);
        }
    }
}
