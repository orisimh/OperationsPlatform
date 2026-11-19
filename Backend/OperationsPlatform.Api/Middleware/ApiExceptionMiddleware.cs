using System.Text.Json;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger, IServiceScopeFactory scopeFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API exception");
            var status = ex switch
            {
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ArgumentException => StatusCodes.Status400BadRequest,
                System.Text.Json.JsonException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                HttpRequestException => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status500InternalServerError
            };
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OperationsPlatform.Api.Data.OperationsDbContext>();
                db.ApplicationLogs.Add(new ApplicationLog
                {
                    Level = "Error", Source = "ApiException", Message = ex.Message,
                    Exception = ex.ToString(), CorrelationId = context.Items["CorrelationId"]?.ToString() ?? string.Empty,
                    RequestMethod = context.Request.Method, RequestPath = context.Request.Path,
                    StatusCode = status, CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* Preserve the API error response even if logging fails. */ }

            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "about:blank", title = status >= 500 ? "Server error" : "Request could not be completed",
                status, detail = status >= 500 ? "An unexpected server error occurred. See SQL logs for details." : ex.Message,
                correlationId = context.Items["CorrelationId"]?.ToString()
            }));
        }
    }
}
