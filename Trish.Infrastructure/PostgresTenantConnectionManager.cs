using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using Trish.Application.Abstractions.Services;

namespace Trish.Infrastructure
{
    public class PostgresTenantConnectionManager : IPostgresTenantConnectionManager
    {
        private readonly IDbConnection _sharedConnection;
        private readonly ILogger<PostgresTenantConnectionManager> _logger;

        public PostgresTenantConnectionManager(IConfiguration configuration, ILogger<PostgresTenantConnectionManager> logger)
        {
            var connectionString = configuration.GetConnectionString("IdentityConnection");
            _sharedConnection = new NpgsqlConnection(connectionString);
            _logger = logger;
        }

        public void EnsureTenantSchemaExistsAsync(string tenantId)
        {
            if (_sharedConnection.State != ConnectionState.Open)
                _sharedConnection.Open();

            using (var command = _sharedConnection.CreateCommand())
            {
                command.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{tenantId}\"";
                command.ExecuteNonQuery();

                _logger.LogInformation("Tenant schema '{TenantId}' created or already exists.", tenantId);
            }
        }

        public void SetTenantContextAsync(IDbConnection connection, string tenantId)
        {
            EnsureTenantSchemaExistsAsync(tenantId);

            using var command = connection.CreateCommand();
            command.CommandText = $"SET search_path TO \"{tenantId}\"";
            command.ExecuteNonQuery();

            _logger.LogDebug("Set search path to tenant_{TenantId}", tenantId);
        }

        public async Task<IDbConnection> GetConnectionForTenantAsync(string tenantId)
        {
            // EnsureTenantSchemaExistsAsync(tenantId);

            var connection = new NpgsqlConnection(_sharedConnection.ConnectionString);
            await connection.OpenAsync();

            SetTenantContextAsync(connection, tenantId);

            return connection;
        }

    }
}
