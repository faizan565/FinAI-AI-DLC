namespace FinAI.Application.Ai;

public interface IAIService
{
    Task<AiResult<TransactionProposal>> ExtractTransactionAsync(string text, DateOnly today, CancellationToken cancellationToken);

    Task<AiResult<SpendingQuestionFilter>> ParseSpendingQuestionAsync(string question, DateOnly today, CancellationToken cancellationToken);

    Task<AiResult<string>> GenerateInsightWordingAsync(MonthlyInsightFacts facts, CancellationToken cancellationToken);
}
