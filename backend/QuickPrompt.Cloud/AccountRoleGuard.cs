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
              AND (SELECT count(*)=1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname='qp_operations' AND c.relname='companion_presence' AND c.relrowsecurity AND c.relforcerowsecurity
                   AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              AND has_table_privilege(current_user,'qp_operations.companion_presence','SELECT')
              AND has_table_privilege(current_user,'qp_operations.companion_presence','INSERT')
              AND NOT has_table_privilege(current_user,'qp_operations.companion_presence','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT has_any_column_privilege(current_user,'qp_operations.companion_presence','REFERENCES')
              AND NOT has_column_privilege(current_user,'qp_operations.companion_presence','OwnerId','UPDATE')
              AND NOT has_column_privilege(current_user,'qp_operations.companion_presence','ConnectionId','UPDATE')
              AND (SELECT bool_and(has_column_privilege(current_user,'qp_operations.companion_presence',name,'UPDATE'))
                   FROM unnest(ARRAY['ObservationId','Signal','ObservedAt','ExpiresAt']) name)
              AND CASE WHEN current_setting('server_version_num')::int>=170000
                   THEN NOT has_table_privilege(current_user,'qp_operations.companion_presence','MAINTAIN') ELSE true END
              AND (SELECT count(*)=2 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence')
              AND (SELECT count(*)=2 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence'
                   AND policyname IN ('companion_presence_actor','companion_presence_maintenance'))
              AND EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence'
                   AND policyname='companion_presence_actor' AND roles=ARRAY['quickprompt_app']::name[] AND cmd='ALL' AND permissive='PERMISSIVE'
                   AND encode(sha256(convert_to(qual || E'\n' || with_check,'UTF8')),'hex')='373703fd3fa693e6729b6d26c067dccefedec1ddb203d9e49bf9ea10333b74f4')
              AND EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='companion_presence'
                   AND policyname='companion_presence_maintenance' AND roles=ARRAY['quickprompt_migrator']::name[]
                   AND cmd='ALL' AND permissive='PERMISSIVE' AND qual='true' AND with_check='true')
              AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='qp_operations.companion_presence'::regclass
                   AND conname='bounded_companion_presence' AND contype='c' AND convalidated)
              AND EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='qp_operations.companion_presence'::regclass
                   AND conname='FK_companion_presence_connections_ConnectionId_OwnerId' AND contype='f' AND convalidated
                   AND confrelid='qp_operations.connections'::regclass AND confdeltype='c')
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
              AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='qp_library' AND table_name='prompts' AND column_name='DeletedAt')
              AND has_function_privilege(current_user,'qp_library.erase_private_prompt(uuid,uuid,integer)','EXECUTE')
              AND (SELECT count(*)=3 FROM pg_policies WHERE schemaname='qp_library' AND permissive='RESTRICTIVE'
                   AND 'quickprompt_app'=ANY(roles) AND (tablename,policyname,cmd) IN
                     (('prompts','erased_prompt_hidden','SELECT'),('prompt_versions','erased_version_hidden','SELECT'),
                      ('prompt_versions','erased_version_insert_rejected','INSERT')))
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
              -- Reviewed factual documents have their own immutable history and private review state.
              AND (SELECT count(*)=2 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                   WHERE (n.nspname,c.relname) IN (('qp_operations','factual_reviews'),('qp_library','factual_documents'))
                     AND c.relrowsecurity AND c.relforcerowsecurity AND c.relowner <> (SELECT oid FROM pg_roles WHERE rolname=current_user))
              -- Each required privilege is checked separately: a comma list tests ANY, not ALL.
              AND has_table_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'SELECT')
              AND has_table_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'INSERT')
              AND has_table_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'UPDATE')
              AND NOT has_table_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'DELETE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT has_any_column_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'REFERENCES')
              AND has_table_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'SELECT')
              AND has_table_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'INSERT')
              AND has_table_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'DELETE')
              AND NOT has_table_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'UPDATE,TRUNCATE,REFERENCES,TRIGGER')
              AND NOT has_any_column_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'UPDATE,REFERENCES')
              -- MAINTAIN does not exist before PostgreSQL 17; do not evaluate it on supported older servers.
              AND CASE WHEN current_setting('server_version_num')::int>=170000
                    THEN NOT has_table_privilege(current_user,to_regclass('qp_operations.factual_reviews')::oid,'MAINTAIN')
                     AND NOT has_table_privilege(current_user,to_regclass('qp_library.factual_documents')::oid,'MAINTAIN')
                    ELSE true END
              AND has_function_privilege(current_user,to_regprocedure('qp_operations.forget_factual_child(uuid,uuid)')::oid,'EXECUTE')
              AND NOT has_function_privilege(current_user,to_regprocedure('qp_operations.expire_factual_reviews()')::oid,'EXECUTE')
              AND NOT has_function_privilege(current_user,to_regprocedure('qp_library.validate_factual_document()')::oid,'EXECUTE')
              AND NOT has_function_privilege(current_user,to_regprocedure('qp_library.erase_factual_derivatives()')::oid,'EXECUTE')
              AND (SELECT count(*)=4 FROM (VALUES
                     ('qp_operations.forget_factual_child(uuid,uuid)',true),('qp_operations.expire_factual_reviews()',true),
                     ('qp_library.validate_factual_document()',false),('qp_library.erase_factual_derivatives()',true)) expected(identity,definer)
                   JOIN pg_proc p ON p.oid=to_regprocedure(expected.identity)::oid
                   WHERE pg_get_userbyid(p.proowner)='quickprompt_migrator' AND p.prosecdef=expected.definer
                     AND EXISTS (SELECT 1 FROM unnest(p.proconfig) setting WHERE replace(setting,' ','')='search_path=pg_catalog,pg_temp')
                     AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) permission
                                     WHERE permission.grantee=0 AND permission.privilege_type='EXECUTE'))
              AND (SELECT count(*)=2 FROM (VALUES
                     ('qp_library.factual_documents','factual_document_from_review','qp_library.validate_factual_document()'),
                     ('qp_library.prompts','erased_prompt_factual_content','qp_library.erase_factual_derivatives()')) expected(relation,name,procedure)
                   JOIN pg_trigger t ON t.tgrelid=to_regclass(expected.relation)::oid AND t.tgname=expected.name
                     AND t.tgfoid=to_regprocedure(expected.procedure)::oid
                   WHERE NOT t.tgisinternal AND t.tgenabled IN ('O','A'))
            FROM pg_roles WHERE rolname=current_user;
            """, connection);
        if (await command.ExecuteScalarAsync(cancellation) is not true)
            throw new InvalidOperationException("Account host requires a limited application role and migrated, forced-RLS tables. No security fallback is available.");
    }
    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}
