using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using crm_api.Data;
using crm_api.Interfaces;
using crm_api.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace crm_api.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        protected readonly CmsDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GenericRepository(CmsDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _httpContextAccessor = httpContextAccessor;
        }

        private long? GetCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var idClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("UserId")?.Value;
            return long.TryParse(idClaim, out var userId) ? userId : null;
        }

        /// <summary>
        /// Read-only query for lists, reports, dropdowns
        /// Returns AsNoTracking() queryable
        /// </summary>
        public IQueryable<T> Query(bool tracking = false, bool ignoreQueryFilters = false)
        {
            IQueryable<T> query = _dbSet;

            if (ignoreQueryFilters)
                query = query.IgnoreQueryFilters();

            if (!tracking)
                query = query.AsNoTracking();

            return query;
        }

        /// <summary>
        /// Get entity by ID for READ operations (DTO mapping, display)
        /// Returns AsNoTracking() entity
        /// </summary>
        public async Task<T?> GetByIdAsync(long id)
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);
        }

        public async Task<T?> GetByIdWithAuditUsersAsync(long id)
        {
            return await _dbSet
                .Include(e => e.CreatedByUser)
                .Include(e => e.UpdatedByUser)
                .Include(e => e.DeletedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);
        }

        /// <summary>
        /// Get entity by ID for WRITE operations (Update, Delete)
        /// Returns tracked entity (NO AsNoTracking)
        /// </summary>
        public async Task<T?> GetByIdForUpdateAsync(long id)
        {
            return await _dbSet
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet
                .Where(expression)
                .AsNoTracking()
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet
                .Where(expression)
                .AsNoTracking()
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public async Task<T> AddAsync(T entity)
        {
            entity.CreatedDate = DateTimeProvider.Now;
            entity.CreatedBy = GetCurrentUserId();
            entity.IsDeleted = false;
            await _dbSet.AddAsync(entity).ConfigureAwait(false);
            return entity;
        }

        /// <summary>
        /// Add multiple entities in bulk
        /// </summary>
        public async Task<IEnumerable<T>> AddAllAsync(IEnumerable<T> entities)
        {
            var currentUserId = GetCurrentUserId();
            var currentDate = DateTimeProvider.Now;
            var entityList = entities.ToList();

            foreach (var entity in entityList)
            {
                entity.CreatedDate = currentDate;
                entity.CreatedBy = currentUserId;
                entity.IsDeleted = false;
            }

            await _dbSet.AddRangeAsync(entityList).ConfigureAwait(false);
            return entityList;
        }

        /// <summary>
        /// Update entity (expects tracked entity)
        /// If entity is detached, EF will automatically attach it
        /// </summary>
        public Task<T> UpdateAsync(T entity)
        {
            entity.UpdatedDate = DateTimeProvider.Now;
            entity.UpdatedBy = GetCurrentUserId();
            _dbSet.Update(entity);
            return Task.FromResult(entity);
        }

        /// <summary>
        /// Update multiple entities in bulk
        /// </summary>
        public Task<IEnumerable<T>> UpdateAllAsync(IEnumerable<T> entities)
        {
            var currentUserId = GetCurrentUserId();
            var currentDate = DateTimeProvider.Now;
            var entityList = entities.ToList();

            foreach (var entity in entityList)
            {
                entity.UpdatedDate = currentDate;
                entity.UpdatedBy = currentUserId;
            }

            _dbSet.UpdateRange(entityList);
            return Task.FromResult<IEnumerable<T>>(entityList);
        }

        /// <summary>
        /// Soft delete entity by ID
        /// Uses GetByIdForUpdateAsync to get tracked entity
        /// Returns true if entity was found and deleted, false otherwise
        /// </summary>
        public async Task<bool> SoftDeleteAsync(long id)
        {
            var entity = await GetByIdForUpdateAsync(id).ConfigureAwait(false);
            if (entity == null)
                return false;

            entity.IsDeleted = true;
            entity.DeletedDate = DateTimeProvider.Now;
            entity.DeletedBy = GetCurrentUserId();

            return true;
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _dbSet.AnyAsync(e => e.Id == id && !e.IsDeleted).ConfigureAwait(false);
        }

        public async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync().ConfigureAwait(false);
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.CountAsync(expression).ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize)
        {
            return await _dbSet
                .AsNoTracking()
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<T>> GetPagedAsync(int pageNumber, int pageSize, Expression<Func<T, bool>> filter)
        {
            return await _dbSet
                .Where(filter)
                .AsNoTracking()
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}
