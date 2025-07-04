﻿using Microsoft.Extensions.Logging;
using Trish.Application.Abstractions.Messaging;

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

        public async Task ProcessDocumentsAsync(string fileUrlPath, string tenentId, string fileName)
        {
            await _memoryService.PopulatePdfCollectionAsync(fileUrlPath, tenentId, fileName);
        }

        public async IAsyncEnumerable<string> QueryDocumentsAsync(string question, string tenentId, string organization)
        {
            string collectionName = tenentId;

            var collectionCheck = await _memoryService.CheckCollectionExistsAsync(collectionName);


            _logger.LogInformation($"Querying collection: {collectionName}");
            _logger.LogInformation($"Question: {question}");

            await foreach (var result in _memoryService.QueryDocumentCollectionStreamAsync(
                collectionName,
                organization,
                question))
            {
                _logger.LogInformation($"Streaming result chunk: {result}");
                yield return result;
            }
        }

        public async Task DeleteDocument(string tenantId, string fileName)
        {
            await _memoryService.DeleteDocumentFromVectorDb(tenantId, fileName);
            _logger.LogInformation($"Deleted document {fileName} from tenant {tenantId}.");
        }
    }
}
