#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0020 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Runtime.CompilerServices;
using System.Text;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class SemanticMemoryService : ISemanticMemoryService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<SemanticMemoryService> logger;
        private readonly Kernel kernel;
        private readonly HttpClient httpClient;
        private static string? _lastUsedCollection;

        public SemanticMemoryService(
            IConfiguration configuration,
            ILogger<SemanticMemoryService> logger,
            Kernel kernel,
            HttpClient httpClient = null)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.kernel = kernel;
            this.httpClient = httpClient ?? new HttpClient();
        }

        #region Core Memory Management


        public async Task<ISemanticTextMemory> CreateMemoryStoreAsync()
        {
            string apiKey = configuration["OpenAI:ApiKey"]!;
            string qdrantUrl = configuration["QdrantSettings:Url"] ?? "http://localhost:6333/";
            int embeddingDimension = 384;
            string embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

            return new MemoryBuilder()
                .WithLoggerFactory(kernel.LoggerFactory)
                .WithMemoryStore(new QdrantMemoryStore(qdrantUrl, embeddingDimension))
                .WithOpenAITextEmbeddingGeneration(embeddingModel, apiKey, dimensions: 384)
                .Build();
        }


        public async Task<bool> CheckCollectionExistsAsync(string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();
            var collections = await memory.GetCollectionsAsync();
            return collections.Contains(collectionName);
        }


        public async Task<List<string>> GetTenantCollectionsAsync(string tenantId)
        {
            var memory = await CreateMemoryStoreAsync();
            var allCollections = await memory.GetCollectionsAsync();

            // Filter for collections belonging to this tenant
            return allCollections.Where(c => c.StartsWith($"{tenantId}_")).ToList();
        }

        //NOT USED
        public async Task EnsureCollectionExistsAsync(string collectionName)
        {
            if (!await CheckCollectionExistsAsync(collectionName))
            {
                var memory = await CreateMemoryStoreAsync();
                // Collection will be created when first document is added
                logger.LogInformation($"Collection {collectionName} will be created on first document");
            }
        }

        #endregion

        #region Document Processing

        //NOT  USED
        public async Task ProcessDocumentAsync(string documentUrl, string tenantId, string documentType)
        {
            string collectionName = $"{tenantId}_{documentType.TrimStart('.')}";

            // Ensure collection exists
            await EnsureCollectionExistsAsync(collectionName);

            try
            {
                // Process based on document type
                if (documentUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // Download PDF first
                    string tempPath = await DownloadFileAsync(documentUrl);
                    await PopulatePdfCollectionAsync(tempPath, collectionName);
                    // Clean up temp file
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                else if (Uri.TryCreate(documentUrl, UriKind.Absolute, out _))
                {
                    await PopulateCollectionFromWebAsync(documentUrl, collectionName);
                }
                else
                {
                    throw new ArgumentException($"Unsupported document source or type: {documentUrl}");
                }

                logger.LogInformation($"Successfully processed document {documentUrl} for tenant {tenantId}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error processing document {documentUrl}: {ex.Message}");
                throw;
            }
        }


        private async Task<string> DownloadFileAsync(string url)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(url));

                byte[] fileData = await httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(tempPath, fileData);

                logger.LogInformation($"Downloaded file from {url} to {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error downloading file from {url}: {ex.Message}");
                throw;
            }
        }

        public async Task PopulateCollectionAsync(string collectionName, List<string> documents)
        {
            var memory = await CreateMemoryStoreAsync();

            for (int i = 0; i < documents.Count; i++)
            {
                try
                {
                    await memory.SaveInformationAsync(
                        collection: collectionName,
                        id: $"document{i}",
                        text: documents[i]
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error saving document {i} to collection {collectionName}: {ex.Message}");
                }
            }

            logger.LogInformation($"Populated collection {collectionName} with {documents.Count} documents");
        }

        public async Task PopulateCollectionFromWebAsync(string url, string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();

            try
            {
                string extractedText = await ExtractTextFromPdfAsync(url);


                List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(
                    TextChunker.SplitPlainTextLines(extractedText, 128),
                    1024);

                int savedCount = 0;
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(paragraphs[i]))
                        continue;

                    await memory.SaveInformationAsync(
                        collection: collectionName,
                        id: $"paragraph{i}",
                        text: paragraphs[i]
                    );
                    savedCount++;
                }

                logger.LogInformation($"INSERTED: Populated collection {collectionName} with {savedCount} paragraphs from {url}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error populating collection from web: {ex.Message}");
                throw;
            }
        }

        public async Task PopulatePdfCollectionAsync(string pdfPath, string collectionName)
        {
            try
            {
                string extractedText = await ExtractTextFromPdfAsync(pdfPath);
                var memory = await CreateMemoryStoreAsync();

                string qdrantUrl = configuration["QdrantSettings:Url"] ?? "http://localhost:6333/";
                var qdrantStore = new QdrantMemoryStore(qdrantUrl, 384);


                List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(
                    TextChunker.SplitPlainTextLines(extractedText, 15),
                    128, overlapTokens: 15);

                await qdrantStore.DeleteCollectionAsync(collectionName);

                // var memory = await CreateMemoryStoreAsync();

                int savedCount = 0;
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(paragraphs[i]))
                        continue;

                    await memory.SaveInformationAsync(
                        collection: collectionName,
                        id: $"paragraph{i}",
                        text: paragraphs[i]
                    );
                    savedCount++;
                }

                logger.LogInformation($"Populated PDF collection {collectionName} with {savedCount} paragraphs");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error processing PDF {pdfPath}: {ex.Message}");
                throw;
            }
        }

        private async Task<string> ExtractTextFromPdfAsync(string pdfPath)
        {
            // Use Task.Run to move the synchronous PDF processing to a background thread
            return await Task.Run(() =>
            {
                using (PdfReader reader = new PdfReader(pdfPath))
                using (PdfDocument pdfDoc = new PdfDocument(reader))
                {
                    StringBuilder textBuilder = new StringBuilder();

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        try
                        {
                            string pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i));
                            textBuilder.AppendLine(pageText);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning($"Error extracting text from page {i}: {ex.Message}");
                            // Continue with next page
                        }
                    }

                    return textBuilder.ToString();
                }
            });
        }

        #endregion

        #region Querying and Retrieval



        public async Task<QueryResponse> QueryDocumentsAsync(string question, string tenantId, string organizatioon, bool streaming = false)
        {
            try
            {
                // Get all collections for this tenant
                var tenantCollections = await GetTenantCollectionsAsync(tenantId);

                if (tenantCollections.Count == 0)
                {
                    logger.LogWarning($"No collections found for tenant {tenantId}");
                    return new QueryResponse
                    {
                        Answer = "No data available to answer this question.",
                        SourceContexts = new List<string>()
                    };
                }

                List<string> allContext = new();

                foreach (var collection in tenantCollections)
                {
                    var contextResults = await FetchDocumentContextAsync(collection, question, searchLimit: 2);
                    allContext.AddRange(contextResults);
                }

                // Generate response
                string answer;
                if (streaming)
                {
                    // Use streaming method - but collect into a single response for now
                    StringBuilder sb = new();
                    await foreach (var chunk in QueryDocumentCollectionStreamAsync(
                        tenantCollections.First(), organizatioon, question, allContext))
                    {
                        sb.Append(chunk);
                    }
                    answer = sb.ToString();
                }
                else
                {
                    // Use direct query
                    answer = await QueryDocumentCollectionAsync(
                        tenantCollections.First(), question, organizatioon, allContext);
                }

                return new QueryResponse
                {
                    Answer = answer,
                    SourceContexts = allContext
                };
            }
            catch (Exception ex)
            {
                logger.LogError($"Error querying documents: {ex.Message}");
                return new QueryResponse
                {
                    Answer = "An error occurred while processing your question.",
                    Error = ex.Message,
                    SourceContexts = new List<string>()
                };
            }
        }


        //JACKPOT
        public async Task<List<string>> FetchDocumentContextAsync(
            string collectionName,
            string question,
            int searchLimit = 3)
        {
            try
            {
                // Create memory store
                var memory = await CreateMemoryStoreAsync();

                // Store retrieved results
                List<string> retrievedContexts = new List<string>();

                // Use the SearchAsync method from ISemanticTextMemory
                await foreach (var result in memory.SearchAsync(
                    collection: collectionName,
                    query: question,
                    minRelevanceScore: 0.1,
                    limit: searchLimit))
                {
                    logger.LogInformation($"Search result - Relevance: {result.Relevance}");
                    if (!string.IsNullOrEmpty(result.Metadata.Text))
                    {
                        retrievedContexts.Add(result.Metadata.Text);
                    }
                }

                logger.LogInformation($"Found {retrievedContexts.Count} context items for query: {question}");
                return retrievedContexts;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in vector search: {ex.Message}");
                logger.LogError($"Stack Trace: {ex.StackTrace}");
                return new List<string>();
            }
        }

        public async Task<string> QueryDocumentCollectionAsync(
            string collectionName,
            string question,
            string orgainzationName,
            List<string> contexts = null)
        {
            try
            {
                StringBuilder contextBuilder = new();

                if (contexts != null && contexts.Count > 0)
                {
                    foreach (var context in contexts)
                    {
                        contextBuilder.AppendLine(context);
                    }
                }
                else
                {
                    var searchResults = await FetchDocumentContextAsync(collectionName, question, 3);
                    foreach (var result1 in searchResults)
                    {
                        contextBuilder.AppendLine(result1);
                    }
                }

                string prompt = $"Context information:\n{contextBuilder}\n\nQuestion: {question}\n\nAnswer:";

                var result = await kernel.InvokePromptAsync(prompt);
                string response = result.ToString();

                logger.LogInformation($"Queried collection {collectionName} with question: {question}");

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError($"Error in query: {ex.Message}");
                return $"An error occurred while processing your question: {ex.Message}";
            }
        }


        // NOT USED
        public async Task<string> QueryDocumentCollectionAsync(
            string collectionName,
            string orgainzationName,
            string question,
            int searchLimit)
        {
            // Pass through to main implementation without contexts
            return await QueryDocumentCollectionAsync(collectionName, question, null);
        }


        // NOT IMPLEMENTED
        public IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync2(
            string collectionName,
            string question,
            List<string> contexts = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync(
           string collectionName,
           string organizationName,
           string question,
           List<string> contexts = null,
            int searchLimit = 3,
           [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var ai = kernel.GetRequiredService<IChatCompletionService>();

            ChatHistory chat;
            logger.LogInformation($"Collection changed from {_lastUsedCollection} to {collectionName}. Chat history reset.");
            chat = new("You are a voice assistant that helps users find information about their organization. " +
                "Use the provided context to answer questions. If no relevant information is found in the context, inform the user that you don't have that specific information for their organization. " +
                "The organization ID will be provided to you in the context. you don't need to respond with the organization");

            StringBuilder contextBuilder = new();

            await foreach (var result in (await CreateMemoryStoreAsync()).SearchAsync(
                collectionName, question, limit: 3, minRelevanceScore: 0.1))
            {
                contextBuilder.AppendLine(result.Metadata.Text);
            }

            int contextToRemove = -1;
            if (contextBuilder.Length != 0)
            {
                contextBuilder.Insert(0, $"Here is information for organization ID {collectionName} but don't respond with the organization Id:\n\n");
                logger.LogInformation($"Adding context for organization ID {collectionName}: {contextBuilder}");
                contextToRemove = chat.Count;
                chat.AddUserMessage(contextBuilder.ToString());
            }

            chat.AddUserMessage(question);

            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chat))
            {
                yield return message.Content;

                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            if (contextToRemove >= 0)
                chat.RemoveAt(contextToRemove);

            logger.LogInformation($"Streamed query for collection {collectionName} with question: {question}");
        }


        public IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync(
            string organizationName,
            string collectionName,
            string question,
            int searchLimit,
            CancellationToken cancellationToken = default)
        {
            // Pass through to main implementation without contexts
            return QueryDocumentCollectionStreamAsync(collectionName, organizationName, question);
        }

        #endregion

        #region Diagnostics & Management

        /// <summary>
        /// Gets metadata about documents for a tenant
        /// </summary>
        public async Task<List<DocumentMetadata>> GetTenantDocumentsAsync(string tenantId)
        {
            var collections = await GetTenantCollectionsAsync(tenantId);
            List<DocumentMetadata> documents = new();

            foreach (var collection in collections)
            {
                try
                {
                    // Extract document type from collection name
                    string documentType = collection.Substring(tenantId.Length + 1);

                    // Get document count
                    int documentCount = await GetCollectionDocumentCountAsync(collection);

                    // Add to document list
                    documents.Add(new DocumentMetadata
                    {
                        Type = documentType,
                        Collection = collection,
                        DocumentCount = documentCount
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error getting document metadata for collection {collection}: {ex.Message}");
                }
            }

            return documents;
        }

        /// <summary>
        /// Gets the number of documents in a collection
        /// </summary>
        private async Task<int> GetCollectionDocumentCountAsync(string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();
            int count = 0;

            // This is inefficient but there's no direct API to get document count
            await foreach (var _ in memory.SearchAsync(collection: collectionName, query: "", limit: 1000))
            {
                count++;
            }

            return count;
        }

        // NOT USED
        public async Task DiagnoseCollectionAsync(string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();

            // Check available collections
            var collections = await memory.GetCollectionsAsync();
            logger.LogInformation($"Available collections: {string.Join(", ", collections)}");

            // Check if specific collection exists
            bool collectionExists = collections.Contains(collectionName);
            logger.LogInformation($"Collection {collectionName} exists: {collectionExists}");

            if (collectionExists)
            {
                // Try to get some basic information about the collection
                int recordCount = 0;
                await foreach (var result in memory.SearchAsync(collection: collectionName, query: "test", limit: 10))
                {
                    recordCount++;
                    logger.LogInformation($"Sample record: {result.Metadata.Text?.Substring(0, Math.Min(100, result.Metadata.Text.Length))}...");
                }
                logger.LogInformation($"Total sample records found: {recordCount}");
            }
        }


        #endregion
    }

    /// <summary>
    /// Response model for document queries
    /// </summary>
    public class QueryResponse
    {
        public string Answer { get; set; }
        public List<string> SourceContexts { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Metadata about a document in a collection
    /// </summary>
    public class DocumentMetadata
    {
        public string Type { get; set; }
        public string Collection { get; set; }
        public int DocumentCount { get; set; }
    }
}