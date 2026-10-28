using Npgsql;

namespace QuickPrompt.Cloud;

/// <summary>Refuse privileged connections or missing RLS before accepting account traffic.</summary>
public sealed class AccountRoleGuard(NpgsqlDataSource data) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellation)
    {
        await using var connection = await data.OpenConnectionAsync(cancellation);
        await using var command = new NpgsqlCommand("""
            SELECT NOT (rolsuper OR rolbypassrls OR rolcreatedb OR rolcreaterole)
              AND (SELECT count(*)=1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_operations' AND c.relname='connections' AND c.relrowsecurity AND c.relforcerowsecurity
                   AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND NOT has_column_privilege(current_user,'qp_operations.connections','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.connections','AuthorizationId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.connections','LibraryId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.connections','McpCapabilities','UPDATE')
              AND EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='qp_operations.connections'::regclass
                   AND c.conname='bounded_connection' AND position('McpCapabilities' in pg_get_constraintdef(c.oid))>0)
              AND NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='connections' AND policyname='connection_capability_upgrade')
              AND NOT has_table_privilege(current_user,'qp_operations.connections','DELETE')
              AND has_column_privilege(current_user,'qp_operations.connections','RevokedAt','UPDATE')
              AND NOT has_schema_privilege(current_user, 'qp_migrations', 'USAGE')
              AND (SELECT count(*) = 14 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_library' AND c.relname IN ('libraries','prompts','prompt_versions','publications','invitations','imports','agent_contexts','agent_preferences','captures','collections','collection_members','collection_imports','prompt_uses','agent_sessions')
                     AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND NOT has_column_privilege(current_user,'qp_library.publications','Template','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.invitations','RecipientId','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_library.prompt_uses','UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'qp_library.prompt_versions','UPDATE')
              AND has_table_privilege(current_user,'qp_library.prompt_uses','SELECT,INSERT')
              AND NOT has_table_privilege(current_user,'qp_library.imports','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_library.collection_imports','UPDATE,DELETE')
              AND NOT has_column_privilege(current_user,'qp_library.publications','CollectionSnapshot','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.publications','CollectionId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.invitations','IsCollection','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_library.captures','UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'qp_library.publications','DELETE')
              AND has_column_privilege(current_user,'qp_library.publications','RevokedAt','UPDATE')
              AND has_column_privilege(current_user,'qp_library.invitations','State','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_contexts','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_contexts','SessionId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_preferences','OwnerId','UPDATE')
              AND has_column_privilege(current_user,'qp_library.agent_contexts','Revision','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_sessions','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_sessions','LibraryId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.agent_sessions','SessionId','UPDATE')
              AND has_column_privilege(current_user,'qp_library.agent_sessions','Revision','UPDATE')
              AND has_column_privilege(current_user,'qp_library.agent_sessions','Payload','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.collections','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.collections','LibraryId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_library.collections','Id','UPDATE')
              AND NOT has_table_privilege(current_user,'qp_library.collection_members','UPDATE')
              AND (SELECT count(*)=3 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_operations' AND c.relname IN ('executions','execution_results','proposal_results') AND c.relrowsecurity AND c.relforcerowsecurity)
              AND NOT has_table_privilege(current_user,'qp_operations.executions','INSERT')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','Status','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','Payload','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.executions','ForgottenAt','UPDATE')
              AND has_table_privilege(current_user,'qp_operations.proposal_results','SELECT')
              AND NOT has_table_privilege(current_user,'qp_operations.proposal_results','UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'qp_operations.proposal_results','INSERT')
              AND NOT has_table_privilege(current_user,'qp_operations.execution_results','UPDATE,DELETE')
              AND has_function_privilege(current_user,'qp_operations.forget_execution_content(uuid,uuid)','EXECUTE')
              AND NOT has_function_privilege(current_user,'qp_operations.expire_execution_content()','EXECUTE')
              AND NOT has_function_privilege(current_user,'qp_operations.erase_execution_content(uuid)','EXECUTE')
              AND (SELECT count(*)=3 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
                   WHERE n.nspname='qp_operations' AND p.proname IN ('erase_execution_content','forget_execution_content','expire_execution_content')
                     AND pg_get_userbyid(p.proowner)='quickprompt_migrator'
                     AND p.prosecdef=(p.proname<>'erase_execution_content')
                     AND EXISTS (SELECT 1 FROM unnest(p.proconfig) setting WHERE replace(setting,' ','')='search_path=pg_catalog,pg_temp')
                     AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) grant_entry
                                     WHERE grant_entry.grantee=0 AND grant_entry.privilege_type='EXECUTE'))
              AND NOT has_table_privilege(current_user,'qp_operations.execution_results','INSERT')
              AND has_column_privilege(current_user,'qp_operations.executions','CancelRequested','UPDATE')
            FROM pg_roles WHERE rolname=current_user;
            """, connection);
        if (await command.ExecuteScalarAsync(cancellation) is not true)
            throw new InvalidOperationException("Account host requires a limited application role and migrated, forced-RLS tables. No security fallback is available.");
    }
    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}
