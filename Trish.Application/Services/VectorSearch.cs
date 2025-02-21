using Cassandra;
using System.Text;
using System.Text.Json;

public class CassandraVectorSearch
{
    private readonly ISession _session;
    private readonly HttpClient _openAiClient;
    private readonly string _openAiKey;
    private PreparedStatement _searchStatement;

    public class SearchResult
    {
        public string Content { get; set; }
        public double Similarity { get; set; }
    }

    public CassandraVectorSearch(string contactPoints, string openAiKey)
    {
        // Initialize Cassandra connection
        var cluster = Cluster.Builder()
            .AddContactPoints(contactPoints.Split(','))
            .WithDefaultKeyspace("shared_keyspace")
            .Build();

        _session = cluster.Connect();

        // Initialize OpenAI client
        _openAiClient = new HttpClient();
        _openAiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
        _openAiKey = openAiKey;

        // PrepareStatements();
    }
    private void PrepareStatements(string safeTenantId)
    {
        string tableName = $"qa_data_{safeTenantId}";

        // Instead of using similarity_cosine, we'll calculate cosine similarity manually
        _searchStatement = _session.Prepare($@"
        SELECT body_blob, vector 
        FROM shared_keyspace.{tableName}");
    }

    public async Task<QueryResponse> QueryFromPdf(string question, string tenantId)
    {
        try
        {
            var safeTenantId = tenantId.Replace("-", "_");
            PrepareStatements(safeTenantId);

            // Get question embedding
            var queryVector = await GetOpenAIEmbedding(question);

            // Execute the basic query to get all vectors
            var boundStatement = _searchStatement.Bind()
                .SetConsistencyLevel(ConsistencyLevel.LocalOne);

            var resultSet = await _session.ExecuteAsync(boundStatement);

            // Calculate cosine similarity in memory
            var searchResults = resultSet
                .Select(row => new SearchResult
                {
                    Content = row.GetValue<string>("body_blob"),
                    Similarity = CalculateCosineSimilarity(queryVector, row.GetValue<List<float>>("vector").ToArray())
                })
                .OrderByDescending(r => r.Similarity)
                .Take(3)
                .ToList();

            // Get completion from OpenAI
            var answer = await GetOpenAICompletion(
                question,
                searchResults.Select(r => r.Content).ToList()
            );

            return new QueryResponse
            {
                Answer = answer,
                SourceDocuments = searchResults.Select(r => r.Content).ToList()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Query failed: {ex.Message}");
            throw;
        }
    }

    private double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
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
        var embedding = responseJson.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => (float)x.GetDouble())
            .ToArray();

        return embedding;
    }

    private async Task<string> GetOpenAICompletion(string question, List<string> contextDocs)
    {
        var context = string.Join("\n", contextDocs);
        var prompt = $"Based on the following context, answer the question. If the answer cannot be found in the context, say 'I don't know'.\n\nContext: {context}\n\nQuestion: {question}\n\nAnswer:";

        var requestBody = new
        {
            model = "gpt-4",  // or your preferred model
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _openAiClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            content
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"OpenAI API error: {await response.Content.ReadAsStringAsync()}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        var responseJson = JsonDocument.Parse(responseString);
        return responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
    /*
    public async Task<QueryResponse> QueryFromPdf(string question, string tenantId)
    {
        try
        {
            // Sanitize tenant ID for table name
            var safeTenantId = tenantId.Replace("-", "_");
            PrepareStatements(safeTenantId);
            // Get question embedding
            var questionVector = await GetOpenAIEmbedding(question);

            // Perform vector search
            var boundStatement = _searchStatement.Bind(questionVector)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne);

            // Format the query with the safe tenant ID
            // boundStatement.RoutingKey = ByteBuffer.Allocate(16); // Set appropriate routing key if needed
            // boundStatement.SetKeyspace("shared_keyspace");

            var resultSet = await _session.ExecuteAsync(boundStatement);

            var searchResults = resultSet
                .Select(row => new SearchResult
                {
                    Content = row.GetValue<string>("content"),
                    Similarity = row.GetValue<double>("similarity")
                })
                .ToList();

            // Get completion from OpenAI
            var answer = await GetOpenAICompletion(
                question,
                searchResults.Select(r => r.Content).ToList()
            );

            return new QueryResponse
            {
                Answer = answer,
                SourceDocuments = searchResults.Select(r => r.Content).ToList()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Query failed: {ex.Message}");
            throw;
        }
    }
    */
}

public class QueryResponse
{
    public string Answer { get; set; }
    public List<string> SourceDocuments { get; set; }
}