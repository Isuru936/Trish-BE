using Trish.Application.Services;
using static Trish.Application.Services.CloudflareServices;

namespace Trish.Application.Abstractions.Services
{
    public interface ICloudflareServices
    {
        Task<R2UploadResult> UploadFileAsync(Stream fileStream, string tenantId, string fileName, string contentType);
        Task<List<R2DocumentLink>> GetDocumentLinksAsync(string tenantId);
    }
}
