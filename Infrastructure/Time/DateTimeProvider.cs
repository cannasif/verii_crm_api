namespace crm_api.Infrastructure.Time
{
    public static class DateTimeProvider
    {
        public static DateTime Now => DateTime.Now;
        public static DateTime UtcNow => DateTime.UtcNow;
    }
}
