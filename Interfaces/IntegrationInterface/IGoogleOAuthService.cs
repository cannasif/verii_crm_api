using crm_api.DTOs;

namespace crm_api.Interfaces
{
    public interface IGoogleOAuthService
    {
        Task<string> CreateAuthorizeUrlAsync(long userId, CancellationToken cancellationToken = default);
        Task<bool> ValidateAndConsumeStateAsync(long userId, string state);
        bool TryExtractStateContext(string state, out long userId, out Guid tenantId);
        Task<GoogleOAuthTokenResult> ExchangeCodeForTokensAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);
        Task<GoogleOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, Guid tenantId, CancellationToken cancellationToken = default);
        Task<string?> GetGoogleEmailAsync(string accessToken, string? idToken, CancellationToken cancellationToken = default);
        Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
    }
}
