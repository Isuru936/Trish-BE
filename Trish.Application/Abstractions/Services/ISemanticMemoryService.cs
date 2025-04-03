using Microsoft.SemanticKernel.Memory;

namespace Trish.Application.Abstractions.Services
{
    public interface ISemanticMemoryService
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        Task<ISemanticTextMemory> CreateMemoryStoreAsync();
        Task<bool> CheckCollectionExistsAsync(string collectionName);
        Task PopulateCollectionFromWebAsync(string url, string collectionName);
        Task PopulatePdfCollectionAsync(string pdfPath, string collectionName);
        Task<string> QueryDocumentCollectionAsync(string collectionName, string question, string organization, int searchLimit = 3);
        //Task<QueryResponse> QueryDocumentsAsync(string question, string tenantId, bool streaming = false);

        IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync(
            string organisation,
            string collectionName,
            string question,
           List<string> contexts = null,
            int searchLimit = 3,
            CancellationToken cancellationToken = default);
        Task<List<string>> FetchDocumentContextAsync(string collectionName,
            string question,
            int searchLimit = 3);
    }
}
