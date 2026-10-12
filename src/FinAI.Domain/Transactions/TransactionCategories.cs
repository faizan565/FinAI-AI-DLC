namespace FinAI.Domain.Transactions;

public static class TransactionCategories
{
    public static IReadOnlyList<string> All { get; } =
        new[] { "Food", "Transport", "Housing", "Utilities", "Entertainment", "Health", "Shopping", "Other", "Uncategorized" };

    public static bool TryNormalize(string? value, out string canonical)
    {
        var match = value is null
            ? null
            : All.FirstOrDefault(category => string.Equals(category, value.Trim(), StringComparison.OrdinalIgnoreCase));

        canonical = match ?? string.Empty;
        return match is not null;
    }
}
