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
                await documentApiClient.ProcessDocumentsAsync(response.Url, tenantID);

                return Results.Ok();
            }).DisableAntiforgery();

            MapGroup.MapPost("/query",
                async (RequestBody request, DocumentProcessor documentApiClient, OpenAIService openAi, [FromServicesAttribute] ICloudflareServices cloudflareServices, [FromServicesAttribute] IHttpContextAccessor contextAccessor) =>
                {

                    var response = documentApiClient.QueryDocumentsAsync(request.question, request.tenant_id);
                    //var optimizedResponse = await openAi.RefineResponseAsync(request.question, response);
                    // response.Answer = optimizedResponse;
                    return response;
                });

            MapGroup.MapGet("/fetchDocs",
                async ([FromServicesAttribute] ICloudflareServices cloudfare, [FromServicesAttribute] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                    // tenantID = "55241378-c72b-4fe9-ae9b-b8535ef62fd8";

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
        public string question { get; set; }
        public string tenant_id { get; set; }
    }
}
