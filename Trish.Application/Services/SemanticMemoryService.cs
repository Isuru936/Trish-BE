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
using Trish.Application.Abstractions.Messaging;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class SemanticMemoryService : ISemanticMemoryService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<SemanticMemoryService> logger;
        private readonly Kernel kernel;
        private readonly HttpClient httpClient;
        private readonly ITenantSchemaRegistry schemaRegistry;
        private readonly MultiTenantDatabaseQueryService dbQueryService;
        private static string? _lastUsedCollection;

        public SemanticMemoryService(
            IConfiguration configuration,
            ILogger<SemanticMemoryService> logger,
            Kernel kernel,
            HttpClient httpClient = null,
            ITenantSchemaRegistry schemaRegistry = null,
            MultiTenantDatabaseQueryService dbQueryService = null)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.kernel = kernel;
            this.httpClient = httpClient ?? new HttpClient();
            this.schemaRegistry = schemaRegistry;
            this.dbQueryService = dbQueryService;
        }

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

                int savedCount = 0;
                for (int i = 0; i < paragraphs.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(paragraphs[i]))
                        continue;

                    string uniqueId = $"paragraph_{Guid.NewGuid()}";

                    logger.LogInformation($"{paragraphs[i]}");

                    await memory.SaveInformationAsync(
                        collection: collectionName,
                        id: uniqueId,
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

        public async IAsyncEnumerable<string> HybridQueryAsync(
            string tenantId,
            string question,
            [EnumeratorCancellation] CancellationToken cancellation = default)
        {
            // var schemaRegistry = kernel.GetRequiredService<ITenantSchemaRegistry>();
            // var dbQueryService = kernel.GetRequiredService<MultiTenantDatabaseQueryService>();
            var ai = kernel.GetRequiredService<IChatCompletionService>();

            logger.LogInformation("Im here in the hybrid query service: {TenantId}", tenantId);

            var schemaDefinition = await schemaRegistry.GetTenantSchemaDefinitionAsync(tenantId);

            var router = new HybridQueryRouter(ai, logger);

            var (queryType, reformulatedQuery) = await router.ClassifyQueryAsync(
                tenantId,
                question,
                schemaDefinition);

            var chatHistory = new ChatHistory(
                "You are a helpful assistant that answers questions based on provided information. " +
                "If the information contains database results, format them in a readable way. " +
                "Be concise and direct in your answers.");
            queryType = Domain.Enums.QueryType.VectorSearch; // For testing purposes

            switch (queryType)
            {
                case Domain.Enums.QueryType.DatabaseQuery:
                    var dbResult = await dbQueryService.ExecuteNaturalLanguageQueryAsync(
                        tenantId,
                        reformulatedQuery);
                    break;

                case Domain.Enums.QueryType.VectorSearch:

                    /* var memory = await CreateMemoryStoreAsync();
                    var results = memory.SearchAsync(
                        collection: tenantId,
                        query: reformulatedQuery,
                        limit: 3,
                        minRelevanceScore: 0.1); */
                    StringBuilder contextBuilder = new();
                    await foreach (var result in (await CreateMemoryStoreAsync()).SearchAsync(
                        tenantId, question, limit: 3, minRelevanceScore: 0.1))
                    {
                        contextBuilder.AppendLine(result.Metadata.Text);
                        logger.LogInformation("Got the result: {Result}", result.Metadata.Text);
                    }
                    logger.LogInformation("Got the result line 240");
                    break;

                case Domain.Enums.QueryType.Hybrid:
                    logger.LogInformation("GOT HYBRID");
                    break;
            }

            chatHistory.AddUserMessage(question);

            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chatHistory))
            {
                yield return message.Content;
            }
        }

    }
}