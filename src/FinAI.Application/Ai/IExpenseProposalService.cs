namespace FinAI.Application.Ai;

public interface IExpenseProposalService
{
    Task<AiResult<TransactionProposal>> ProposeAsync(ExpenseProposalRequest request, CancellationToken cancellationToken);
}
