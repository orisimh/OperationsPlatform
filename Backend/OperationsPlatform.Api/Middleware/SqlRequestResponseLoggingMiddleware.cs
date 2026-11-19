using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Middleware;

public sealed class SqlRequestResponseLoggingMiddleware(RequestDelegate next, IConfiguration configuration, IServiceScopeFactory scopeFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var maxChars = configuration.GetValue<int>("Logging:MaxBodyChars", 12000);
        var requestBody = await ReadRequestBodyAsync(context.Request, maxChars);
        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;
        try
        {
            await next(context);
        }
        finally
        {
            responseBuffer.Position = 0;
            var responseText = await new StreamReader(responseBuffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
            responseBuffer.Position = 0;
            await responseBuffer.CopyToAsync(originalBody);
            context.Response.Body = originalBody;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
                db.ApplicationLogs.Add(new ApplicationLog
                {
                    Level = context.Response.StatusCode >= 500 ? "Error" : context.Response.StatusCode >= 400 ? "Warning" : "Information",
                    Source = "HttpRequest",
                    Message = Truncate($"{context.Request.Method} {context.Request.Path} returned {context.Response.StatusCode}", 2000),
                    CorrelationId = context.Items["CorrelationId"]?.ToString() ?? string.Empty,
                    RequestMethod = context.Request.Method,
                    RequestPath = Truncate(GetSanitizedRequestPath(context.Request), 500),
                    StatusCode = context.Response.StatusCode,
                    RequestHeadersJson = SerializeHeaders(context.Request.Headers),
                    ResponseHeadersJson = SerializeHeaders(context.Response.Headers),
                    RequestBody = RedactSecrets(requestBody),
                    ResponseBody = RedactSecrets(Truncate(responseText, maxChars)),
                    CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(context.RequestAborted);
            }
            catch
            {
                // Logging must never break the business request if SQL logging itself is unavailable.
            }
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request, int maxChars)
    {
        if (request.ContentLength == 0 || !request.Body.CanRead) return null;
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var buffer = new char[Math.Min(4096, Math.Max(1, maxChars + 1))];
        var builder = new StringBuilder(Math.Min(maxChars + 1, 4096));
        while (builder.Length <= maxChars)
        {
            var wanted = Math.Min(buffer.Length, maxChars + 1 - builder.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, wanted), request.HttpContext.RequestAborted);
            if (read == 0) break;
            builder.Append(buffer, 0, read);
        }
        request.Body.Position = 0;
        return builder.Length > maxChars ? builder.ToString(0, maxChars) + "...[truncated]" : builder.ToString();
    }


    private static string GetSanitizedRequestPath(HttpRequest request)
    {
        var path = request.Path.ToString();
        var query = QueryHelpers.ParseQuery(request.QueryString.Value ?? string.Empty);
        var pairs = query.SelectMany(item => item.Value.Select(value =>
        {
            var safeValue = IsSensitivePropertyName(item.Key) ? "***REDACTED***" : value ?? string.Empty;
            return $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(safeValue)}";
        }));
        var queryString = string.Join("&", pairs);
        return string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
    }

    private static string SerializeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = headers.ToDictionary(
            x => x.Key,
            x => IsSensitivePropertyName(x.Key) || x.Key.Equals("set-cookie", StringComparison.OrdinalIgnoreCase)
                ? "***REDACTED***" : x.Value.ToString());
        return JsonSerializer.Serialize(safeHeaders);
    }

    private static string? RedactSecrets(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        try
        {
            using var document = JsonDocument.Parse(text);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) WriteRedacted(document.RootElement, writer);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch { return Regex.Replace(text, "(?i)(token|password|secretValue|apiKey)(\\\"?\\s*[:=]\\s*\\\")[^\\\"]+", "$1$2***"); }
    }


    private static string? RedactNestedJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            using var nested = JsonDocument.Parse(value);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) WriteRedacted(nested.RootElement, writer);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch { return "***CONFIGURATION REDACTED***"; }
    }

    private static bool IsSensitivePropertyName(string name)
    {
        var normalized = name.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized.Contains("secret", StringComparison.Ordinal) ||
            normalized.Contains("apikey", StringComparison.Ordinal) ||
            normalized.Contains("token", StringComparison.Ordinal) ||
            normalized.Contains("authorization", StringComparison.Ordinal) ||
            normalized.Contains("password", StringComparison.Ordinal) ||
            normalized.Contains("cookie", StringComparison.Ordinal);
    }

    private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsSensitivePropertyName(property.Name))
                        writer.WriteStringValue("***REDACTED***");
                    else if (property.Name.EndsWith("ConfigJson", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                        writer.WriteStringValue(RedactNestedJson(property.Value.GetString()));
                    else WriteRedacted(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteRedacted(item, writer); writer.WriteEndArray(); break;
            default: element.WriteTo(writer); break;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...[truncated]";
}
