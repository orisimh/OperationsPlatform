using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OperationsPlatform.Api.Data;
using OperationsPlatform.Api.Models;
using OperationsPlatform.Api.Services;
using System.Text.Json;

namespace OperationsPlatform.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController(
    OperationDefinitionService definitions,
    OperationEngine engine,
    CSharpWorkerClient csharpWorker,
    BuiltInHandlerCatalog handlers,
    OperationsDbContext db) : ControllerBase
{
    [HttpGet("/api/handlers")]
    public async Task<ActionResult<IReadOnlyList<HandlerDescriptorDto>>> GetHandlers(CancellationToken ct)
    {
        var namesByCode = await db.Operations
            .AsNoTracking()
            .Where(operation => operation.IsActive)
            .ToDictionaryAsync(operation => operation.Code, operation => operation.Name, StringComparer.OrdinalIgnoreCase, ct);

        return Ok(handlers.GetDescriptors(namesByCode));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OperationDto>>> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await definitions.GetAllAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OperationDto>> GetById(int id, CancellationToken ct)
    {
        var operation = await definitions.GetByIdAsync(id, ct);
        return operation is null ? NotFound() : Ok(operation);
    }

    [HttpPost]
    public async Task<ActionResult<OperationDto>> Create([FromBody] UpsertOperationRequest request, CancellationToken ct)
    {
        var operation = await definitions.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = operation.Id }, operation);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OperationDto>> Update(int id, [FromBody] UpsertOperationRequest request, CancellationToken ct)
    {
        var operation = await definitions.UpdateAsync(id, request, ct);
        return operation is null ? NotFound() : Ok(operation);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await definitions.SoftDeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:int}/execute")]
    public async Task<ActionResult<OperationExecutionResponse>> Execute(int id, [FromBody] ExecuteOperationRequest request, CancellationToken ct)
    {
        var result = await engine.ExecuteAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPost("validate-csharp")]
    public async Task<ActionResult<ValidateCSharpResponse>> ValidateCSharp([FromBody] ValidateCSharpRequest request, CancellationToken ct)
    {
        using var config = JsonDocument.Parse("{}");
        var result = await csharpWorker.ExecuteAsync(request.SourceCode, "sample-a", "sample-b", config.RootElement, true, ct);
        return Ok(new ValidateCSharpResponse
        {
            Success = result.Success,
            Message = result.Success ? "Compilation succeeded." : result.ErrorMessage,
            Diagnostics = result.Diagnostics
        });
    }
}
