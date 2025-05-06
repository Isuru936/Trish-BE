using Trish.API.Extensions;
using Trish.Application;
using Trish.Identitty;
using Trish.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var cfg = builder.Configuration;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

builder.Services.ConfigureInfrastructureService(cfg);
builder.Services.ConfigureApplicationServices(cfg);
builder.Services.ConfigureIdentityService(cfg);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddAzureWebAppDiagnostics();



builder.Services.AddAuthorization();
// builder.Services.AddSingleton(sp => new TenantIdProvider().GetTenantId());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        builder => builder.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = ".AspNetCore.Antiforgery.VyLW6ORzMgk";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.Configure<LoggerFilterOptions>(options =>
    {
        options.MinLevel = LogLevel.Information;
    });
}

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowSpecificOrigin");
app.UseAntiforgery();
app.UseHttpsRedirection();
app.MapEndpoint();

app.Run();