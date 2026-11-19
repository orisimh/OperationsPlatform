using System.Text.Json;

namespace OperationsPlatform.Api.Services;

public sealed class BuilderOperationExecutor(BuiltInHandlerCatalog handlers, IConfiguration configuration)
{
    public BuilderExecutionResult Execute(JsonElement operationConfiguration, string? fieldA, string? fieldB)
    {
        if (!operationConfiguration.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Builder configuration must contain a 'nodes' array.");

        var maxNodes = configuration.GetValue<int>("Execution:MaxBuilderNodes", 30);
        if (nodes.GetArrayLength() == 0 || nodes.GetArrayLength() > maxNodes)
            throw new InvalidOperationException($"Builder must contain between 1 and {maxNodes} nodes.");

        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var trace = new List<object>();
        foreach (var node in nodes.EnumerateArray())
        {
            var id = GetString(node, "id", string.Empty);
            var handlerKey = GetString(node, "handlerKey", string.Empty);
            if (string.IsNullOrWhiteSpace(id) || outputs.ContainsKey(id))
                throw new InvalidOperationException("Every Builder node must have a unique, non-empty id.");
            if (!handlers.Exists(handlerKey))
                throw new InvalidOperationException($"Builder node '{id}' references unsupported handler '{handlerKey}'. Only Built-in handlers are allowed in Builder nodes.");

            var inputARef = GetString(node, "inputA", "$fieldA");
            var inputBRef = GetString(node, "inputB", "$fieldB");
            var nodeA = Convert.ToString(Resolve(inputARef, fieldA, fieldB, outputs), System.Globalization.CultureInfo.InvariantCulture);
            var nodeB = Convert.ToString(Resolve(inputBRef, fieldA, fieldB, outputs), System.Globalization.CultureInfo.InvariantCulture);
            using var emptyConfig = JsonDocument.Parse("{}");
            var nodeConfig = node.TryGetProperty("configuration", out var c) ? c : emptyConfig.RootElement;
            var result = handlers.Execute(handlerKey, nodeA, nodeB, nodeConfig);
            outputs[id] = result;
            trace.Add(new { nodeId = id, handlerKey, inputA = nodeA, inputB = nodeB, output = result });
        }

        var returnRef = GetString(operationConfiguration, "return", outputs.Keys.Last() + ".output");
        var finalResult = Resolve(returnRef, fieldA, fieldB, outputs);
        return new BuilderExecutionResult(finalResult, trace);
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
