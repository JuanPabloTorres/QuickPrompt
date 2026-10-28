using Npgsql;
using QuickPrompt.Accounts.Executions;

namespace QuickPrompt.Cloud;

/// <summary>The dispatcher can coordinate operations, but cannot read Identity or the private library.</summary>
public sealed class ExecutionRoleGuard(ExecutionDataSource data) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellation)
    {
        await using var connection = await data.Data.OpenConnectionAsync(cancellation);
        await using var command = new NpgsqlCommand("""
            SELECT NOT (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole)
              AND current_user='quickprompt_execution'
              AND NOT has_schema_privilege(current_user, 'qp_migrations', 'USAGE')
              AND NOT has_schema_privilege(current_user, 'qp_identity', 'USAGE')
              AND NOT has_schema_privilege(current_user, 'qp_library', 'USAGE')
              AND NOT has_table_privilege(current_user,'qp_operations.connections','SELECT,INSERT,UPDATE,DELETE')
              AND (SELECT count(*)=3 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_operations' AND c.relname IN ('executions','execution_results','proposal_results')
                   AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND NOT has_column_privilege(current_user,'qp_operations.executions','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','Payload','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','ForgottenAt','UPDATE')
              AND has_function_privilege(current_user,'qp_operations.expire_execution_content()','EXECUTE')
              AND NOT has_function_privilege(current_user,'qp_operations.forget_execution_content(uuid,uuid)','EXECUTE')
              AND NOT has_function_privilege(current_user,'qp_operations.erase_execution_content(uuid)','EXECUTE')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','ConnectionId','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_operations.executions','DELETE')
              AND has_table_privilege(current_user,'qp_operations.proposal_results','SELECT')
              AND NOT has_table_privilege(current_user,'qp_operations.proposal_results','UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'qp_operations.execution_results','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_operations.execution_results','DELETE')
              AND has_column_privilege(current_user,'qp_operations.executions','Status','UPDATE')
              AND (SELECT count(*)=2 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE (n.nspname,c.relname) IN (('qp_operations','factual_reviews'),('qp_library','factual_documents'))
                     AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              -- Catalog OIDs avoid requiring library schema access just to inspect its ACL.
              AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE (n.nspname,c.relname) IN (('qp_operations','factual_reviews'),('qp_library','factual_documents'))
                     AND (has_table_privilege(current_user,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                       OR has_any_column_privilege(current_user,c.oid,'SELECT,INSERT,UPDATE,REFERENCES')))
              AND CASE WHEN current_setting('server_version_num')::int>=170000
                    THEN NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                         WHERE (n.nspname,c.relname) IN (('qp_operations','factual_reviews'),('qp_library','factual_documents'))
                           AND has_table_privilege(current_user,c.oid,'MAINTAIN'))
                    ELSE true END
              AND has_function_privilege(current_user,to_regprocedure('qp_operations.expire_factual_reviews()')::oid,'EXECUTE')
              AND NOT has_function_privilege(current_user,to_regprocedure('qp_operations.forget_factual_child(uuid,uuid)')::oid,'EXECUTE')
              AND (SELECT count(*)=2 FROM (VALUES
                     ('qp_operations.expire_factual_reviews()'),('qp_operations.forget_factual_child(uuid,uuid)')) expected(identity)
                   JOIN pg_proc p ON p.oid=to_regprocedure(expected.identity)::oid
                   WHERE pg_get_userbyid(p.proowner)='quickprompt_migrator' AND p.prosecdef
                     AND EXISTS (SELECT 1 FROM unnest(p.proconfig) setting WHERE replace(setting,' ','')='search_path=pg_catalog,pg_temp')
                     AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) permission
                                     WHERE permission.grantee=0 AND permission.privilege_type='EXECUTE'))
            FROM pg_roles WHERE rolname=current_user;
            """, connection);
        if (await command.ExecuteScalarAsync(cancellation) is not true)
            throw new InvalidOperationException("Execution dispatcher requires its limited role, forced RLS and immutable inputs/results.");
    }
    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}
