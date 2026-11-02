using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickPrompt.Accounts.Persistence;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real isolated PostgreSQL only; never falls back to SQLite or an in-memory provider.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public NpgsqlDataSource Application { get; private set; } = null!;
    public ServiceProvider Services { get; private set; } = null!;
    public string MigrationConnection { get; private set; } = "";
    public string ApplicationConnection { get; private set; } = "";
    public string ExecutionConnection { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_POSTGRES") != "1")
            throw new InvalidOperationException("Run Start-QuickPromptPostgres.ps1 and explicitly set QUICKPROMPT_TEST_POSTGRES=1 for this integration suite.");
        var secretDirectory = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_POSTGRES_SECRETS")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "saas-development", "secrets");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 55432, Database = "quickprompt_development",
            Username = "quickprompt_migrator", Password = File.ReadAllText(Path.Combine(secretDirectory, "postgres-migrator.txt")).Trim(),
            IncludeErrorDetail = false, Timeout = 5, CommandTimeout = 10
        };
        MigrationConnection = connection.ConnectionString;
        await using (var migration = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>()
            .UseNpgsql(MigrationConnection, options => options.MigrationsHistoryTable("history", "qp_migrations")).Options))
            await migration.Database.MigrateAsync();
        connection.Username = "quickprompt_app";
        connection.Password = File.ReadAllText(Path.Combine(secretDirectory, "postgres-application.txt")).Trim();
        // A one-connection pool deliberately forces reuse across actors and transactions.
        connection.MaxPoolSize = 1;
        ApplicationConnection = connection.ConnectionString;
        Application = NpgsqlDataSource.Create(connection.ConnectionString);
        connection.Username = "quickprompt_execution";
        connection.Password = File.ReadAllText(Path.Combine(secretDirectory, "postgres-execution.txt")).Trim();
        connection.MaxPoolSize = 4;
        ExecutionConnection = connection.ConnectionString;
        var services = new ServiceCollection().AddLogging();
        services.AddDataProtection();
        services.AddQuickPromptAccounts(Application);
        Services = services.BuildServiceProvider();
    }

    public async Task<Guid> CreateUser(bool confirmed = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var user = new AccountUser { Id = Guid.NewGuid(), UserName = $"qa-{Guid.NewGuid():N}@example.invalid", EmailConfirmed = confirmed };
        user.Email = user.UserName;
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().CreateAsync(user);
        Assert.True(result.Succeeded, "Synthetic account creation must succeed through Identity.");
        return user.Id;
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        if (Application is not null) await Application.DisposeAsync();
    }
}
