using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Trish.Domain.Enums;

namespace Trish.Application.Services
{
    public class HybridQueryRouter
    {
        private readonly IChatCompletionService _ai;
        private readonly ILogger _logger;

        public HybridQueryRouter(IChatCompletionService ai, ILogger logger)
        {
            _ai = ai;
            _logger = logger;
        }

        public async Task<(QueryType Type, string ReformulatedQuery)> ClassifyQueryAsync(
            string tenantId,
            string userQuery,
            string schemaDefinition)
        {
            var chat = new ChatHistory();

            chat.AddSystemMessage($@"
            Analyze this query to determine if it requires:
            1. Database lookup (for specific records, counts, latest entries, structured data)
            2. Document search (for policies, procedures, general information, unstructured content)
            
            
            The tenant's database has these tables:
            {schemaDefinition}
            
            Return ONLY the following format:
            TYPE: [DATABASE|DOCUMENT]
            QUERY: [Reformulated query optimized for the selected system]");

            chat.AddUserMessage(userQuery);

            var response = await _ai.GetChatMessageContentAsync(chat);

            string[] lines = response.Content!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string typeStr = lines[0].Replace("TYPE:", "").Trim();
            string reformulatedQuery = lines.Length > 1 ? lines[1].Replace("QUERY:", "").Trim() : userQuery;

            QueryType type = typeStr switch
            {
                "DATABASE" => QueryType.DatabaseQuery,
                "DOCUMENT" => QueryType.VectorSearch,
                "HYBRID" => QueryType.Hybrid,
                _ => QueryType.VectorSearch // Default to document search
            };

            _logger.LogInformation("Response: {Response}", response);

            return (type, reformulatedQuery);
        }
    }
}
