using FinAI.Application.Ai;
using FinAI.Application.Transactions;

namespace FinAI.Application.Insights;

public interface IMonthlyInsightService
{
    Task<ServiceResult<MonthlyInsightDto>> GetAsync(string ownerId, int year, int month, CancellationToken cancellationToken);
}

public sealed record MonthlyInsightDto(
    int Year,
    int Month,
    decimal Total,
    bool HasData,
    IReadOnlyList<MonthlyCategoryShare> Categories,
    decimal? PreviousMonthTotal,
    decimal? ChangeAmount,
    decimal? ChangePercent,
    string Insight,
    bool GeneratedByAi);

public sealed class MonthlyInsightService(ITransactionService transactions, IAIService aiService) : IMonthlyInsightService
{
    private const int PercentDecimals = 1;

    public async Task<ServiceResult<MonthlyInsightDto>> GetAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        var current = await transactions.GetMonthlySummaryAsync(ownerId, year, month, cancellationToken);
        if (current.Status != ServiceStatus.Success || current.Value is not { } summary)
        {
            return ServiceResult<MonthlyInsightDto>.Invalid(current.Errors ?? new Dictionary<string, string[]>());
        }

        var previousYear = month == 1 ? year - 1 : year;
        var previousMonth = month == 1 ? 12 : month - 1;
        var previous = await transactions.GetMonthlySummaryAsync(ownerId, previousYear, previousMonth, cancellationToken);
        var previousTotal = previous.Status == ServiceStatus.Success ? previous.Value?.Total : null;

        if (summary.Categories.Count == 0)
        {
            return ServiceResult<MonthlyInsightDto>.Success(new MonthlyInsightDto(
                year, month, 0m, false, Array.Empty<MonthlyCategoryShare>(), previousTotal, null, null,
                MonthlyInsightTemplate.NoData(year, month), false));
        }

        var total = summary.Total;
        var categories = summary.Categories
            .Select(c => new MonthlyCategoryShare(c.Category, c.Total, Percent(c.Total, total)))
            .ToList();
        decimal? changeAmount = previousTotal is { } prior ? total - prior : null;
        decimal? changePercent = previousTotal is > 0m and var priorPositive ? Percent(total - priorPositive, priorPositive) : null;

        var facts = new MonthlyInsightFacts(year, month, total, categories, previousTotal, changeAmount, changePercent);
        var wording = await aiService.GenerateInsightWordingAsync(facts, cancellationToken);
        var aiText = wording.IsSuccess ? InsightWordingValidator.Validate(wording.Value) : null;
        var generatedText = aiText is { IsSuccess: true, Value: { } text } ? text : null;

        return ServiceResult<MonthlyInsightDto>.Success(new MonthlyInsightDto(
            year,
            month,
            total,
            true,
            categories,
            previousTotal,
            changeAmount,
            changePercent,
            generatedText ?? MonthlyInsightTemplate.Render(facts),
            generatedText is not null));
    }

    private static decimal Percent(decimal part, decimal whole) =>
        decimal.Round(part * 100m / whole, PercentDecimals, MidpointRounding.AwayFromZero);
}
