using Microsoft.AspNetCore.Http;
using Trish.Application.Services.Response;

namespace Trish.Application.Abstractions.Services
{
    public interface IDocumentAPIClient
    {
        Task UploadPdfAsync(IFormFile file, string tenantId);
        Task<QueryResponse> QueryFromPdf(string? question, string? tenantId);
    }
}
