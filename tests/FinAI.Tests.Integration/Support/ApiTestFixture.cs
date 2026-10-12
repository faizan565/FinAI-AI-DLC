using FinAI.Application.Ai;
using FinAI.Application.Ai;
using FinAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinAI.Tests.Integration.Support;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string ConnectionStringVariable = "FINAI_TEST_CONNECTION_STRING";

    public ApiFactory()
    {
        ConnectionString = ResolveTestConnectionString();
    }

    public string ConnectionString { get; }

    public FakeAiService AiService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FinAI", ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAIService>();
            services.AddSingleton<IAIService>(AiService);
        });
    }

    private static string ResolveTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set the '{ConnectionStringVariable}' environment variable to a dedicated test database connection string.");
        }

        if (!connectionString.Contains("Database=FinAI_Test", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("Initial Catalog=FinAI_Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The integration test connection string must target a database named FinAI_Test so the development database is never reset.");
        }

        return connectionString;
    }
}

public sealed class ApiTestFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FinAIDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinAIDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        Factory.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "Api";
}
