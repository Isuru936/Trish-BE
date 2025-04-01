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

        public async IAsyncEnumerable<string> QueryDocumentsAsync(string question, string tenentId)
        {
            string collectionName = tenentId;

            // First, diagnose the collection to understand its state
            var collectionCheck = await _memoryService.CheckCollectionExistsAsync(collectionName);

            // Log the incoming query details
            _logger.LogInformation($"Querying collection: {collectionName}");
            _logger.LogInformation($"Question: {question}");

            // Fetch document context and await the results
            //  var contexts = await _memoryService.FetchDocumentContextAsync(collectionName, question, 3);
            // _logger.LogInformation($"contexts: {contexts}");


            // Stream results using the streaming method from the memory service
            await foreach (var result in _memoryService.QueryDocumentCollectionStreamAsync(
                collectionName,
                question))
            {
                _logger.LogInformation($"Streaming result chunk: {result}");
                yield return result;
            }
        }
    }
}
