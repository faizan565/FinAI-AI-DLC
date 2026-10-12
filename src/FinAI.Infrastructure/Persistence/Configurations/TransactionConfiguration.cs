using FinAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinAI.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public const int DescriptionMaxLength = 200;
    public const int CategoryMaxLength = 50;
    public const int UserIdMaxLength = 450;

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .HasMaxLength(UserIdMaxLength)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.Date)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(DescriptionMaxLength)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasMaxLength(CategoryMaxLength)
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Date });
    }
}
