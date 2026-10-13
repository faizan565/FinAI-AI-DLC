namespace FinAI.Application.Ai;

public sealed record MonthlyCategoryShare(string Category, decimal Total, decimal Percentage);

public sealed record MonthlyInsightFacts(
    int Year,
    int Month,
    decimal Total,
    IReadOnlyList<MonthlyCategoryShare> Categories,
    decimal? PreviousMonthTotal,
    decimal? ChangeAmount,
    decimal? ChangePercent);
