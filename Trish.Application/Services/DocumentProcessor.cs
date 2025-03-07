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
            // string webUrl = "https://pub-ab70ac4697984da092b57e2ecb34152e.r2.dev/Isuru%20Bandara-*-e0c90358-2c3c-4efb-9b20-7a9e6f7b69a5.pdf";

            // Check if collection exists, if not populate
            if (!await _memoryService.CheckCollectionExistsAsync(tenentId))
            {
                await _memoryService.PopulateCollectionFromWebAsync(fileUrlPath, tenentId);
            }

            // Process PDF
            string pdfCollectionName = "pdf-documents";

            if (!await _memoryService.CheckCollectionExistsAsync(pdfCollectionName))
            {
                await _memoryService.PopulatePdfCollectionAsync(fileUrlPath, tenentId);
            }
        }

        public async Task<List<string>> QueryDocumentsAsync(string question, string tenentId)
        {
            string collectionName = tenentId;
            return await _memoryService.FetchDocumentContextAsync(collectionName, question);
        }
    }
}
