// File: Repository/Repository.cs
using BookMyService.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BookMyServiceBE.Repository
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext _db;
        internal DbSet<T> dbSet;
        public Repository(ApplicationDbContext db)
        {
            _db = db;
            dbSet = _db.Set<T>();
        }

        public async Task CreateAsync(T entity)
        {
            await dbSet.AddAsync(entity);
            await SaveAsync();
        }

        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string? includeProperties = null,
            bool tracked = true)
        {
            IQueryable<T> query = dbSet;
            if (!tracked) query = query.AsNoTracking();
            if (filter != null) query = query.Where(filter);

            if (!string.IsNullOrWhiteSpace(includeProperties))
                foreach (var inc in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    query = query.Include(inc.Trim());

            if (orderBy != null) query = orderBy(query);
            return await query.ToListAsync();
        }

        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter,
            string? includeProperties, int pageSize, int pageNumber, bool tracked = true)
        {
            IQueryable<T> query = dbSet;
            if (!tracked) query = query.AsNoTracking();
            if (filter != null) query = query.Where(filter);

            if (!string.IsNullOrWhiteSpace(includeProperties))
                foreach (var inc in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    query = query.Include(inc.Trim());

            if (pageSize > 0)
            {
                pageSize = Math.Clamp(pageSize, 1, 100);
                pageNumber = Math.Max(pageNumber, 1);
                query = query.Skip(pageSize * (pageNumber - 1)).Take(pageSize);
            }
            return await query.ToListAsync();
        }

        public async Task<T?> GetAsync(Expression<Func<T, bool>> filter,
            string? includeProperties = null, bool tracked = true)
        {
            IQueryable<T> query = dbSet.Where(filter);
            if (!tracked) query = query.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(includeProperties))
                foreach (var inc in includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    query = query.Include(inc.Trim());

            return await query.FirstOrDefaultAsync();
        }

        public async Task<T> UpdateAsync(T entity)
        {
            var prop = entity.GetType().GetProperty("UpdatedAt");
            if (prop != null && prop.PropertyType == typeof(DateTime?))
                prop.SetValue(entity, DateTime.UtcNow);

            dbSet.Update(entity);
            await SaveAsync();
            return entity;
        }

        public async Task RemoveAsync(T entity)
        {
            dbSet.Remove(entity);
            await SaveAsync();
        }

        public Task SaveAsync() => _db.SaveChangesAsync();
    }
}
