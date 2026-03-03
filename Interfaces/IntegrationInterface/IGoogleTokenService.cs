using crm_api.DTOs;
using crm_api.Models;

namespace crm_api.Interfaces
{
    public interface IGoogleTokenService
    {
        Task<UserGoogleAccount?> GetAccountAsync(long userId, CancellationToken cancellationToken = default);
        Task<UserGoogleAccount> UpsertConnectionAsync(
            long userId,
            GoogleOAuthTokenResult tokenResult,
            string? googleEmail,
            string configuredScopes,
            CancellationToken cancellationToken = default);
        Task<string?> GetValidAccessTokenAsync(long userId, CancellationToken cancellationToken = default);
        Task<bool> DisconnectAsync(long userId, CancellationToken cancellationToken = default);
    }
}
