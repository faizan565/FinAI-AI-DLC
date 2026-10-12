namespace FinAI.Application.Ai;

public sealed record TransactionProposal(decimal Amount, DateOnly Date, string Description, string Category, bool DateIsDefaulted);

public sealed record ExpenseProposalRequest(string? Text);

public sealed record RawExpenseFields(string? AmountJson, string? Date, string? Description, string? Category);
