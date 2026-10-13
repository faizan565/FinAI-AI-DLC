using System.Security.Claims;
using FinAI.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
public sealed class TransactionsController(ITransactionService transactions) : ControllerBase
{
    private string? OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TransactionPeriod? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? category, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var filter = new TransactionFilter(period, from, to, category, search);
        var result = await transactions.ListAsync(ownerId, filter, page, pageSize, cancellationToken);
        return FromResult(result, value => Ok(value));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await transactions.GetAsync(ownerId, id, cancellationToken);
        return FromResult(result, value => Ok(value));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TransactionInput request, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await transactions.CreateAsync(ownerId, request, cancellationToken);
        return FromResult(result, created => CreatedAtAction(nameof(Get), new { id = created.Id }, created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TransactionInput request, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await transactions.UpdateAsync(ownerId, id, request, cancellationToken);
        return FromResult(result, value => Ok(value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var status = await transactions.DeleteAsync(ownerId, id, cancellationToken);
        return status == ServiceStatus.Success ? NoContent() : NotFound();
    }

    private IActionResult FromResult<T>(ServiceResult<T> result, Func<T, IActionResult> onSuccess) => result.Status switch
    {
        ServiceStatus.Success when result.Value is not null => onSuccess(result.Value),
        ServiceStatus.Invalid => ValidationProblem(new ValidationProblemDetails(
            new Dictionary<string, string[]>(result.Errors ?? new Dictionary<string, string[]>()))),
        _ => NotFound()
    };
}
