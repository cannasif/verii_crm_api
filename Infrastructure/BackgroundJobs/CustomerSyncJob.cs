using Hangfire;
using Infrastructure.BackgroundJobs.Interfaces;
using crm_api.Interfaces;
using crm_api.Data;
using crm_api.Models;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 120, 300 })]
    public class CustomerSyncJob : ICustomerSyncJob
    {
        private const string RecurringJobId = "erp-customer-sync-job";
        private readonly IUnitOfWork _unitOfWork;
        private readonly IErpService _erpService;
        private readonly CmsDbContext _db;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<CustomerSyncJob> _logger;

        public CustomerSyncJob(
            IUnitOfWork unitOfWork,
            IErpService erpService,
            CmsDbContext db,
            ILocalizationService localizationService,
            ILogger<CustomerSyncJob> logger)
        {
            _unitOfWork = unitOfWork;
            _erpService = erpService;
            _db = db;
            _localizationService = localizationService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation(_localizationService.GetLocalizedString("CustomerSyncJob.Started"));

            var erpResponse = await _erpService.GetCarisAsync(null);
            if (erpResponse == null || !erpResponse.Success)
            {
                var message = erpResponse?.ExceptionMessage ?? erpResponse?.Message ?? _localizationService.GetLocalizedString("CustomerSyncJob.ErpFetchFailed");
                var ex = new InvalidOperationException(message);
                await LogRecordFailureAsync("ERP_FETCH", ex);
                _logger.LogWarning("Customer sync aborted: ERP fetch failed. Message: {Message}", message);
                return;
            }

            if (erpResponse?.Data == null || erpResponse.Data.Count == 0)
            {
                _logger.LogInformation("Customer sync skipped: no ERP records returned.");
                return;
            }

            var createdCount = 0;
            var updatedCount = 0;
            var reactivatedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;
            var duplicatePayloadCount = 0;
            var processedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var erpCustomer in erpResponse.Data)
            {
                var code = erpCustomer.CariKod ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    skippedCount++;
                    continue;
                }

                if (!processedCodes.Add(code))
                {
                    duplicatePayloadCount++;
                    continue;
                }

                try
                {
                    var customer = await _unitOfWork.Customers
                        .Query(tracking: true, ignoreQueryFilters: true)
                        .FirstOrDefaultAsync(x => x.CustomerCode == code);

                    var name = string.IsNullOrWhiteSpace(erpCustomer.CariIsim) ? code : erpCustomer.CariIsim!;
                    var taxOffice = erpCustomer.VergiDairesi ?? string.Empty;
                    var taxNumber = erpCustomer.VergiNumarasi ?? string.Empty;
                    var tcknNumber = erpCustomer.TcknNumber ?? string.Empty;
                    var email = erpCustomer.Email ?? string.Empty;
                    var website = erpCustomer.Web ?? string.Empty;
                    var phone1 = erpCustomer.CariTel ?? string.Empty;
                    var address = erpCustomer.CariAdres ?? string.Empty;
                    var branchCode = erpCustomer.SubeKodu;
                    var businessUnitCode = erpCustomer.IsletmeKodu;

                    if (customer == null)
                    {
                        await _unitOfWork.Customers.AddAsync(new Customer
                        {
                            CustomerCode = code,
                            CustomerName = name,
                            TaxOffice = taxOffice,
                            TaxNumber = taxNumber,
                            TcknNumber = tcknNumber,
                            Email = email,
                            Website = website,
                            Phone1 = phone1,
                            Address = address,
                            BranchCode = branchCode,
                            BusinessUnitCode = businessUnitCode,
                            IsERPIntegrated = true,
                            ERPIntegrationNumber = code,
                            LastSyncDate = DateTime.UtcNow
                        });
                        await _unitOfWork.SaveChangesAsync();
                        createdCount++;
                        continue;
                    }

                    var updated = false;
                    var reactivated = false;

                    if (customer.CustomerName != name) { customer.CustomerName = name; updated = true; }
                    if (customer.TaxOffice != taxOffice) { customer.TaxOffice = taxOffice; updated = true; }
                    if (customer.TaxNumber != taxNumber) { customer.TaxNumber = taxNumber; updated = true; }
                    if (customer.TcknNumber != tcknNumber) { customer.TcknNumber = tcknNumber; updated = true; }
                    if (customer.Email != email) { customer.Email = email; updated = true; }
                    if (customer.Website != website) { customer.Website = website; updated = true; }
                    if (customer.Phone1 != phone1) { customer.Phone1 = phone1; updated = true; }
                    if (customer.Address != address) { customer.Address = address; updated = true; }
                    if (customer.BranchCode != branchCode) { customer.BranchCode = branchCode; updated = true; }
                    if (customer.BusinessUnitCode != businessUnitCode) { customer.BusinessUnitCode = businessUnitCode; updated = true; }

                    if (customer.IsDeleted)
                    {
                        customer.IsDeleted = false;
                        customer.DeletedDate = null;
                        customer.DeletedBy = null;
                        updated = true;
                        reactivated = true;
                    }

                    if (customer.IsERPIntegrated != true) { customer.IsERPIntegrated = true; updated = true; }
                    if (customer.ERPIntegrationNumber != code) { customer.ERPIntegrationNumber = code; updated = true; }

                    if (!updated)
                    {
                        continue;
                    }

                    customer.UpdatedDate = DateTimeProvider.Now;
                    customer.UpdatedBy = null;
                    customer.LastSyncDate = DateTime.UtcNow;

                    await _unitOfWork.SaveChangesAsync();

                    if (reactivated)
                    {
                        reactivatedCount++;
                    }
                    else
                    {
                        updatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    await LogRecordFailureAsync(code, ex);
                    _db.ChangeTracker.Clear();
                }
            }

            _logger.LogInformation(
                "Customer sync completed. created={Created}, updated={Updated}, reactivated={Reactivated}, failed={Failed}, skipped={Skipped}, duplicatePayload={DuplicatePayload}.",
                createdCount,
                updatedCount,
                reactivatedCount,
                failedCount,
                skippedCount,
                duplicatePayloadCount);
            _logger.LogInformation(_localizationService.GetLocalizedString("CustomerSyncJob.Completed"));
        }

        private async Task LogRecordFailureAsync(string code, Exception ex)
        {
            _logger.LogError(ex, "Customer sync record failed. CustomerCode: {CustomerCode}", code);

            try
            {
                _db.JobFailureLogs.Add(new JobFailureLog
                {
                    JobId = $"{RecurringJobId}:{code}:{DateTime.UtcNow:yyyyMMddHHmmssfff}",
                    JobName = $"{typeof(CustomerSyncJob).FullName}.ExecuteAsync",
                    FailedAt = DateTime.UtcNow,
                    Reason = $"CustomerCode={code}",
                    ExceptionType = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    StackTrace = ex.StackTrace?.Length > 8000 ? ex.StackTrace[..8000] : ex.StackTrace,
                    Queue = "default",
                    RetryCount = 0,
                    CreatedDate = DateTimeProvider.Now,
                    IsDeleted = false
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Customer sync failure could not be written to RII_JOB_FAILURE_LOG. CustomerCode: {CustomerCode}", code);
            }
        }
    }
}
