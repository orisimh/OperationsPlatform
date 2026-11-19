using System.Text.Json;
using OperationsPlatform.Api.Entities;

namespace OperationsPlatform.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(OperationsDbContext db)
    {
        var operations = new List<OperationDefinition>
        {
            BuiltIn("string.concatenate", "Concatenate Strings", "Combines two strings with an optional separator.", "String", "String", "text_fields", 10,
                new { separator = "" }, new { type = "String", label = "First string", required = true, placeholder = "Hello" }, new { type = "String", label = "Second string", required = true, placeholder = "World" }),
            BuiltIn("string.contains", "Contains", "Checks whether the first string contains the second string.", "String", "Boolean", "search", 20,
                new { caseSensitive = false }, new { type = "String", label = "Text", required = true }, new { type = "String", label = "Search text", required = true }),
            BuiltIn("string.equals", "Equals", "Compares two strings.", "String", "Boolean", "compare_arrows", 30,
                new { caseSensitive = false }, new { type = "String", label = "First string", required = true }, new { type = "String", label = "Second string", required = true }),
            BuiltIn("string.startsWith", "Starts With", "Checks whether the first string starts with the second string.", "String", "Boolean", "vertical_align_top", 40,
                new { caseSensitive = false }, new { type = "String", label = "Text", required = true }, new { type = "String", label = "Prefix", required = true }),
            BuiltIn("string.endsWith", "Ends With", "Checks whether the first string ends with the second string.", "String", "Boolean", "vertical_align_bottom", 50,
                new { caseSensitive = false }, new { type = "String", label = "Text", required = true }, new { type = "String", label = "Suffix", required = true }),
            BuiltIn("string.replace", "Replace", "Replaces every occurrence of Field B in Field A.", "String", "String", "find_replace", 60,
                new { replacementText = "", caseSensitive = true }, new { type = "String", label = "Source text", required = true }, new { type = "String", label = "Text to find", required = true }),
            BuiltIn("string.reverse", "Reverse", "Reverses the first string. Field B is retained for the common two-input contract.", "String", "String", "swap_horiz", 70,
                new Dictionary<string, object?>(), new { type = "String", label = "Text", required = true }, new { type = "String", label = "Additional input (not used)", required = true }),
            BuiltIn("string.lowercase", "Lowercase", "Converts Field A to lowercase.", "String", "String", "text_fields", 80,
                new { trimFirst = false }, new { type = "String", label = "Text", required = true }, new { type = "String", label = "Additional input (not used)", required = true }),
            BuiltIn("string.uppercase", "Uppercase", "Converts Field A to uppercase.", "String", "String", "text_fields", 90,
                new { trimFirst = false }, new { type = "String", label = "Text", required = true }, new { type = "String", label = "Additional input (not used)", required = true }),
            BuiltIn("string.trim", "Trim", "Removes whitespace at the beginning and end of Field A.", "String", "String", "format_align_left", 100,
                new Dictionary<string, object?>(), new { type = "String", label = "Text", required = true }, new { type = "String", label = "Additional input (not used)", required = true }),

            BuiltIn("integer.add", "Addition", "Adds two integer values.", "Integer", "Integer", "add", 110,
                new Dictionary<string, object?>(), new { type = "Integer", label = "First number", required = true }, new { type = "Integer", label = "Second number", required = true }),
            BuiltIn("integer.subtract", "Subtraction", "Subtracts Field B from Field A.", "Integer", "Integer", "remove", 120,
                new Dictionary<string, object?>(), new { type = "Integer", label = "First number", required = true }, new { type = "Integer", label = "Second number", required = true }),
            BuiltIn("integer.multiply", "Multiplication", "Multiplies two integer values.", "Integer", "Integer", "close", 130,
                new Dictionary<string, object?>(), new { type = "Integer", label = "First number", required = true }, new { type = "Integer", label = "Second number", required = true }),
            BuiltIn("integer.divide", "Division", "Divides Field A by Field B. The result may contain decimals.", "Integer", "Decimal", "functions", 140,
                new Dictionary<string, object?>(), new { type = "Integer", label = "Dividend", required = true }, new { type = "Integer", label = "Divisor", required = true }),
            BuiltIn("integer.modulo", "Modulo", "Returns the remainder after integer division.", "Integer", "Integer", "percent", 150,
                new Dictionary<string, object?>(), new { type = "Integer", label = "First number", required = true }, new { type = "Integer", label = "Second number", required = true }),

            Rest("weather.forecast", "Weather Forecast", "Gets current weather for latitude and longitude using Open-Meteo.", 160),
            CSharpSample(),
            WeatherTemperatureCSharpSample(),
            BuilderSample(),
            WeatherTemperatureBuilderSample()
        };

        foreach (var operation in operations)
        {
            if (!db.Operations.Any(x => x.Code == operation.Code)) db.Operations.Add(operation);
        }
        await db.SaveChangesAsync();
    }

    private static OperationDefinition BuiltIn(string code, string name, string description, string type, string output,
        string icon, int sort, object configuration, object inputA, object inputB) => new()
    {
        Code = code, Name = name, Description = description, OperationType = type,
        ImplementationType = "BuiltIn", ExecutionMode = "Server", OutputType = output,
        Category = type == "Integer" ? "Mathematics" : "String", Icon = icon, SortOrder = sort,
        InputAConfigJson = JsonSerializer.Serialize(inputA), InputBConfigJson = JsonSerializer.Serialize(inputB),
        ConfigurationJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["handlerKey"] = code,
            ["separator"] = configuration.GetType().GetProperty("separator")?.GetValue(configuration),
            ["caseSensitive"] = configuration.GetType().GetProperty("caseSensitive")?.GetValue(configuration),
            ["replacementText"] = configuration.GetType().GetProperty("replacementText")?.GetValue(configuration),
            ["trimFirst"] = configuration.GetType().GetProperty("trimFirst")?.GetValue(configuration)
        }.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value))
    };

    private static OperationDefinition Rest(string code, string name, string description, int sort)
    {
        var config = new
        {
            httpMethod = "GET",
            url = "https://api.open-meteo.com/v1/forecast",
            queryParameters = new Dictionary<string, string>
            {
                ["latitude"] = "{{fieldA}}",
                ["longitude"] = "{{fieldB}}",
                ["current"] = "temperature_2m,relative_humidity_2m,apparent_temperature,wind_speed_10m,weather_code",
                ["daily"] = "temperature_2m_max,temperature_2m_min,precipitation_probability_max,weather_code",
                ["timezone"] = "auto",
                ["forecast_days"] = "1"
            },
            headers = new Dictionary<string, string> { ["Accept"] = "application/json" },
            timeoutSeconds = 15,
            responseType = "Json"
        };
        return new OperationDefinition
        {
            Code = code, Name = name, Description = description, OperationType = "RestApi",
            ImplementationType = "RestApi", ExecutionMode = "Server", OutputType = "Json",
            Category = "External Services", Icon = "cloud", SortOrder = sort,
            InputAConfigJson = JsonSerializer.Serialize(new { type = "Number", label = "Latitude", required = true, placeholder = "32.0853", min = -90, max = 90 }),
            InputBConfigJson = JsonSerializer.Serialize(new { type = "Number", label = "Longitude", required = true, placeholder = "34.7818", min = -180, max = 180 }),
            ConfigurationJson = JsonSerializer.Serialize(config)
        };
    }

    private static OperationDefinition CSharpSample()
    {
        const string source = "using System;\nusing System.Text.Json;\npublic static class CustomOperation\n{\n    public static object? Execute(string? fieldA, string? fieldB, JsonElement configuration)\n    {\n        var prefix = configuration.TryGetProperty(\"prefix\", out var value) ? value.GetString() ?? \"\" : \"\";\n        return $\"{prefix}{fieldA}{fieldB}\";\n    }\n}";
        return new OperationDefinition
        {
            Code = "custom.csharp-combine", Name = "Custom C# Combine", Description = "Example operation compiled by the separate Roslyn worker.",
            OperationType = "String", ImplementationType = "CSharp", ExecutionMode = "Server", OutputType = "String",
            Category = "Custom Code", Icon = "code", SortOrder = 170,
            InputAConfigJson = JsonSerializer.Serialize(new { type = "String", label = "First text", required = true }),
            InputBConfigJson = JsonSerializer.Serialize(new { type = "String", label = "Second text", required = true }),
            ConfigurationJson = JsonSerializer.Serialize(new { sourceCode = source, operationConfiguration = new { prefix = "[Custom] " } })
        };
    }

    private static OperationDefinition WeatherTemperatureCSharpSample()
    {
        const string source = "using System;\nusing System.Text.Json;\npublic static class CustomOperation\n{\n    public static object? Execute(string? fieldA, string? fieldB, JsonElement configuration)\n    {\n        using var document = JsonDocument.Parse(fieldA ?? \"{}\");\n        if (!document.RootElement.TryGetProperty(\"current\", out var current) || !current.TryGetProperty(\"temperature_2m\", out var temperature))\n            return \"Weather temperature unavailable\";\n        var label = configuration.TryGetProperty(\"label\", out var labelElement) ? labelElement.GetString() ?? \"Temperature\" : \"Temperature\";\n        return $\"{label}: {temperature.GetRawText()} C\";\n    }\n}";
        return new OperationDefinition
        {
            Code = "weather.temperature-csharp", Name = "Weather Temperature C#", Description = "Extracts current temperature from Weather Forecast JSON using custom C#.",
            OperationType = "RestApi", ImplementationType = "CSharp", ExecutionMode = "Server", OutputType = "String",
            Category = "Custom Code", Icon = "device_thermostat", SortOrder = 175,
            InputAConfigJson = JsonSerializer.Serialize(new { type = "String", label = "Weather JSON", required = true }),
            InputBConfigJson = JsonSerializer.Serialize(new { type = "String", label = "Unused", required = false }),
            ConfigurationJson = JsonSerializer.Serialize(new { sourceCode = source, operationConfiguration = new { label = "Temperature" } })
        };
    }

    private static OperationDefinition BuilderSample()
    {
        var config = new
        {
            nodes = new object[]
            {
                new { id = "lowerA", handlerKey = "string.lowercase", inputA = "$fieldA", inputB = "$fieldB", configuration = new Dictionary<string, object?>() },
                new { id = "trimA", handlerKey = "string.trim", inputA = "lowerA.output", inputB = "$fieldB", configuration = new Dictionary<string, object?>() },
                new { id = "lowerB", handlerKey = "string.lowercase", inputA = "$fieldB", inputB = "$fieldA", configuration = new Dictionary<string, object?>() },
                new { id = "trimB", handlerKey = "string.trim", inputA = "lowerB.output", inputB = "$fieldA", configuration = new Dictionary<string, object?>() },
                new { id = "compare", handlerKey = "string.equals", inputA = "trimA.output", inputB = "trimB.output", configuration = new { caseSensitive = true } }
            },
            @return = "compare.output"
        };
        return new OperationDefinition
        {
            Code = "builder.normalized-equals", Name = "Normalize And Compare", Description = "Builder example: lowercase, trim and compare two strings.",
            OperationType = "String", ImplementationType = "Builder", ExecutionMode = "Server", OutputType = "Boolean",
            Category = "Composite", Icon = "workflow", SortOrder = 180,
            InputAConfigJson = JsonSerializer.Serialize(new { type = "String", label = "First string", required = true }),
            InputBConfigJson = JsonSerializer.Serialize(new { type = "String", label = "Second string", required = true }),
            ConfigurationJson = JsonSerializer.Serialize(config)
        };
    }

    private static OperationDefinition WeatherTemperatureBuilderSample()
    {
        var config = new
        {
            nodes = new object[]
            {
                new { id = "weather", operationCode = "weather.forecast", inputA = "$fieldA", inputB = "$fieldB", configuration = new Dictionary<string, object?>() },
                new { id = "temperature", operationCode = "weather.temperature-csharp", inputA = "weather.output", inputB = "$fieldB", configuration = new Dictionary<string, object?>() },
                new { id = "format", operationCode = "string.uppercase", inputA = "temperature.output", inputB = "$fieldB", configuration = new { trimFirst = true } }
            },
            @return = "format.output"
        };
        return new OperationDefinition
        {
            Code = "builder.weather-temperature-summary", Name = "Weather Temperature Summary", Description = "Composite example: REST weather forecast, C# temperature extraction, and uppercase formatting.",
            OperationType = "RestApi", ImplementationType = "Builder", ExecutionMode = "Server", OutputType = "String",
            Category = "Composite", Icon = "ac_unit", SortOrder = 185,
            InputAConfigJson = JsonSerializer.Serialize(new { type = "Number", label = "Latitude", required = true, placeholder = "32.0853", min = -90, max = 90 }),
            InputBConfigJson = JsonSerializer.Serialize(new { type = "Number", label = "Longitude", required = true, placeholder = "34.7818", min = -180, max = 180 }),
            ConfigurationJson = JsonSerializer.Serialize(config)
        };
    }
}
