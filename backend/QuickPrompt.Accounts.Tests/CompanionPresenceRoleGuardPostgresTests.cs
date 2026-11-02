using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Only transaction-local drift in the synthetic fixture; closing the session restores the exact presence catalog.</summary>
public sealed class CompanionPresenceRoleGuardPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    public static IEnumerable<object[]> Drifts => new[] {
        new object[] { false, "ALTER TABLE qp_operations.companion_presence DISABLE ROW LEVEL SECURITY", false },
        new object[] { false, "ALTER TABLE qp_operations.companion_presence NO FORCE ROW LEVEL SECURITY", false },
        new object[] { false, "GRANT DELETE ON qp_operations.companion_presence TO quickprompt_app", false },
        new object[] { false, "GRANT UPDATE (\"OwnerId\") ON qp_operations.companion_presence TO quickprompt_app", false },
        new object[] { false, "GRANT REFERENCES (\"Signal\") ON qp_operations.companion_presence TO quickprompt_app", false },
        new object[] { false, "REVOKE INSERT ON qp_operations.companion_presence FROM quickprompt_app", false },
        new object[] { false, "CREATE POLICY presence_extra_qa ON qp_operations.companion_presence TO quickprompt_app USING(true) WITH CHECK(true)", true },
        new object[] { false, "ALTER POLICY companion_presence_actor ON qp_operations.companion_presence USING(true)", false },
        new object[] { false, "ALTER POLICY companion_presence_actor ON qp_operations.companion_presence WITH CHECK(true)", false },
        new object[] { false, "ALTER POLICY companion_presence_actor ON qp_operations.companion_presence TO PUBLIC", false },
        new object[] { false, "ALTER POLICY companion_presence_maintenance ON qp_operations.companion_presence TO quickprompt_app", false },
        new object[] { false, "ALTER TABLE qp_operations.companion_presence DROP CONSTRAINT bounded_companion_presence", false },
        new object[] { false, "ALTER TABLE qp_operations.companion_presence DROP CONSTRAINT \"FK_companion_presence_connections_ConnectionId_OwnerId\"", false },
        new object[] { true, "GRANT SELECT ON qp_operations.companion_presence TO quickprompt_execution", false },
        new object[] { true, "GRANT UPDATE (\"Signal\") ON qp_operations.companion_presence TO quickprompt_execution", false }
    };

    [Theory, Trait("Category", "CompanionPresence")]
    [MemberData(nameof(Drifts))]
    public async Task RuntimeRejectsPresenceDriftAndSessionRollbackRestoresTheCatalog(bool dispatch, string drift, bool contrastExpectedNamesOnly)
    {
        var before = await Fingerprint(); var initialized = false;
        var settings = new NpgsqlConnectionStringBuilder(postgres.MigrationConnection);
        if (settings.Host != "127.0.0.1" || settings.Port != 55432 || settings.Database != "quickprompt_development")
            throw new InvalidOperationException("Presence drift is restricted to the synthetic PostgreSQL fixture.");
        var secretRoot = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_POSTGRES_SECRETS")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "saas-development", "secrets");
        settings.Username = "quickprompt_bootstrap"; settings.Password = File.ReadAllText(Path.Combine(secretRoot, "postgres-bootstrap.txt")).Trim();
        settings.Pooling = false; settings.Timeout = 5; settings.CommandTimeout = 5; settings.IncludeErrorDetail = false;
        var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
        builder.UsePhysicalConnectionInitializer(_ => throw new NotSupportedException("Use the async guard."), async connection => {
            await using var setup = new NpgsqlCommand("BEGIN; SET LOCAL lock_timeout='3s'; SET LOCAL statement_timeout='5s'; " + drift, connection);
            await setup.ExecuteNonQueryAsync();
            if (contrastExpectedNamesOnly)
            {
                // The former whitelist predicate still accepts this third permissive policy.
                await using var legacy = new NpgsqlCommand("SELECT count(*)=2 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence' AND policyname IN ('companion_presence_actor','companion_presence_maintenance')", connection);
                Assert.Equal(true, await legacy.ExecuteScalarAsync());
            }
            var role = dispatch ? "quickprompt_execution" : "quickprompt_app";
            await using var setRole = new NpgsqlCommand("SET LOCAL ROLE " + role, connection);
            await setRole.ExecuteNonQueryAsync(); initialized = true;
        });
        var data = builder.Build();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => dispatch
                ? new ExecutionRoleGuard(new ExecutionDataSource(data)).StartAsync(CancellationToken.None)
                : new AccountRoleGuard(data).StartAsync(CancellationToken.None));
            Assert.True(initialized, "A setup failure is not evidence of guard rejection.");
        }
        finally { await data.DisposeAsync(); Assert.Equal(before, await Fingerprint()); }
    }

    private async Task<string> Fingerprint()
    {
        await using var command = postgres.Application.CreateCommand("""
            SELECT md5(concat_ws('|',
              (SELECT concat_ws(':',relrowsecurity,relforcerowsecurity,relowner,relacl::text) FROM pg_class WHERE oid='qp_operations.companion_presence'::regclass),
              (SELECT string_agg(concat_ws(':',attname,attacl::text),';' ORDER BY attnum) FROM pg_attribute WHERE attrelid='qp_operations.companion_presence'::regclass AND attnum>0),
              (SELECT string_agg(concat_ws(':',policyname,roles::text,cmd,permissive,qual,with_check),';' ORDER BY policyname COLLATE "C") FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence')));
            """);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
