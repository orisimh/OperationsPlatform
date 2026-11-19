using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;

namespace OperationsPlatform.Api.Services;

public sealed class BuilderOperationExecutor(
    OperationsDbContext db,
    BuiltInHandlerCatalog handlers,
    GenericRestApiExecutor restExecutor,
    CSharpWorkerClient csharpWorker,
    IConfiguration configuration)
{
    public async Task<BuilderExecutionResult> ExecuteAsync(
        JsonElement operationConfiguration,
        string? fieldA,
        string? fieldB,
        int outerOperationId,
        CancellationToken ct,
        int depth = 0,
        HashSet<int>? operationChain = null)
    {
        if (depth >= 10)
            throw new InvalidOperationException("Builder nesting exceeded the maximum depth of 10.");

        if (!operationConfiguration.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Builder configuration must contain a 'nodes' array.");

        var maxNodes = configuration.GetValue<int>("Execution:MaxBuilderNodes", 30);
        if (nodes.GetArrayLength() == 0 || nodes.GetArrayLength() > maxNodes)
            throw new InvalidOperationException($"Builder must contain between 1 and {maxNodes} nodes.");

        var chain = operationChain is null
            ? new HashSet<int>()
            : new HashSet<int>(operationChain);
        chain.Add(outerOperationId);

        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var trace = new List<object>();
        foreach (var node in nodes.EnumerateArray())
        {
            var id = GetString(node, "id", string.Empty);
            if (string.IsNullOrWhiteSpace(id) || outputs.ContainsKey(id))
                throw new InvalidOperationException("Every Builder node must have a unique, non-empty id.");

            var inputARef = GetString(node, "inputA", "$fieldA");
            var inputBRef = GetString(node, "inputB", "$fieldB");
            var nodeA = ConvertToString(Resolve(inputARef, fieldA, fieldB, outputs));
            var nodeB = ConvertToString(Resolve(inputBRef, fieldA, fieldB, outputs));
            using var emptyConfig = JsonDocument.Parse("{}");
            var nodeConfig = node.TryGetProperty("configuration", out var c) ? c : emptyConfig.RootElement;

            object? result;
            string handlerKey = GetString(node, "handlerKey", string.Empty);
            string operationCode = string.Empty;

            var operationId = node.TryGetProperty("operationId", out var operationIdValue) && operationIdValue.TryGetInt32(out var parsedOperationId)
                ? parsedOperationId
                : (int?)null;

            if (operationId.HasValue)
            {
                var operation = await db.Operations.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == operationId.Value && x.IsActive, ct)
                    ?? throw new InvalidOperationException($"Builder node '{id}' references an inactive or missing operation ID '{operationId.Value}'.");

                if (chain.Contains(operation.Id))
                    throw new InvalidOperationException($"Builder cycle detected at operation '{operation.Code}'.");

                operationCode = operation.Code;
                handlerKey = GetHandlerKey(operation.ConfigurationJson);
                result = await ExecuteOperationAsync(operation, nodeA, nodeB, ct, depth, chain);
            }
            else if (node.TryGetProperty("operationCode", out var operationCodeValue) && operationCodeValue.ValueKind == JsonValueKind.String)
            {
                operationCode = operationCodeValue.GetString() ?? string.Empty;
                var operation = await db.Operations.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Code == operationCode && x.IsActive, ct)
                    ?? throw new InvalidOperationException($"Builder node '{id}' references an inactive or missing operation '{operationCode}'.");

                if (chain.Contains(operation.Id))
                    throw new InvalidOperationException($"Builder cycle detected at operation '{operation.Code}'.");

                operationId = operation.Id;
                handlerKey = GetHandlerKey(operation.ConfigurationJson);
                result = await ExecuteOperationAsync(operation, nodeA, nodeB, ct, depth, chain);
            }
            else
            {
                if (!handlers.Exists(handlerKey))
                    throw new InvalidOperationException($"Builder node '{id}' references unsupported handler '{handlerKey}'.");
                result = handlers.Execute(handlerKey, nodeA, nodeB, nodeConfig);
            }

            outputs[id] = result;
            trace.Add(new
            {
                nodeId = id,
                operationId,
                operationCode,
                handlerKey,
                inputA = nodeA,
                inputB = nodeB,
                output = result
            });
        }

        var returnRef = GetString(operationConfiguration, "return", outputs.Keys.Last() + ".output");
        var finalResult = Resolve(returnRef, fieldA, fieldB, outputs);
        return new BuilderExecutionResult(finalResult, trace);
    }

    private async Task<object?> ExecuteOperationAsync(
        Entities.OperationDefinition operation,
        string? fieldA,
        string? fieldB,
        CancellationToken ct,
        int depth,
        HashSet<int> operationChain)
    {
        using var configDoc = JsonDocument.Parse(operation.ConfigurationJson);
        var config = configDoc.RootElement;
        switch (operation.ImplementationType.ToLowerInvariant())
        {
            case "builtin":
            {
                var handlerKey = GetString(config, "handlerKey", string.Empty);
                if (!handlers.Exists(handlerKey))
                    throw new InvalidOperationException($"Operation '{operation.Code}' references unknown built-in handler '{handlerKey}'.");
                return handlers.Execute(handlerKey, fieldA, fieldB, config);
            }
            case "restapi":
                return await restExecutor.ExecuteAsync(config, fieldA, fieldB, ct);
            case "csharp":
            {
                var sourceCode = GetString(config, "sourceCode", string.Empty);
                using var operationConfig = JsonDocument.Parse(config.TryGetProperty("operationConfiguration", out var oc) ? oc.GetRawText() : "{}");
                var workerResult = await csharpWorker.ExecuteAsync(sourceCode, fieldA, fieldB, operationConfig.RootElement, false, ct);
                if (!workerResult.Success)
                    throw new InvalidOperationException(workerResult.ErrorMessage ?? string.Join("; ", workerResult.Diagnostics));
                return JsonSerializer.Deserialize<JsonElement>(workerResult.ResultJson ?? "null");
            }
            case "builder":
            {
                var nestedChain = new HashSet<int>(operationChain) { operation.Id };
                var nestedResult = await ExecuteAsync(config, fieldA, fieldB, operation.Id, ct, depth + 1, nestedChain);
                return nestedResult.Result;
            }
            default:
                throw new InvalidOperationException($"Operation '{operation.Code}' has unsupported implementation type '{operation.ImplementationType}'.");
        }
    }

    private static string GetHandlerKey(string configurationJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configurationJson);
            return GetString(doc.RootElement, "handlerKey", string.Empty);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ConvertToString(object? value)
    {
        if (value is null) return string.Empty;
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText()
            };
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static object? Resolve(string reference, string? fieldA, string? fieldB, IReadOnlyDictionary<string, object?> outputs)
    {
        if (reference.Equals("$fieldA", StringComparison.OrdinalIgnoreCase)) return fieldA;
        if (reference.Equals("$fieldB", StringComparison.OrdinalIgnoreCase)) return fieldB;
        if (reference.StartsWith("literal:", StringComparison.OrdinalIgnoreCase)) return reference[8..];
        if (reference.EndsWith(".output", StringComparison.OrdinalIgnoreCase))
        {
            var id = reference[..^7];
            if (outputs.TryGetValue(id, out var result)) return result;
        }
        throw new InvalidOperationException($"Builder reference '{reference}' is invalid or refers to a node that has not executed yet.");
    }

    private static string GetString(JsonElement element, string name, string fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;
}

public sealed record BuilderExecutionResult(object? Result, IReadOnlyList<object> Trace);
