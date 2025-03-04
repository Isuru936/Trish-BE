using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Trish.Application.Abstractions.Services;
using PutObjectRequest = Amazon.S3.Model.PutObjectRequest;

namespace Trish.Application.Services
{
    public class CloudflareServices : ICloudflareServices
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _publicUrl;


        public CloudflareServices(string accessKeyId, string secretAccessKey, string serviceUrl, string bucketName, string publicUrl)
        {

            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                SignatureVersion = "4",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };

            _s3Client = new AmazonS3Client(
                new BasicAWSCredentials(accessKeyId, secretAccessKey),
                config
            );
            _bucketName = bucketName;
            _publicUrl = publicUrl;
        }

        public async Task<R2UploadResult> UploadFileAsync(Stream fileStream, string tenantId, string fileName, string contentType)
        {
            try
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = $"{Path.GetFileNameWithoutExtension(fileName)}-*-{tenantId}.pdf",
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

        public async Task<List<R2DocumentLink>> GetDocumentLinksAsync(string tenantId)
        {
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    // Use delimiter to match the pattern fileName-*-tenantId
                    Delimiter = "/",
                };

                var response = await _s3Client.ListObjectsV2Async(listRequest);
                var documents = new List<R2DocumentLink>();

                var serviceUri = new Uri(_s3Client.Config.ServiceURL);
                var baseDomain = serviceUri.Host;

                foreach (var obj in response.S3Objects)
                {
                    var parts = obj.Key.Split("-*-");

                    // Ensure the split resulted in exactly two parts (filename and tenantId)
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    var extractedTenantId = parts[1];

                    // Only process documents that match the given tenantId
                    if (extractedTenantId != (tenantId + ".pdf"))
                    {
                        continue;
                    }

                    var fileName = parts[0]; // Extracted filename without the tenantId suffix

                    var urlRequest = new GetPreSignedUrlRequest
                    {
                        BucketName = _bucketName,
                        Key = obj.Key,
                        Expires = DateTime.UtcNow.AddHours(1),
                        Protocol = Protocol.HTTPS,
                    };

                    var presignedUrl = _s3Client.GetPreSignedURL(urlRequest);

                    var url = $"{_publicUrl}{obj.Key}";

                    documents.Add(new R2DocumentLink
                    {
                        FileName = fileName,
                        Key = obj.Key,
                        Url = url,
                        LastModified = obj.LastModified,
                        Size = obj.Size,
                        UrlExpiration = DateTime.UtcNow.AddHours(1)
                    });
                }


                return documents;
            }
            catch (AmazonS3Exception s3Ex)
            {
                throw new Exception($"Failed to list documents: {s3Ex.Message}", s3Ex);
            }
        }

        public class R2DocumentLink
        {
            public string FileName { get; set; }
            public string Key { get; set; }
            public string Url { get; set; }
            public DateTime LastModified { get; set; }
            public long Size { get; set; }
            public DateTime UrlExpiration { get; set; }
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
