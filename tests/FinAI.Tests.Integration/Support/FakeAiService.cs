using FinAI.Application.Ai;

namespace FinAI.Tests.Integration.Support;

public sealed class FakeAiService : IAIService
{
    public AiResult<TransactionProposal> Result { get; set; } = AiResult<TransactionProposal>.Fail(AiFailure.Unavailable);

    public AiResult<SpendingQuestionFilter> SpendingFilter { get; set; } = AiResult<SpendingQuestionFilter>.Fail(AiFailure.Unavailable);

    public AiResult<string> InsightWording { get; set; } = AiResult<string>.Fail(AiFailure.Unavailable);

    public Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult(Result);

    public Task<AiResult<SpendingQuestionFilter>> ParseSpendingQuestionAsync(string question, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult(SpendingFilter);

    public Task<AiResult<string>> GenerateInsightWordingAsync(MonthlyInsightFacts facts, CancellationToken cancellationToken) =>
        Task.FromResult(InsightWording);
}
