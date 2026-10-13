using FinAI.Application.Ai;
using FinAI.Application.Insights;
using FinAI.Application.Transactions;
using FinAI.Infrastructure.Ai;
using FinAI.Infrastructure.Identity;
using FinAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinAI.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FinAI";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with dotnet user-secrets or an environment variable.");
        }

        services.AddDbContext<FinAIDbContext>(options => options.UseSqlServer(connectionString));

        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<FinAIDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddSingleton(TimeProvider.System);

        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<IAIService, GeminiAIService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(options.AttemptTimeoutSeconds);
        });
        services.AddScoped<IExpenseProposalService, ExpenseProposalService>();
        services.AddScoped<ISpendingQuestionService, SpendingQuestionService>();
        services.AddScoped<IMonthlyInsightService, MonthlyInsightService>();

        return services;
    }
}
