namespace FinAI.Web.Models;

public sealed record DashboardViewModel(
    int Year,
    int Month,
    ApiMonthlySummary? Summary,
    IReadOnlyList<ApiTransaction> RecentTransactions,
    string? ErrorMessage);
