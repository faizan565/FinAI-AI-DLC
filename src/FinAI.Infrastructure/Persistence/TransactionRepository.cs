using FinAI.Application.Transactions;
using FinAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Infrastructure.Persistence;

public sealed class TransactionRepository(FinAIDbContext dbContext) : ITransactionRepository
{
    public Task<int> CountAsync(string ownerId, TransactionListQuery query, CancellationToken cancellationToken) =>
        ApplyFilters(dbContext.Transactions.AsNoTracking(), ownerId, query)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<TransactionDto>> ListAsync(string ownerId, TransactionListQuery query, int skip, int take, CancellationToken cancellationToken) =>
        await ApplyFilters(dbContext.Transactions.AsNoTracking(), ownerId, query)
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(take)
            .Select(t => new TransactionDto(t.Id, t.Amount, t.Date, t.Description, t.Category))
            .ToListAsync(cancellationToken);

    private static IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> source, string ownerId, TransactionListQuery query)
    {
        var filtered = source.Where(t => t.UserId == ownerId);
        if (query.FromInclusive is { } from)
        {
            filtered = filtered.Where(t => t.Date >= from);
        }

        if (query.ToExclusive is { } to)
        {
            filtered = filtered.Where(t => t.Date < to);
        }

        if (query.Category is { } category)
        {
            filtered = filtered.Where(t => t.Category == category);
        }

        if (query.Search is { } search)
        {
            filtered = filtered.Where(t => t.Description.Contains(search));
        }

        return filtered;
    }

    public Task<TransactionDto?> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == ownerId && t.Id == id)
            .Select(t => new TransactionDto(t.Id, t.Amount, t.Date, t.Description, t.Category))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CategoryTotalDto>> GetCategoryTotalsAsync(string ownerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var totals = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == ownerId && t.Date >= from && t.Date < to)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);

        return totals
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Category)
            .Select(c => new CategoryTotalDto(c.Category, c.Total))
            .ToList();
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(string ownerId, Guid id, TransactionInput input, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .Where(t => t.UserId == ownerId && t.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.Amount, input.Amount)
                .SetProperty(t => t.Date, input.Date)
                .SetProperty(t => t.Description, input.Description)
                .SetProperty(t => t.Category, input.Category), cancellationToken);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .Where(t => t.UserId == ownerId && t.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return rows > 0;
    }
}
