namespace FinAI.Application.Transactions;

public interface ITransactionService
{
    Task<ServiceResult<TransactionDto>> CreateAsync(string ownerId, TransactionInput input, CancellationToken cancellationToken);

    Task<ServiceResult<PagedResult<TransactionDto>>> ListAsync(string ownerId, TransactionFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    Task<ServiceResult<TransactionDto>> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    Task<ServiceResult<TransactionDto>> UpdateAsync(string ownerId, Guid id, TransactionInput input, CancellationToken cancellationToken);

    Task<ServiceStatus> DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    Task<ServiceResult<MonthlySummaryDto>> GetMonthlySummaryAsync(string ownerId, int year, int month, CancellationToken cancellationToken);
}
