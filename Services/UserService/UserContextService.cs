using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using crm_api.Interfaces;
using Microsoft.AspNetCore.Http;

namespace crm_api.Services
{
    public class UserContextService : IUserContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public long? GetCurrentUserId()
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(value, out var userId) && userId > 0 ? userId : null;
        }

        public string? GetCurrentRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
        }

        public Guid? GetCurrentTenantId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user != null)
            {
                var claimValues = new[]
                {
                    user.FindFirst("tenant_id")?.Value,
                    user.FindFirst("tenantId")?.Value,
                    user.FindFirst("TenantId")?.Value,
                };

                foreach (var claim in claimValues)
                {
                    if (Guid.TryParse(claim, out var tenantId))
                    {
                        return tenantId;
                    }
                }
            }

            var branchCode = _httpContextAccessor.HttpContext?.Items["BranchCode"]?.ToString();
            if (!string.IsNullOrWhiteSpace(branchCode))
            {
                return CreateDeterministicTenantIdFromBranch(branchCode);
            }

            return null;
        }

        public Guid ResolveTenantIdOrThrow()
        {
            var tenantId = GetCurrentTenantId();
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant context could not be resolved.");
            }

            return tenantId.Value;
        }

        private static Guid CreateDeterministicTenantIdFromBranch(string branchCode)
        {
            var normalized = branchCode.Trim().ToLowerInvariant();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"crm-tenant:{normalized}"));
            Span<byte> guidBytes = stackalloc byte[16];
            hash[..16].CopyTo(guidBytes);

            return new Guid(guidBytes);
        }
    }
}
