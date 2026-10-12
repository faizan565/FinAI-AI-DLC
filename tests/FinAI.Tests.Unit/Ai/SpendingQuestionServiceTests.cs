using FinAI.Application.Ai;
using FinAI.Application.Transactions;
using FinAI.Domain.Entities;
using FinAI.Tests.Unit.Transactions;

namespace FinAI.Tests.Unit.Ai;

public sealed class SpendingQuestionServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 10, 15, 10, 0, 0, TimeSpan.Zero);
    private const string Owner = "owner-a";

    [Fact]
    public async Task AskAsync_Total_ForCurrentMonth_SumsOwnerTransactions()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 10m, new DateOnly(2025, 10, 1), "Food"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 5.50m, new DateOnly(2025, 10, 31), "Transport"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.Total, SpendingPeriod.CurrentMonth));

        var result = await service.AskAsync(Owner, "How much did I spend this month?", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(15.50m, result.Value!.Total);
        Assert.Equal(new DateOnly(2025, 10, 1), result.Value.From);
        Assert.Equal(new DateOnly(2025, 10, 31), result.Value.ToInclusive);
    }

    [Fact]
    public async Task AskAsync_PreviousMonth_ExcludesCurrentMonthAndHandlesYearBoundary()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 40m, new DateOnly(2025, 9, 30), "Food"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 99m, new DateOnly(2025, 10, 1), "Food"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.Total, SpendingPeriod.PreviousMonth));

        var result = await service.AskAsync(Owner, "last month", CancellationToken.None);

        Assert.Equal(40m, result.Value!.Total);
        Assert.Equal(new DateOnly(2025, 9, 1), result.Value.From);
        Assert.Equal(new DateOnly(2025, 9, 30), result.Value.ToInclusive);
    }

    [Fact]
    public async Task AskAsync_PreviousMonth_InJanuary_UsesDecemberOfPreviousYear()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 7m, new DateOnly(2024, 12, 31), "Food"), CancellationToken.None);
        var service = new SpendingQuestionService(
            new RecordingAi(Filter(SpendingIntent.Total, SpendingPeriod.PreviousMonth)),
            repo,
            new FixedTimeProvider(new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero)));

        var result = await service.AskAsync(Owner, "last month", CancellationToken.None);

        Assert.Equal(7m, result.Value!.Total);
        Assert.Equal(new DateOnly(2024, 12, 1), result.Value.From);
    }

    [Fact]
    public async Task AskAsync_ByCategory_ReturnsRankedTotals()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 10m, new DateOnly(2025, 10, 2), "Food"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 30m, new DateOnly(2025, 10, 3), "Housing"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 20m, new DateOnly(2025, 10, 4), "Transport"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.ByCategory, SpendingPeriod.CurrentMonth));

        var result = await service.AskAsync(Owner, "by category", CancellationToken.None);

        Assert.Equal(60m, result.Value!.Total);
        Assert.Equal(["Housing", "Transport", "Food"], result.Value.Categories.Select(c => c.Category));
    }

    [Fact]
    public async Task AskAsync_TopCategories_LimitsRanking()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 10m, new DateOnly(2025, 10, 2), "Food"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 30m, new DateOnly(2025, 10, 3), "Housing"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 20m, new DateOnly(2025, 10, 4), "Transport"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.TopCategories, SpendingPeriod.CurrentMonth, limit: 2));

        var result = await service.AskAsync(Owner, "top 2", CancellationToken.None);

        Assert.Equal(["Housing", "Transport"], result.Value!.Categories.Select(c => c.Category));
    }

    [Fact]
    public async Task AskAsync_TopCategories_TotalIsSumOfShownCategoriesOnly()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx(Owner, 10m, new DateOnly(2025, 10, 2), "Food"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 30m, new DateOnly(2025, 10, 3), "Housing"), CancellationToken.None);
        await repo.AddAsync(Tx(Owner, 20m, new DateOnly(2025, 10, 4), "Transport"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.TopCategories, SpendingPeriod.CurrentMonth, limit: 2));

        var result = await service.AskAsync(Owner, "top 2", CancellationToken.None);

        Assert.Equal(50m, result.Value!.Total);
    }

    [Fact]
    public async Task AskAsync_EmptyPeriod_ReturnsZeroTotal()
    {
        var service = Build(new FakeTransactionRepository(), Filter(SpendingIntent.Total, SpendingPeriod.CurrentMonth));

        var result = await service.AskAsync(Owner, "how much", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.Total);
        Assert.Empty(result.Value.Categories);
    }

    [Fact]
    public async Task AskAsync_OtherOwnersTransactions_AreExcluded()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(Tx("owner-b", 500m, new DateOnly(2025, 10, 2), "Food"), CancellationToken.None);
        var service = Build(repo, Filter(SpendingIntent.Total, SpendingPeriod.CurrentMonth));

        var result = await service.AskAsync(Owner, "how much", CancellationToken.None);

        Assert.Equal(0m, result.Value!.Total);
    }

    [Fact]
    public async Task AskAsync_EmptyQuestion_ReturnsInvalidInput_WithoutCallingAi()
    {
        var ai = new RecordingAi(Filter(SpendingIntent.Total, SpendingPeriod.CurrentMonth));
        var service = new SpendingQuestionService(ai, new FakeTransactionRepository(), new FixedTimeProvider(Now));

        var result = await service.AskAsync(Owner, "   ", CancellationToken.None);

        Assert.Equal(AiFailure.InvalidInput, result.Failure);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task AskAsync_QuestionOver300Characters_ReturnsTooLong_WithoutCallingAi()
    {
        var ai = new RecordingAi(Filter(SpendingIntent.Total, SpendingPeriod.CurrentMonth));
        var service = new SpendingQuestionService(ai, new FakeTransactionRepository(), new FixedTimeProvider(Now));

        var result = await service.AskAsync(Owner, new string('a', 301), CancellationToken.None);

        Assert.Equal(AiFailure.InvalidInput, result.Failure);
        Assert.Contains(new AiFieldError(AiField.Text, AiFieldErrorCode.TooLong), result.FieldErrors);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task AskAsync_AiUnavailable_ReturnsFailureWithoutAnswer()
    {
        var service = new SpendingQuestionService(
            new RecordingAi(AiResult<SpendingQuestionFilter>.Fail(AiFailure.Unavailable)),
            new FakeTransactionRepository(),
            new FixedTimeProvider(Now));

        var result = await service.AskAsync(Owner, "how much", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailure.Unavailable, result.Failure);
    }

    private static Transaction Tx(string owner, decimal amount, DateOnly date, string category) => new()
    {
        Id = Guid.NewGuid(),
        UserId = owner,
        Amount = amount,
        Date = date,
        Description = "x",
        Category = category
    };

    private static AiResult<SpendingQuestionFilter> Filter(SpendingIntent intent, SpendingPeriod period, string? category = null, int? limit = null) =>
        AiResult<SpendingQuestionFilter>.Success(new SpendingQuestionFilter(intent, category, period, null, null, limit));

    private static SpendingQuestionService Build(ITransactionRepository repo, AiResult<SpendingQuestionFilter> filter) =>
        new(new RecordingAi(filter), repo, new FixedTimeProvider(Now));

    private sealed class RecordingAi(AiResult<SpendingQuestionFilter> filter) : IAIService
    {
        public int Calls { get; private set; }

        public Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiResult<SpendingQuestionFilter>> ParseSpendingQuestionAsync(string question, DateOnly today, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(filter);
        }
    }
}
