using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Trish.API.Interfaces;
using Trish.Application.Abstractions.Services;

namespace Trish.API.Module
{
    public class DocumentModule : IApiModule
    {
        public void MapEndpoint(WebApplication app)
        {
            RouteGroupBuilder MapGroup = app.MapGroup("api").WithTags("Documents");

            MapGroup.MapPost("/upload-file",
            async (IFormFile file, [FromServices] IDocumentAPIClient documentApiClient, [FromServices] ICloudflareServices cloudflareServices, [FromServices] IHttpContextAccessor contextAccessor) =>
            {
                string tenantID = contextAccessor.HttpContext?.Request.Headers["TenantID"]!.FirstOrDefault();

                tenantID = Guid.NewGuid().ToString();

                if (string.IsNullOrEmpty(tenantID))
                {
                    return Results.BadRequest(new { message = "TenantID is required" });
                }

                await documentApiClient.UploadPdfAsync(file, tenantID);
                var response = cloudflareServices.UploadFileAsync(file.OpenReadStream(), file.FileName, file.ContentType);
                return Results.Ok(new { message = response.Result });
            }).DisableAntiforgery();

            MapGroup.MapPost("/query",
                async (RequestBody request, [FromServices] IDocumentAPIClient documentApiClient, [FromServicesAttribute] ICloudflareServices cloudflareServices, [FromServicesAttribute] IHttpContextAccessor contextAccessor) =>
                {
                    var response = await documentApiClient.QueryFromPdf(request.question, request.tenant_id);
                    return response;
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
