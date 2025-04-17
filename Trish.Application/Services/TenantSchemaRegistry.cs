using Dapper;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using Trish.Application.Abstractions.Services;

namespace Trish.Infrastructure
{
    public class TenantSchemaRegistry : ITenantSchemaRegistry
    {
        private readonly IPostgresTenantConnectionManager _connectionManager;
        private readonly ILogger<TenantSchemaRegistry> _logger;
        private readonly ConcurrentDictionary<string, string> _schemaCache = new();

        public TenantSchemaRegistry(IPostgresTenantConnectionManager connectionManager, ILogger<TenantSchemaRegistry> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        public async Task<string> GetTenantSchemaDefinitionAsync(string tenantId)
        {
            if (_schemaCache.TryGetValue(tenantId, out var cachedSchema))
                return cachedSchema;

            var schema = await FetchSchemaFromDatabaseAsync(tenantId);
            _schemaCache[tenantId] = schema;
            return schema;
        }
        public async Task<string> FetchSchemaFromDatabaseAsync(string tenantId)
        {
            using var connection = await _connectionManager.GetConnectionForTenantAsync(tenantId);

            var tables = await connection.QueryAsync<string>(@"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE table_schema = @schema",
                new { schema = $"tenant_{tenantId}" });

            var schemaBuilder = new StringBuilder();
            schemaBuilder.AppendLine("Tables:");

            foreach (var tableName in tables)
            {
                schemaBuilder.AppendLine($"- {tableName}");

                // Get columns for this table
                var columns = await connection.QueryAsync(@"
                SELECT column_name, data_type, column_description(
                    (SELECT oid FROM pg_class WHERE relname = @tableName), 
                    ordinal_position
                ) as description
                FROM information_schema.columns 
                WHERE table_schema = @schema AND table_name = @tableName
                ORDER BY ordinal_position",
                    new { schema = $"tenant_{tenantId}", tableName });

                foreach (var column in columns)
                {
                    var description = !string.IsNullOrEmpty(column.description)
                        ? $" - {column.description}"
                        : "";

                    schemaBuilder.AppendLine($"  - {column.column_name} ({column.data_type}){description}");
                }
            }

            return schemaBuilder.ToString();
        }
    }
}
