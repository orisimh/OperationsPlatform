using System.Globalization;
using System.Text.Json;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Services;

/// <summary>Allow-listed functions that can be selected by configuration or used as Operation Builder nodes.</summary>
public sealed class BuiltInHandlerCatalog
{
    private readonly Dictionary<string, Func<string?, string?, JsonElement, object?>> _handlers;

    public BuiltInHandlerCatalog()
    {
        _handlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["string.concatenate"] = (a, b, c) => $"{a ?? string.Empty}{GetString(c, "separator", string.Empty)}{b ?? string.Empty}",
            ["string.contains"] = (a, b, c) => (a ?? string.Empty).Contains(b ?? string.Empty, GetComparison(c)),
            ["string.equals"] = (a, b, c) => string.Equals(a, b, GetComparison(c)),
            ["string.startsWith"] = (a, b, c) => (a ?? string.Empty).StartsWith(b ?? string.Empty, GetComparison(c)),
            ["string.endsWith"] = (a, b, c) => (a ?? string.Empty).EndsWith(b ?? string.Empty, GetComparison(c)),
            ["string.replace"] = (a, b, c) => Replace(a, b, c),
            ["string.reverse"] = (a, _, _) => new string((a ?? string.Empty).Reverse().ToArray()),
            ["string.lowercase"] = (a, _, c) => GetBool(c, "trimFirst", false) ? (a ?? string.Empty).Trim().ToLowerInvariant() : (a ?? string.Empty).ToLowerInvariant(),
            ["string.uppercase"] = (a, _, c) => GetBool(c, "trimFirst", false) ? (a ?? string.Empty).Trim().ToUpperInvariant() : (a ?? string.Empty).ToUpperInvariant(),
            ["string.trim"] = (a, _, _) => (a ?? string.Empty).Trim(),
            ["integer.add"] = (a, b, _) => checked(ParseInt(a) + ParseInt(b)),
            ["integer.subtract"] = (a, b, _) => checked(ParseInt(a) - ParseInt(b)),
            ["integer.multiply"] = (a, b, _) => checked(ParseInt(a) * ParseInt(b)),
            ["integer.divide"] = (a, b, _) => Divide(a, b),
            ["integer.modulo"] = (a, b, _) => Modulo(a, b)
        };
    }

    public IReadOnlyCollection<string> Keys => _handlers.Keys;

    public IReadOnlyList<HandlerDescriptorDto> GetDescriptors() =>
        Keys.Select(key => new HandlerDescriptorDto(key, GetLabel(key))).ToList();

    public object? Execute(string handlerKey, string? fieldA, string? fieldB, JsonElement configuration)
    {
        if (!_handlers.TryGetValue(handlerKey, out var handler))
            throw new InvalidOperationException($"Unknown handler '{handlerKey}'.");
        return handler(fieldA, fieldB, configuration);
    }

    public bool Exists(string handlerKey) => _handlers.ContainsKey(handlerKey);

    private static string GetLabel(string key) => key switch
    {
        "string.concatenate" => "Concatenate",
        "string.contains" => "Contains",
        "string.equals" => "Equals",
        "string.startsWith" => "Starts With",
        "string.endsWith" => "Ends With",
        "string.replace" => "Replace",
        "string.reverse" => "Reverse",
        "string.lowercase" => "Lowercase",
        "string.uppercase" => "Uppercase",
        "string.trim" => "Trim",
        "integer.add" => "Add",
        "integer.subtract" => "Subtract",
        "integer.multiply" => "Multiply",
        "integer.divide" => "Divide",
        "integer.modulo" => "Modulo",
        _ => key
    };

    private static object Replace(string? a, string? b, JsonElement config)
    {
        var source = a ?? string.Empty;
        var search = b ?? string.Empty;
        if (search.Length == 0) throw new ArgumentException("The search value (Field B) cannot be empty.");
        return source.Replace(search, GetString(config, "replacementText", string.Empty), GetComparison(config));
    }

    private static object Divide(string? a, string? b)
    {
        var left = ParseInt(a);
        var right = ParseInt(b);
        if (right == 0) throw new DivideByZeroException("Division by zero is not allowed.");
        return (decimal)left / right;
    }

    private static object Modulo(string? a, string? b)
    {
        var left = ParseInt(a);
        var right = ParseInt(b);
        if (right == 0) throw new DivideByZeroException("Modulo by zero is not allowed.");
        return left % right;
    }

    private static int ParseInt(string? value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"'{value}' is not a valid 32-bit integer.");
        return result;
    }

    private static StringComparison GetComparison(JsonElement config) =>
        GetBool(config, "caseSensitive", false) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool GetBool(JsonElement element, string name, bool fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) ? value.GetBoolean() : fallback;

    private static string GetString(JsonElement element, string name, string fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;
}
