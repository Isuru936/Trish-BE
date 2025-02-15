namespace Trish.Application.Abstractions.Persistence
{
    public interface IGenericRepository<T>
        where T : class
    {
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<IReadOnlyCollection<T>> GetAllAsync();
        Task<T?> GetByIdAsync(Guid id);
        Task<T?> GetByUserNameAsync(string username);
        Task<T?> Get(Guid id, Func<IQueryable<T>, IQueryable<T>>? includeExpression = null);
        Task<List<T>> GetAll(Func<IQueryable<T>, IQueryable<T>>? includeExpression = null);
    }
}
