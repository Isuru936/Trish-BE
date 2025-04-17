using System.Data;

namespace Trish.Application.Abstractions.Services
{
    public interface IPostgresTenantConnectionManager
    {
        void SetTenantContextAsync(IDbConnection connection, string tenantId);
        Task<IDbConnection> GetConnectionForTenantAsync(string tenantId);
    }
}
