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
