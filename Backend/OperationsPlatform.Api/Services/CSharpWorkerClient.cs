using System.Diagnostics;
using System.Text.Json;

namespace OperationsPlatform.Api.Services;

public sealed class CSharpWorkerClient(IConfiguration configuration)
{
    public async Task<WorkerExecutionResult> ExecuteAsync(string sourceCode, string? fieldA, string? fieldB, JsonElement operationConfiguration, bool validateOnly, CancellationToken cancellationToken)
    {
        var maxLength = configuration.GetValue<int>("CSharpWorker:MaxSourceCodeLength", 30000);
        if (string.IsNullOrWhiteSpace(sourceCode) || sourceCode.Length > maxLength)
            return WorkerExecutionResult.Failed($"C# source code must contain 1-{maxLength} characters.");

        var relativePath = configuration["CSharpWorker:RelativeDllPath"] ?? "csharp-worker/OperationsPlatform.CSharpWorker.dll";
        var workerPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        if (!File.Exists(workerPath))
            return WorkerExecutionResult.Failed($"C# worker was not found at '{workerPath}'. Build the full solution so the worker is copied beside the API.");

        var timeoutSeconds = configuration.GetValue<int>("CSharpWorker:ExecutionTimeoutSeconds", 8);
        var request = new WorkerRequest(sourceCode, fieldA, fieldB, operationConfiguration, validateOnly);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(workerPath)!,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add(workerPath);
        try
        {
            if (!process.Start()) return WorkerExecutionResult.Failed("Could not start the C# worker process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));
            process.StandardInput.Close();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* process may already have exited */ }
                if (cancellationToken.IsCancellationRequested) throw;
                return WorkerExecutionResult.Failed($"C# execution exceeded the {timeoutSeconds}-second limit.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                return WorkerExecutionResult.Failed(string.IsNullOrWhiteSpace(stderr) ? "C# worker failed." : Truncate(stderr, 3000));

            var response = JsonSerializer.Deserialize<WorkerResponse>(stdout);
            if (response is null) return WorkerExecutionResult.Failed("C# worker returned an invalid response.");
            return response.Success
                ? new WorkerExecutionResult(true, response.ResultJson, response.Diagnostics, null)
                : new WorkerExecutionResult(false, null, response.Diagnostics, response.ErrorMessage);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return WorkerExecutionResult.Failed($"C# worker invocation failed: {ex.Message}");
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed record WorkerRequest(string SourceCode, string? FieldA, string? FieldB, JsonElement Configuration, bool ValidateOnly);
    private sealed record WorkerResponse(bool Success, string? ResultJson, string[] Diagnostics, string? ErrorMessage);
}

public sealed record WorkerExecutionResult(bool Success, string? ResultJson, IReadOnlyList<string> Diagnostics, string? ErrorMessage)
{
    public static WorkerExecutionResult Failed(string message) => new(false, null, Array.Empty<string>(), message);
}
