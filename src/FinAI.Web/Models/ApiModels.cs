namespace FinAI.Web.Models;

public sealed record ApiCredentials(string UserName, string Password);

public sealed record ApiUser(string Id, string UserName);

public sealed record ApiCsrfToken(string RequestToken);

public sealed record ApiTransactionInput(decimal Amount, DateOnly Date, string Description, string Category);

public sealed record ApiTransaction(Guid Id, decimal Amount, DateOnly Date, string Description, string Category);

public sealed record ApiPagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ApiCategoryTotal(string Category, decimal Total);

public sealed record ApiMonthlySummary(int Year, int Month, decimal Total, IReadOnlyList<ApiCategoryTotal> Categories);

public sealed record ApiExpenseProposalRequest(string Text);

public sealed record ApiExpenseProposal(decimal Amount, DateOnly Date, string Description, string Category, bool DateIsDefaulted);
