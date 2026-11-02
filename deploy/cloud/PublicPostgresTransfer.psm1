#requires -Version 7.2
Set-StrictMode -Version Latest

function Get-TransferTextHash([string]$Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant()
}

function Quote-TransferIdentifier([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or $Name.Contains([char]0)) { throw 'Invalid PostgreSQL identifier.' }
    '"' + $Name.Replace('"','""') + '"'
}

function Invoke-TransferScalar($Connection, [string]$Sql, $Transaction = $null) {
    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql; $command.CommandTimeout = 60
        if ($null -ne $Transaction) { $command.Transaction = $Transaction }
        return [string]$command.ExecuteScalar()
    } finally { $command.Dispose() }
}

function Invoke-TransferSql($Connection, [string]$Sql, $Transaction = $null) {
    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql; $command.CommandTimeout = 60
        if ($null -ne $Transaction) { $command.Transaction = $Transaction }
        $null = $command.ExecuteNonQuery()
    } finally { $command.Dispose() }
}

function Get-TransferInventory($Connection) {
    $sql = @'
SELECT jsonb_build_object(
 'database',current_database(), 'operator',current_user,
 'version',current_setting('server_version_num')::int,
 'canCreateDatabase',(SELECT rolcreatedb FROM pg_roles WHERE rolname=current_user),
 'canSetMigrator',pg_has_role(current_user,'quickprompt_migrator','SET'),
 'locale',(SELECT jsonb_build_object('encoding',pg_encoding_to_char(encoding),'provider',datlocprovider,'collate',datcollate,'ctype',datctype)
   FROM pg_database WHERE datname=current_database()),
 'relationBytes',(SELECT COALESCE(sum(pg_total_relation_size(c.oid)),0) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname LIKE 'qp_%' AND c.relkind IN ('r','m')),
 'largeObjects',(SELECT count(*) FROM pg_largeobject_metadata),
 'restrictivePublic',(SELECT count(*) FROM pg_policy p JOIN pg_class c ON c.oid=p.polrelid JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname LIKE 'qp_%' AND NOT p.polpermissive AND 0=ANY(p.polroles)),
 'schemas',(SELECT jsonb_agg(jsonb_build_object('name',nspname,'owner',pg_get_userbyid(nspowner)) ORDER BY nspname)
   FROM pg_namespace WHERE nspname LIKE 'qp_%'),
 'tables',(SELECT jsonb_agg(jsonb_build_object('schema',n.nspname,'name',c.relname,'owner',pg_get_userbyid(c.relowner),
   'rls',c.relrowsecurity,'force',c.relforcerowsecurity) ORDER BY n.nspname,c.relname)
   FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname LIKE 'qp_%' AND c.relkind='r'),
 'sequences',(SELECT jsonb_agg(jsonb_build_object('schema',n.nspname,'name',c.relname,'owner',pg_get_userbyid(c.relowner)) ORDER BY n.nspname,c.relname)
   FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname LIKE 'qp_%' AND c.relkind='S'),
 'unsupported',(SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
   WHERE n.nspname LIKE 'qp_%' AND c.relkind IN ('p','v','m','f'))
)::text;
'@
    return (Invoke-TransferScalar $Connection $sql | ConvertFrom-Json -AsHashtable)
}

function Assert-TransferInventory($Inventory, [string]$Database, [int]$Tables, [int]$RlsTables,
    [string[]]$AllowedLocaleProviders=@('c')) {
    $schemas = @('qp_host','qp_identity','qp_library','qp_migrations','qp_operations')
    if ($Inventory.database -cne $Database -or $Inventory.version -lt 180000 -or $Inventory.version -ge 190000 -or
        -not $Inventory.canSetMigrator -or $Inventory.largeObjects -ne 0 -or $Inventory.restrictivePublic -ne 0 -or
        $Inventory.relationBytes -gt 268435456 -or $Inventory.locale.encoding -cne 'UTF8' -or $Inventory.locale.provider -cnotin $AllowedLocaleProviders -or
        $Inventory.unsupported -ne 0 -or @($Inventory.schemas).Count -ne 5 -or @($Inventory.tables).Count -ne $Tables -or
        @($Inventory.sequences).Count -ne 3 -or @($Inventory.tables | Where-Object rls).Count -ne $RlsTables) {
        throw 'Database inventory differs from the reviewed PostgreSQL 18 transfer scope.'
    }
    foreach ($schema in $Inventory.schemas) {
        if ($schema.name -cnotin $schemas -or $schema.owner -cne 'quickprompt_migrator') { throw 'Unexpected schema or owner.' }
    }
    foreach ($table in $Inventory.tables) {
        if ($table.schema -cnotin $schemas -or $table.owner -cne 'quickprompt_migrator' -or $table.rls -ne $table.force) { throw 'Unexpected table or RLS owner.' }
    }
    foreach ($sequence in $Inventory.sequences) {
        if ($sequence.schema -cnotin $schemas -or $sequence.owner -cne 'quickprompt_migrator') { throw 'Unexpected sequence owner.' }
    }
}

function Get-TransferReaderSql($Inventory, [string]$Role, [string]$Policy, [string]$Operator) {
    if ($Role -cnotmatch '^qp_transfer_[a-f0-9]{32}$' -or $Policy -cnotmatch '^qp_transfer_[a-f0-9]{32}$') { throw 'A unique reviewed reader identifier is required.' }
    $roleName=Quote-TransferIdentifier $Role; $policyName=Quote-TransferIdentifier $Policy
    $schemaNames=($Inventory.schemas | ForEach-Object { Quote-TransferIdentifier $_.name }) -join ','
    $tableNames=($Inventory.tables | ForEach-Object { (Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name) }) -join ','
    $sequenceNames=($Inventory.sequences | ForEach-Object { (Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name) }) -join ','
    $createPolicies=@($Inventory.tables | Where-Object rls | ForEach-Object {
        'CREATE POLICY '+$policyName+' ON '+(Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name)+' FOR SELECT TO '+$roleName+' USING (true);'
    }) -join "`n"
    $dropPolicies=@($Inventory.tables | Where-Object rls | ForEach-Object {
        'DROP POLICY '+$policyName+' ON '+(Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name)+';'
    }) -join "`n"
    return @{
        createRole="CREATE ROLE $roleName NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;"
        prepare=@"
CREATE ROLE $roleName NOLOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
GRANT $roleName TO $(Quote-TransferIdentifier $Operator) WITH INHERIT FALSE, SET TRUE;
SET LOCAL ROLE quickprompt_migrator;
GRANT USAGE ON SCHEMA $schemaNames TO $roleName;
GRANT SELECT ON TABLE $tableNames TO $roleName;
GRANT SELECT ON SEQUENCE $sequenceNames TO $roleName;
$createPolicies
RESET ROLE;
"@
        cleanup=@"
SET LOCAL ROLE quickprompt_migrator;
$dropPolicies
REVOKE SELECT ON TABLE $tableNames FROM $roleName;
REVOKE SELECT ON SEQUENCE $sequenceNames FROM $roleName;
REVOKE USAGE ON SCHEMA $schemaNames FROM $roleName;
RESET ROLE;
DROP ROLE $roleName;
"@
    }
}

function Get-TransferRowsSql($Inventory) {
    $rows=@($Inventory.tables | ForEach-Object {
        $relation=(Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name)
        $literal=$relation.Replace("'","''")
        ('SELECT ''{0}'' AS relation,count(*) AS rows,encode(sha256(convert_to(COALESCE(string_agg(to_jsonb(t)::text,E''\n'' ORDER BY to_jsonb(t)::text COLLATE pg_catalog."C"),''''),''UTF8'')),''hex'') AS fingerprint FROM {1} t' -f $literal,$relation)
    })
    return 'SELECT jsonb_agg(to_jsonb(r) ORDER BY relation COLLATE pg_catalog."C")::text FROM ('+($rows -join ' UNION ALL ')+') r;'
}

function Get-TransferSequenceSql($Inventory) {
    $rows=@($Inventory.sequences | ForEach-Object {
        $relation=(Quote-TransferIdentifier $_.schema)+'.'+(Quote-TransferIdentifier $_.name)
        "SELECT '$($relation.Replace("'","''"))' AS relation,last_value,is_called FROM $relation"
    })
    'SELECT jsonb_agg(to_jsonb(s) ORDER BY relation)::text FROM ('+($rows -join ' UNION ALL ')+') s;'
}

function Get-TransferPendingExecutionSql {
    # ExecutionRecords maps ExecutionStatus through HasConversion<string>();
    # enum ordinals are not the persisted PostgreSQL contract.
    'SELECT count(*) FROM qp_operations.executions WHERE "Status" IN (''Queued'',''Running'');'
}

Export-ModuleMember -Function Get-TransferTextHash, Quote-TransferIdentifier, Invoke-TransferScalar, Invoke-TransferSql, Get-TransferInventory, Assert-TransferInventory, Get-TransferReaderSql, Get-TransferRowsSql, Get-TransferSequenceSql, Get-TransferPendingExecutionSql
