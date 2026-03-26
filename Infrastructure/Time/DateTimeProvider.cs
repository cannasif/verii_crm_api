namespace crm_api.Infrastructure.Time
{
    public static class DateTimeProvider
    {
        public static DateTime Now => DateTime.UtcNow;
        public static DateTime UtcNow => DateTime.UtcNow;
    }
}
