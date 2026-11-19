using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Services;

public sealed class SqlApplicationLogWriter(OperationsDbContext db)
{
    public async Task WriteAsync(string level, string source, string message, string correlationId,
        string? exception = null, string? requestMethod = null, string? requestPath = null,
        int? statusCode = null, string? requestBody = null, string? responseBody = null,
        CancellationToken ct = default)
    {
        db.ApplicationLogs.Add(new ApplicationLog
        {
            Level = level, Source = source, Message = message, CorrelationId = correlationId,
            Exception = exception, RequestMethod = requestMethod, RequestPath = requestPath,
            StatusCode = statusCode, RequestBody = requestBody, ResponseBody = responseBody,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
