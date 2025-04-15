using Cassandra;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Reflection;
using Trish.Application.Abstractions.Messaging;
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
            services.AddLogging(configure =>
            {
                configure.AddConsole(); // Adds console logging
                configure.AddDebug();   // Adds debug output logging
                configure.SetMinimumLevel(LogLevel.Information);
            });

            services.AddSingleton<Kernel>(sp =>
            {
                var apiKey = configuration["OpenAi:ApiKey"]
                    ?? throw new InvalidOperationException("OpenAI API key not configured");

                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddOpenAIChatCompletion("gpt-3.5-turbo-0125", apiKey);
                kernelBuilder.AddQdrantVectorStore("http://localhost:6333");
#pragma warning disable SKEXP0010
                kernelBuilder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey, dimensions: 384);
                kernelBuilder.Services.AddHttpClient();
                kernelBuilder.Services.AddLogging();
                kernelBuilder.Services.AddQdrantVectorStore("localhost");

                return kernelBuilder.Build();
            });


            // Semantic Memory Service Registration
            services.AddScoped<ISemanticMemoryService, SemanticMemoryService>();

            services.AddScoped<DocumentProcessor>();

            services.AddScoped<ICloudflareServices>(sp =>
                new CloudflareServices(
                    configuration["Cloudflare:AccessKeyId"]!,
                    configuration["Cloudflare:SecretAccessKey"]!,
                    configuration["Cloudflare:ServiceUrl"]!,
                    configuration["Cloudflare:BucketName"]!,
                    configuration["Cloudflare:PublicUrl"]!
                )
            );

            services.AddSingleton<ISession>(sp =>
            {
                var cluster = Cluster.Builder()
                    .AddContactPoint(configuration["CassandraSettings:ContactPoints"]) // Use "cassandra" if running in Docker Compose
                    .WithPort(int.Parse(configuration["CassandraSettings:Port"]!))
                    .Build();

                return cluster.Connect(configuration["CassandraSettings:KeySpace"]); // Replace with your actual keyspace
            });

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IValidator<SignUpCommand>, SignUpCommandValidator>();

            return services;
        }
    }
}
