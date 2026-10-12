using FinAI.Application.Ai;

namespace FinAI.Tests.Integration.Support;

public sealed class FakeAiService : IAIService
{
    public AiResult<TransactionProposal> Result { get; set; } = AiResult<TransactionProposal>.Fail(AiFailure.Unavailable);

    public Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult(Result);
}
