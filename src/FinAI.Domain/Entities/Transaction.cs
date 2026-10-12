namespace FinAI.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public required string Description { get; set; }

    public required string Category { get; set; }
}
