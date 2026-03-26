using crm_api.Interfaces;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace crm_api.Services
{
    public class UserSessionCacheService : IUserSessionCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly double _jwtExpiryMinutes;

        public UserSessionCacheService(IMemoryCache memoryCache, IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _memoryCache = memoryCache;
            _unitOfWork = unitOfWork;
            _jwtExpiryMinutes = ResolveJwtExpiryMinutes(configuration);
        }

        public string GetCacheKey(Guid sessionId)
        {
            return $"session_{sessionId:D}";
        }

        public void SetActiveSession(Guid sessionId, long userId, DateTime? absoluteExpirationUtc = null)
        {
            var expiration = absoluteExpirationUtc.GetValueOrDefault(DateTimeProvider.UtcNow.AddMinutes(_jwtExpiryMinutes));
            if (expiration <= DateTimeProvider.UtcNow)
            {
                expiration = DateTimeProvider.UtcNow.AddMinutes(1);
            }

            _memoryCache.Set(GetCacheKey(sessionId), userId, expiration);
        }

        public void RemoveSession(Guid sessionId)
        {
            _memoryCache.Remove(GetCacheKey(sessionId));
        }

        public async Task<bool> RestoreSessionAsync(Guid sessionId, long userId, CancellationToken cancellationToken = default)
        {
            var session = await _unitOfWork.UserSessions.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.SessionId == sessionId
                        && s.UserId == userId
                        && s.RevokedAt == null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (session == null || session.RevokedAt != null)
            {
                return false;
            }

            var expiresAtUtc = session.CreatedAt.AddMinutes(_jwtExpiryMinutes);
            if (expiresAtUtc <= DateTimeProvider.UtcNow)
            {
                return false;
            }

            SetActiveSession(session.SessionId, session.UserId, expiresAtUtc);
            return true;
        }

        private static double ResolveJwtExpiryMinutes(IConfiguration configuration)
        {
            var expiryValue = configuration["JwtSettings:ExpiryMinutes"];
            return double.TryParse(expiryValue, out var expiryMinutes) && expiryMinutes > 0
                ? expiryMinutes
                : 60;
        }
    }
}
