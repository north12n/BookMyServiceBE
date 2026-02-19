// File: Repository/IRepository.cs
using System.Linq.Expressions;

namespace BookMyServiceBE.Repository
{
    public interface IRepository<T> where T : class
    {
        Task<List<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            string? includeProperties = null,
            bool tracked = true);

        Task<List<T>> GetAllAsync(
            Expression<Func<T, bool>>? filter,
            string? includeProperties,
            int pageSize,
            int pageNumber,
            bool tracked = true);

        Task<T?> GetAsync(Expression<Func<T, bool>> filter,
            string? includeProperties = null,
            bool tracked = true);

        Task CreateAsync(T entity);
        Task<T> UpdateAsync(T entity);
        Task RemoveAsync(T entity);
        Task SaveAsync();
    }
}
