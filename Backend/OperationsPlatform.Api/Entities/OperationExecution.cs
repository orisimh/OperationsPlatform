namespace OperationsPlatform.Api.Entities;

public sealed class OperationExecution
{
    public long Id { get; set; }
    public int? OperationId { get; set; }
    public string OperationCodeSnapshot { get; set; } = string.Empty;
    public string OperationNameSnapshot { get; set; } = string.Empty;
    public string OperationTypeSnapshot { get; set; } = string.Empty;
    public string ImplementationTypeSnapshot { get; set; } = string.Empty;
    public int OperationVersionSnapshot { get; set; } = 1;
    public string InputA { get; set; } = string.Empty;
    public string InputB { get; set; } = string.Empty;
    public string? ResultJson { get; set; }
    public string Status { get; set; } = "Success";
    public string? ErrorMessage { get; set; }
    public long ExecutionDurationMs { get; set; }
    public string? ExecutionTraceJson { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
    public OperationDefinition? Operation { get; set; }
}
