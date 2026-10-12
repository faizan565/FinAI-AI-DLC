using FinAI.Domain.Entities;
using FinAI.Infrastructure.Identity;
using FinAI.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Infrastructure.Persistence;

public sealed class FinAIDbContext(DbContextOptions<FinAIDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new TransactionConfiguration());
    }
}
