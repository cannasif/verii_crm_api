namespace crm_api.Helpers
{
    public class HangfireMonitoringOptions
    {
        public const string SectionName = "HangfireMonitoring";

        /// <summary>
        /// If true, failed Hangfire jobs are persisted into RII_JOB_FAILURE_LOG.
        /// This has no relation with general application ILogger sinks.
        /// </summary>
        public bool EnableFailureSqlLog { get; set; } = true;

        public int FinalRetryCountThreshold { get; set; } = 3;
        public List<string> CriticalJobs { get; set; } = new();
        public List<string> AlertEmails { get; set; } = new();
    }
}
