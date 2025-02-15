using Microsoft.EntityFrameworkCore;
using Trish.Application.Abstractions.Persistence;

namespace Trish.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<T> AddAsync(T entity)
        {
            await _context.AddAsync(entity);
            return entity;
        }

        public Task DeleteAsync(T entity)
        {
            try
            {
                _context.Set<T>().Remove(entity);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new Exception($"Couldn't delete entity {entity.GetType().Name} with id {entity}", ex);
            }
        }

        public async Task UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task<IReadOnlyCollection<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public async Task<T?> GetByIdAsync(Guid id)
        {
            return await _context.Set<T>().FirstOrDefaultAsync(entity => EF.Property<Guid>(entity, "Id") == id);
        }

        public async Task<T?> GetByUserNameAsync(string name)
        {
            return await _context.Set<T>().FirstOrDefaultAsync(entity => EF.Property<string>(entity, "UserName") == name);
        }

        public async Task<T?> Get(Guid id, Func<IQueryable<T>, IQueryable<T>>? includeExpression = null)
        {
            IQueryable<T> query = _context.Set<T>().AsNoTracking();

            if (includeExpression != null)
            {
                query = includeExpression(query);
            }

            return await query.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);
        }

        public async Task<List<T>> GetAll(Func<IQueryable<T>, IQueryable<T>>? includeExpression = null)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includeExpression != null)
            {
                query = includeExpression(query);
            }

            return await query.ToListAsync();
        }
    }
}