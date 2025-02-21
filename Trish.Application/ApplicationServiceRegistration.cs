using Cassandra;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Trish.Application.Abstractions.Services;
using Trish.Application.Behaviours;
using Trish.Application.Features.Auth.Command;
using Trish.Application.Features.Auth.Validator;
using Trish.Application.Services;

namespace Trish.Application
{
    public static class ApplicationServiceRegistration
    {
        public static IServiceCollection ConfigureApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICloudflareServices>(sp =>
                new CloudflareServices(
                    configuration["Cloudflare:AccountId"]!,
                    configuration["Cloudflare:AccessKeyId"]!,
                    configuration["Cloudflare:SecretAccessKey"]!,
                    configuration["Cloudflare:BucketName"]!
                )
            );

            services.AddSingleton<ISession>(sp =>
            {
                var cluster = Cluster.Builder()
                    .AddContactPoint("localhost") // Use "cassandra" if running in Docker Compose
                    .WithPort(9042)
                    .Build();

                return cluster.Connect("shared_keyspace"); // Replace with your actual keyspace
            });

            services.AddSingleton<CassandraVectorSearch2>(sp =>
                new CassandraVectorSearch2(
                   "cassandra",
                   "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"
                )
            );

            services.AddSingleton<PdfProcessor>(sp =>
                new PdfProcessor(
                    "cassandra",
                   "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"
                )
            );

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IDocumentAPIClient, DocumentAPIClient>();
            services.AddScoped<IValidator<SignUpCommand>, SignUpCommandValidator>();

            return services;
        }
    }
}
