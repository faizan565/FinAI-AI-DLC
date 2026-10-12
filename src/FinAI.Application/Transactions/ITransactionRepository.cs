using FinAI.Domain.Entities;

namespace FinAI.Application.Transactions;

public interface ITransactionRepository
{
    Task<int> CountAsync(string ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionDto>> ListAsync(string ownerId, int skip, int take, CancellationToken cancellationToken);

    Task<TransactionDto?> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CategoryTotalDto>> GetCategoryTotalsAsync(string ownerId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(string ownerId, Guid id, TransactionInput input, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken);
}
