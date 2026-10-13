using FinAI.Application.Ai;

namespace FinAI.Tests.Unit.Ai;

public sealed class ExpenseProposalServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProposeAsync_EmptyText_ReturnsInvalidInput_WithoutCallingAi(string? text)
    {
        var ai = new RecordingAiService();
        var service = new ExpenseProposalService(ai, new FixedTimeProvider(Now));

        var result = await service.ProposeAsync(new ExpenseProposalRequest(text), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AiFailure.InvalidInput, result.Failure);
        Assert.Contains(new AiFieldError(AiField.Text, AiFieldErrorCode.Missing), result.FieldErrors);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task ProposeAsync_TextOver500Characters_ReturnsTooLong_WithoutCallingAi()
    {
        var ai = new RecordingAiService();
        var service = new ExpenseProposalService(ai, new FixedTimeProvider(Now));

        var result = await service.ProposeAsync(new ExpenseProposalRequest(new string('a', 501)), CancellationToken.None);

        Assert.Equal(AiFailure.InvalidInput, result.Failure);
        Assert.Contains(new AiFieldError(AiField.Text, AiFieldErrorCode.TooLong), result.FieldErrors);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task ProposeAsync_ValidText_TrimsTextAndPassesTodayToAi()
    {
        var proposal = new TransactionProposal(2500m, new DateOnly(2025, 6, 15), "Lunch", "Food", false);
        var ai = new RecordingAiService { Result = AiResult<TransactionProposal>.Success(proposal) };
        var service = new ExpenseProposalService(ai, new FixedTimeProvider(Now));

        var result = await service.ProposeAsync(new ExpenseProposalRequest("  Lunch 2500  "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(proposal, result.Value);
        Assert.Equal("Lunch 2500", ai.LastText);
        Assert.Equal(new DateOnly(2025, 6, 15), ai.LastToday);
    }

    private sealed class RecordingAiService : IAIService
    {
        public int Calls { get; private set; }

        public string? LastText { get; private set; }

        public DateOnly LastToday { get; private set; }

        public AiResult<TransactionProposal> Result { get; init; } = AiResult<TransactionProposal>.Fail(AiFailure.Unavailable);

        public Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken)
        {
            Calls++;
            LastText = text;
            LastToday = today;
            return Task.FromResult(Result);
        }

        public Task<AiResult<SpendingQuestionFilter>> ParseSpendingQuestionAsync(string question, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(AiResult<SpendingQuestionFilter>.Fail(AiFailure.Unavailable));

        public Task<AiResult<string>> GenerateInsightWordingAsync(MonthlyInsightFacts facts, CancellationToken cancellationToken) =>
            Task.FromResult(AiResult<string>.Fail(AiFailure.Unavailable));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
