namespace FinAI.Application.Ai;

public interface IAIService
{
    Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken);
}
