#requires -Version 7.2
param([string]$EvidenceDirectory = '.playwright/postgres-restore')
$ErrorActionPreference = 'Stop'
$pgContainer = 'quickprompt-saas-postgres'
$pgProject = & docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}' $pgContainer
$pgScope = & docker inspect --format '{{index .Config.Labels "io.quickprompt.scope"}}' $pgContainer
if ($LASTEXITCODE -ne 0 -or $pgProject -ne 'quickprompt-saas-development' -or $pgScope -ne 'saas-development') {
    throw 'No se verificó el contenedor de desarrollo propio.'
}
$pgEvidence = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) $EvidenceDirectory))
$pgQaRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $PSScriptRoot) '.playwright')) + [IO.Path]::DirectorySeparatorChar
if (-not $pgEvidence.StartsWith($pgQaRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'La evidencia debe quedar dentro de .playwright.' }
[IO.Directory]::CreateDirectory($pgEvidence) | Out-Null
$pgRestore = 'quickprompt_restore_' + [Guid]::NewGuid().ToString('N')
$pgArchive = '/tmp/' + $pgRestore + '.dump'

function Invoke-DevelopmentPsql([string]$Database, [string]$Sql) {
    $pgOutput = $Sql | & docker exec -i $pgContainer psql -X -q -v ON_ERROR_STOP=1 -U quickprompt_bootstrap -d $Database -At 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'La comprobación PostgreSQL de desarrollo falló; no se imprimieron datos.' }
    return ($pgOutput -join "`n").Trim()
}

# Core rows and factual records have separate deterministic fingerprints. Both
# must match after restore; only hashes, never source contents, enter the receipt.
$pgFingerprintSql = @'
SELECT md5(COALESCE(string_agg(row_json, E'\n' ORDER BY row_json), ''))
FROM (
 SELECT 'user:' || row_to_json(u)::text AS row_json FROM qp_identity."AspNetUsers" u
 UNION ALL SELECT 'library:' || row_to_json(l)::text FROM qp_library.libraries l
 UNION ALL SELECT 'prompt:' || row_to_json(p)::text FROM qp_library.prompts p
 UNION ALL SELECT 'prompt-use:' || row_to_json(u)::text FROM qp_library.prompt_uses u
 UNION ALL SELECT 'version:' || row_to_json(v)::text FROM qp_library.prompt_versions v
 UNION ALL SELECT 'publication:' || row_to_json(p)::text FROM qp_library.publications p
 UNION ALL SELECT 'invitation:' || row_to_json(i)::text FROM qp_library.invitations i
 UNION ALL SELECT 'import:' || row_to_json(i)::text FROM qp_library.imports i
 UNION ALL SELECT 'capture:' || row_to_json(c)::text FROM qp_library.captures c
 UNION ALL SELECT 'agent-session:' || row_to_json(c)::text FROM qp_library.agent_sessions c
 UNION ALL SELECT 'agent-context:' || row_to_json(c)::text FROM qp_library.agent_contexts c
 UNION ALL SELECT 'agent-preference:' || row_to_json(p)::text FROM qp_library.agent_preferences p
 UNION ALL SELECT 'collection:' || row_to_json(c)::text FROM qp_library.collections c
 UNION ALL SELECT 'collection-member:' || row_to_json(m)::text FROM qp_library.collection_members m
 UNION ALL SELECT 'collection-import:' || row_to_json(i)::text FROM qp_library.collection_imports i
 UNION ALL SELECT 'execution:' || row_to_json(e)::text FROM qp_operations.executions e
 UNION ALL SELECT 'visible-run:' || row_to_json(r)::text FROM qp_operations.agent_runs r
 UNION ALL SELECT 'visible-event:' || row_to_json(e)::text FROM qp_operations.agent_run_events e
 UNION ALL SELECT 'execution-result:' || row_to_json(r)::text FROM qp_operations.execution_results r
 UNION ALL SELECT 'proposal-result:' || row_to_json(r)::text FROM qp_operations.proposal_results r
 UNION ALL SELECT 'connection:' || row_to_json(c)::text FROM qp_operations.connections c
 UNION ALL SELECT 'oauth-client:' || row_to_json(a)::text FROM qp_identity."OpenIddictApplications" a
 UNION ALL SELECT 'oauth-grant:' || row_to_json(a)::text FROM qp_identity."OpenIddictAuthorizations" a
 UNION ALL SELECT 'oauth-token:' || row_to_json(t)::text FROM qp_identity."OpenIddictTokens" t
 UNION ALL SELECT 'oauth-scope:' || row_to_json(s)::text FROM qp_identity."OpenIddictScopes" s
 UNION ALL SELECT 'migration:' || row_to_json(m)::text FROM qp_migrations.history m
 UNION ALL SELECT 'host-key:' || row_to_json(k)::text FROM qp_host.data_protection_keys k
) rows;
'@
$pgBefore = Invoke-DevelopmentPsql 'quickprompt_development' $pgFingerprintSql
$pgFactualDataSql = @'
SELECT md5(COALESCE(string_agg(row_json,E'\n' ORDER BY row_json),'')) FROM (
 SELECT 'factual-review:'||row_to_json(r)::text AS row_json FROM qp_operations.factual_reviews r
 UNION ALL SELECT 'factual-document:'||row_to_json(d)::text FROM qp_library.factual_documents d
) rows;
'@
$pgFactualContractSql = @'
SELECT md5(COALESCE(string_agg(definition,E'\n' ORDER BY definition),'')) FROM (
 SELECT 'table:'||n.nspname||'.'||c.relname||':'||pg_get_userbyid(c.relowner)||':'||c.relrowsecurity||':'||c.relforcerowsecurity AS definition
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE (n.nspname='qp_library' AND c.relname='factual_documents') OR (n.nspname='qp_operations' AND c.relname='factual_reviews')
 UNION ALL SELECT 'policy:'||row_to_json(p)::text FROM pg_policies p
 WHERE (schemaname='qp_library' AND tablename='factual_documents') OR (schemaname='qp_operations' AND tablename='factual_reviews')
   OR (schemaname='qp_operations' AND tablename='executions' AND policyname='factual_expiry_cancel')
 UNION ALL SELECT 'index:'||schemaname||'.'||tablename||':'||indexdef FROM pg_indexes
 WHERE (schemaname='qp_library' AND tablename='factual_documents') OR (schemaname='qp_operations' AND tablename='factual_reviews')
 UNION ALL SELECT 'constraint:'||n.nspname||'.'||t.relname||':'||c.conname||':'||pg_get_constraintdef(c.oid)||':'||c.convalidated
 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace n ON n.oid=t.relnamespace
 WHERE (n.nspname='qp_library' AND t.relname='factual_documents') OR (n.nspname='qp_operations' AND t.relname='factual_reviews')
 UNION ALL SELECT 'column:'||n.nspname||'.'||c.relname||':'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull
 FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE a.attnum>0 AND NOT a.attisdropped AND ((n.nspname='qp_library' AND c.relname='factual_documents') OR (n.nspname='qp_operations' AND c.relname='factual_reviews'))
 UNION ALL SELECT 'table-acl:'||n.nspname||'.'||c.relname||':'||pg_get_userbyid(p.grantor)||':'||CASE WHEN p.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(p.grantee) END||':'||p.privilege_type||':'||p.is_grantable
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace,
 LATERAL aclexplode(COALESCE(c.relacl,acldefault('r',c.relowner))) p
 WHERE (n.nspname='qp_library' AND c.relname='factual_documents') OR (n.nspname='qp_operations' AND c.relname='factual_reviews')
 UNION ALL SELECT 'function:'||n.nspname||'.'||p.proname||':'||pg_get_functiondef(p.oid)||':'||pg_get_userbyid(p.proowner)
 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE (n.nspname='qp_library' AND p.proname IN ('validate_factual_document','erase_factual_derivatives'))
 OR (n.nspname='qp_operations' AND p.proname IN ('expire_factual_reviews','forget_factual_child'))
 UNION ALL SELECT 'function-acl:'||n.nspname||'.'||p.proname||':'||pg_get_function_identity_arguments(p.oid)||':'||CASE WHEN a.grantee=0 THEN 'PUBLIC' ELSE pg_get_userbyid(a.grantee) END||':'||a.privilege_type||':'||a.is_grantable
 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace,
 LATERAL aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a
 WHERE (n.nspname='qp_library' AND p.proname IN ('validate_factual_document','erase_factual_derivatives'))
 OR (n.nspname='qp_operations' AND p.proname IN ('expire_factual_reviews','forget_factual_child'))
 UNION ALL SELECT 'trigger:'||pg_get_triggerdef(t.oid)||':'||t.tgenabled::text FROM pg_trigger t
 WHERE NOT t.tgisinternal AND t.tgname IN ('factual_document_from_review','erased_prompt_factual_content')
) definitions;
'@
$pgFactualSafetySql = @'
SELECT (SELECT count(*)=2 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE ((n.nspname='qp_library' AND c.relname='factual_documents') OR (n.nspname='qp_operations' AND c.relname='factual_reviews'))
 AND c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='quickprompt_migrator')
 AND (SELECT count(*)=4 FROM pg_policies WHERE
   (schemaname='qp_library' AND tablename='factual_documents' AND policyname IN ('factual_document_actor','factual_document_maintenance'))
   OR (schemaname='qp_operations' AND tablename='factual_reviews' AND policyname IN ('factual_review_actor','factual_review_maintenance')))
 AND has_table_privilege('quickprompt_app','qp_operations.factual_reviews','SELECT')
 AND has_table_privilege('quickprompt_app','qp_operations.factual_reviews','INSERT')
 AND has_table_privilege('quickprompt_app','qp_operations.factual_reviews','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_operations.factual_reviews','DELETE')
 AND has_table_privilege('quickprompt_app','qp_library.factual_documents','SELECT')
 AND has_table_privilege('quickprompt_app','qp_library.factual_documents','INSERT')
 AND has_table_privilege('quickprompt_app','qp_library.factual_documents','DELETE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.factual_documents','UPDATE')
 AND NOT has_table_privilege('quickprompt_execution','qp_operations.factual_reviews','SELECT,INSERT,UPDATE,DELETE')
 AND NOT has_table_privilege('quickprompt_execution','qp_library.factual_documents','SELECT,INSERT,UPDATE,DELETE')
 AND has_function_privilege('quickprompt_execution','qp_operations.expire_factual_reviews()','EXECUTE')
 AND NOT has_function_privilege('quickprompt_app','qp_operations.expire_factual_reviews()','EXECUTE')
 AND has_function_privilege('quickprompt_app','qp_operations.forget_factual_child(uuid,uuid)','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_operations.forget_factual_child(uuid,uuid)','EXECUTE')
 AND (SELECT count(*)=4 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE pg_get_userbyid(p.proowner)='quickprompt_migrator'
 AND ((n.nspname='qp_library' AND p.proname IN ('validate_factual_document','erase_factual_derivatives'))
 OR (n.nspname='qp_operations' AND p.proname IN ('expire_factual_reviews','forget_factual_child'))))
 AND (SELECT count(*)=2 FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE NOT t.tgisinternal AND t.tgenabled='O' AND n.nspname='qp_library'
 AND ((c.relname='factual_documents' AND t.tgname='factual_document_from_review') OR (c.relname='prompts' AND t.tgname='erased_prompt_factual_content')))
 AND NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace,
 LATERAL aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a WHERE a.grantee=0 AND a.privilege_type='EXECUTE'
 AND ((n.nspname='qp_library' AND p.proname IN ('validate_factual_document','erase_factual_derivatives'))
 OR (n.nspname='qp_operations' AND p.proname IN ('expire_factual_reviews','forget_factual_child'))))
 AND EXISTS (SELECT 1 FROM qp_library.factual_documents)
 AND EXISTS (SELECT 1 FROM qp_operations.factual_reviews)
 AND NOT EXISTS (SELECT 1 FROM qp_library.factual_documents d JOIN qp_library.prompts p ON p."LibraryId"=d."LibraryId" AND p."Id"=d."PromptId" WHERE p."DeletedAt" IS NOT NULL)
 AND NOT EXISTS (SELECT 1 FROM qp_operations.factual_reviews r JOIN qp_library.prompts p ON p."LibraryId"=r."LibraryId" AND p."Id"=r."PromptId"
 WHERE p."DeletedAt" IS NOT NULL AND (r."Status"<>'INVALIDATED' OR r."Source"<>'null'::jsonb OR r."Title"<>'' OR r."ProposalFacts"<>'[]'::jsonb
 OR r."ConfirmedFacts"<>'[]'::jsonb OR r."Preview" IS NOT NULL OR r."DocumentId" IS NOT NULL));
'@
$pgFactualDataBefore = Invoke-DevelopmentPsql 'quickprompt_development' $pgFactualDataSql
$pgFactualContractBefore = Invoke-DevelopmentPsql 'quickprompt_development' $pgFactualContractSql
if ((Invoke-DevelopmentPsql 'quickprompt_development' $pgFactualSafetySql) -ne 't') {
    throw 'Faltan datos factuales sintéticos o no cumplen sus protecciones antes del restore.'
}
$pgErasureContractSql = @'
SELECT md5(COALESCE(string_agg(definition,E'\n' ORDER BY definition),'')) FROM (
 SELECT 'policy:'||row_to_json(p)::text AS definition FROM pg_policies p
  WHERE schemaname='qp_library' AND policyname IN ('erased_prompt_hidden','erased_version_hidden','erased_version_insert_rejected',
    'erasure_collection_read','erasure_collection_change','erasure_member_read','erasure_member_delete')
 UNION ALL SELECT 'function:'||pg_get_functiondef(p.oid)||':'||pg_get_userbyid(p.proowner)||':'||COALESCE(p.proacl::text,'')
  FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='qp_library'
   AND p.proname IN ('erase_private_prompt','protect_erased_prompt','reject_erased_prompt_version')
 UNION ALL SELECT 'trigger:'||pg_get_triggerdef(t.oid) FROM pg_trigger t
  WHERE NOT t.tgisinternal AND t.tgname IN ('erased_prompt_immutable','erased_prompt_version_rejected')
 UNION ALL SELECT 'constraint:'||pg_get_constraintdef(c.oid) FROM pg_constraint c
  WHERE c.conrelid='qp_library.prompts'::regclass AND c.conname='private_prompt_erased_shape'
) definitions;
'@
$pgErasureContractBefore = Invoke-DevelopmentPsql 'quickprompt_development' $pgErasureContractSql
$pgErasureSafetySql = @'
SELECT (SELECT count(*)=3 FROM pg_policies WHERE schemaname='qp_library' AND permissive='RESTRICTIVE'
 AND 'quickprompt_app'=ANY(roles) AND policyname IN ('erased_prompt_hidden','erased_version_hidden','erased_version_insert_rejected'))
 AND has_function_privilege('quickprompt_app','qp_library.erase_private_prompt(uuid,uuid,integer)','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_library.erase_private_prompt(uuid,uuid,integer)','EXECUTE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.prompt_versions','UPDATE')
 AND EXISTS (SELECT 1 FROM qp_library.prompts WHERE "DeletedAt" IS NOT NULL)
 AND NOT EXISTS (SELECT 1 FROM qp_library.prompts WHERE "DeletedAt" IS NOT NULL
   AND ("Title"<>'' OR "Template"<>'' OR "Description"<>'' OR "Defaults"<>'{}'::jsonb))
 AND NOT EXISTS (SELECT 1 FROM qp_library.prompt_versions v JOIN qp_library.prompts p
   ON p."LibraryId"=v."LibraryId" AND p."Id"=v."PromptId" WHERE p."DeletedAt" IS NOT NULL
   AND (v."Title"<>'' OR v."Template"<>'' OR v."Description"<>'' OR v."Defaults"<>'{}'::jsonb));
'@
if ((Invoke-DevelopmentPsql 'quickprompt_development' $pgErasureSafetySql) -ne 't') {
    throw 'Faltan borrados sintéticos comprobados o protecciones de su contenido antes del restore.'
}
$pgSearchFunctionBefore = Invoke-DevelopmentPsql 'quickprompt_development' "SELECT md5(pg_get_functiondef('qp_library.search_text_latin(text)'::regprocedure));"
$pgMcpContractSql = @'
BEGIN;
-- pg_dump reparses CHECK expressions. PostgreSQL may render an array cast as
-- per-element casts afterwards. Parse both definitions once before comparing;
-- do not strip casts, terms, permissions or allowed values from the contract.
CREATE TEMP TABLE qp_restore_connection_contract (LIKE qp_operations.connections);
DO $canonical$
DECLARE definition text;
BEGIN
  SELECT pg_get_constraintdef(c.oid) INTO definition FROM pg_constraint c
    WHERE c.conrelid='qp_operations.connections'::regclass
      AND c.conname='bounded_connection' AND c.convalidated;
  IF definition IS NULL THEN RAISE EXCEPTION 'Missing validated connection contract'; END IF;
  EXECUTE 'ALTER TABLE pg_temp.qp_restore_connection_contract ADD CONSTRAINT canonical_contract ' || definition;
END $canonical$;
SELECT pg_get_constraintdef(c.oid) FROM pg_constraint c
WHERE c.conrelid='pg_temp.qp_restore_connection_contract'::regclass AND c.conname='canonical_contract';
ROLLBACK;
'@
$pgMcpContractBefore = Invoke-DevelopmentPsql 'quickprompt_development' $pgMcpContractSql
if (-not $pgMcpContractBefore) { throw 'El origen no tiene un contrato MCP validado para restaurar.' }
if ((Invoke-DevelopmentPsql 'quickprompt_development' 'SELECT count(*) FROM qp_library.prompts;') -eq '0') {
    throw 'Ejecuta primero la integración A/B con datos sintéticos; una restauración vacía no prueba el contrato.'
}
& docker exec $pgContainer pg_dump -U quickprompt_bootstrap -d quickprompt_development -Fc -f $pgArchive
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el backup sintético.' }
& docker cp "${pgContainer}:$pgArchive" (Join-Path $pgEvidence 'synthetic-accounts.dump')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo conservar el backup de prueba.' }
& docker exec $pgContainer createdb -U quickprompt_bootstrap -O quickprompt_migrator $pgRestore
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear una base de restauración nueva.' }
& docker exec $pgContainer pg_restore -U quickprompt_bootstrap --role=quickprompt_migrator -d $pgRestore --exit-on-error $pgArchive
if ($LASTEXITCODE -ne 0) { throw 'La restauración falló; se conserva la base para diagnóstico.' }
$pgAfter = Invoke-DevelopmentPsql $pgRestore $pgFingerprintSql
if ($pgBefore -ne $pgAfter) { throw 'La restauración no conserva las filas sintéticas.' }
if ($pgFactualDataBefore -ne (Invoke-DevelopmentPsql $pgRestore $pgFactualDataSql)) { throw 'La restauración cambió las revisiones o documentos factuales.' }
if ($pgFactualContractBefore -ne (Invoke-DevelopmentPsql $pgRestore $pgFactualContractSql)) { throw 'La restauración cambió el contrato factual de tablas, funciones, políticas, índices o permisos.' }
if ((Invoke-DevelopmentPsql $pgRestore $pgFactualSafetySql) -ne 't') { throw 'La restauración no conserva los permisos y la erasure factual.' }
if ($pgErasureContractBefore -ne (Invoke-DevelopmentPsql $pgRestore $pgErasureContractSql)) {
    throw 'La restauración cambió las políticas, funciones, triggers o restricciones del borrado privado.'
}
if ((Invoke-DevelopmentPsql $pgRestore $pgErasureSafetySql) -ne 't') {
    throw 'La restauración no conserva las tumbas sin contenido y sus protecciones de acceso.'
}
$pgSearchFunctionAfter = Invoke-DevelopmentPsql $pgRestore "SELECT md5(pg_get_functiondef('qp_library.search_text_latin(text)'::regprocedure));"
if ($pgSearchFunctionBefore -ne $pgSearchFunctionAfter) { throw 'La restauración cambió la función de búsqueda del catálogo.' }
$pgSearchPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT has_function_privilege('quickprompt_app','qp_library.search_text_latin(text)','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_library.search_text_latin(text)','EXECUTE')
 AND (SELECT count(*)=1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      WHERE n.nspname='qp_library' AND p.proname='search_text_latin' AND p.provolatile='i' AND NOT p.prosecdef
        AND pg_get_userbyid(p.proowner)='quickprompt_migrator'
        AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) entry
                        WHERE entry.grantee=0 AND entry.privilege_type='EXECUTE'));
'@
if ($pgSearchPrivileges -ne 't') { throw 'La restauración no conserva los límites de la función de búsqueda.' }
$pgPolicies = Invoke-DevelopmentPsql $pgRestore @'
SELECT count(*) FROM pg_class c JOIN pg_namespace n ON c.relnamespace=n.oid
WHERE ((n.nspname='qp_library' AND c.relname IN ('libraries','prompts','prompt_versions','publications','invitations','imports','agent_contexts','agent_preferences','captures','collections','collection_members','collection_imports','prompt_uses','agent_sessions','factual_documents'))
 OR (n.nspname='qp_operations' AND c.relname IN ('executions','execution_results','proposal_results','connections','agent_runs','agent_run_events','factual_reviews')))
AND c.relrowsecurity AND c.relforcerowsecurity AND pg_get_userbyid(c.relowner)='quickprompt_migrator';
'@
if ($pgPolicies -ne '22') { throw 'La restauración no conserva RLS y propiedad limitada.' }
$pgImmutable = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_column_privilege('quickprompt_app','qp_library.publications','Template','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.invitations','RecipientId','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.prompt_uses','UPDATE,DELETE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.prompt_versions','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.imports','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.publications','DELETE')
 AND has_column_privilege('quickprompt_app','qp_library.publications','RevokedAt','UPDATE')
 AND has_column_privilege('quickprompt_app','qp_library.invitations','State','UPDATE');
'@
if ($pgImmutable -ne 't') { throw 'La restauración no conserva los permisos de inmutabilidad y revocación.' }
$pgMemoryPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_column_privilege('quickprompt_app','qp_library.agent_contexts','OwnerId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.agent_contexts','SessionId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.agent_preferences','OwnerId','UPDATE')
 AND has_column_privilege('quickprompt_app','qp_library.agent_contexts','Revision','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.agent_sessions','OwnerId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.agent_sessions','LibraryId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.agent_sessions','SessionId','UPDATE')
 AND has_column_privilege('quickprompt_app','qp_library.agent_sessions','Payload','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.agent_runs','OwnerId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.agent_runs','HostToken','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.agent_runs','ConnectionId','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_operations.agent_run_events','UPDATE,DELETE')
 AND NOT has_table_privilege('quickprompt_execution','qp_operations.agent_runs','SELECT,INSERT,UPDATE,DELETE');
'@
if ($pgMemoryPrivileges -ne 't') { throw 'La restauración no conserva las fronteras de identidad de memoria.' }
$pgCollectionPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_column_privilege('quickprompt_app','qp_library.collections','OwnerId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.collections','LibraryId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.collections','Id','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.collection_members','UPDATE')
 AND has_column_privilege('quickprompt_app','qp_library.collections','Name','UPDATE')
 AND has_table_privilege('quickprompt_app','qp_library.collection_members','DELETE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.collection_imports','UPDATE,DELETE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.publications','CollectionId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.publications','CollectionSnapshot','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_library.invitations','IsCollection','UPDATE');
'@
if ($pgCollectionPrivileges -ne 't') { throw 'La restauración no conserva la identidad y permisos de colecciones.' }
$pgExecutionPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_table_privilege('quickprompt_app','qp_operations.executions','INSERT')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.executions','Status','UPDATE')
 AND has_column_privilege('quickprompt_app','qp_operations.executions','CancelRequested','UPDATE')
 AND NOT has_column_privilege('quickprompt_execution','qp_operations.executions','Payload','UPDATE')
 AND NOT has_column_privilege('quickprompt_execution','qp_operations.executions','ConnectionId','UPDATE')
 AND NOT has_table_privilege('quickprompt_execution','qp_operations.proposal_results','UPDATE,DELETE')
 AND NOT has_table_privilege('quickprompt_app','qp_operations.proposal_results','INSERT,UPDATE,DELETE')
 AND NOT has_table_privilege('quickprompt_execution','qp_operations.execution_results','UPDATE')
 AND NOT has_schema_privilege('quickprompt_execution','qp_library','USAGE')
 AND NOT has_schema_privilege('quickprompt_execution','qp_identity','USAGE');
'@
if ($pgExecutionPrivileges -ne 't') { throw 'La restauración no conserva las fronteras del dispatcher y resultados.' }
$pgRetention = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_column_privilege('quickprompt_app','qp_operations.executions','Payload','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.executions','ForgottenAt','UPDATE')
 AND NOT has_column_privilege('quickprompt_execution','qp_operations.executions','ForgottenAt','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_operations.execution_results','UPDATE,DELETE')
 AND has_function_privilege('quickprompt_app','qp_operations.forget_execution_content(uuid,uuid)','EXECUTE')
 AND NOT has_function_privilege('quickprompt_app','qp_operations.expire_execution_content()','EXECUTE')
 AND NOT has_function_privilege('quickprompt_app','qp_operations.erase_execution_content(uuid)','EXECUTE')
 AND has_function_privilege('quickprompt_execution','qp_operations.expire_execution_content()','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_operations.forget_execution_content(uuid,uuid)','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_operations.erase_execution_content(uuid)','EXECUTE')
 AND has_function_privilege('quickprompt_app','qp_operations.expire_agent_event_content()','EXECUTE')
 AND NOT has_function_privilege('quickprompt_execution','qp_operations.expire_agent_event_content()','EXECUTE')
 AND (SELECT count(*)=1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      WHERE n.nspname='qp_operations' AND p.proname='expire_agent_event_content'
       AND pg_get_userbyid(p.proowner)='quickprompt_migrator' AND p.prosecdef
       AND EXISTS (SELECT 1 FROM unnest(p.proconfig) setting WHERE replace(setting,' ','')='search_path=pg_catalog,pg_temp')
       AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) entry
                       WHERE entry.grantee=0 AND entry.privilege_type='EXECUTE'))
 AND (SELECT count(*)=3 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      WHERE n.nspname='qp_operations' AND p.proname IN ('erase_execution_content','forget_execution_content','expire_execution_content')
       AND pg_get_userbyid(p.proowner)='quickprompt_migrator' AND p.prosecdef=(p.proname<>'erase_execution_content')
       AND EXISTS (SELECT 1 FROM unnest(p.proconfig) setting WHERE replace(setting,' ','')='search_path=pg_catalog,pg_temp')
       AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) entry
                       WHERE entry.grantee=0 AND entry.privilege_type='EXECUTE'));
'@
if ($pgRetention -ne 't') { throw 'La restauración no conserva los límites de retención y olvido.' }
$pgDelegationPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT NOT has_column_privilege('quickprompt_app','qp_operations.connections','OwnerId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.connections','AuthorizationId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.connections','LibraryId','UPDATE')
 AND NOT has_column_privilege('quickprompt_app','qp_operations.connections','McpCapabilities','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.captures','UPDATE')
 AND NOT has_table_privilege('quickprompt_app','qp_library.captures','DELETE')
 AND NOT has_table_privilege('quickprompt_app','qp_operations.connections','DELETE')
 AND has_column_privilege('quickprompt_app','qp_operations.connections','RevokedAt','UPDATE')
 AND NOT has_table_privilege('quickprompt_execution','qp_operations.connections','SELECT,INSERT,UPDATE,DELETE');
'@
if ($pgDelegationPrivileges -ne 't') { throw 'La restauración no conserva las fronteras de consentimiento y revocación.' }
$pgMcpConstraint = Invoke-DevelopmentPsql $pgRestore @'
SELECT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='qp_operations.connections'::regclass
 AND c.conname='bounded_connection' AND position('McpCapabilities' in pg_get_constraintdef(c.oid))>0)
 AND NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='qp_operations' AND tablename='connections' AND policyname='connection_capability_upgrade');
'@
if ($pgMcpConstraint -ne 't') { throw 'La restauración no conserva el contrato de perfiles MCP por biblioteca.' }
$pgMcpContractAfter = Invoke-DevelopmentPsql $pgRestore $pgMcpContractSql
if ($pgMcpContractBefore -ne $pgMcpContractAfter) { throw 'La restauración cambió el contrato de capacidades MCP del origen.' }
$pgHostKeyPrivileges = Invoke-DevelopmentPsql $pgRestore @'
SELECT has_table_privilege('quickprompt_app','qp_host.data_protection_keys','SELECT')
 AND has_table_privilege('quickprompt_app','qp_host.data_protection_keys','INSERT')
 AND NOT has_table_privilege('quickprompt_app','qp_host.data_protection_keys','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
 AND NOT has_schema_privilege('quickprompt_app','qp_host','CREATE')
 AND NOT has_schema_privilege('quickprompt_execution','qp_host','USAGE')
 AND NOT has_table_privilege('quickprompt_execution','qp_host.data_protection_keys','SELECT,INSERT,UPDATE,DELETE')
 AND (SELECT pg_get_userbyid(c.relowner)='quickprompt_migrator' FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname='qp_host' AND c.relname='data_protection_keys')
 AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace,
      LATERAL aclexplode(COALESCE(c.relacl,acldefault('r',c.relowner))) permission
      WHERE n.nspname='qp_host' AND c.relname='data_protection_keys' AND permission.grantee=0);
'@
if ($pgHostKeyPrivileges -ne 't') { throw 'La restauración no conserva el keyring append-only aislado del dispatcher.' }
$pgEncryptedHostKeys = Invoke-DevelopmentPsql $pgRestore @'
SELECT count(*)>0 AND bool_and("Xml" IS NOT NULL AND
 (xpath_exists('/*[local-name()="key"]//*[local-name()="encryptedSecret"]', XMLPARSE(DOCUMENT "Xml"))
  OR xpath_exists('/*[local-name()="revocation"]', XMLPARSE(DOCUMENT "Xml"))))
FROM qp_host.data_protection_keys;
'@
if ($pgEncryptedHostKeys -ne 't') { throw 'Ejecuta el probe de persistencia cifrada de claves antes del restore; faltan claves cifradas comprobables.' }
$pgRlsRead = Invoke-DevelopmentPsql $pgRestore 'SET ROLE quickprompt_app; SELECT count(*) FROM qp_library.libraries; RESET ROLE;'
if ($pgRlsRead -ne '0') { throw 'La base restaurada revela filas sin contexto de actor.' }
@{ status='passed'; source='quickprompt_development'; restoredDatabase=$pgRestore; fingerprint=$pgAfter;
   archiveSha256=(Get-FileHash -LiteralPath (Join-Path $pgEvidence 'synthetic-accounts.dump') -Algorithm SHA256).Hash;
   forcedRlsTables=22; factualRowsSameAsSource=$true; factualContractSameAsSource=$true; factualErasureAndPrivileges=$true; factualFingerprint=$pgFactualDataBefore; privateErasureContractSameAsSource=$true; erasedContentAndVisibility=$true; immutableHostKeyPrivileges=$true; encryptedHostKeys=$true; immutableCollectionSharing=$true; immutableCollectionIdentity=$true; executionContentRetention=$true; mcpCapabilitiesConstraint=$true; mcpCapabilitiesContractSameAsSource=$true; immutableCaptureReceipts=$true; immutableDelegationPrivileges=$true; immutableSharingPrivileges=$true; immutableMemoryPrivileges=$true; immutableExecutionPrivileges=$true; immutableVisibleRunIdentity=$true; anonymousVisibleLibraries=0; checkedAt=[DateTimeOffset]::UtcNow.ToString('O') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $pgEvidence 'restore-result.json')
Write-Host 'Restauración comprobada: identidad, bibliotecas, prompts, revisiones, migraciones, propietario y RLS conservados. Base sintética retenida para inspección.'
