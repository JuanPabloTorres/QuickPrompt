using Npgsql;

namespace QuickPrompt.Cloud;

/// <summary>Public hosting needs append-only access to its encrypted host key ring, separate from tenant RLS.</summary>
public sealed class HostKeyRingGuard(NpgsqlDataSource data) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellation)
    {
        await using var connection = await data.OpenConnectionAsync(cancellation);
        await using var command = new NpgsqlCommand("""
            SELECT has_table_privilege(current_user,'qp_host.data_protection_keys','SELECT')
              AND has_table_privilege(current_user,'qp_host.data_protection_keys','INSERT')
              AND NOT has_table_privilege(current_user,'qp_host.data_protection_keys','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT has_schema_privilege(current_user,'qp_host','CREATE')
              AND NOT has_schema_privilege('quickprompt_execution','qp_host','USAGE')
              AND (SELECT c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user)
                   FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_host' AND c.relname='data_protection_keys')
              AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace,
                   LATERAL aclexplode(COALESCE(c.relacl,acldefault('r',c.relowner))) permission
                   WHERE n.nspname='qp_host' AND c.relname='data_protection_keys' AND permission.grantee=0);
            """, connection);
        if (await command.ExecuteScalarAsync(cancellation) is not true)
            throw new InvalidOperationException("Public hosting requires migrated append-only host keys inaccessible to the dispatcher and PUBLIC.");
    }
    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}
