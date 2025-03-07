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
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    public class SemanticMemoryService : ISemanticMemoryService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<SemanticMemoryService> logger;
        private readonly Kernel kernel;

        public SemanticMemoryService(IConfiguration configuration, ILogger<SemanticMemoryService> logger, Kernel kernel)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.kernel = kernel;
        }

        public async Task<bool> CheckCollectionExistsAsync(string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();
            var collections = await memory.GetCollectionsAsync();
            return collections.Contains(collectionName);
        }

        public async Task<ISemanticTextMemory> CreateMemoryStoreAsync()
        {
            string apiKey = configuration["OpenAI:ApiKey"]!;
            string qdrantUrl = configuration["QdrantSettings:Url"] ?? "http://localhost:6333/";
            int embeddingDimension = int.Parse(configuration["QdrantSettings:EmbeddingDimension"] ?? "1536");

            return new MemoryBuilder()
                .WithLoggerFactory(kernel.LoggerFactory)
                .WithMemoryStore(new QdrantMemoryStore(qdrantUrl, embeddingDimension))
                .WithOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey)
                .Build();
        }

        public async Task PopulateCollectionFromWebAsync(string url, string collectionName)
        {
            var memory = await CreateMemoryStoreAsync();

            using HttpClient client = new();
            string s = await client.GetStringAsync(url);

            List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(
                TextChunker.SplitPlainTextLines(
                    WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]+>|&nbsp;", "")),
                    128),
                1024);

            for (int i = 0; i < paragraphs.Count; i++)
                await memory.SaveInformationAsync(collectionName, paragraphs[i], $"paragraph{i}");

            logger.LogInformation($"Populated collection {collectionName} with {paragraphs.Count} paragraphs");
        }

        public async Task PopulatePdfCollectionAsync(string pdfPath, string collectionName)
        {
            // PDF text extraction
            string extractedText = await ExtractTextFromPdfAsync(pdfPath);

            // Process text into chunks
            List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(
                TextChunker.SplitPlainTextLines(extractedText, 128),
                1024);

            // Store extracted text
            var memory = await CreateMemoryStoreAsync();

            var pragraphCount = await memory.GetAsync(collectionName, "paragraph0");

            for (int i = 0; i < paragraphs.Count; i++)
            {
                logger.LogInformation($"Saving paragraph {i} to collection {collectionName}");
                await memory.SaveInformationAsync(collectionName, paragraphs[i], $"paragraph{i}");
            }

            logger.LogInformation($"Populated PDF collection {collectionName}");
        }

        private async Task<string> ExtractTextFromPdfAsync(string pdfPath)
        {
            using (PdfReader reader = new PdfReader(pdfPath))
            using (PdfDocument pdfDoc = new PdfDocument(reader))
            {
                string text = "";
                for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                {
                    text += PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i)) + Environment.NewLine;
                }
                return await Task.FromResult(text);
            }
        }

        public async Task<string> QueryDocumentCollectionAsync(string collectionName, string question, int searchLimit = 3)
        {
            // Get chat completion service
            var ai = kernel.GetRequiredService<IChatCompletionService>();

            // Create memory store
            var memory = await CreateMemoryStoreAsync();

            // Create chat history
            ChatHistory chat = new("You are an AI assistant that helps people find information.");

            // Search for relevant context
            StringBuilder contextBuilder = new();
            await foreach (var result in memory.SearchAsync(collectionName, question, limit: searchLimit))
            {
                contextBuilder.AppendLine(result.Metadata.Text);
            }

            // Prepare context
            int contextToRemove = -1;
            if (contextBuilder.Length != 0)
            {
                contextBuilder.Insert(0, "Here's some additional information: ");
                contextToRemove = chat.Count;
                chat.AddUserMessage(contextBuilder.ToString());
            }

            // Add user question
            chat.AddUserMessage(question);

            // Prepare response builder
            StringBuilder responseBuilder = new();

            // Stream AI response
            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chat))
            {
                responseBuilder.Append(message.Content);
                logger.BeginScope("AI Response");
                logger.LogInformation(message.Content);
            }

            // Remove temporary context if added
            if (contextToRemove >= 0)
                chat.RemoveAt(contextToRemove);

            // Log the query and response
            logger.LogInformation($"Queried collection {collectionName} with question: {question}");

            return responseBuilder.ToString();
        }


        public async Task<List<string>> FetchDocumentContextAsync(
            string collectionName,
            string question,
            int searchLimit = 3)
        {
            // Create memory store
            var memory = await CreateMemoryStoreAsync();

            await DiagnoseCollectionAsync(collectionName);

            // Log collection details
            logger.LogInformation($"Searching in collection: {collectionName}");

            // Check if collection exists
            var collections = await memory.GetCollectionsAsync();
            logger.LogInformation($"Available collections: {string.Join(", ", collections)}");

            // Store retrieved results
            List<string> retrievedContexts = new List<string>();

            try
            {
                // Attempt search
                await foreach (var result in memory.SearchAsync(collectionName, question, limit: searchLimit))
                {
                    logger.LogInformation($"Search result - Text: {result.Metadata.Text}, Relevance: {result.Relevance}");
                    retrievedContexts.Add(result.Metadata.Text);
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error searching collection: {ex.Message}");
                logger.LogError($"Stack Trace: {ex.StackTrace}");
            }

            if (retrievedContexts.Count == 0)
            {
                logger.LogWarning($"No results found for collection: {collectionName}, question: {question}");
            }

            return retrievedContexts;
        }
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
                await foreach (var result in memory.SearchAsync(collectionName, "test", limit: 10))
                {
                    recordCount++;
                    logger.LogInformation($"Sample record: {result.Metadata.Text}");
                }
                logger.LogInformation($"Total records found: {recordCount}");
            }
        }


        // Overload for async event-based streaming
        public async IAsyncEnumerable<string> QueryDocumentCollectionStreamAsync(
            string collectionName,
            string question,
            int searchLimit = 3,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Get chat completion service
            var ai = kernel.GetRequiredService<IChatCompletionService>();

            // Create memory store
            var memory = await CreateMemoryStoreAsync();

            // Create chat history
            ChatHistory chat = new("You are an AI assistant that helps people find information.");

            // Search for relevant context
            StringBuilder contextBuilder = new();
            await foreach (var result in memory.SearchAsync(collectionName, question, limit: searchLimit))
            {
                contextBuilder.AppendLine(result.Metadata.Text);
            }

            // Prepare context
            int contextToRemove = -1;
            if (contextBuilder.Length != 0)
            {
                contextBuilder.Insert(0, "Here's some additional information: ");
                contextToRemove = chat.Count;
                chat.AddUserMessage(contextBuilder.ToString());
            }

            // Add user question
            chat.AddUserMessage(question);

            // Stream AI response
            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(chat))
            {
                yield return message.Content;

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                    break;
            }

            // Remove temporary context if added
            if (contextToRemove >= 0)
                chat.RemoveAt(contextToRemove);

            // Log the query
            logger.LogInformation($"Streamed query for collection {collectionName} with question: {question} with answer: {contextBuilder}");
        }

    }
}
