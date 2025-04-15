using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Trish.Application.Abstractions.Cassandra;
using Trish.Identitty.Cassandra;

namespace Trish.Identitty;

public static class IdentityServiceRegistration
{
    public static IServiceCollection ConfigureIdentityService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDatabaseContext>(options =>
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

        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 3;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        })
        .AddEntityFrameworkStores<IdentityDatabaseContext>()
        .AddDefaultTokenProviders();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = configuration["Jwt:ValidAudience"],
            ValidAudience = configuration["Jwt:ValidAudience"],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)
                ),
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminPriviledges", policy => policy.RequireRole("Admin"));
            options.AddPolicy("UserPriviledges", policy => policy.RequireRole("User"));
        });

        services.Configure<CassandraSettings>(
    configuration.GetSection("CassandraSettings"));

        services.AddSingleton<ICassandraConnectionManager, CassandraConnectionManager>();

        return services;
    }

    public static IApplicationBuilder MigrateIdentityContext(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDatabaseContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdentityDatabaseContext>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            try
            {
                logger.LogInformation("Starting Identity database migration...");

                // Check if database exists, if not create it
                dbContext.Database.EnsureCreated();

                // Apply any pending migrations
                if (dbContext.Database.GetPendingMigrations().Any())
                {
                    logger.LogInformation("Applying pending migrations...");
                    dbContext.Database.Migrate();
                    logger.LogInformation("Migrations applied successfully");


                }
                else
                {
                    logger.LogInformation("No pending migrations found");
                }

                SeedRoles(roleManager, logger).Wait();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while migrating the identity database");
                throw;
            }
        }

        return app;
    }


    private static async Task SeedRoles(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        // Define roles
        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogInformation($"Creating role: {role}");
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation($"Role {role} created successfully");
            }
        }
    }
}

