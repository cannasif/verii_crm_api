using Hangfire.States;
using Hangfire.Storage;
using Hangfire;
using Infrastructure.BackgroundJobs;
using Infrastructure.BackgroundJobs.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using crm_api.Data;
using crm_api.Models;

namespace crm_api.Helpers
{
    public class HangfireJobStateFilter : IApplyStateFilter
    {
        private readonly ILogger<HangfireJobStateFilter> _logger;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly HangfireMonitoringOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;

        public HangfireJobStateFilter(
            ILogger<HangfireJobStateFilter> logger,
            IBackgroundJobClient backgroundJobClient,
            IOptions<HangfireMonitoringOptions> options,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _backgroundJobClient = backgroundJobClient;
            _options = options.Value;
            _scopeFactory = scopeFactory;
        }

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            var jobId = context.BackgroundJob?.Id ?? "unknown";
            var job = context.BackgroundJob?.Job;
            var jobName = job == null ? "unknown" : $"{job.Type.FullName}.{job.Method.Name}";
            var queue = context.GetJobParameter<string>("Queue");

            if (context.NewState is FailedState failedState)
            {
                var retryCount = context.GetJobParameter<int>("RetryCount");
                _logger.LogError(
                    failedState.Exception,
                    "Hangfire job failed. JobId: {JobId}, Job: {JobName}, RetryCount: {RetryCount}, Reason: {Reason}",
                    jobId,
                    jobName,
                    retryCount,
                    failedState.Reason);

                // SQL'e sadece Hangfire failed-state olayları yazılır.
                // Genel ILogger.LogError akışı bu tabloya yönlendirilmez.
                if (_options.EnableFailureSqlLog)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
                        var log = new JobFailureLog
                        {
                            JobId = jobId,
                            JobName = jobName,
                            FailedAt = DateTime.UtcNow,
                            Reason = failedState.Reason,
                            ExceptionType = failedState.Exception?.GetType().FullName,
                            ExceptionMessage = failedState.Exception?.Message,
                            StackTrace = failedState.Exception?.StackTrace?.Length > 8000
                                ? failedState.Exception.StackTrace[..8000]
                                : failedState.Exception?.StackTrace,
                            Queue = queue,
                            RetryCount = retryCount,
                            CreatedDate = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        db.JobFailureLogs.Add(log);
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "JobFailureLog SQL kaydı başarısız. JobId: {JobId}", jobId);
                    }
                }

                // Dead-letter strategy:
                // for critical jobs, when retries are exhausted, enqueue a separate dead-letter job.
                if (IsCriticalJob(jobName) && retryCount >= _options.FinalRetryCountThreshold)
                {
                    var payload = new HangfireDeadLetterPayload
                    {
                        JobId = jobId,
                        JobName = jobName,
                        Queue = queue,
                        RetryCount = retryCount,
                        Reason = failedState.Reason,
                        ExceptionType = failedState.Exception?.GetType().FullName,
                        ExceptionMessage = failedState.Exception?.Message,
                        OccurredAtUtc = DateTime.UtcNow
                    };

                    _backgroundJobClient.Enqueue<IHangfireDeadLetterJob>(x => x.ProcessAsync(payload));
                }
            }
            else if (context.NewState is SucceededState succeededState)
            {
                _logger.LogInformation(
                    "Hangfire job succeeded. JobId: {JobId}, Job: {JobName}, Latency: {Latency}, Duration: {Duration}",
                    jobId,
                    jobName,
                    succeededState.Latency,
                    succeededState.PerformanceDuration);
            }
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
        }

        private bool IsCriticalJob(string jobName)
        {
            if (_options.CriticalJobs == null || _options.CriticalJobs.Count == 0)
            {
                return false;
            }

            return _options.CriticalJobs.Any(pattern =>
                !string.IsNullOrWhiteSpace(pattern) &&
                jobName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }
}
