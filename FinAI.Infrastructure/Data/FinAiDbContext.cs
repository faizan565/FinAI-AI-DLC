using Microsoft.EntityFrameworkCore;
using FinAI.Domain.Entities;
using FinAI.Infrastructure.Data.Configuration;

namespace FinAI.Infrastructure.Data
{
    /// <summary>
    /// Entity Framework Core database context for FinAI application.
    /// Manages transaction data persistence to SQL Server.
    /// </summary>
    public class FinAiDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the FinAiDbContext class.
        /// </summary>
        /// <param name="options">Database context options</param>
        public FinAiDbContext(DbContextOptions<FinAiDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the Transactions entity set.
        /// </summary>
        public DbSet<Transaction> Transactions { get; set; } = null!;

        /// <summary>
        /// Configures the model using Fluent API.
        /// </summary>
        /// <param name="modelBuilder">The model builder</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all entity configurations
            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        }
    }
}
