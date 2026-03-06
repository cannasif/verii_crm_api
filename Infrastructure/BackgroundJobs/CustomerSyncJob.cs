using Hangfire;
using Infrastructure.BackgroundJobs.Interfaces;
using crm_api.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs
{
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 120, 300 })]
    public class CustomerSyncJob : ICustomerSyncJob
    {
        private readonly ICustomerService _customerService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<CustomerSyncJob> _logger;

        public CustomerSyncJob(
            ICustomerService customerService,
            ILocalizationService localizationService,
            ILogger<CustomerSyncJob> logger)
        {
            _customerService = customerService;
            _localizationService = localizationService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                _logger.LogInformation(_localizationService.GetLocalizedString("CustomerSyncJob.Started"));

                await _customerService.SyncCustomersFromErpAsync();

                _logger.LogInformation(_localizationService.GetLocalizedString("CustomerSyncJob.Completed"));
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null
                    ? $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                    : "";
                _logger.LogError(ex,
                    "CustomerSyncJob failed. {ExceptionType}: {Message}{Inner} | {StackTrace}",
                    ex.GetType().FullName,
                    ex.Message,
                    inner,
                    ex.StackTrace);
                throw;
            }
        }
    }
}
