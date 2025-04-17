using Microsoft.SemanticKernel.Memory;
using System.Runtime.CompilerServices;

namespace Trish.Application.Abstractions.Messaging
{
    public interface ISemanticMemoryService
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        Task<ISemanticTextMemory> CreateMemoryStoreAsync();
        // Task PopulateCollectionFromWebAsync(string url, string collectionName);
        Task PopulatePdfCollectionAsync(string pdfPath, string collectionName);
        // Task<string> QueryDocumentCollectionAsync(string collectionName, string question, string organization, int searchLimit = 3);

        IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync(
            string organisation,
            string collectionName,
            string question,
            List<string> contexts = null,
            int searchLimit = 3,
            CancellationToken cancellationToken = default);

        Task<bool> CheckCollectionExistsAsync(string collectionName);
        IAsyncEnumerable<string> HybridQueryAsync(
            string tenantId,
            string question,
            [EnumeratorCancellation] CancellationToken cancellation = default);
    }
}
