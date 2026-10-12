using FinAI.Domain.Entities;
using FinAI.Domain.Entities;
using FinAI.Domain.Transactions;

namespace FinAI.Application.Transactions;

public sealed class TransactionService(ITransactionRepository repository, TimeProvider timeProvider) : ITransactionService
{
    public const int MaxPageSize = 100;
    public const int DescriptionMaxLength = 200;

    private const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public async Task<ServiceResult<TransactionDto>> CreateAsync(string ownerId, TransactionInput input, CancellationToken cancellationToken)
    {
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return ServiceResult<TransactionDto>.Invalid(errors);
        }

        var normalized = Normalize(input);
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            Amount = normalized.Amount,
            Date = normalized.Date,
            Description = normalized.Description,
            Category = normalized.Category
        };

        await repository.AddAsync(transaction, cancellationToken);
        return ServiceResult<TransactionDto>.Success(ToDto(transaction.Id, normalized));
    }

    public async Task<ServiceResult<PagedResult<TransactionDto>>> ListAsync(string ownerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            return ServiceResult<PagedResult<TransactionDto>>.Invalid(new Dictionary<string, string[]>
            {
                ["pageSize"] = new[] { $"Page size must be between 1 and {MaxPageSize}." }
            });
        }

        var skip = (long)(page - 1) * pageSize;
        if (skip > int.MaxValue)
        {
            return ServiceResult<PagedResult<TransactionDto>>.Invalid(new Dictionary<string, string[]>
            {
                ["page"] = new[] { "Page is out of range." }
            });
        }

        var totalCount = await repository.CountAsync(ownerId, cancellationToken);
        var items = await repository.ListAsync(ownerId, (int)skip, pageSize, cancellationToken);
        return ServiceResult<PagedResult<TransactionDto>>.Success(new PagedResult<TransactionDto>(items, page, pageSize, totalCount));
    }

    public async Task<ServiceResult<TransactionDto>> GetAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        var transaction = await repository.GetAsync(ownerId, id, cancellationToken);
        return transaction is null
            ? ServiceResult<TransactionDto>.NotFound()
            : ServiceResult<TransactionDto>.Success(transaction);
    }

    public async Task<ServiceResult<TransactionDto>> UpdateAsync(string ownerId, Guid id, TransactionInput input, CancellationToken cancellationToken)
    {
        var errors = Validate(input);
        if (errors.Count > 0)
        {
            return ServiceResult<TransactionDto>.Invalid(errors);
        }

        var normalized = Normalize(input);
        var updated = await repository.UpdateAsync(ownerId, id, normalized, cancellationToken);
        return updated
            ? ServiceResult<TransactionDto>.Success(ToDto(id, normalized))
            : ServiceResult<TransactionDto>.NotFound();
    }

    public async Task<ServiceStatus> DeleteAsync(string ownerId, Guid id, CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(ownerId, id, cancellationToken);
        return deleted ? ServiceStatus.Success : ServiceStatus.NotFound;
    }

    public async Task<ServiceResult<MonthlySummaryDto>> GetMonthlySummaryAsync(string ownerId, int year, int month, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (year < 2000 || year > today.Year + 1 || month < 1 || month > 12)
        {
            return ServiceResult<MonthlySummaryDto>.Invalid(new Dictionary<string, string[]>
            {
                ["month"] = new[] { "Year must be between 2000 and next year, and month must be between 1 and 12." }
            });
        }

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var categories = await repository.GetCategoryTotalsAsync(ownerId, from, to, cancellationToken);
        var total = categories.Sum(c => c.Total);
        return ServiceResult<MonthlySummaryDto>.Success(new MonthlySummaryDto(year, month, total, categories));
    }

    private IReadOnlyDictionary<string, string[]> Validate(TransactionInput input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.Amount <= 0)
        {
            errors[nameof(TransactionInput.Amount)] = new[] { "Amount must be greater than zero." };
        }
        else if (input.Amount > MaxAmount)
        {
            errors[nameof(TransactionInput.Amount)] = new[] { "Amount is too large." };
        }
        else if (decimal.Round(input.Amount, 2) != input.Amount)
        {
            errors[nameof(TransactionInput.Amount)] = new[] { "Amount must have at most two decimal places." };
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (input.Date > today.AddYears(1))
        {
            errors[nameof(TransactionInput.Date)] = new[] { "Date cannot be more than one year in the future." };
        }

        var description = (input.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            errors[nameof(TransactionInput.Description)] = new[] { "Description is required." };
        }
        else if (description.Length > DescriptionMaxLength)
        {
            errors[nameof(TransactionInput.Description)] = new[] { $"Description must be {DescriptionMaxLength} characters or fewer." };
        }

        if (!TransactionCategories.TryNormalize(input.Category, out _))
        {
            errors[nameof(TransactionInput.Category)] = new[] { "Category must be one of the allowed categories." };
        }

        return errors;
    }

    private static TransactionInput Normalize(TransactionInput input)
    {
        TransactionCategories.TryNormalize(input.Category, out var category);
        return input with { Description = input.Description.Trim(), Category = category };
    }

    private static TransactionDto ToDto(Guid id, TransactionInput input) =>
        new(id, input.Amount, input.Date, input.Description, input.Category);
}
