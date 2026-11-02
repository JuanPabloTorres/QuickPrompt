using Npgsql;
using QuickPrompt.Nova.Application;
using Xunit;
using Xunit.Abstractions;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Captures only the synthetic fixture's schema policy, never account content or credentials.</summary>
public sealed class PairedReasoningSchemaCaptureTests(PostgresFixture postgres, ITestOutputHelper output) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task NewPresencePolicyRetainsOwnerPredicatesAndCapturesItsExactDeparsedFingerprint()
    {
        await using var command = postgres.Application.CreateCommand("""
            SELECT qual,with_check,encode(sha256(convert_to(qual || E'\n' || with_check,'UTF8')),'hex')
            FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence' AND policyname='companion_presence_actor';
            """);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        var qual = reader.GetString(0); var check = reader.GetString(1); var hash = reader.GetString(2);
        Assert.Contains("\"OwnerId\"", qual); Assert.Contains("quickprompt.actor_id", qual);
        Assert.Contains(PairedReasoningProtocol.GrantProfile, check); Assert.Contains("c.\"OwnerId\"", check);
        Assert.Contains("c.\"LibraryId\" IS NULL", check);
        Assert.Equal("40e71595196dd4db30a906d50253d0834f35f91ff2adaf58e8d4e90a901d8d72", hash);
        output.WriteLine("PolicySha256=" + hash); output.WriteLine("USING=" + qual); output.WriteLine("WITH_CHECK=" + check);
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public async Task ExactMigrationSqlCanRollBackAnEmptyReasoningProfileSetOnScopedTemporaryRelations()
    {
        // The shared fixture already contains explicitly consented QA grants.
        // Do not delete/convert them just to exercise the empty-set branch.
        // Replay real Up/Down with only relation bindings changed; all columns,
        // CHECK logic and owner-policy expressions are the actual migration SQL.
        await using var database = new NpgsqlConnection(postgres.MigrationConnection); await database.OpenAsync();
        await using var transaction = await database.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("""
            CREATE TEMP TABLE paired_connections (
              "Id" uuid,"OwnerId" uuid,"ProfileId" text,"LibraryId" uuid,"McpCapabilities" integer,
              "CreatedAt" timestamptz,"ExpiresAt" timestamptz,
              CONSTRAINT bounded_connection CHECK(true));
            CREATE TEMP TABLE companion_presence ("OwnerId" uuid,"ConnectionId" uuid);
            ALTER TABLE paired_connections ENABLE ROW LEVEL SECURITY;
            ALTER TABLE paired_connections FORCE ROW LEVEL SECURITY;
            ALTER TABLE pg_temp.companion_presence ENABLE ROW LEVEL SECURITY;
            ALTER TABLE pg_temp.companion_presence FORCE ROW LEVEL SECURITY;
            CREATE POLICY companion_presence_actor ON pg_temp.companion_presence TO quickprompt_app USING(false) WITH CHECK(false);
            """, database, transaction)) await setup.ExecuteNonQueryAsync();
        string Scoped(string sql) => sql.Replace("qp_operations.connections", "pg_temp.paired_connections", StringComparison.Ordinal)
            .Replace("qp_operations.companion_presence", "pg_temp.companion_presence", StringComparison.Ordinal);
        foreach (var phase in new[] { "Up", "Down" })
        {
            await using var command = new NpgsqlCommand(Scoped(PairedReasoningMigrationTests.Sql(phase)), database, transaction);
            await command.ExecuteNonQueryAsync();
        }
        await using (var constraint = new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='bounded_connection' AND conrelid='pg_temp.paired_connections'::regclass", database, transaction))
        {
            var final = (string)(await constraint.ExecuteScalarAsync())!;
            Assert.Contains("ollama-qwen3-8b-proposals", final); Assert.DoesNotContain(PairedReasoningProtocol.GrantProfile, final);
        }
        await using (var policy = new NpgsqlCommand("SELECT with_check FROM pg_policies WHERE schemaname LIKE 'pg_temp_%' AND tablename='companion_presence' AND policyname='companion_presence_actor'", database, transaction))
        {
            var final = (string)(await policy.ExecuteScalarAsync())!;
            Assert.Contains("quickprompt.actor_id", final); Assert.DoesNotContain(PairedReasoningProtocol.GrantProfile, final);
        }
        await using (var probe = new NpgsqlCommand("SELECT count(*) FROM pg_constraint WHERE conname='rollback_without_reasoning_profiles' AND conrelid='pg_temp.paired_connections'::regclass", database, transaction))
            Assert.Equal(0L, await probe.ExecuteScalarAsync());
        await transaction.RollbackAsync();
        output.WriteLine("scopedRelationReplay=true; realBaselineDowngraded=false; transactionRolledBack=true");
    }
}
