using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class MultiTenantDatabaseQueryService
    {
        private readonly IPostgresTenantConnectionManager _tenantConnectionManager;
        private readonly IChatCompletionService _ai;
        private readonly ITenantSchemaRegistry _schemaRegistry;
        private readonly ILogger _logger;

        public MultiTenantDatabaseQueryService(IPostgresTenantConnectionManager tenantConnectionManager,
                                               IChatCompletionService ai,
                                               ITenantSchemaRegistry schemaRegistry,
                                               ILogger logger)
        {
            _tenantConnectionManager = tenantConnectionManager;
            _ai = ai;
            _schemaRegistry = schemaRegistry;
            _logger = logger;
        }

        public async Task<string> ExecuteNaturalLanguageQueryAsync(string tenantId, string question)
        {
            string schemaDefinition = await _schemaRegistry.GetTenantSchemaDefinitionAsync(tenantId);

            string sqlQuery = await GenerateSqlFromQuestionAsync(question, schemaDefinition);

            using var connection = await _tenantConnectionManager.GetConnectionForTenantAsync(tenantId);

            try
            {
                var result = await connection.QueryAsync(sqlQuery);
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query: {SqlQuery}", sqlQuery);
                throw new Exception("Error executing SQL query", ex);
            }
        }

        private async Task<string> GenerateSqlFromQuestionAsync(string question, string schemaDefinition)
        {
            var chat = new ChatHistory();

            chat.AddSystemMessage($@"
             You are a SQL expert that converts natural language questions to SQL queries for PostgreSQL.
            Only return the SQL query without any explanation.
            Use the schema definition below to create accurate queries.
            For safety, limit results to 100 records maximum.
            
            {schemaDefinition}
            ");

            chat.AddUserMessage($"Question: {question}\nSQL:");

            var response = await _ai.GetChatMessageContentAsync(chat);
            string sql = response.Content.Trim();

            _logger.LogInformation("Generated SQL: {Sql}", sql);

            return sql;
        }
    }
}
