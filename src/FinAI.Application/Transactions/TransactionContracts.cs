namespace FinAI.Application.Transactions;

public sealed record TransactionInput(decimal Amount, DateOnly Date, string Description, string Category);

public sealed record TransactionDto(Guid Id, decimal Amount, DateOnly Date, string Description, string Category);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record CategoryTotalDto(string Category, decimal Total);

public sealed record MonthlySummaryDto(int Year, int Month, decimal Total, IReadOnlyList<CategoryTotalDto> Categories);

public enum ServiceStatus
{
    Success,
    Invalid,
    NotFound
}

public sealed record ServiceResult<T>(ServiceStatus Status, T? Value = default, IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ServiceResult<T> Success(T value) => new(ServiceStatus.Success, value);

    public static ServiceResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(ServiceStatus.Invalid, default, errors);

    public static ServiceResult<T> NotFound() => new(ServiceStatus.NotFound);
}
