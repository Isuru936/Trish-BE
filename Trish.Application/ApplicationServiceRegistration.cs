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
            Console.WriteLine("Cloudflare services registered", configuration["Cloudflare:ServiceUrl"]);
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

            services.AddSingleton<CassandraVectorSearch>(sp =>
                new CassandraVectorSearch(configuration)
            );

            services.AddSingleton<PdfProcessor>(sp =>
                new PdfProcessor(
                    configuration["CassandraSettings:ContactPoints"]!, // Use "cassandra" if running in Docker Compose
                    configuration["OpenAI:ApiKey"]!,
                    configuration["CassandraSettings:KeySpace"]!)
            );

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            // services.AddScoped<IDocumentAPIClient, DocumentAPIClient>();
            services.AddScoped<IValidator<SignUpCommand>, SignUpCommandValidator>();

            return services;
        }
    }
}
