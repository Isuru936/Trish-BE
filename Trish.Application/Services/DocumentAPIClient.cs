/*
 * 
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using Trish.Application.Abstractions.Services;

namespace Trish.Application.Services
{
    internal class DocumentAPIClient : IDocumentAPIClient
    {
        private readonly HttpClient _client;

        public DocumentAPIClient(HttpClient client)
        {
            _client = client;
            _client.BaseAddress = new Uri("http://python-service:5000");
        }

        public async Task UploadPdfAsync(IFormFile file, string tenantId)
        {
            var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream());
            content.Add(fileContent, "file", file.FileName);

            var response = await _client.PostAsync($"/upload?tenant_id={tenantId}", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<QueryResponse?> QueryFromPdf(string? question, string? tenantId)
        {
            try
            {
                var requestBody = new
                {
                    tenant_id = tenantId,
                    question = question
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"/query", content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");
                    return null; // Gracefully handle errors
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var deserializedResponse = JsonSerializer.Deserialize<QueryResponse>(responseString);

                return deserializedResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return null;
            }
        }

    }
}

*/