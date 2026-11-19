using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Entities;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Services;

public sealed class OperationEngine(
    OperationsDbContext db,
    BuiltInHandlerCatalog handlers,
    GenericRestApiExecutor restExecutor,
    CSharpWorkerClient csharpWorker,
    BuilderOperationExecutor builderExecutor,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<OperationExecutionResponse> ExecuteAsync(int operationId, ExecuteOperationRequest request, CancellationToken ct)
    {
        var operation = await db.Operations.FirstOrDefaultAsync(x => x.Id == operationId && x.IsActive, ct)
            ?? throw new KeyNotFoundException("Operation was not found or is inactive.");
        var stopwatch = Stopwatch.StartNew();
        object? result = null;
        object? trace = null;
        string? error = null;
        var success = false;
        try
        {
            var maxLength = configuration.GetValue<int>("Execution:MaxInputLength", 10000);
            if ((request.FieldA?.Length ?? 0) > maxLength || (request.FieldB?.Length ?? 0) > maxLength)
                throw new ArgumentException($"Inputs cannot exceed {maxLength} characters.");
            ValidateInput(request.FieldA, operation.InputAConfigJson, "Field A");
            ValidateInput(request.FieldB, operation.InputBConfigJson, "Field B");

            using var configDoc = JsonDocument.Parse(operation.ConfigurationJson);
            var config = configDoc.RootElement;
            var implementation = operation.ImplementationType.ToLowerInvariant();
            switch (implementation)
            {
                case "builtin":
                {
                    var handlerKey = GetString(config, "handlerKey", string.Empty);
                    result = handlers.Execute(handlerKey, request.FieldA, request.FieldB, config);
                    break;
                }
                case "restapi":
                    result = await restExecutor.ExecuteAsync(config, request.FieldA, request.FieldB, ct);
                    break;
                case "csharp":
                {
                    var sourceCode = GetString(config, "sourceCode", string.Empty);
                    using var operationConfig = JsonDocument.Parse(config.TryGetProperty("operationConfiguration", out var oc) ? oc.GetRawText() : "{}");
                    var workerResult = await csharpWorker.ExecuteAsync(sourceCode, request.FieldA, request.FieldB, operationConfig.RootElement, false, ct);
                    if (!workerResult.Success) throw new InvalidOperationException(workerResult.ErrorMessage ?? string.Join("; ", workerResult.Diagnostics));
                    result = JsonSerializer.Deserialize<JsonElement>(workerResult.ResultJson ?? "null");
                    break;
                }
                case "builder":
                {
                    var builderResult = builderExecutor.Execute(config, request.FieldA, request.FieldB);
                    result = builderResult.Result;
                    trace = builderResult.Trace;
                    break;
                }
                default:
                    throw new InvalidOperationException($"Unsupported implementation type '{operation.ImplementationType}'.");
            }
            success = true;
        }
        catch (Exception ex)
        {
            error = ex is AggregateException ? ex.GetBaseException().Message : ex.Message;
            db.ApplicationLogs.Add(new ApplicationLog
            {
                Level = "Error", Source = "OperationEngine", Message = $"Operation '{operation.Code}' failed.",
                CorrelationId = httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString() ?? "operation-" + Guid.NewGuid().ToString("N"), Exception = ex.ToString(),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        stopwatch.Stop();

        var execution = new OperationExecution
        {
            OperationId = operation.Id, OperationCodeSnapshot = operation.Code,
            OperationNameSnapshot = operation.Name, OperationTypeSnapshot = operation.OperationType,
            ImplementationTypeSnapshot = operation.ImplementationType, OperationVersionSnapshot = operation.Version,
            InputA = request.FieldA ?? string.Empty, InputB = request.FieldB ?? string.Empty,
            ResultJson = success ? JsonSerializer.Serialize(result) : null,
            Status = success ? "Success" : "Failed", ErrorMessage = error,
            ExecutionDurationMs = stopwatch.ElapsedMilliseconds,
            ExecutionTraceJson = trace is null ? null : JsonSerializer.Serialize(trace), ExecutedAtUtc = DateTime.UtcNow
        };
        db.OperationExecutions.Add(execution);
        await db.SaveChangesAsync(ct);

        var lastThree = await db.OperationExecutions.AsNoTracking()
            .Where(x => x.OperationId == operation.Id)
            .OrderByDescending(x => x.ExecutedAtUtc).Take(3)
            .Select(x => new HistoryItemDto
            {
                Id = x.Id, OperationId = x.OperationId, OperationName = x.OperationNameSnapshot,
                OperationCode = x.OperationCodeSnapshot, OperationType = x.OperationTypeSnapshot, OperationVersion = x.OperationVersionSnapshot,
                InputA = x.InputA, InputB = x.InputB, ResultJson = x.ResultJson,
                Status = x.Status, ErrorMessage = x.ErrorMessage,
                ExecutionDurationMs = x.ExecutionDurationMs, ExecutedAtUtc = x.ExecutedAtUtc
            }).ToListAsync(ct);

        var monthStartUtc = GetMonthStartUtc(configuration["Execution:TimeZoneId"] ?? "Israel Standard Time");
        var monthlyCount = await db.OperationExecutions.CountAsync(x => x.OperationId == operation.Id && x.ExecutedAtUtc >= monthStartUtc, ct);

        return new OperationExecutionResponse
        {
            Success = success, Result = result, ErrorMessage = error,
            ExecutionDurationMs = stopwatch.ElapsedMilliseconds, ExecutionId = execution.Id,
            OperationName = operation.Name, LastThreeExecutions = lastThree,
            MonthlyExecutionCount = monthlyCount, ExecutionTrace = trace
        };
    }


    private static void ValidateInput(string? value, string inputConfigJson, string fieldName)
    {
        using var config = JsonDocument.Parse(inputConfigJson);
        var root = config.RootElement;
        var required = !root.TryGetProperty("required", out var requiredValue) || requiredValue.ValueKind != JsonValueKind.False;
        if (required && string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{fieldName} is required.");
        if (string.IsNullOrWhiteSpace(value)) return;
        var type = root.TryGetProperty("type", out var typeValue) ? typeValue.GetString() ?? "String" : "String";
        if (type.Equals("Integer", StringComparison.OrdinalIgnoreCase) && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException($"{fieldName} must be a valid integer.");
        if (type.Equals("Number", StringComparison.OrdinalIgnoreCase) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            throw new ArgumentException($"{fieldName} must be a valid number.");
        if (type.Equals("Date", StringComparison.OrdinalIgnoreCase) && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException($"{fieldName} must be a valid date.");
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numeric))
        {
            if (root.TryGetProperty("min", out var minValue) && minValue.TryGetDecimal(out var min) && numeric < min)
                throw new ArgumentException($"{fieldName} must be greater than or equal to {min}.");
            if (root.TryGetProperty("max", out var maxValue) && maxValue.TryGetDecimal(out var max) && numeric > max)
                throw new ArgumentException($"{fieldName} must be less than or equal to {max}.");
        }
    }

    private static DateTime GetMonthStartUtc(string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            var localStart = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        }
        catch (TimeZoneNotFoundException)
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    private static string GetString(JsonElement element, string name, string fallback) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback : fallback;
}
