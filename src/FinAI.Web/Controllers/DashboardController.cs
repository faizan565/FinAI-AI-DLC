using FinAI.Web.Models;
using FinAI.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinAI.Web.Controllers;

public sealed class DashboardController(FinAiApiClient api) : FinAiControllerBase(api)
{
    private const int RecentCount = 5;
    private const int MaxQuestionLength = 300;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return await RenderAsync(null, null, null, cancellationToken);
    }

    [HttpPost("ask")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string? question, CancellationToken cancellationToken)
    {
        var text = (question ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return await RenderAsync(text, null, "Enter a question about your spending.", cancellationToken);
        }

        if (text.Length > MaxQuestionLength)
        {
            return await RenderAsync(text, null, $"Questions must be {MaxQuestionLength} characters or fewer.", cancellationToken);
        }

        var answer = await Api.AskSpendingQuestionAsync(text, cancellationToken);
        if (SessionExpiredRedirect(answer.Error) is { } expired)
        {
            return expired;
        }

        return await RenderAsync(text, answer.Value, answer.Error?.Message, cancellationToken);
    }

    private async Task<IActionResult> RenderAsync(string? question, ApiSpendingAnswer? answer, string? questionError, CancellationToken cancellationToken)
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

        var insight = await Api.GetMonthlyInsightAsync(now.Year, now.Month, cancellationToken);
        if (SessionExpiredRedirect(insight.Error) is { } expiredInsight)
        {
            return expiredInsight;
        }

        var errorMessage = summary.Error?.Message ?? recent.Error?.Message;
        var model = new DashboardViewModel(
            now.Year,
            now.Month,
            summary.Value,
            recent.Value?.Items ?? Array.Empty<ApiTransaction>(),
            errorMessage,
            question,
            answer,
            questionError,
            insight.Value,
            insight.Error?.Message ?? (insight.Value is null ? "Monthly insight is unavailable right now." : null));

        return View("Index", model);
    }
}

