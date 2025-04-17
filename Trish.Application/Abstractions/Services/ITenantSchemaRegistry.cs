namespace Trish.Application.Abstractions.Services
{
    public interface ITenantSchemaRegistry
    {
        Task<string> GetTenantSchemaDefinitionAsync(string tenantId);
        Task<string> FetchSchemaFromDatabaseAsync(string tenantId);
    }
}
