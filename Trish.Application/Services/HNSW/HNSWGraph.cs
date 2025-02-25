namespace Trish.Application.Services.HNSW
{
    public class HNSWGraph
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
            int nodeLevel = GenerateRandomLevel();
            var node = new HNSWNode(vector, content, allNodes.Count, nodeLevel);

            if (entryPoint == null)
            {
                entryPoint = node;
                allNodes.Add(node);
                return;
            }

            var currentNode = entryPoint;

            // Search for neighbors at each level
            for (int level = Math.Min(nodeLevel, entryPoint.Level); level >= 0; level--)
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
            if (nodeLevel > entryPoint.Level)
            {
                entryPoint = node;
            }

            allNodes.Add(node);
        }

        private void ConnectNodes(HNSWNode node, List<HNSWNode> neighbors, int level)
        {
            foreach (var neighbor in neighbors)
            {
                // Both node and neighbor should already have their connection sets initialized
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

        public List<(string content, double similarity)> Search(float[] queryVector, int k)
        {
            if (entryPoint == null) return new List<(string, double)>();

            var currentNode = entryPoint;

            // Search through levels
            for (int level = entryPoint.Level; level > 0; level--)
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
}
