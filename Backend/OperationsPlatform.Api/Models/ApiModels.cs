namespace OperationsPlatform.Api.Models;

public sealed class OperationDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string ImplementationType { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = "Server";
    public string InputAConfigJson { get; set; } = "{}";
    public string InputBConfigJson { get; set; } = "{}";
    public string OutputType { get; set; } = "String";
    public string ConfigurationJson { get; set; } = "{}";
    public string Category { get; set; } = "General";
    public string Icon { get; set; } = "bolt";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class UpsertOperationRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OperationType { get; set; } = "String";
    public string ImplementationType { get; set; } = "BuiltIn";
    public string ExecutionMode { get; set; } = "Server";
    public string InputAConfigJson { get; set; } = "{}";
    public string InputBConfigJson { get; set; } = "{}";
    public string OutputType { get; set; } = "String";
    public string ConfigurationJson { get; set; } = "{}";
    public string Category { get; set; } = "General";
    public string Icon { get; set; } = "bolt";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class ExecuteOperationRequest
{
    public string? FieldA { get; set; }
    public string? FieldB { get; set; }
}

public sealed class OperationExecutionResponse
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public long ExecutionDurationMs { get; set; }
    public long ExecutionId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public List<HistoryItemDto> LastThreeExecutions { get; set; } = new();
    public int MonthlyExecutionCount { get; set; }
    public object? ExecutionTrace { get; set; }
}

public sealed class HistoryItemDto
{
    public long Id { get; set; }
    public int? OperationId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public int OperationVersion { get; set; }
    public string InputA { get; set; } = string.Empty;
    public string InputB { get; set; } = string.Empty;
    public string? ResultJson { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long ExecutionDurationMs { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class ApplicationLogDto
{
    public long Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
    public string? RequestHeadersJson { get; set; }
    public string? ResponseHeadersJson { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? Exception { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class DashboardDto
{
    public int ActiveOperations { get; set; }
    public int ExecutionsThisMonth { get; set; }
    public int FailedExecutionsThisMonth { get; set; }
    public double SuccessRate { get; set; }
    public List<HistoryItemDto> RecentExecutions { get; set; } = new();
    public List<OperationUsageDto> MostUsedOperations { get; set; } = new();
}

public sealed class OperationUsageDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ValidateCSharpRequest
{
    public string SourceCode { get; set; } = string.Empty;
}

public sealed class ValidateCSharpResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public IReadOnlyList<string> Diagnostics { get; set; } = Array.Empty<string>();
}
