using Cassandra;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Trish.Application.Services.HNSW;

public class CassandraVectorSearch
{
    private readonly ISession _session;
    private readonly HttpClient _openAiClient;
    private readonly string _openAiKey;
    private PreparedStatement _searchStatement;
    private readonly ConcurrentDictionary<string, HNSWGraph> _tenantIndices;

    public CassandraVectorSearch(string contactPoints, string openAiKey)
    {
        var cluster = Cluster.Builder()
            .AddContactPoints("localhost")
            .WithPort(9042)
            .WithDefaultKeyspace("shared_keyspace")
            .Build();

        _session = cluster.Connect();
        _openAiClient = new HttpClient();
        _openAiClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
        _openAiKey = openAiKey;
        _tenantIndices = new ConcurrentDictionary<string, HNSWGraph>();
    }

    private async Task InitializeHNSWIndex(string safeTenantId)
    {
        if (_tenantIndices.ContainsKey(safeTenantId)) return;

        var hnswGraph = new HNSWGraph();
        string tableName = $"qa_data_{safeTenantId}";

        var statement = _session.Prepare($@"
            SELECT body_blob, vector 
            FROM shared_keyspace.{tableName}");

        var resultSet = await _session.ExecuteAsync(statement.Bind());

        foreach (var row in resultSet)
        {
            var vector = row.GetValue<List<float>>("vector").ToArray();
            var content = row.GetValue<string>("body_blob");
            hnswGraph.AddNode(vector, content);
        }

        _tenantIndices.TryAdd(safeTenantId, hnswGraph);
    }

    public async Task<QueryResponse> QueryFromPdf(string question, string tenantId)
    {
        try
        {
            var safeTenantId = tenantId.Replace("-", "_");
            await InitializeHNSWIndex(safeTenantId);

            var queryVector = await GetOpenAIEmbedding(question);
            var hnswIndex = _tenantIndices[safeTenantId];

            var searchResults = hnswIndex.Search(queryVector, 3)
                .Select(result => new SearchResult
                {
                    Content = result.content,
                    Similarity = result.similarity
                })
                .ToList();

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

        // if (responseJson.RootElement.GetProperty("message").GetString().Equals("i don't know") {
        //    responseJson.RootElement.GetProperty("message") = "I don't know, but ill inform an agent to address this issue"
        // }

        return responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }
}

internal class SearchResult
{
    public string Content { get; set; }
    public double Similarity { get; set; }
}

public class QueryResponse
{
    public string Answer { get; set; }
    public List<string> SourceDocuments { get; set; }
}