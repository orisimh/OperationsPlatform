using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Services;

public sealed class GenericRestApiExecutor(
    IHttpClientFactory httpClientFactory,
    IConfiguration appConfiguration,
    OperationsDbContext db,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<object?> ExecuteAsync(JsonElement config, string? fieldA, string? fieldB, CancellationToken cancellationToken)
    {
        var methodName = GetString(config, "httpMethod", "GET").ToUpperInvariant();
        var urlTemplate = GetString(config, "url", string.Empty);
        if (string.IsNullOrWhiteSpace(urlTemplate))
            throw new InvalidOperationException("REST API configuration is missing 'url'.");

        var url = ReplaceUrlTokens(urlTemplate, fieldA, fieldB);
        var query = new List<string>();
        if (config.TryGetProperty("queryParameters", out var queryParameters) && queryParameters.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in queryParameters.EnumerateObject())
            {
                var value = ReplaceTokens(JsonValueToString(item.Value), fieldA, fieldB);
                query.Add($"{Uri.EscapeDataString(item.Name)}={Uri.EscapeDataString(value)}");
            }
        }
        if (query.Count > 0) url += (url.Contains('?') ? "&" : "?") + string.Join("&", query);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("REST API URL must be an absolute HTTP or HTTPS URL.");
        await ValidateTargetAsync(uri, cancellationToken);

        using var request = new HttpRequestMessage(new HttpMethod(methodName), uri);
        var body = GetString(config, "bodyTemplate", string.Empty);
        if (!string.IsNullOrWhiteSpace(body) && methodName is not "GET" and not "HEAD")
            request.Content = new StringContent(ReplaceBodyTemplate(body, fieldA, fieldB), Encoding.UTF8,
                GetString(config, "contentType", "application/json"));

        if (config.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in headers.EnumerateObject())
            {
                var value = ReplaceTokens(JsonValueToString(header.Value), fieldA, fieldB);
                if (!request.Headers.TryAddWithoutValidation(header.Name, value) && request.Content is not null)
                    request.Content.Headers.TryAddWithoutValidation(header.Name, value);
            }
        }

        ApplyAuthentication(config, request);
        var timeoutSeconds = Math.Clamp(
            GetInt(config, "timeoutSeconds", appConfiguration.GetValue<int>("RestApi:DefaultTimeoutSeconds", 15)), 1, 120);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var client = httpClientFactory.CreateClient("OperationsExternal");
        var requestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(timeout.Token);
        var externalResponseLogged = false;

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var maxBytes = appConfiguration.GetValue<int>("RestApi:MaxResponseBytes", 1_048_576);
            if (response.Content.Headers.ContentLength is long length && length > maxBytes)
                throw new InvalidOperationException($"External API response exceeded the configured {maxBytes}-byte limit.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeout.Token)) > 0)
            {
                if (memory.Length + read > maxBytes)
                    throw new InvalidOperationException($"External API response exceeded the configured {maxBytes}-byte limit.");
                await memory.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }

            var responseText = Encoding.UTF8.GetString(memory.ToArray());
            await WriteExternalLogAsync(request, uri, requestBody, response.StatusCode,
                response.Headers, response.Content.Headers, responseText, null, cancellationToken);
            externalResponseLogged = true;

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"External API returned {(int)response.StatusCode} ({response.ReasonPhrase}): {Truncate(responseText, 1000)}");

            if (GetString(config, "responseType", "Json").Equals("Text", StringComparison.OrdinalIgnoreCase))
                return responseText;

            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseText) ? "null" : responseText);
            return document.RootElement.Clone();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!externalResponseLogged)
                await WriteExternalLogAsync(request, uri, requestBody, null, null, null, null, ex, cancellationToken);
            throw;
        }
    }

    private async Task WriteExternalLogAsync(
        HttpRequestMessage request,
        Uri uri,
        string? requestBody,
        HttpStatusCode? statusCode,
        System.Net.Http.Headers.HttpResponseHeaders? responseHeaders,
        System.Net.Http.Headers.HttpContentHeaders? responseContentHeaders,
        string? responseBody,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        ApplicationLog? logEntry = null;
        try
        {
            var maxChars = appConfiguration.GetValue<int>("Logging:MaxBodyChars", 12000);
            var requestHeaders = request.Headers.ToDictionary(
                x => x.Key,
                x => IsSensitiveHeader(x.Key) ? "***REDACTED***" : string.Join(",", x.Value));
            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                    requestHeaders[header.Key] = IsSensitiveHeader(header.Key) ? "***REDACTED***" : string.Join(",", header.Value);
            }

            var responseHeaderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (responseHeaders is not null)
            {
                foreach (var header in responseHeaders)
                    responseHeaderMap[header.Key] = IsSensitiveHeader(header.Key) ? "***REDACTED***" : string.Join(",", header.Value);
            }
            if (responseContentHeaders is not null)
            {
                foreach (var header in responseContentHeaders)
                    responseHeaderMap[header.Key] = IsSensitiveHeader(header.Key) ? "***REDACTED***" : string.Join(",", header.Value);
            }

            var safeRequestBody = RedactBody(Truncate(requestBody ?? string.Empty, maxChars));
            var safeResponseBody = RedactBody(Truncate(responseBody ?? string.Empty, maxChars));
            var correlationId = httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
                ?? "external-" + Guid.NewGuid().ToString("N");
            var successfulStatus = statusCode.HasValue && (int)statusCode.Value >= 200 && (int)statusCode.Value < 300;

            logEntry = new ApplicationLog
            {
                Level = exception is null && successfulStatus ? "Information" : "Error",
                Source = "ExternalApi",
                Message = Truncate(exception is null
                    ? $"Outbound {request.Method.Method} {uri.GetLeftPart(UriPartial.Path)} returned {(statusCode.HasValue ? (int)statusCode.Value : 0)}."
                    : $"Outbound {request.Method.Method} {uri.GetLeftPart(UriPartial.Path)} failed: {exception.Message}", 2000),
                CorrelationId = correlationId,
                RequestMethod = request.Method.Method,
                RequestPath = Truncate(uri.GetLeftPart(UriPartial.Path), 500),
                StatusCode = statusCode.HasValue ? (int)statusCode.Value : null,
                RequestHeadersJson = JsonSerializer.Serialize(requestHeaders),
                ResponseHeadersJson = JsonSerializer.Serialize(responseHeaderMap),
                RequestBody = string.IsNullOrEmpty(safeRequestBody) ? null : safeRequestBody,
                ResponseBody = string.IsNullOrEmpty(safeResponseBody) ? null : safeResponseBody,
                Exception = exception?.ToString(),
                CreatedAtUtc = DateTime.UtcNow
            };
            db.ApplicationLogs.Add(logEntry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (logEntry is not null)
                db.Entry(logEntry).State = EntityState.Detached;
            // Logging must not hide an external API outcome if the log database is unavailable.
        }
    }

    private static bool IsSensitiveHeader(string name)
    {
        var normalized = name.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized.Contains("authorization", StringComparison.Ordinal) ||
            normalized.Contains("cookie", StringComparison.Ordinal) ||
            normalized.Contains("apikey", StringComparison.Ordinal) ||
            normalized.Contains("token", StringComparison.Ordinal) ||
            normalized.Contains("secret", StringComparison.Ordinal) ||
            normalized.Contains("password", StringComparison.Ordinal);
    }

    private static string? RedactBody(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            using var document = JsonDocument.Parse(value);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) WriteRedacted(document.RootElement, writer);
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch
        {
            return Regex.Replace(value,
                "(?i)(password|token|secretValue|apiKey|authorization)(\\\"?\\s*[:=]\\s*\\\")[^\\\"]+",
                "$1$2***REDACTED***");
        }
    }

    private static void WriteRedacted(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (IsSensitiveHeader(property.Name))
                    writer.WriteStringValue("***REDACTED***");
                else
                    WriteRedacted(property.Value, writer);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteRedacted(item, writer);
            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    private async Task ValidateTargetAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (appConfiguration.GetValue<bool>("RestApi:AllowPrivateNetworkTargets", false)) return;
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var parsed)) addresses = new[] { parsed };
        else addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsPrivateOrLocal))
            throw new InvalidOperationException("The configured REST API target resolves to a private, loopback or link-local address. Enable RestApi:AllowPrivateNetworkTargets only for trusted intranet integrations.");
    }

    private static bool IsPrivateOrLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) || bytes[0] == 0;
        }
        var ipv6 = ip.GetAddressBytes();
        var uniqueLocal = (ipv6[0] & 0xFE) == 0xFC; // fc00::/7
        var siteLocal = ipv6[0] == 0xFE && (ipv6[1] & 0xC0) == 0xC0; // fec0::/10
        return ip.IsIPv6LinkLocal || siteLocal || uniqueLocal || ip.Equals(IPAddress.IPv6Loopback);
    }

    private static void ApplyAuthentication(JsonElement config, HttpRequestMessage request)
    {
        if (!config.TryGetProperty("authentication", out var auth) || auth.ValueKind != JsonValueKind.Object) return;
        var type = GetString(auth, "type", "None");
        var secret = GetString(auth, "secretValue", string.Empty);
        if (string.IsNullOrWhiteSpace(secret)) return;
        if (type.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);
        else if (type.Equals("ApiKeyHeader", StringComparison.OrdinalIgnoreCase))
            request.Headers.TryAddWithoutValidation(GetString(auth, "headerName", "X-API-Key"), secret);
    }

    private static string ReplaceTokens(string value, string? a, string? b) => value
        .Replace("{{fieldA}}", a ?? string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("{{fieldB}}", b ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string ReplaceUrlTokens(string value, string? a, string? b) => value
        .Replace("{{fieldA}}", Uri.EscapeDataString(a ?? string.Empty), StringComparison.OrdinalIgnoreCase)
        .Replace("{{fieldB}}", Uri.EscapeDataString(b ?? string.Empty), StringComparison.OrdinalIgnoreCase);

    private static string ReplaceBodyTemplate(string template, string? a, string? b)
    {
        try
        {
            var node = JsonNode.Parse(template);
            return ReplaceJsonTokens(node, a, b)?.ToJsonString() ?? "null";
        }
        catch (JsonException)
        {
            // Non-JSON bodies (for example XML or plain text) use direct token substitution.
            return ReplaceTokens(template, a, b);
        }
    }

    private static JsonNode? ReplaceJsonTokens(JsonNode? node, string? a, string? b)
    {
        if (node is JsonObject obj)
        {
            var copy = new JsonObject();
            foreach (var pair in obj) copy[pair.Key] = ReplaceJsonTokens(pair.Value, a, b);
            return copy;
        }
        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (var item in array) copy.Add(ReplaceJsonTokens(item, a, b));
            return copy;
        }
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return JsonValue.Create(ReplaceTokens(text ?? string.Empty, a, b));
        return node?.DeepClone();
    }

    private static string JsonValueToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
        _ => value.ToString()
    };

    private static string GetString(JsonElement element, string name, string fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;

    private static int GetInt(JsonElement element, string name, int fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...[truncated]";
}
