using System.Text;
using System.Text.Json;

namespace Trish.Application.Services
{
    public class OpenAIService
    {
        private readonly HttpClient _httpClient;
        private const string OpenAiApiKey = "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";
        private const string OpenAiUrl = "https://api.openai.com/v1/chat/completions";

        public OpenAIService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {OpenAiApiKey}");
        }

        public async Task<string> OptimizeQueryAsync(string userQuery)
        {
            var requestBody = new
            {
                model = "gpt-4",
                messages = new[]
                {
                new { role = "system", content = "You are an assistant that refines user queries for a RAG database." },
                new { role = "user", content = $"Optimize this query for efficient retrieval: {userQuery}" }
            }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(OpenAiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(responseString);
            return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
        }

        public async Task<string> RefineResponseAsync(string dbResponse)
        {
            var requestBody = new
            {
                model = "gpt-4",
                messages = new[]
                {
            new { role = "system", content = "You are an assistant that makes database responses sound natural." },
            new { role = "user", content = $"Make this response more human-friendly: {dbResponse}" }
        }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(OpenAiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(responseString);
            return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }

    }
}
