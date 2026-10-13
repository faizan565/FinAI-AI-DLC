using System.Security.Claims;
using FinAI.Application.Insights;
using FinAI.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(ITransactionService transactions, IMonthlyInsightService insights) : ControllerBase
{
    private string? OwnerId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet("monthly-insights")]
    public async Task<IActionResult> MonthlyInsights([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await insights.GetAsync(ownerId, year, month, cancellationToken);
        return result.Status switch
        {
            ServiceStatus.Success when result.Value is not null => Ok(result.Value),
            ServiceStatus.Invalid => ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>(result.Errors ?? new Dictionary<string, string[]>()))),
            _ => NotFound()
        };
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        if (OwnerId is not { } ownerId)
        {
            return Unauthorized();
        }

        var result = await transactions.GetMonthlySummaryAsync(ownerId, year, month, cancellationToken);
        return result.Status switch
        {
            ServiceStatus.Success when result.Value is not null => Ok(result.Value),
            ServiceStatus.Invalid => ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>(result.Errors ?? new Dictionary<string, string[]>()))),
            _ => NotFound()
        };
    }
}
