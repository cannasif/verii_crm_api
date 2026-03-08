using crm_api.Models;
using crm_api.DTOs;

namespace crm_api.Interfaces
{
    public interface IJwtService
    {
        ApiResponse<string> GenerateToken(User user, Guid sessionId);
    }
}
