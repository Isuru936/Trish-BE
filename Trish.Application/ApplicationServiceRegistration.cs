using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trish.Application.Abstraction.Services;
using Trish.Application.Services;

namespace Trish.Application
{
    public static class ApplicationServiceRegistration
    {
        public static IServiceCollection ConfigureApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICloudflareServices>(sp =>
            new CloudflareServices(
                configuration["Cloudflare:AccountId"],
                configuration["Cloudflare:AccessKeyId"],
                configuration["Cloudflare:SecretAccessKey"],
                configuration["Cloudflare:BucketName"]
                )
            );



            services.AddScoped<IDocumentAPIClient, DocumentAPIClient>();

            return services;
        }
    }
}
