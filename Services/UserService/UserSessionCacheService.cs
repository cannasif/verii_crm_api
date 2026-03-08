using crm_api.Interfaces;
using crm_api.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace crm_api.Services
{
    public class UserSessionCacheService : IUserSessionCacheService
    {
        private static readonly TimeSpan DefaultCacheLifetime = TimeSpan.FromDays(30);

        private readonly IMemoryCache _memoryCache;
        private readonly IUnitOfWork _unitOfWork;

        public UserSessionCacheService(IMemoryCache memoryCache, IUnitOfWork unitOfWork)
        {
            _memoryCache = memoryCache;
            _unitOfWork = unitOfWork;
        }

        public string GetCacheKey(Guid sessionId)
        {
            return $"session_{sessionId:D}";
        }

        public void SetActiveSession(Guid sessionId, long userId, DateTime? absoluteExpirationUtc = null)
        {
            var expiration = absoluteExpirationUtc.GetValueOrDefault(DateTime.UtcNow.Add(DefaultCacheLifetime));
            if (expiration <= DateTime.UtcNow)
            {
                expiration = DateTime.UtcNow.AddMinutes(1);
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

            if (session == null || !session.IsActive)
            {
                return false;
            }

            SetActiveSession(session.SessionId, session.UserId, session.CreatedAt.AddDays(30));
            return true;
        }
    }
}
