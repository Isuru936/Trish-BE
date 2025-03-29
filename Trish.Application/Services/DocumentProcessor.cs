using Microsoft.Extensions.Logging;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class DocumentProcessor
    {
        private readonly ISemanticMemoryService _memoryService;
        private readonly ILogger<DocumentProcessor> _logger;

        public DocumentProcessor(
            ISemanticMemoryService memoryService,
            ILogger<DocumentProcessor> logger)
        {
            _memoryService = memoryService;
            _logger = logger;
        }

        public async Task ProcessDocumentsAsync(string fileUrlPath, string tenentId)
        {

            // Check if collection exists, if not populate
            //if (await _memoryService.CheckCollectionExistsAsync(tenentId))
            // {
            //    await _memoryService.CreateCollectionAsync(tenentId);
            //}
            await _memoryService.PopulatePdfCollectionAsync(fileUrlPath, tenentId);

            // Process PDF

        }

        public async Task<List<string>> QueryDocumentsAsync(string question, string tenentId)
        {
            try
            {
                string collectionName = tenentId;

                // First, diagnose the collection to understand its state
                var collectionCheck = await _memoryService.CheckCollectionExistsAsync(collectionName);

                // Log the incoming query details
                _logger.LogInformation($"Querying collection: {collectionName}");
                _logger.LogInformation($"Question: {question}");

                // Fetch document context
                var results = await _memoryService.FetchDocumentContextAsync(collectionName, question);

                // Log the results
                _logger.LogInformation($"Number of results found: {results.Count}");
                foreach (var result in results)
                {
                    _logger.LogInformation($"Retrieved context: {result}");
                }

                return results;
            }
            catch (Exception ex)
            {
                // Log the full exception details
                _logger.LogError($"Error in QueryDocumentsAsync: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                // Optionally, you can rethrow or return an empty list
                return new List<string>();
            }
        }
    }
}
