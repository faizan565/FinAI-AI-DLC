namespace FinAI.Web.Models;

public sealed record DashboardViewModel(
    int Year,
    int Month,
    ApiMonthlySummary? Summary,
    IReadOnlyList<ApiTransaction> RecentTransactions,
    string? ErrorMessage,
    string? Question = null,
    ApiSpendingAnswer? Answer = null,
    string? QuestionError = null,
    ApiMonthlyInsight? Insight = null,
    string? InsightError = null);
