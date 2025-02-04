using Amazon.Runtime;
using Amazon.S3;
using Trish.Application.Abstraction.Services;
using PutObjectRequest = Amazon.S3.Model.PutObjectRequest;

namespace Trish.Application.Services
{
    internal class CloudflareServices : ICloudflareServices
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public CloudflareServices(string accountId, string accessKeyId, string secretAccessKey, string bucketName)
        {

            var config = new AmazonS3Config
            {
                ServiceURL = "https://1e2a7b647a5b70e7e3971a9db1dace1c.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            _s3Client = new AmazonS3Client(
                new BasicAWSCredentials(accessKeyId, secretAccessKey),
                config
            );
            _bucketName = bucketName;
        }

        public async Task<R2UploadResult> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = fileStream,
                    ContentType = contentType,
                    DisablePayloadSigning = true,
                    AutoCloseStream = false,
                };

                var response = await _s3Client.PutObjectAsync(putRequest);

                var url = $"https://{_bucketName}.{_s3Client.Config.ServiceURL}/{fileName}";

                return new R2UploadResult
                {
                    Success = true,
                    Key = fileName,
                    Url = url
                };
            }
            catch (AmazonS3Exception s3Ex)
            {
                return new R2UploadResult
                {
                    Success = false,
                    ErrorMessage = $"S3 Error: {s3Ex.Message}. Error Code: {s3Ex.ErrorCode}"
                };
            }
            catch (Exception ex)
            {
                return new R2UploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    public class R2UploadResult
    {
        public bool Success { get; set; }
        public string Key { get; set; }
        public string Url { get; set; }
        public string ErrorMessage { get; set; }
    }
}
