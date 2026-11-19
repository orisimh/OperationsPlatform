namespace OperationsPlatform.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedValue = context.Request.Headers.TryGetValue("X-Correlation-ID", out var supplied)
            ? supplied.ToString() : string.Empty;
        var correlationId = !string.IsNullOrWhiteSpace(suppliedValue) && suppliedValue.Length <= 80 && !suppliedValue.Any(char.IsControl)
            ? suppliedValue : Guid.NewGuid().ToString("N");
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context);
    }
}
