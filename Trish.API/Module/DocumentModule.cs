using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Trish.API.Interfaces;
using Trish.Application.Abstractions.Services;
using Trish.Application.Services;

namespace Trish.API.Module
{
    public class DocumentModule : IApiModule
    {
        public void MapEndpoint(WebApplication app)
        {
            RouteGroupBuilder MapGroup = app.MapGroup("api").WithTags("Documents");

            MapGroup.MapPost("/upload-file", async (
                [FromForm] IFormFile file,
                [FromServices] DocumentProcessor documentApiClient,
                [FromServices] ICloudflareServices cloudflareServices,
                [FromServices] IHttpContextAccessor contextAccessor) =>
            {
                string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                if (string.IsNullOrEmpty(tenantID))
                {
                    return Results.BadRequest(new { message = "TenantID is required" });
                }

                var response = await cloudflareServices.UploadFileAsync(file.OpenReadStream(), tenantID, file.FileName, file.ContentType);
                await documentApiClient.ProcessDocumentsAsync(response.Url, tenantID);

                return Results.Ok();
            }).DisableAntiforgery();

            MapGroup.MapPost("/query",
                async ([FromBody] RequestBody request, [FromServices] DocumentProcessor documentApiClient, [FromServices] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();


                    var response = documentApiClient.QueryDocumentsAsync(request.question, request.tenant_id, request.organization, request.useDbQuerying);
                    return response;
                });


            MapGroup.MapPost("/upload", async (
            HttpRequest request,
            [FromQuery] string tableName,
            [FromQuery] bool hasHeaderRow = true,
            [FromQuery] char delimiter = ',',
            [FromServices] DocumentProcessor documentProcessor = null,
            [FromServices] IHttpContextAccessor contextAccessor = null) =>
            {
                // Get tenant ID from headers
                string tenantId = contextAccessor.HttpContext?.Request.Headers["TenantID"].FirstOrDefault();
                if (string.IsNullOrEmpty(tenantId))
                {
                    return Results.BadRequest("TenantID header is required");
                }

                // Validate table name
                if (string.IsNullOrEmpty(tableName))
                {
                    return Results.BadRequest("TableName query parameter is required");
                }

                // Check if request contains file
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                {
                    return Results.BadRequest("No CSV file provided");
                }

                var file = request.Form.Files[0];
                if (file.Length == 0)
                {
                    return Results.BadRequest("CSV file is empty");
                }

                try
                {
                    // Process the CSV file
                    using var stream = file.OpenReadStream();
                    var result = await documentProcessor.UploadCsvAsync(
                        stream,
                        tableName,
                        tenantId,
                        hasHeaderRow,
                        delimiter
                    );

                    return Results.Ok(result);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        title: "Error processing CSV file",
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            })
        .DisableAntiforgery() // For file upload
        .WithName("UploadCsv")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Upload CSV file to create or update a database table";
            operation.Description = "Uploads a CSV file and creates or updates a table in the tenant's database";
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "tableName",
                In = ParameterLocation.Query,
                Required = true,
                Schema = new OpenApiSchema { Type = "string" }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "hasHeaderRow",
                In = ParameterLocation.Query,
                Required = false,
                Schema = new OpenApiSchema { Type = "boolean", Default = new OpenApiBoolean(true) }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "delimiter",
                In = ParameterLocation.Query,
                Required = false,
                Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString(",") }
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "TenantID",
                In = ParameterLocation.Header,
                Required = true,
                Schema = new OpenApiSchema { Type = "string" }
            });
            return operation;
        });

            MapGroup.MapGet("/fetchDocs",
                async ([FromServices] ICloudflareServices cloudfare, [FromServices] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                    if (string.IsNullOrEmpty(tenantID))
                    {
                        return Results.BadRequest(new { message = "TenantID is required" });
                    }
                    var response = await cloudfare.GetDocumentLinksAsync(tenantID);
                    return Results.Ok(response);
                });

            MapGroup.MapGet("/test", () =>
            {
                return Results.Ok(new { message = "Test" });
            });


            app.MapGet("/api/antiforgery/token", (HttpContext httpContext, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(httpContext);
                return Results.Ok(new
                {
                    requestToken = tokens.RequestToken,
                    headerName = "X-CSRF-TOKEN"
                });
            });

        }
    }


    class RequestBody
    {
        public string question { get; set; } = string.Empty;
        public string tenant_id { get; set; } = string.Empty;
        public string organization { get; set; } = string.Empty;
        public bool useDbQuerying { get; set; } = false;
        public string email { get; set; } = string.Empty;
    }
}
