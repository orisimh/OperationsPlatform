namespace OperationsPlatform.Api.Entities;

public sealed class OperationDefinition
{
    public int Id { get; set; }
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
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<OperationExecution> Executions { get; set; } = new List<OperationExecution>();
}
