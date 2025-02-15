using Trish.Application.Services;

namespace Trish.Application.Abstractions.Services
{
    public interface ICloudflareServices
    {
        Task<R2UploadResult> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    }
}
