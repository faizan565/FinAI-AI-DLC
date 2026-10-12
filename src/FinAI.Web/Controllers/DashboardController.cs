using FinAI.Web.Models;
using FinAI.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Web.Controllers;

public sealed class DashboardController(FinAiApiClient api) : FinAiControllerBase(api)
{
    private const int RecentCount = 5;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        ViewData["Title"] = "Dashboard";

        var summary = await Api.GetMonthlySummaryAsync(now.Year, now.Month, cancellationToken);
        if (SessionExpiredRedirect(summary.Error) is { } expired)
        {
            return expired;
        }

        var recent = await Api.ListTransactionsAsync(1, RecentCount, cancellationToken);
        if (SessionExpiredRedirect(recent.Error) is { } expiredRecent)
        {
            return expiredRecent;
        }

        var errorMessage = summary.Error?.Message ?? recent.Error?.Message;
        var model = new DashboardViewModel(
            now.Year,
            now.Month,
            summary.Value,
            recent.Value?.Items ?? Array.Empty<ApiTransaction>(),
            errorMessage);

        return View(model);
    }
}
