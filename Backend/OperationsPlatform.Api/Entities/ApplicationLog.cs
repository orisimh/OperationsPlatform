namespace OperationsPlatform.Api.Entities;

public sealed class ApplicationLog
{
    public long Id { get; set; }
    public string Level { get; set; } = "Information";
    public string Source { get; set; } = "Application";
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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
