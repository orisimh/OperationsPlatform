using Microsoft.AspNetCore.Mvc;
using OperationsPlatform.Api.Models;
using OperationsPlatform.Api.Services;

namespace OperationsPlatform.Api.Controllers;

[ApiController]
[Route("api/history")]
public sealed class HistoryController(HistoryService history) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<HistoryItemDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int? operationId = null, [FromQuery] string? status = null, CancellationToken ct = default)
        => Ok(await history.GetHistoryAsync(page, pageSize, operationId, status, ct));
}
