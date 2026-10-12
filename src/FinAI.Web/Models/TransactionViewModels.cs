using System.ComponentModel.DataAnnotations;

namespace FinAI.Web.Models;

public static class TransactionCategoryOptions
{
    public static IReadOnlyList<string> All { get; } =
        ["Food", "Transport", "Housing", "Utilities", "Entertainment", "Health", "Shopping", "Other", "Uncategorized"];
}

public sealed class TransactionFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [Range(0.01, 9999999999999999.99, ErrorMessage = "Amount must be at least 0.01.")]
    public decimal Amount { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? Date { get; set; }

    [Required]
    [StringLength(200, ErrorMessage = "Description must be 200 characters or fewer.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = "Other";

    public ApiTransactionInput ToInput() =>
        new(Amount, DateOnly.FromDateTime(Date.GetValueOrDefault()), Description.Trim(), Category);

    public static TransactionFormViewModel FromTransaction(ApiTransaction transaction) => new()
    {
        Id = transaction.Id,
        Amount = transaction.Amount,
        Date = transaction.Date.ToDateTime(TimeOnly.MinValue),
        Description = transaction.Description,
        Category = transaction.Category
    };
}

public sealed record TransactionListViewModel(
    IReadOnlyList<ApiTransaction> Items,
    int Page,
    int TotalPages,
    int TotalCount,
    string? ErrorMessage);
