using Cassandra;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Trish.Application.Services.HNSW;


/* public class HNSWNode
{
    public float[] Vector { get; set; }
    public string Content { get; set; }
    public Dictionary<int, HashSet<HNSWNode>> Connections { get; set; }
    public int Id { get; set; }

    public HNSWNode(float[] vector, string content, int id)
    {
        Vector = vector;
        Content = content;
        Id = id;
        Connections = new Dictionary<int, HashSet<HNSWNode>>();
    }
} */

/* public class HNSWGraph
{
    private readonly int M;  // Maximum number of connections per node
    private readonly int MaxLevel;  // Maximum level in the hierarchy
    private readonly int EfConstruction;  // Size of dynamic candidate list during construction
    private readonly Random random;
    private readonly List<HNSWNode> allNodes;
    private HNSWNode entryPoint;

    public HNSWGraph(int m = 16, int maxLevel = 4, int efConstruction = 200)
    {
        M = m;
        MaxLevel = maxLevel;
        EfConstruction = efConstruction;
        random = new Random();
        allNodes = new List<HNSWNode>();
    }

    private double CalculateDistance(float[] v1, float[] v2)
    {
        return 1 - CosineDistance(v1, v2);
    }

    private double CosineDistance(float[] v1, float[] v2)
    {
        double dotProduct = 0;
        double norm1 = 0;
        double norm2 = 0;

        for (int i = 0; i < v1.Length; i++)
        {
            dotProduct += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }

        return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
    }

    public void AddNode(float[] vector, string content)
    {
        var node = new HNSWNode(vector, content, allNodes.Count);
        int nodeLevel = GenerateRandomLevel();

        if (entryPoint == null)
        {
            entryPoint = node;
            allNodes.Add(node);
            return;
        }

        var currentNode = entryPoint;

        // Search for neighbors at each level
        for (int level = Math.Min(nodeLevel, GetNodeLevel(entryPoint)); level >= 0; level--)
        {
            var neighbors = SearchLayer(currentNode, node.Vector, level, 1);
            if (neighbors.Any())
            {
                currentNode = neighbors.First();
            }
        }

        // Connect the new node at each level
        for (int level = 0; level <= nodeLevel; level++)
        {
            var neighbors = SearchLayer(currentNode, node.Vector, level, M);
            ConnectNodes(node, neighbors, level);
        }

        // Update entry point if necessary
        if (nodeLevel > GetNodeLevel(entryPoint))
        {
            entryPoint = node;
        }

        allNodes.Add(node);
    }

    private void ConnectNodes(HNSWNode node, List<HNSWNode> neighbors, int level)
    {
        if (!node.Connections.ContainsKey(level))
        {
            node.Connections[level] = new HashSet<HNSWNode>();
        }

        foreach (var neighbor in neighbors)
        {
            if (!neighbor.Connections.ContainsKey(level))
            {
                neighbor.Connections[level] = new HashSet<HNSWNode>();
            }

            node.Connections[level].Add(neighbor);
            neighbor.Connections[level].Add(node);
        }
    }

    private List<HNSWNode> SearchLayer(HNSWNode entryPoint, float[] queryVector, int level, int ef)
    {
        var visited = new HashSet<HNSWNode>();
        var candidates = new PriorityQueue<HNSWNode, double>();
        var results = new List<(HNSWNode node, double distance)>();

        double initialDistance = CalculateDistance(queryVector, entryPoint.Vector);
        candidates.Enqueue(entryPoint, initialDistance);
        results.Add((entryPoint, initialDistance));
        visited.Add(entryPoint);

        while (candidates.Count > 0)
        {
            var current = candidates.Dequeue();

            if (current.Connections.ContainsKey(level))
            {
                foreach (var neighbor in current.Connections[level])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        double distance = CalculateDistance(queryVector, neighbor.Vector);

                        if (results.Count < ef || distance < results.Max(x => x.distance))
                        {
                            candidates.Enqueue(neighbor, distance);
                            results.Add((neighbor, distance));

                            if (results.Count > ef)
                            {
                                results.RemoveAt(results.Count - 1);
                            }
                        }
                    }
                }
            }
        }

        return results.Select(x => x.node).ToList();
    }

    private int GenerateRandomLevel()
    {
        double r = random.NextDouble();
        return (int)(-Math.Log(r) * (MaxLevel - 1));
    }

    private int GetNodeLevel(HNSWNode node)
    {
        return node.Connections.Keys.Max();
    }

    public List<(string content, double similarity)> Search(float[] queryVector, int k)
    {
        if (entryPoint == null) return new List<(string, double)>();

        var currentNode = entryPoint;

        // Search through levels
        for (int level = GetNodeLevel(entryPoint); level > 0; level--)
        {
            var neighbors = SearchLayer(currentNode, queryVector, level, 1);
            if (neighbors.Any())
            {
                currentNode = neighbors.First();
            }
        }

        // Search bottom layer with larger ef
        var results = SearchLayer(currentNode, queryVector, 0, k * 2)
            .Select(node => (node.Content, 1 - CalculateDistance(queryVector, node.Vector)))
            .OrderByDescending(x => x.Item2)
            .Take(k)
            .ToList();

        return results;
    }
}
*/

public class CassandraVectorSearch2
{
    private readonly ISession _session;
    private readonly HttpClient _openAiClient;
    private readonly string _openAiKey;
    private PreparedStatement _searchStatement;
    private readonly HNSWGraph _hnswIndex;
    private readonly ConcurrentDictionary<string, HNSWGraph> _tenantIndices;

    public CassandraVectorSearch2(string contactPoints, string openAiKey)
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
        return responseJson.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }

    // ... rest of the existing methods (GetOpenAIEmbedding, GetOpenAICompletion) remain the same
}