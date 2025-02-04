using System.Text.Json.Serialization;

namespace Trish.Application.Services.Response
{
    public class QueryResponse
    {
        [JsonPropertyName("answer")]
        public string? Answer { get; set; }
        [JsonPropertyName("source_documents")]
        public List<string>? SourceDocuments { get; set; }
    }
}
