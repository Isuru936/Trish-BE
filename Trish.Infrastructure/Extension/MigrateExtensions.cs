using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Trish.Infrastructure.Extensions
{
    public static class MigrateExtensions
    {
        public static IApplicationBuilder ApplyMigrations(this IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {
                    // Attempt to connect to the database first
                    dbContext.Database.GetConnectionString();

                    // Check if there are any pending migrations
                    if (dbContext.Database.GetPendingMigrations().Any())
                    {
                        Console.WriteLine("Applying pending migrations...");
                        dbContext.Database.Migrate();
                        Console.WriteLine("Migrations applied successfully.");
                    }
                    else
                    {
                        Console.WriteLine("No pending migrations found.");
                    }
                }
                catch (PostgresException ex)
                {
                    Console.WriteLine($"PostgreSQL Error: {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Migration Error: {ex.Message}");
                    throw;
                }
            }

            return app;
        }
    }
}