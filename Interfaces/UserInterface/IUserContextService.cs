namespace crm_api.Interfaces
{
    public interface IUserContextService
    {
        long? GetCurrentUserId();
        string? GetCurrentRole();
        Guid? GetCurrentTenantId();
        Guid ResolveTenantIdOrThrow();
    }
}
