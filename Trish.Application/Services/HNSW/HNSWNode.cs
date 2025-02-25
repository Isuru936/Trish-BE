namespace Trish.Application.Services.HNSW
{
    public class HNSWNode
    {
        public float[] Vector { get; set; }
        public string Content { get; set; }
        public Dictionary<int, HashSet<HNSWNode>> Connections { get; set; }
        public int Id { get; set; }
        public int Level { get; set; }  // Store the node's level explicitly

        public HNSWNode(float[] vector, string content, int id, int level)
        {
            Vector = vector;
            Content = content;
            Id = id;
            Level = level;
            Connections = new Dictionary<int, HashSet<HNSWNode>>();

            // Initialize connection sets for all levels up to the node's level
            for (int i = 0; i <= level; i++)
            {
                Connections[i] = new HashSet<HNSWNode>();
            }
        }
    }
}
