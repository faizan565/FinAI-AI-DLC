using FinAI.Application.Transactions;

namespace FinAI.Application.Ai;

public enum SpendingIntent
{
    Total,
    Category,
    ByCategory,
    TopCategories,
    Unsupported
}

public enum SpendingPeriod
{
    CurrentMonth,
    PreviousMonth,
    SpecificMonth
}

public sealed record SpendingQuestionFilter(SpendingIntent Intent, string? Category, SpendingPeriod Period, int? Year, int? Month, int? Limit);

public sealed record RawSpendingQuestionFields(string? Intent, string? Category, string? Period, int? Year, int? Month, int? Limit);

public sealed record SpendingAnswer(SpendingIntent Intent, string? Category, DateOnly From, DateOnly ToInclusive, decimal Total, IReadOnlyList<CategoryTotalDto> Categories);
