namespace Trish.Application.Services
{
    using Cassandra;
    using iText.Kernel.Pdf;
    using iText.Kernel.Pdf.Canvas.Parser;
    using iText.Kernel.Pdf.Canvas.Parser.Listener;
    using Microsoft.AspNetCore.Http;
    using System.Text;
    using System.Text.Json;

    public class PdfProcessor
    {
        private readonly Cassandra.ISession _session;
        private readonly HttpClient _openAiClient;
        private readonly string _openAiKey;

        public PdfProcessor(string contactPoints, string openAiKey)
        {
            var cluster = Cluster.Builder()
                .AddContactPoints("localhost")
                .WithPort(9042)
                .WithDefaultKeyspace("shared_keyspace")
                .Build();

            _session = cluster.Connect();

            // Initialize OpenAI client
            _openAiClient = new HttpClient();
            _openAiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
            _openAiKey = openAiKey;

            // Ensure keyspace exists
            _session.Execute(@"
            CREATE KEYSPACE IF NOT EXISTS shared_keyspace 
            WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 1}
        ");
        }

        // In PdfProcessor class
        public async Task ProcessPdfFile(IFormFile file, string tenantId)
        {
            using var pdfStream = file.OpenReadStream();

            // Extract text from PDF
            var text = ExtractTextFromPdf(pdfStream);

            // Split text into chunks
            var chunks = SplitTextIntoChunks(text, 800, 200);

            // Create table for tenant if not exists
            var safeTenantId = tenantId.Replace("-", "_");
            await CreateTableForTenant(safeTenantId);

            // Process chunks (limited to 50 as in Python version)
            var limitedChunks = chunks.Take(50);
            foreach (var chunk in limitedChunks)
            {
                await ProcessChunk(chunk, safeTenantId);
            }
        }

        private string ExtractTextFromPdf(Stream pdfStream)
        {
            var text = new StringBuilder();
            var pdfReader = new PdfReader(pdfStream);
            var pdfDocument = new PdfDocument(pdfReader);

            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
            {
                var strategy = new SimpleTextExtractionStrategy();
                var page = pdfDocument.GetPage(i);
                text.Append(PdfTextExtractor.GetTextFromPage(page, strategy));
            }

            return text.ToString();
        }

        private List<string> SplitTextIntoChunks(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i += chunkSize - overlap)
            {
                var chunkWords = words.Skip(i).Take(chunkSize).ToList();
                if (chunkWords.Any())
                {
                    chunks.Add(string.Join(" ", chunkWords));
                }
            }

            return chunks;
        }

        private async Task CreateTableForTenant(string safeTenantId)
        {
            var tableName = $"qa_data_{safeTenantId}";

            _session.Execute($@"
            CREATE TABLE IF NOT EXISTS shared_keyspace.{tableName} (
                row_id uuid PRIMARY KEY,
                attributes_blob text,
                body_blob text,
                metadata_s text,
                vector list<float>
            )
        ");
        }

        private async Task<float[]> GetOpenAIEmbedding(string text)
        {
            var requestBody = new
            {
                input = text,
                model = "text-embedding-ada-002"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _openAiClient.PostAsync(
                "https://api.openai.com/v1/embeddings",
                content
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API error: {await response.Content.ReadAsStringAsync()}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var responseJson = JsonDocument.Parse(responseString);
            return responseJson.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(x => (float)x.GetDouble())
                .ToArray();
        }

        private async Task ProcessChunk(string chunk, string safeTenantId)
        {
            var embedding = await GetOpenAIEmbedding(chunk);
            var rowId = Guid.NewGuid();

            var statement = _session.Prepare($@"
            INSERT INTO shared_keyspace.qa_data_{safeTenantId} 
            (row_id, body_blob, vector) 
            VALUES (?, ?, ?)
        ");

            var boundStatement = statement.Bind(
                rowId,
                chunk,
                embedding.ToList()  // Cassandra expects List<float>
            );

            await _session.ExecuteAsync(boundStatement);
        }
    }
}
