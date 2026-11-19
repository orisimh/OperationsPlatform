using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Entities;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Services;

public sealed class OperationDefinitionService(OperationsDbContext db, CSharpWorkerClient csharpWorker, BuiltInHandlerCatalog handlers, IConfiguration configuration)
{
    private static readonly HashSet<string> OperationTypes = new(StringComparer.OrdinalIgnoreCase) { "String", "Integer", "RestApi" };
    private static readonly HashSet<string> ImplementationTypes = new(StringComparer.OrdinalIgnoreCase) { "BuiltIn", "CSharp", "Builder", "RestApi" };
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    public async Task<IReadOnlyList<OperationDto>> GetAllAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.Operations.AsNoTracking();
        if (!includeInactive) query = query.Where(x => x.IsActive);
        var entities = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
        return entities.Select(ToDto).ToList();
    }

    public async Task<OperationDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await db.Operations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<OperationDto> CreateAsync(UpsertOperationRequest request, CancellationToken ct)
    {
        ValidateRequest(request);
        await ValidateImplementationAsync(request, ct);
        var normalizedCode = request.Code.Trim();
        if (await db.Operations.AnyAsync(x => x.Code == normalizedCode, ct))
            throw new InvalidOperationException($"Operation code '{normalizedCode}' already exists.");

        var now = DateTime.UtcNow;
        var entity = new OperationDefinition
        {
            Code = normalizedCode, Name = request.Name.Trim(), Description = request.Description?.Trim() ?? string.Empty,
            OperationType = request.OperationType, ImplementationType = request.ImplementationType,
            ExecutionMode = "Server", InputAConfigJson = request.InputAConfigJson,
            InputBConfigJson = request.InputBConfigJson, OutputType = request.OutputType,
            ConfigurationJson = request.ConfigurationJson,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "bolt" : request.Icon.Trim(),
            IsActive = request.IsActive, SortOrder = request.SortOrder,
            Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.Operations.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<OperationDto?> UpdateAsync(int id, UpsertOperationRequest request, CancellationToken ct)
    {
        var entity = await db.Operations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (request.ImplementationType.Equals("RestApi", StringComparison.OrdinalIgnoreCase))
            request.ConfigurationJson = PreserveExistingRestSecret(entity.ConfigurationJson, request.ConfigurationJson);
        ValidateRequest(request);
        await ValidateImplementationAsync(request, ct);
        var normalizedCode = request.Code.Trim();
        if (await db.Operations.AnyAsync(x => x.Id != id && x.Code == normalizedCode, ct))
            throw new InvalidOperationException($"Operation code '{normalizedCode}' already exists.");

        var normalizedDescription = request.Description?.Trim() ?? string.Empty;
        var normalizedCategory = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        var normalizedIcon = string.IsNullOrWhiteSpace(request.Icon) ? "bolt" : request.Icon.Trim();
        var changed = entity.Code != normalizedCode || entity.Name != request.Name.Trim() || entity.Description != normalizedDescription ||
            entity.OperationType != request.OperationType || entity.ImplementationType != request.ImplementationType ||
            entity.InputAConfigJson != request.InputAConfigJson || entity.InputBConfigJson != request.InputBConfigJson ||
            entity.ConfigurationJson != request.ConfigurationJson || entity.OutputType != request.OutputType ||
            entity.Category != normalizedCategory || entity.Icon != normalizedIcon ||
            entity.IsActive != request.IsActive || entity.SortOrder != request.SortOrder;

        entity.Code = normalizedCode; entity.Name = request.Name.Trim(); entity.Description = normalizedDescription;
        entity.OperationType = request.OperationType; entity.ImplementationType = request.ImplementationType;
        entity.ExecutionMode = "Server"; entity.InputAConfigJson = request.InputAConfigJson;
        entity.InputBConfigJson = request.InputBConfigJson; entity.OutputType = request.OutputType;
        entity.ConfigurationJson = request.ConfigurationJson; entity.Category = normalizedCategory;
        entity.Icon = normalizedIcon; entity.IsActive = request.IsActive; entity.SortOrder = request.SortOrder;
        if (changed) entity.Version++;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await db.Operations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return false;
        if (entity.IsActive)
        {
            entity.IsActive = false;
            entity.Version++;
        }
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void ValidateRequest(UpsertOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 120)
            throw new ArgumentException("Code is required and must be at most 120 characters.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
            throw new ArgumentException("Name is required and must be at most 160 characters.");
        if (!OperationTypes.Contains(request.OperationType))
            throw new ArgumentException("OperationType must be String, Integer or RestApi.");
        if (!ImplementationTypes.Contains(request.ImplementationType))
            throw new ArgumentException("ImplementationType must be BuiltIn, CSharp, Builder or RestApi.");
        if ((request.Description?.Length ?? 0) > 1000) throw new ArgumentException("Description cannot exceed 1000 characters.");
        if ((request.Category?.Length ?? 0) > 80) throw new ArgumentException("Category cannot exceed 80 characters.");
        if ((request.Icon?.Length ?? 0) > 80) throw new ArgumentException("Icon cannot exceed 80 characters.");
        if (string.IsNullOrWhiteSpace(request.OutputType) || request.OutputType.Length > 40)
            throw new ArgumentException("OutputType is required and must be at most 40 characters.");
        if (string.IsNullOrWhiteSpace(request.InputAConfigJson) || string.IsNullOrWhiteSpace(request.InputBConfigJson) || string.IsNullOrWhiteSpace(request.ConfigurationJson))
            throw new ArgumentException("Input and operation configuration JSON fields are required.");

        using var inputA = JsonDocument.Parse(request.InputAConfigJson);
        using var inputB = JsonDocument.Parse(request.InputBConfigJson);
        using var configuration = JsonDocument.Parse(request.ConfigurationJson);
        if (inputA.RootElement.ValueKind != JsonValueKind.Object || inputB.RootElement.ValueKind != JsonValueKind.Object || configuration.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Input and operation configurations must be JSON objects.");
    }

    private async Task ValidateImplementationAsync(UpsertOperationRequest request, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(request.ConfigurationJson);
        var config = document.RootElement;
        if (request.ImplementationType.Equals("CSharp", StringComparison.OrdinalIgnoreCase))
        {
            if (!config.TryGetProperty("sourceCode", out var source) || source.ValueKind != JsonValueKind.String)
                throw new ArgumentException("CSharp operations require configuration.sourceCode.");
            using var emptyConfig = JsonDocument.Parse("{}");
            var validation = await csharpWorker.ExecuteAsync(source.GetString() ?? string.Empty, "sample-a", "sample-b", emptyConfig.RootElement, true, ct);
            if (!validation.Success)
                throw new ArgumentException("C# compilation failed: " + string.Join(Environment.NewLine, validation.Diagnostics.Take(10)) + " " + validation.ErrorMessage);
            return;
        }

        if (request.ImplementationType.Equals("RestApi", StringComparison.OrdinalIgnoreCase))
        {
            var method = config.TryGetProperty("httpMethod", out var methodValue) ? methodValue.GetString() ?? "GET" : "GET";
            var url = config.TryGetProperty("url", out var urlValue) ? urlValue.GetString() : null;
            if (!HttpMethods.Contains(method)) throw new ArgumentException("REST API httpMethod must be GET, POST, PUT, PATCH, DELETE, HEAD or OPTIONS.");
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("REST API configuration requires an absolute HTTP or HTTPS URL.");
            return;
        }

        if (request.ImplementationType.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            if (!config.TryGetProperty("handlerKey", out var handler) || handler.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(handler.GetString()))
                throw new ArgumentException("Built-in operations require configuration.handlerKey.");
            if (!handlers.Exists(handler.GetString()!))
                throw new ArgumentException($"Unknown built-in handler '{handler.GetString()}'.");
        }
        else if (request.ImplementationType.Equals("Builder", StringComparison.OrdinalIgnoreCase))
        {
            var maxNodes = configuration.GetValue<int>("Execution:MaxBuilderNodes", 30);
            if (!config.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() == 0 || nodes.GetArrayLength() > maxNodes)
                throw new ArgumentException($"Builder operations require a nodes array containing between 1 and {maxNodes} nodes.");
            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var orderedIds = new List<string>();
            foreach (var node in nodes.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Each Builder node must be a JSON object.");
                var nodeId = node.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
                if (string.IsNullOrWhiteSpace(nodeId) || !nodeIds.Add(nodeId))
                    throw new ArgumentException("Builder node IDs must be unique and non-empty.");

                var operationId = 0;
                var hasOperationId = node.TryGetProperty("operationId", out var operationIdValue) && operationIdValue.TryGetInt32(out operationId);
                var hasOperationCode = node.TryGetProperty("operationCode", out var operationCodeValue) && operationCodeValue.ValueKind == JsonValueKind.String;
                var key = node.TryGetProperty("handlerKey", out var keyValue) ? keyValue.GetString() : null;

                if (hasOperationId)
                {
                    if (!db.Operations.Any(x => x.Id == operationId && x.IsActive))
                        throw new ArgumentException($"Builder node '{nodeId}' references an inactive or missing operation ID '{operationId}'.");
                }
                else if (hasOperationCode)
                {
                    var operationCode = operationCodeValue.GetString();
                    if (string.IsNullOrWhiteSpace(operationCode) || !db.Operations.Any(x => x.Code == operationCode && x.IsActive))
                        throw new ArgumentException($"Builder node '{nodeId}' references an inactive or missing operation '{operationCode}'.");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(key) || !handlers.Exists(key))
                        throw new ArgumentException($"Builder node '{nodeId}' references an unknown built-in handler.");
                }

                ValidateBuilderReference(GetNodeString(node, "inputA", "$fieldA"), nodeIds.Where(x => x != nodeId).ToHashSet(StringComparer.OrdinalIgnoreCase), nodeId);
                ValidateBuilderReference(GetNodeString(node, "inputB", "$fieldB"), nodeIds.Where(x => x != nodeId).ToHashSet(StringComparer.OrdinalIgnoreCase), nodeId);
                orderedIds.Add(nodeId);
            }
            var returnReference = config.TryGetProperty("return", out var returnValue) && returnValue.ValueKind == JsonValueKind.String
                ? returnValue.GetString() ?? string.Empty : orderedIds[^1] + ".output";
            ValidateBuilderReference(returnReference, nodeIds, "final output");
            await ValidateBuilderGraphAsync(config, ct);
        }
    }


    private async Task ValidateBuilderGraphAsync(JsonElement config, CancellationToken ct)
    {
        await ValidateBuilderGraphNodeAsync(config, new HashSet<int>(), 0, ct);
    }

    private async Task ValidateBuilderGraphNodeAsync(JsonElement config, HashSet<int> operationChain, int depth, CancellationToken ct)
    {
        if (depth >= 10)
            throw new ArgumentException("Builder nesting cannot exceed 10 levels.");
        if (!config.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            return;

        foreach (var node in nodes.EnumerateArray())
        {
            OperationDefinition? operation = null;
            if (node.TryGetProperty("operationId", out var operationIdValue) && operationIdValue.TryGetInt32(out var operationId))
            {
                if (operationChain.Contains(operationId))
                    throw new ArgumentException("Builder graph contains a cycle between operation references.");
                operation = await db.Operations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == operationId && x.IsActive, ct);
            }
            else if (node.TryGetProperty("operationCode", out var operationCodeValue) && operationCodeValue.ValueKind == JsonValueKind.String)
            {
                var operationCode = operationCodeValue.GetString();
                operation = await db.Operations.AsNoTracking().FirstOrDefaultAsync(x => x.Code == operationCode && x.IsActive, ct);
                if (operation is not null && operationChain.Contains(operation.Id))
                    throw new ArgumentException("Builder graph contains a cycle between operation references.");
            }

            if (operation?.ImplementationType.Equals("Builder", StringComparison.OrdinalIgnoreCase) == true)
            {
                var nestedChain = new HashSet<int>(operationChain) { operation.Id };
                using var nestedConfig = JsonDocument.Parse(operation.ConfigurationJson);
                await ValidateBuilderGraphNodeAsync(nestedConfig.RootElement, nestedChain, depth + 1, ct);
            }
        }
    }

    private static string GetNodeString(JsonElement node, string name, string fallback) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;

    private static void ValidateBuilderReference(string reference, HashSet<string> availableNodeIds, string nodeId)
    {
        if (reference.Equals("$fieldA", StringComparison.OrdinalIgnoreCase) ||
            reference.Equals("$fieldB", StringComparison.OrdinalIgnoreCase) ||
            reference.StartsWith("literal:", StringComparison.OrdinalIgnoreCase)) return;
        if (reference.EndsWith(".output", StringComparison.OrdinalIgnoreCase) &&
            availableNodeIds.Contains(reference[..^7])) return;
        throw new ArgumentException($"Builder node '{nodeId}' has an invalid input/output reference '{reference}'. A node can only use Field A, Field B, a literal or an output from a previous node.");
    }

    private static string PreserveExistingRestSecret(string existingJson, string incomingJson)
    {
        try
        {
            var existing = JsonNode.Parse(existingJson) as JsonObject;
            var incoming = JsonNode.Parse(incomingJson) as JsonObject;
            var oldAuth = existing?["authentication"] as JsonObject;
            var newAuth = incoming?["authentication"] as JsonObject;
            if (oldAuth is null || newAuth is null) return incomingJson;

            var oldType = oldAuth["type"]?.GetValue<string>() ?? "None";
            var newType = newAuth["type"]?.GetValue<string>() ?? "None";
            var incomingSecret = newAuth["secretValue"]?.GetValue<string>();
            var existingSecret = oldAuth["secretValue"]?.GetValue<string>();
            if (oldType.Equals(newType, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(existingSecret) &&
                (string.IsNullOrWhiteSpace(incomingSecret) || incomingSecret == "***REDACTED***"))
            {
                newAuth["secretValue"] = existingSecret;
            }

            // Preserve masked custom secret headers when the editor resubmits their redaction marker.
            var oldHeaders = existing?["headers"] as JsonObject;
            var newHeaders = incoming?["headers"] as JsonObject;
            if (oldHeaders is not null && newHeaders is not null)
            {
                foreach (var oldHeader in oldHeaders)
                {
                    if (!IsSensitivePropertyName(oldHeader.Key) || oldHeader.Value is null) continue;
                    var matching = newHeaders.FirstOrDefault(x => x.Key.Equals(oldHeader.Key, StringComparison.OrdinalIgnoreCase));
                    if (matching.Key is null) continue; // Removing a header explicitly remains possible.
                    var newValue = matching.Value is JsonValue jv && jv.TryGetValue<string>(out var text) ? text : null;
                    if (string.IsNullOrWhiteSpace(newValue) || newValue == "***REDACTED***")
                        newHeaders[matching.Key] = oldHeader.Value.DeepClone();
                }
            }
            return incoming!.ToJsonString();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return incomingJson; // Regular request validation will report malformed JSON.
        }
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

    private static string RedactSecretValues(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output)) WriteRedacted(document.RootElement, writer);
            return System.Text.Encoding.UTF8.GetString(output.ToArray());
        }
        catch (JsonException)
        {
            return "{}";
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
                if (IsSensitivePropertyName(property.Name))
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

    private static OperationDto ToDto(OperationDefinition x) => new()
    {
        Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description,
        OperationType = x.OperationType, ImplementationType = x.ImplementationType,
        ExecutionMode = x.ExecutionMode, InputAConfigJson = x.InputAConfigJson,
        InputBConfigJson = x.InputBConfigJson, OutputType = x.OutputType,
        ConfigurationJson = RedactSecretValues(x.ConfigurationJson), Category = x.Category, Icon = x.Icon,
        IsActive = x.IsActive, SortOrder = x.SortOrder, Version = x.Version,
        CreatedAtUtc = x.CreatedAtUtc, UpdatedAtUtc = x.UpdatedAtUtc
    };
}
