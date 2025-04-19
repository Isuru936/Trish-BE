using Microsoft.Extensions.Logging;
using Trish.Application.Abstractions.Messaging;

namespace Trish.Application.Services
{
    public class DocumentProcessor
    {
        private readonly ISemanticMemoryService _memoryService;
        private readonly ILogger<DocumentProcessor> _logger;
        private readonly MultiTenantDatabaseQueryService _databaseQueryService;

        public DocumentProcessor(
            ISemanticMemoryService memoryService,
            ILogger<DocumentProcessor> logger,
            MultiTenantDatabaseQueryService databaseQueryService)
        {
            _memoryService = memoryService;
            _logger = logger;
            _databaseQueryService = databaseQueryService;
        }

        public async Task ProcessDocumentsAsync(string fileUrlPath, string tenentId)
        {
            await _memoryService.PopulatePdfCollectionAsync(fileUrlPath, tenentId);
        }

        public async IAsyncEnumerable<string> QueryDocumentsAsync(string question, string tenentId, string organization, bool useDbQuery)
        {
            string collectionName = tenentId;

            var collectionCheck = await _memoryService.CheckCollectionExistsAsync(collectionName);


            _logger.LogInformation($"Querying collection: {collectionName}");
            _logger.LogInformation($"Question: {question}");

            if (!useDbQuery)
            {
                await foreach (var result in _memoryService.QueryDocumentCollectionStreamAsync(
                 collectionName,
                 organization,
                 question))
                {
                    _logger.LogInformation($"Streaming result chunk: {result}");
                    yield return result;
                }
            }
            else
            {
                await foreach (var result in _memoryService.HybridQueryAsync(
                    tenentId,
                    collectionName,
                    organization))
                {
                    _logger.LogInformation($"Streaming result chunk: {result}");
                    yield return result;
                }
            }
        }

        public async Task<string> UploadCsvAsync(Stream csvFile, string tableName, string tenantId, bool hasHeaderRow = true, char delimiter = ',')
        {
            _logger.LogInformation($"Processing CSV upload for tenant {tenantId} to table {tableName}");

            try
            {
                // Pass the CSV stream to the database query service
                var result = await _databaseQueryService.UploadCsvToTableAsync(
                    tenantId,
                    tableName,
                    csvFile,
                    hasHeaderRow,
                    delimiter
                );

                _logger.LogInformation($"CSV upload complete for tenant {tenantId} to table {tableName}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading CSV for tenant {tenantId} to table {tableName}");
                throw;
            }
        }


    }
}
