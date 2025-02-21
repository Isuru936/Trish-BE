namespace Trish.Application.Services.HNSW
{
    public class HNSWNode
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
    }
}
