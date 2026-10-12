using FinAI.Domain.Transactions;

namespace FinAI.Application.Ai;

public static class SpendingQuestionValidator
{
    public const int DefaultLimit = 3;
    public const int MaxLimit = 10;
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public static AiResult<SpendingQuestionFilter> Validate(RawSpendingQuestionFields fields)
    {
        if (!Enum.TryParse<SpendingIntent>(fields.Intent, ignoreCase: true, out var intent) || !Enum.IsDefined(intent))
        {
            return Unsupported();
        }

        if (intent == SpendingIntent.Unsupported)
        {
            return AiResult<SpendingQuestionFilter>.Success(new SpendingQuestionFilter(intent, null, SpendingPeriod.CurrentMonth, null, null, null));
        }

        if (!Enum.TryParse<SpendingPeriod>(fields.Period, ignoreCase: true, out var period) || !Enum.IsDefined(period))
        {
            return Unsupported();
        }

        string? category = null;
        if (intent == SpendingIntent.Category)
        {
            if (!TransactionCategories.TryNormalize(fields.Category, out var canonical))
            {
                return Unsupported();
            }

            category = canonical;
        }

        int? year = null;
        int? month = null;
        if (period == SpendingPeriod.SpecificMonth)
        {
            if (fields.Year is not { } fieldYear || fields.Month is not { } fieldMonth || fieldYear < MinYear || fieldYear > MaxYear || fieldMonth is < 1 or > 12)
            {
                return Unsupported();
            }

            year = fieldYear;
            month = fieldMonth;
        }

        int? limit = null;
        if (intent == SpendingIntent.TopCategories)
        {
            limit = fields.Limit ?? DefaultLimit;
            if (limit is < 1 or > MaxLimit)
            {
                return Unsupported();
            }
        }

        return AiResult<SpendingQuestionFilter>.Success(new SpendingQuestionFilter(intent, category, period, year, month, limit));
    }

    private static AiResult<SpendingQuestionFilter> Unsupported() => AiResult<SpendingQuestionFilter>.Fail(AiFailure.InvalidOutput);
}
