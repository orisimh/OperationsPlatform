using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Services;

public sealed class HistoryService(OperationsDbContext db)
{
    public async Task<PagedResult<HistoryItemDto>> GetHistoryAsync(int page, int pageSize, int? operationId, string? status, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var query = db.OperationExecutions.AsNoTracking();
        if (operationId.HasValue) query = query.Where(x => x.OperationId == operationId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.ExecutedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new HistoryItemDto
            {
                Id = x.Id, OperationId = x.OperationId, OperationName = x.OperationNameSnapshot,
                OperationCode = x.OperationCodeSnapshot, OperationType = x.OperationTypeSnapshot, OperationVersion = x.OperationVersionSnapshot,
                InputA = x.InputA, InputB = x.InputB, ResultJson = x.ResultJson,
                Status = x.Status, ErrorMessage = x.ErrorMessage,
                ExecutionDurationMs = x.ExecutionDurationMs, ExecutedAtUtc = x.ExecutedAtUtc
            }).ToListAsync(ct);
        return new PagedResult<HistoryItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
