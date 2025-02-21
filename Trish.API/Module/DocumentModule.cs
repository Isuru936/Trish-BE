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

            MapGroup.MapPost("/upload-file",
            async (IFormFile file, PdfProcessor documentApiClient, [FromServices] ICloudflareServices cloudflareServices, [FromServices] IHttpContextAccessor contextAccessor) =>
            {
                string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();


                tenantID = Guid.NewGuid().ToString();
                tenantID = "55241378-c72b-4fe9-ae9b-b8535ef62fd8";

                if (string.IsNullOrEmpty(tenantID))
                {
                    return Results.BadRequest(new { message = "TenantID is required" });
                }

                await documentApiClient.ProcessPdfFile(file, tenantID);
                var response = cloudflareServices.UploadFileAsync(file.OpenReadStream(), tenantID, file.FileName, file.ContentType);
                return Results.Ok(new { message = response.Result });
            }).DisableAntiforgery();

            MapGroup.MapPost("/query",
                async (RequestBody request, CassandraVectorSearch documentApiClient, OpenAIService openAi, [FromServicesAttribute] ICloudflareServices cloudflareServices, [FromServicesAttribute] IHttpContextAccessor contextAccessor) =>
                {
                    var optimizedQ = await openAi.OptimizeQueryAsync(request.question);
                    Console.WriteLine("optiized", optimizedQ);
                    var response = await documentApiClient.QueryFromPdf(optimizedQ, request.tenant_id);
                    var optimizedResponse = await openAi.RefineResponseAsync(response?.Answer);
                    response.Answer = optimizedResponse;
                    return response;
                });

            MapGroup.MapGet("/fetchDocs",
                async ([FromServicesAttribute] ICloudflareServices cloudfare, [FromServicesAttribute] IHttpContextAccessor contextAccessor) =>
                {
                    string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                    tenantID = "55241378-c72b-4fe9-ae9b-b8535ef62fd8";

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
