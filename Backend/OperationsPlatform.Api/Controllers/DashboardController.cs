using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(OperationsDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
    {
        var startUtc = GetMonthStartUtc(configuration["Execution:TimeZoneId"] ?? "Israel Standard Time");
        var executions = db.OperationExecutions.AsNoTracking().Where(x => x.ExecutedAtUtc >= startUtc);
        var total = await executions.CountAsync(ct);
        var failed = await executions.CountAsync(x => x.Status == "Failed", ct);
        var recent = await db.OperationExecutions.AsNoTracking().OrderByDescending(x => x.ExecutedAtUtc).Take(8)
            .Select(x => new HistoryItemDto
            {
                Id = x.Id, OperationId = x.OperationId, OperationName = x.OperationNameSnapshot,
                OperationCode = x.OperationCodeSnapshot, OperationType = x.OperationTypeSnapshot, OperationVersion = x.OperationVersionSnapshot,
                InputA = x.InputA, InputB = x.InputB, ResultJson = x.ResultJson,
                Status = x.Status, ErrorMessage = x.ErrorMessage,
                ExecutionDurationMs = x.ExecutionDurationMs, ExecutedAtUtc = x.ExecutedAtUtc
            }).ToListAsync(ct);
        var mostUsed = await executions.GroupBy(x => x.OperationNameSnapshot)
            .Select(g => new OperationUsageDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(5).ToListAsync(ct);
        return Ok(new DashboardDto
        {
            ActiveOperations = await db.Operations.CountAsync(x => x.IsActive, ct),
            ExecutionsThisMonth = total, FailedExecutionsThisMonth = failed,
            SuccessRate = total == 0 ? 100 : Math.Round((double)(total - failed) * 100 / total, 1),
            RecentExecutions = recent, MostUsedOperations = mostUsed
        });
    }

    private static DateTime GetMonthStartUtc(string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
            return TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1), zone);
        }
        catch (TimeZoneNotFoundException)
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
