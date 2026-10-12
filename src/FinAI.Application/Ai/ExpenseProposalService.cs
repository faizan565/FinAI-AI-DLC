namespace FinAI.Application.Ai;

public sealed class ExpenseProposalService(IAIService aiService, TimeProvider timeProvider) : IExpenseProposalService
{
    public const int MaxTextLength = 500;

    public Task<AiResult<TransactionProposal>> ProposeAsync(ExpenseProposalRequest request, CancellationToken cancellationToken)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Task.FromResult(AiResult<TransactionProposal>.Invalid(
                AiFailure.InvalidInput,
                [new AiFieldError(AiField.Text, AiFieldErrorCode.Missing)]));
        }

        if (text.Length > MaxTextLength)
        {
            return Task.FromResult(AiResult<TransactionProposal>.Invalid(
                AiFailure.InvalidInput,
                [new AiFieldError(AiField.Text, AiFieldErrorCode.TooLong)]));
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return aiService.ExtractTransactionAsync(text, today, cancellationToken);
    }
}
