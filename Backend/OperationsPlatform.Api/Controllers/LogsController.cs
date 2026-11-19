using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Models;

namespace OperationsPlatform.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController(OperationsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ApplicationLogDto>>> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] string? level = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        var query = db.ApplicationLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(x => x.Level == level);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ApplicationLogDto
            {
                Id = x.Id, Level = x.Level, Source = x.Source, Message = x.Message,
                CorrelationId = x.CorrelationId, RequestMethod = x.RequestMethod,
                RequestPath = x.RequestPath, StatusCode = x.StatusCode,
                RequestHeadersJson = x.RequestHeadersJson, ResponseHeadersJson = x.ResponseHeadersJson,
                RequestBody = x.RequestBody, ResponseBody = x.ResponseBody,
                Exception = x.Exception, CreatedAtUtc = x.CreatedAtUtc
            }).ToListAsync(ct);
        return Ok(new PagedResult<ApplicationLogDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = count });
    }
}
