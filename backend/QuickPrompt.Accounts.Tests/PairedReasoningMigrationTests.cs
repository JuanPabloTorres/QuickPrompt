using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using QuickPrompt.Accounts.Persistence;
using QuickPrompt.Accounts.Persistence.Migrations;
using QuickPrompt.Nova.Application;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real EF migration metadata and SQL generation only. Never connects to PostgreSQL.</summary>
public sealed class PairedReasoningMigrationTests
{
    [Fact]
    public void MigrationOnlyExtendsTheExistingWorkerAllowlists()
    {
        var up = Assert.IsType<SqlOperation>(Assert.Single(new PairedReasoningProfile().UpOperations)).Sql;
        Assert.Equal(2, up.Split(PairedReasoningProtocol.GrantProfile).Length - 1);
        Assert.Contains("\"OwnerId\" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid", up);
        Assert.Contains("c.\"OwnerId\"=companion_presence.\"OwnerId\"", up);
        Assert.Contains("\"LibraryId\" IS NULL AND \"McpCapabilities\"=0", up);
        Assert.DoesNotContain("CREATE TABLE", up); Assert.DoesNotContain("ADD COLUMN", up); Assert.DoesNotContain("GRANT ", up);
    }

    [Fact]
    public void EfDiscoversTheAdditiveMigrationAndCanGenerateItsScriptWithoutAConnection()
    {
        using var database = new AccountDatabase(new DbContextOptionsBuilder<AccountDatabase>()
            .UseNpgsql("Host=example.invalid;Database=synthetic;Username=synthetic;Password=not-a-credential", options => options.MigrationsHistoryTable("history", "qp_migrations")).Options);
        var migrations = database.GetService<IMigrationsAssembly>();
        Assert.Contains("20261102183000_PairedReasoningProfile", migrations.Migrations.Keys);
        Assert.False(database.Database.HasPendingModelChanges());
        var sql = database.GetService<IMigrator>().GenerateScript("20261029151628_CompanionOperationalPresence", "20261102183000_PairedReasoningProfile");
        Assert.Contains(PairedReasoningProtocol.GrantProfile, sql); Assert.Contains("ALTER POLICY", sql);
        Assert.DoesNotContain("CREATE TABLE", sql);
        var refused = Assert.Throws<NotSupportedException>(() => database.GetService<IMigrator>()
            .GenerateScript("20261102183000_PairedReasoningProfile", "20261029151628_CompanionOperationalPresence"));
        Assert.Contains("Restore a verified backup", refused.Message);
    }
}
