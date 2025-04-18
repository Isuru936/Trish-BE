using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
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
                await documentApiClient.ProcessDocumentsAsync(response.Url, tenantID, file.FileName);

                return Results.Ok();
            }).DisableAntiforgery();


            MapGroup.MapDelete("/delete-file",
                async ([FromBody] FileName request,
                       [FromServices] ICloudflareServices cloudfare,
                       [FromServices] DocumentProcessor documentApiClient,
                       [FromServices] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                    if (string.IsNullOrEmpty(tenantID))
                    {
                        return Results.BadRequest(new { message = "TenantID is required" });
                    }

                    await documentApiClient.DeleteDocument(tenantID, request.fileName);
                    await cloudfare.DeleteFileAsync(tenantID, request.fileName);
                    return Results.Ok();

                });

            MapGroup.MapPost("/query",
                async ([FromBody] RequestBody request, [FromServices] DocumentProcessor documentApiClient, [FromServices] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                    var response = documentApiClient.QueryDocumentsAsync(request.question, request.tenant_id, request.organization);
                    return response;
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
    }

    class FileName
    {
        public string fileName { get; set; } = string.Empty;
    }
}
