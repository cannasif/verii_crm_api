using crm_api.Models;
using crm_api.Repositories;
using crm_api.Models.PowerBi;
using crm_api.Models.UserPermissions;

namespace crm_api.UnitOfWork
{
    /// <summary>
    /// Unit of Work pattern interface for managing transactions and repositories
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// User repository
        /// </summary>
        IGenericRepository<User> Users { get; }
        IGenericRepository<Country> Countries { get; }
        IGenericRepository<City> Cities { get; }
        IGenericRepository<District> Districts { get; }
        IGenericRepository<CustomerType> CustomerTypes { get; }
        IGenericRepository<Customer> Customers { get; }
        IGenericRepository<CustomerImage> CustomerImages { get; }
        IGenericRepository<Title> Titles { get; }
        IGenericRepository<UserAuthority> UserAuthorities { get; }
        IGenericRepository<Contact> Contacts { get; }
        IGenericRepository<Activity> Activities { get; }
        IGenericRepository<ActivityImage> ActivityImages { get; }
        IGenericRepository<ActivityType> ActivityTypes { get; }
        IGenericRepository<ActivityMeetingType> ActivityMeetingTypes { get; }
        IGenericRepository<ActivityTopicPurpose> ActivityTopicPurposes { get; }
        IGenericRepository<ActivityShipping> ActivityShippings { get; }
        IGenericRepository<ProductPricing> ProductPricings { get; }
        IGenericRepository<ProductPricingGroupBy> ProductPricingGroupBys { get; }
        IGenericRepository<UserDiscountLimit> UserDiscountLimits { get; }
        IGenericRepository<PaymentType> PaymentTypes { get; }
        IGenericRepository<SalesTypeDefinition> SalesTypeDefinitions { get; }
        IGenericRepository<ShippingAddress> ShippingAddresses { get; }
        IGenericRepository<Quotation> Quotations { get; }
        IGenericRepository<TempQuotattion> TempQuotattions { get; }
        IGenericRepository<TempQuotattionLine> TempQuotattionLines { get; }
        IGenericRepository<TempQuotattionExchangeLine> TempQuotattionExchangeLines { get; }
        IGenericRepository<QuotationLine> QuotationLines { get; }
        IGenericRepository<UserSession> UserSessions { get; }
        IGenericRepository<QuotationExchangeRate> QuotationExchangeRates { get; }
        IGenericRepository<QuotationNotes> QuotationNotes { get; }
        IGenericRepository<Demand> Demands { get; }
        IGenericRepository<DemandLine> DemandLines { get; }
        IGenericRepository<DemandExchangeRate> DemandExchangeRates { get; }
        IGenericRepository<DemandNotes> DemandNotes { get; }
        IGenericRepository<Order> Orders { get; }
        IGenericRepository<OrderLine> OrderLines { get; }
        IGenericRepository<OrderExchangeRate> OrderExchangeRates { get; }
        IGenericRepository<OrderNotes> OrderNotes { get; }
        IGenericRepository<UserDetail> UserDetails { get; }
        IGenericRepository<PricingRuleHeader> PricingRuleHeaders { get; }
        IGenericRepository<PricingRuleLine> PricingRuleLines { get; }
        IGenericRepository<PricingRuleSalesman> PricingRuleSalesmen { get; }
        IGenericRepository<DocumentSerialType> DocumentSerialTypes { get; }
        IGenericRepository<Stock> Stocks { get; }
        IGenericRepository<StockDetail> StockDetails { get; }
        IGenericRepository<ApprovalAction> ApprovalActions { get; }
        IGenericRepository<ApprovalFlow> ApprovalFlows { get; }
        IGenericRepository<ApprovalFlowStep> ApprovalFlowSteps { get; }
        IGenericRepository<ApprovalRequest> ApprovalRequests { get; }
        IGenericRepository<ApprovalRoleGroup> ApprovalRoleGroups { get; }
        IGenericRepository<ApprovalRole> ApprovalRoles { get; }
        IGenericRepository<ApprovalUserRole> ApprovalUserRoles { get; }
        IGenericRepository<crm_api.Models.Notification.Notification> Notifications { get; }
        IGenericRepository<crm_api.Models.ReportBuilder.ReportDefinition> ReportDefinitions { get; }
        IGenericRepository<crm_api.Models.ReportBuilder.ReportAssignment> ReportAssignments { get; }
        IGenericRepository<SmtpSetting> SmtpSettings { get; }
        IGenericRepository<PowerBIReportDefinition> PowerBIReportDefinitions { get; }
        IGenericRepository<PowerBIGroup> PowerBIGroups { get; }
        IGenericRepository<UserPowerBIGroup> UserPowerBIGroups { get; }
        IGenericRepository<PowerBIGroupReportDefinition> PowerBIGroupReportDefinitions { get; }
        IGenericRepository<PowerBIConfiguration> PowerBIConfigurations { get; }
        IGenericRepository<PowerBIReportRoleMapping> PowerBIReportRoleMappings { get; }
        IGenericRepository<PermissionDefinition> PermissionDefinitions { get; }
        IGenericRepository<PermissionGroup> PermissionGroups { get; }
        IGenericRepository<PermissionGroupPermission> PermissionGroupPermissions { get; }
        IGenericRepository<UserPermissionGroup> UserPermissionGroups { get; }

        /// <summary>
        /// Save all changes to the database within a transaction
        /// </summary>
        /// <returns>Number of affected rows</returns>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Begin a new transaction
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commit the current transaction
        /// </summary>
        Task CommitTransactionAsync();

        /// <summary>
        /// Rollback the current transaction
        /// </summary>
        Task RollbackTransactionAsync();

        /// <summary>
        /// Get repository for any entity type
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>Generic repository for the entity</returns>
        IGenericRepository<T> Repository<T>() where T : BaseEntity;
    }
}
