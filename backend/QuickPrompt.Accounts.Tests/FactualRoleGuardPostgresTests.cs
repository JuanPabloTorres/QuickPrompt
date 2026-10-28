using Npgsql;
using QuickPrompt.Accounts.Executions;
using QuickPrompt.Cloud;
using Xunit;

namespace QuickPrompt.Accounts.Tests;

/// <summary>Real role guards against transaction-local catalog drift. No weakened contract is ever committed.</summary>
public sealed class FactualRoleGuardPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private sealed record Drift(bool Dispatcher, string Sql, int MinimumServerVersion = 0);
    private static readonly Dictionary<string, Drift> Cases = new(StringComparer.Ordinal) {
        ["app-document-rls"] = new(false, "ALTER TABLE qp_library.factual_documents DISABLE ROW LEVEL SECURITY"),
        ["app-review-force"] = new(false, "ALTER TABLE qp_operations.factual_reviews NO FORCE ROW LEVEL SECURITY"),
        ["app-document-update"] = new(false, "GRANT UPDATE ON qp_library.factual_documents TO quickprompt_app"),
        ["app-document-column-update"] = new(false, "GRANT UPDATE (\"Title\") ON qp_library.factual_documents TO quickprompt_app"),
        ["app-review-delete"] = new(false, "GRANT DELETE ON qp_operations.factual_reviews TO quickprompt_app"),
        ["app-review-column-references"] = new(false, "GRANT REFERENCES (\"Source\") ON qp_operations.factual_reviews TO quickprompt_app"),
        ["app-review-maintain"] = new(false, "GRANT MAINTAIN ON qp_operations.factual_reviews TO quickprompt_app", 170000),
        ["app-document-maintain"] = new(false, "GRANT MAINTAIN ON qp_library.factual_documents TO quickprompt_app", 170000),
        ["app-review-required-insert"] = new(false, "REVOKE INSERT ON qp_operations.factual_reviews FROM quickprompt_app"),
        ["app-forget-missing"] = new(false, "ALTER FUNCTION qp_operations.forget_factual_child(uuid,uuid) RENAME TO forget_factual_child_guard_qa_missing"),
        ["app-forget-denied"] = new(false, "REVOKE EXECUTE ON FUNCTION qp_operations.forget_factual_child(uuid,uuid) FROM quickprompt_app"),
        ["app-forget-public"] = new(false, "GRANT EXECUTE ON FUNCTION qp_operations.forget_factual_child(uuid,uuid) TO PUBLIC"),
        ["app-forget-unsafe-path"] = new(false, "ALTER FUNCTION qp_operations.forget_factual_child(uuid,uuid) SET search_path=public"),
        ["app-document-trigger-disabled"] = new(false, "ALTER TABLE qp_library.factual_documents DISABLE TRIGGER factual_document_from_review"),
        ["dispatcher-review-force"] = new(true, "ALTER TABLE qp_operations.factual_reviews NO FORCE ROW LEVEL SECURITY"),
        ["dispatcher-document-rls"] = new(true, "ALTER TABLE qp_library.factual_documents DISABLE ROW LEVEL SECURITY"),
        ["dispatcher-review-select"] = new(true, "GRANT SELECT ON qp_operations.factual_reviews TO quickprompt_execution"),
        ["dispatcher-document-column-select"] = new(true, "GRANT SELECT (\"Text\") ON qp_library.factual_documents TO quickprompt_execution"),
        ["dispatcher-review-maintain"] = new(true, "GRANT MAINTAIN ON qp_operations.factual_reviews TO quickprompt_execution", 170000),
        ["dispatcher-document-maintain"] = new(true, "GRANT MAINTAIN ON qp_library.factual_documents TO quickprompt_execution", 170000),
        ["dispatcher-expire-missing"] = new(true, "ALTER FUNCTION qp_operations.expire_factual_reviews() RENAME TO expire_factual_reviews_guard_qa_missing"),
        ["dispatcher-expire-denied"] = new(true, "REVOKE EXECUTE ON FUNCTION qp_operations.expire_factual_reviews() FROM quickprompt_execution"),
        ["dispatcher-forget-granted"] = new(true, "GRANT EXECUTE ON FUNCTION qp_operations.forget_factual_child(uuid,uuid) TO quickprompt_execution"),
        ["dispatcher-expire-unsafe-path"] = new(true, "ALTER FUNCTION qp_operations.expire_factual_reviews() SET search_path=public")
    };
    public static IEnumerable<object[]> DriftCases => Cases.Keys.Select(name => new object[] { name });

    [Fact]
    public async Task ActualLimitedApplicationAndDispatcherRolesAcceptTheMigratedContract()
    {
        await new AccountRoleGuard(postgres.Application).StartAsync(CancellationToken.None);
        await using var dispatch = new ExecutionDataSource(NpgsqlDataSource.Create(postgres.ExecutionConnection));
        await new ExecutionRoleGuard(dispatch).StartAsync(CancellationToken.None);
    }

    [Theory]
    [MemberData(nameof(DriftCases))]
    public async Task StartupRejectsFactualDriftAndClosingTheSessionRestoresTheExactCatalog(string caseId)
    {
        var drift = Cases[caseId];
        if (drift.MinimumServerVersion > 0)
        {
            await using var version = postgres.Application.CreateCommand("SHOW server_version_num");
            var actual = int.Parse((string)(await version.ExecuteScalarAsync())!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(actual >= drift.MinimumServerVersion,
                "This MAINTAIN drift requires PostgreSQL 17+. Its DDL was not executed; select the explicit supported QA fixture.");
        }
        var before = await Fingerprint(); var initialized = false;
        var data = DriftSource(drift, () => initialized = true);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => drift.Dispatcher
                ? new ExecutionRoleGuard(new ExecutionDataSource(data)).StartAsync(CancellationToken.None)
                : new AccountRoleGuard(data).StartAsync(CancellationToken.None));
            Assert.True(initialized, "The real limited-role session must reach the guard; setup errors do not prove rejection.");
        }
        finally
        {
            await data.DisposeAsync(); // Pooling=false closes the physical session and rolls back BEGIN, including DDL/SET LOCAL ROLE.
            Assert.Equal(before, await Fingerprint());
        }
    }

    private NpgsqlDataSource DriftSource(Drift drift, Action initialized)
    {
        var settings = new NpgsqlConnectionStringBuilder(postgres.MigrationConnection);
        if (settings.Host != "127.0.0.1" || settings.Port != 55432 || settings.Database != "quickprompt_development")
            throw new InvalidOperationException("Catalog drift is allowed only in the explicit isolated PostgreSQL fixture.");
        var secrets = Environment.GetEnvironmentVariable("QUICKPROMPT_TEST_POSTGRES_SECRETS")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPromptNova", "saas-development", "secrets");
        settings.Username = "quickprompt_bootstrap";
        settings.Password = File.ReadAllText(Path.Combine(secrets, "postgres-bootstrap.txt")).Trim();
        settings.Pooling = false; settings.Timeout = 5; settings.CommandTimeout = 5; settings.IncludeErrorDetail = false;
        var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
        // Npgsql's initializer runs once per physical connection, before the unmodified guard opens it.
        // Both APIs are supplied; the test intentionally uses async only (official Npgsql API documentation).
        builder.UsePhysicalConnectionInitializer(_ => throw new NotSupportedException("Use the asynchronous guard path."), async connection => {
            var role = drift.Dispatcher ? "quickprompt_execution" : "quickprompt_app";
            await using var setup = new NpgsqlCommand("BEGIN; SET LOCAL lock_timeout='3s'; SET LOCAL statement_timeout='5s'; "
                + drift.Sql + "; SET LOCAL ROLE " + role, connection);
            await setup.ExecuteNonQueryAsync();
            await using var identity = new NpgsqlCommand("SELECT current_user", connection);
            Assert.Equal(role, await identity.ExecuteScalarAsync()); initialized();
        });
        return builder.Build();
    }

    private async Task<string> Fingerprint()
    {
        await using var query = postgres.Application.CreateCommand("""
            SELECT md5(concat_ws('|',
              (SELECT string_agg(concat_ws(':',n.nspname,c.relname,c.relowner,c.relrowsecurity,c.relforcerowsecurity,c.relacl::text),';' ORDER BY n.nspname,c.relname)
               FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
               WHERE (n.nspname,c.relname) IN (('qp_library','factual_documents'),('qp_operations','factual_reviews'))),
              (SELECT string_agg(concat_ws(':',c.relname,a.attname,a.attacl::text),';' ORDER BY c.relname,a.attnum)
               FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
               WHERE a.attnum>0 AND NOT a.attisdropped AND (n.nspname,c.relname) IN (('qp_library','factual_documents'),('qp_operations','factual_reviews'))),
              (SELECT string_agg(concat_ws(':',p.oid,p.proname,p.proowner,p.prosecdef,p.proconfig::text,p.proacl::text),';' ORDER BY p.oid)
               FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE (n.nspname,p.proname) IN
                 (('qp_operations','expire_factual_reviews'),('qp_operations','forget_factual_child'),('qp_library','validate_factual_document'),('qp_library','erase_factual_derivatives'))),
              (SELECT string_agg(concat_ws(':',t.tgname,t.tgfoid,t.tgenabled),';' ORDER BY t.tgname) FROM pg_trigger t
               WHERE t.tgname IN ('factual_document_from_review','erased_prompt_factual_content'))));
            """);
        return (string)(await query.ExecuteScalarAsync())!;
    }
}
