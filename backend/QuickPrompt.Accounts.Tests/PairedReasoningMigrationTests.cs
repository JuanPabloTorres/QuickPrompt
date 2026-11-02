using System.Reflection;
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
    public void MigrationOnlyExtendsTheExistingWorkerAllowlistsAndDownNeverDeletesOrConvertsGrants()
    {
        var up = Sql("Up"); var down = Sql("Down");
        Assert.Equal(2, up.Split(PairedReasoningProtocol.GrantProfile).Length - 1);
        // Removing just the added enum must reproduce the full original CHECK
        // and RLS contract emitted by Down after its manual-intervention guard.
        const string restore = "ALTER TABLE qp_operations.connections DROP CONSTRAINT bounded_connection;";
        Assert.Equal(down[down.IndexOf(restore, StringComparison.Ordinal)..].Trim(),
            up.Replace(",'" + PairedReasoningProtocol.GrantProfile + "'", "", StringComparison.Ordinal).Trim());
        Assert.Contains("\"OwnerId\" = nullif(current_setting('quickprompt.actor_id',true),'')::uuid", up);
        Assert.Contains("c.\"OwnerId\"=companion_presence.\"OwnerId\"", up);
        Assert.Contains("\"LibraryId\" IS NULL AND \"McpCapabilities\"=0", up);
        Assert.DoesNotContain("CREATE TABLE", up); Assert.DoesNotContain("ADD COLUMN", up); Assert.DoesNotContain("GRANT ", up);
        Assert.DoesNotContain("UPDATE ", down); Assert.DoesNotContain("DELETE ", down);
        Assert.True(down.IndexOf("RAISE EXCEPTION", StringComparison.Ordinal) < down.IndexOf(restore, StringComparison.Ordinal));
        Assert.Contains("CHECK (\"ProfileId\"<>'" + PairedReasoningProtocol.GrantProfile + "')", down);
        Assert.Contains("EXCEPTION WHEN check_violation", down);
        Assert.DoesNotContain("NO FORCE", down); Assert.DoesNotContain("DISABLE ROW", down);
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
    }

    internal static string Sql(string method)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(PairedReasoningProfile).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(new PairedReasoningProfile(), [builder]);
        return Assert.IsType<SqlOperation>(Assert.Single(builder.Operations)).Sql;
    }
}
