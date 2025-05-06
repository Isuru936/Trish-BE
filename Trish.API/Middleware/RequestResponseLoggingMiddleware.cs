using System.Text;

namespace Trish.API.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Log the request
            await LogRequest(context);

            // Capture the original body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Create a new memory stream
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                // Continue down the pipeline
                await _next(context);

                // Log the response
                await LogResponse(context, responseBody, originalBodyStream);
            }
            finally
            {
                // Always restore the original body stream
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task LogRequest(HttpContext context)
        {
            context.Request.EnableBuffering();

            var requestReader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var requestContent = await requestReader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            _logger.LogInformation(
                "HTTP Request: {Method} {Scheme}://{Host}{Path}{QueryString} {Protocol} Body: {Body}",
                context.Request.Method,
                context.Request.Scheme,
                context.Request.Host,
                context.Request.Path,
                context.Request.QueryString,
                context.Request.Protocol,
                requestContent);
        }

        private async Task LogResponse(HttpContext context, MemoryStream responseBody, Stream originalBodyStream)
        {
            responseBody.Position = 0;
            var response = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Position = 0;

            _logger.LogInformation(
                "HTTP Response: Status: {StatusCode} Body: {Body}",
                context.Response.StatusCode,
                response);

            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}
