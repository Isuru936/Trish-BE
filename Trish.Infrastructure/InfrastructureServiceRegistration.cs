using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using Trish.Application.Abstractions.Persistence;
using Trish.Application.Abstractions.Services;
using Trish.Infrastructure.Repositories;

namespace Trish.Infrastructure
{
    public static class InfrastructureServiceRegistration
    {
        public static IServiceCollection ConfigureInfrastructureService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("IdentityConnection"),
                    npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(3);
                        npgsqlOptions.CommandTimeout(30);
                    })
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();  // Only in development
            });

            services.AddTransient<IDbConnection>(sp =>
            {
                var connectionString = configuration.GetConnectionString("IdentityConnection");
                return new Npgsql.NpgsqlConnection(connectionString);
            });

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));

            services.AddScoped<ITenantSchemaRegistry, TenantSchemaRegistry>();
            services.AddScoped<IPostgresTenantConnectionManager, PostgresTenantConnectionManager>();

            return services;
        }


        public static IApplicationBuilder MigrateInfrastructureContext(this IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

                try
                {
                    logger.LogInformation("Starting Identity database migration...");

                    // Check if database exists, if not create it
                    dbContext.Database.EnsureCreated();

                    // Apply any pending migrations
                    if (dbContext.Database.GetPendingMigrations().Any())
                    {
                        logger.LogInformation("Applying Application pending migrations...");
                        dbContext.Database.Migrate();
                        logger.LogInformation("Migrations applied successfully");
                    }
                    else
                    {
                        logger.LogInformation("No pending migrations found");
                    }


                    // You could also seed initial data here if needed
                    // await SeedDefaultData(dbContext, scope.ServiceProvider);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating the identity database");
                    throw;
                }
            }

            return app;
        }

    }
}
