using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// A34D - custom C# code is compiled and executed in a separate worker process.
var input = await Console.In.ReadToEndAsync();
try
{
    var request = JsonSerializer.Deserialize<WorkerRequest>(input, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Invalid worker request.");
    var response = CompileAndRun(request);
    Console.Out.Write(JsonSerializer.Serialize(response));
}
catch (Exception ex)
{
    Console.Out.Write(JsonSerializer.Serialize(new WorkerResponse(false, null, Array.Empty<string>(), ex.Message)));
}

static WorkerResponse CompileAndRun(WorkerRequest request)
{
    var syntaxTree = CSharpSyntaxTree.ParseText(request.SourceCode, new CSharpParseOptions(LanguageVersion.CSharp12));
    var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
    var references = trustedAssemblies
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => MetadataReference.CreateFromFile(path));

    var compilation = CSharpCompilation.Create(
        $"DynamicOperation_{Guid.NewGuid():N}",
        new[] { syntaxTree },
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

    using var assemblyStream = new MemoryStream();
    var emit = compilation.Emit(assemblyStream);
    var diagnostics = emit.Diagnostics
        .Where(d => d.Severity == DiagnosticSeverity.Error)
        .Select(d => d.ToString())
        .Take(30)
        .ToArray();

    if (!emit.Success)
        return new WorkerResponse(false, null, diagnostics, "C# compilation failed. Review the diagnostics.");

    assemblyStream.Position = 0;
    var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
    var type = assembly.GetType("CustomOperation", throwOnError: true)!;
    var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("The source must define public static CustomOperation.Execute(string? fieldA, string? fieldB, JsonElement configuration).");

    var parameters = method.GetParameters();
    if (parameters.Length != 3 || parameters[0].ParameterType != typeof(string) ||
        parameters[1].ParameterType != typeof(string) || parameters[2].ParameterType != typeof(JsonElement))
        throw new InvalidOperationException("Execute must have the exact signature: public static object? Execute(string? fieldA, string? fieldB, JsonElement configuration).");

    if (request.ValidateOnly)
        return new WorkerResponse(true, null, Array.Empty<string>(), null);

    var protocolOutput = Console.Out;
    object? result;
    try
    {
        Console.SetOut(TextWriter.Null);
        result = method.Invoke(null, new object?[] { request.FieldA, request.FieldB, request.Configuration });
    }
    finally
    {
        Console.SetOut(protocolOutput);
    }
    var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    return new WorkerResponse(true, resultJson, Array.Empty<string>(), null);
}

internal sealed record WorkerRequest(string SourceCode, string? FieldA, string? FieldB, JsonElement Configuration, bool ValidateOnly);
internal sealed record WorkerResponse(bool Success, string? ResultJson, string[] Diagnostics, string? ErrorMessage);
