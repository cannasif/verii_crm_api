using crm_api.Data;
using crm_api.DTOs;
using crm_api.Interfaces;
using crm_api.Models;
using Microsoft.EntityFrameworkCore;

namespace crm_api.Services
{
    public class GoogleTokenService : IGoogleTokenService
    {
        private readonly CmsDbContext _dbContext;
        private readonly IGoogleOAuthService _googleOAuthService;
        private readonly IEncryptionService _encryptionService;
        private readonly IUserContextService _userContextService;
        private readonly ILogger<GoogleTokenService> _logger;

        public GoogleTokenService(
            CmsDbContext dbContext,
            IGoogleOAuthService googleOAuthService,
            IEncryptionService encryptionService,
            IUserContextService userContextService,
            ILogger<GoogleTokenService> logger)
        {
            _dbContext = dbContext;
            _googleOAuthService = googleOAuthService;
            _encryptionService = encryptionService;
            _userContextService = userContextService;
            _logger = logger;
        }

        public Task<UserGoogleAccount?> GetAccountAsync(long userId, CancellationToken cancellationToken = default)
        {
            return _dbContext.UserGoogleAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        }

        public async Task<UserGoogleAccount> UpsertConnectionAsync(
            long userId,
            Guid tenantId,
            GoogleOAuthTokenResult tokenResult,
            string? googleEmail,
            string configuredScopes,
            CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            var account = await _dbContext.UserGoogleAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (account == null)
            {
                account = new UserGoogleAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TenantId = tenantId,
                    CreatedAt = now,
                };

                await _dbContext.UserGoogleAccounts.AddAsync(account, cancellationToken);
            }

            account.TenantId = tenantId;

            if (!string.IsNullOrWhiteSpace(tokenResult.RefreshToken))
            {
                account.RefreshTokenEncrypted = _encryptionService.Encrypt(tokenResult.RefreshToken);
            }

            account.AccessTokenEncrypted = _encryptionService.Encrypt(tokenResult.AccessToken);
            account.ExpiresAt = now.AddSeconds(Math.Max(tokenResult.ExpiresInSeconds, 60));
            account.Scopes = string.IsNullOrWhiteSpace(tokenResult.Scope)
                ? configuredScopes
                : tokenResult.Scope;
            account.GoogleEmail = googleEmail;
            account.IsConnected = true;
            account.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return account;
        }

        public async Task<string?> GetValidAccessTokenAsync(long userId, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            var account = await _dbContext.UserGoogleAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (account == null || !account.IsConnected)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var accessToken = SafeDecrypt(account.AccessTokenEncrypted);
            var shouldRefresh = forceRefresh
                || string.IsNullOrWhiteSpace(accessToken)
                || !account.ExpiresAt.HasValue
                || account.ExpiresAt.Value <= now.AddMinutes(1);

            if (!shouldRefresh)
            {
                return accessToken;
            }

            var refreshToken = SafeDecrypt(account.RefreshTokenEncrypted);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                await MarkDisconnectedAsync(account, cancellationToken);
                return null;
            }

            var tenantId = account.TenantId != Guid.Empty
                ? account.TenantId
                : (_userContextService.GetCurrentTenantId() ?? Guid.Empty);

            if (tenantId == Guid.Empty)
            {
                _logger.LogWarning("Tenant context could not be resolved for Google token refresh. UserId: {UserId}", userId);
                await MarkDisconnectedAsync(account, cancellationToken);
                return null;
            }

            try
            {
                var refreshed = await _googleOAuthService.RefreshAccessTokenAsync(refreshToken, tenantId, cancellationToken);
                account.AccessTokenEncrypted = _encryptionService.Encrypt(refreshed.AccessToken);
                if (!string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                {
                    account.RefreshTokenEncrypted = _encryptionService.Encrypt(refreshed.RefreshToken);
                }

                if (!string.IsNullOrWhiteSpace(refreshed.Scope))
                {
                    account.Scopes = refreshed.Scope;
                }

                account.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(refreshed.ExpiresInSeconds, 60));
                account.IsConnected = true;
                account.UpdatedAt = DateTimeOffset.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
                return refreshed.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google access token refresh failed for user {UserId}", userId);
                await MarkDisconnectedAsync(account, cancellationToken);
                return null;
            }
        }

        public async Task<bool> DisconnectAsync(long userId, CancellationToken cancellationToken = default)
        {
            var account = await _dbContext.UserGoogleAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (account == null)
            {
                return false;
            }

            await MarkDisconnectedAsync(account, cancellationToken);
            return true;
        }

        private string? SafeDecrypt(string? encryptedValue)
        {
            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                return null;
            }

            try
            {
                return _encryptionService.Decrypt(encryptedValue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt a Google token.");
                return null;
            }
        }

        private async Task MarkDisconnectedAsync(UserGoogleAccount account, CancellationToken cancellationToken)
        {
            account.IsConnected = false;
            account.AccessTokenEncrypted = null;
            account.RefreshTokenEncrypted = null;
            account.ExpiresAt = null;
            account.Scopes = null;
            account.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
