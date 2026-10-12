using FinAI.Application.Transactions;

namespace FinAI.Application.Ai;

public interface ISpendingQuestionService
{
    Task<AiResult<SpendingAnswer>> AskAsync(string ownerId, string? text, CancellationToken cancellationToken);
}

public sealed class SpendingQuestionService(IAIService aiService, ITransactionRepository repository, TimeProvider timeProvider) : ISpendingQuestionService
{
    public const int MaxTextLength = 300;

    public async Task<AiResult<SpendingAnswer>> AskAsync(string ownerId, string? text, CancellationToken cancellationToken)
    {
        var question = (text ?? string.Empty).Trim();
        if (question.Length == 0)
        {
            return AiResult<SpendingAnswer>.Invalid(AiFailure.InvalidInput, [new AiFieldError(AiField.Text, AiFieldErrorCode.Missing)]);
        }

        if (question.Length > MaxTextLength)
        {
            return AiResult<SpendingAnswer>.Invalid(AiFailure.InvalidInput, [new AiFieldError(AiField.Text, AiFieldErrorCode.TooLong)]);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var parsed = await aiService.ParseSpendingQuestionAsync(question, today, cancellationToken);
        if (!parsed.IsSuccess || parsed.Value is not { } filter)
        {
            return AiResult<SpendingAnswer>.Fail(parsed.Failure ?? AiFailure.InvalidOutput);
        }

        if (filter.Intent == SpendingIntent.Unsupported)
        {
            return AiResult<SpendingAnswer>.Success(new SpendingAnswer(SpendingIntent.Unsupported, null, today, today, 0m, Array.Empty<CategoryTotalDto>()));
        }

        var (from, toExclusive) = ResolvePeriod(filter, today);
        var categories = (await repository.GetCategoryTotalsAsync(ownerId, from, toExclusive, cancellationToken))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category)
            .ToList();

        IReadOnlyList<CategoryTotalDto> shown = filter.Intent switch
        {
            SpendingIntent.Category => categories.Where(c => c.Category == filter.Category).ToList(),
            SpendingIntent.ByCategory => categories,
            SpendingIntent.TopCategories => categories.Take(filter.Limit ?? SpendingQuestionValidator.DefaultLimit).ToList(),
            _ => Array.Empty<CategoryTotalDto>()
        };

        var total = filter.Intent is SpendingIntent.Category or SpendingIntent.TopCategories
            ? shown.Sum(c => c.Total)
            : categories.Sum(c => c.Total);

        return AiResult<SpendingAnswer>.Success(new SpendingAnswer(filter.Intent, filter.Category, from, toExclusive.AddDays(-1), total, shown));
    }

    private static (DateOnly From, DateOnly ToExclusive) ResolvePeriod(SpendingQuestionFilter filter, DateOnly today) =>
        filter.Period switch
        {
            SpendingPeriod.PreviousMonth => MonthRange(new DateOnly(today.Year, today.Month, 1).AddMonths(-1)),
            SpendingPeriod.SpecificMonth => MonthRange(new DateOnly(filter.Year ?? today.Year, filter.Month ?? today.Month, 1)),
            _ => MonthRange(new DateOnly(today.Year, today.Month, 1))
        };

    private static (DateOnly From, DateOnly ToExclusive) MonthRange(DateOnly firstDay) => (firstDay, firstDay.AddMonths(1));
}
