using FinAI.Application.Transactions;
using FinAI.Domain.Entities;

namespace FinAI.Tests.Unit.Transactions;

internal sealed class FakeTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _items = new();

    public IReadOnlyList<Transaction> Items => _items;

    public Task<int> CountAsync(string ownerId, TransactionListQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Filter(ownerId, query).Count());

    public Task<IReadOnlyList<TransactionDto>> ListAsync(string ownerId, TransactionListQuery query, int skip, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransactionDto>>(
            Filter(ownerId, query).OrderByDescending(t => t.Date).ThenBy(t => t.Id).Skip(skip).Take(take).Select(ToDto).ToList());

    private IEnumerable<Transaction> Filter(string ownerId, TransactionListQuery query) =>
        _items.Where(t =>
            t.UserId == ownerId &&
            (query.FromInclusive is null || t.Date >= query.FromInclusive) &&
            (query.ToExclusive is null || t.Date < query.ToExclusive) &&
            (query.Category is null || t.Category == query.Category) &&
            (query.Search is null || t.Description.Contains(query.Search, StringComparison.OrdinalIgnoreCase)));

    public Task<TransactionDto?> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Where(t => t.UserId == ownerId && t.Id == id).Select(ToDto).SingleOrDefault());

    public Task<IReadOnlyList<CategoryTotalDto>> GetCategoryTotalsAsync(string ownerId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoryTotalDto>>(
            _items.Where(t => t.UserId == ownerId && t.Date >= from && t.Date < to)
                .GroupBy(t => t.Category)
                .Select(g => new CategoryTotalDto(g.Key, g.Sum(t => t.Amount)))
                .ToList());

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        _items.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(string ownerId, Guid id, TransactionInput input, CancellationToken cancellationToken)
    {
        var existing = _items.SingleOrDefault(t => t.UserId == ownerId && t.Id == id);
        if (existing is null)
        {
            return Task.FromResult(false);
        }

        existing.Amount = input.Amount;
        existing.Date = input.Date;
        existing.Description = input.Description;
        existing.Category = input.Category;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.RemoveAll(t => t.UserId == ownerId && t.Id == id) > 0);

    private static TransactionDto ToDto(Transaction t) =>
        new(t.Id, t.Amount, t.Date, t.Description, t.Category);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
