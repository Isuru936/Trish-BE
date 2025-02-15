using Trish.Application.Abstractions.Persistence;

namespace Trish.Infrastructure
{
    internal class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context) => _context = context;

        public Task SaveChangesAsync(CancellationToken cancellation)
        {
            return _context.SaveChangesAsync(cancellation);
        }
    }
}
