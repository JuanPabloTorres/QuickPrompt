#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$NativeClientDirectory,
    [string]$ReceiptPath
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgres.psm1') -Force
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgresTransfer.psm1') -Force -DisableNameChecking
Import-Module (Join-Path $repository 'deploy/cloud/PublicPostgresNative.psm1') -Force
$null=Import-PublicPostgresDriver $repository
$null=Get-TransferNativeRuntime $NativeClientDirectory
$ports=@(55439,55440)
if(@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue|Where-Object LocalPort -In $ports).Count){throw 'Synthetic fixture ports occupied; no adoption.'}
$root=New-TransferPrivateDirectory (Join-Path ([IO.Path]::GetTempPath()) ('quickprompt-transfer-fixture-'+[Guid]::NewGuid().ToString('N'))) $repository
$role='qp_transfer_fixture';$identity=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$accountParts=$identity.Split('\');$principal=$accountParts[1]+'@'+$accountParts[0]
$source=$null;$target=$null;$snapshot=$null;$started=@();$checks=[Collections.Generic.List[string]]::new();$failure=$null
$key=[Security.Cryptography.RSA]::Create(3072)
try{
    $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=QuickPrompt synthetic transfer TLS',$key,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true,$false,0,$true))
    $san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$san.AddIpAddress([Net.IPAddress]::Loopback)
    $request.CertificateExtensions.Add($san.Build())
    $certificate=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(1))
    try{
        $certificatePath=New-TransferPrivateFile (Join-Path $root 'server.crt') $repository
        [IO.File]::WriteAllText($certificatePath,$certificate.ExportCertificatePem())
        $keyPath=New-TransferPrivateFile (Join-Path $root 'server.key') $repository
        [IO.File]::WriteAllText($keyPath,$key.ExportRSAPrivateKeyPem())
    }finally{$certificate.Dispose()}
    foreach($port in $ports){
        $cluster=New-TransferPrivateDirectory (Join-Path $root ([string]$port)) $repository
        $data=Join-Path $cluster 'data'
        $initOutput=& (Join-Path $NativeClientDirectory 'initdb.exe') '-D' $data '--username' 'qp_fixture_bootstrap' '--auth-host=sspi' '--auth-local=reject' '--encoding=UTF8' '--no-locale' 2>&1
        if($LASTEXITCODE -ne 0){throw 'Synthetic initdb failed.'};$initOutput=$null
        [IO.File]::WriteAllText((Join-Path $data 'pg_hba.conf'),"host all $role,qp_fixture_bootstrap 127.0.0.1/32 sspi include_realm=1 compat_realm=1 map=qp_transfer_fixture`nhost all all 127.0.0.1/32 reject`nhost all all ::1/128 reject`n")
        [IO.File]::WriteAllText((Join-Path $data 'pg_ident.conf'),"qp_transfer_fixture $principal $role`nqp_transfer_fixture $principal qp_fixture_bootstrap`n")
        $configuration="listen_addresses='127.0.0.1'`nport=$port`nunix_socket_directories=''`nssl=on`nssl_cert_file='$($certificatePath.Replace('\','/'))'`nssl_key_file='$($keyPath.Replace('\','/'))'`nmax_connections=8`nshared_buffers='16MB'`nTimeZone='UTC'`nlog_statement='none'`nlog_min_error_statement='panic'`n"
        [IO.File]::AppendAllText((Join-Path $data 'postgresql.conf'),"`n"+$configuration)
        $started+=@{port=$port;data=$data;root=$cluster}
        $launcher=Start-Process -FilePath (Join-Path $NativeClientDirectory 'pg_ctl.exe') -ArgumentList @('-D',('"'+$data+'"'),'-l',('"'+(Join-Path $cluster 'postgres.log')+'"'),'-w','-t','30','start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $cluster 'start.stdout') -RedirectStandardError (Join-Path $cluster 'start.stderr')
        try{if(-not $launcher.WaitForExit(40000) -or $launcher.ExitCode -ne 0){throw 'Synthetic cluster start failed.'}}finally{$launcher.Dispose()}
        $builder=[Npgsql.NpgsqlConnectionStringBuilder]::new();$builder.Host='127.0.0.1';$builder.Port=$port;$builder.Username='qp_fixture_bootstrap';$builder.Database='postgres';$builder.SslMode='VerifyFull';$builder.RootCertificate=$certificatePath;$builder.Pooling=$false;$builder.Timeout=10
        $connection=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$connection.Open()
        Invoke-TransferSql $connection 'CREATE ROLE quickprompt_migrator NOLOGIN;CREATE ROLE quickprompt_app NOLOGIN;CREATE ROLE quickprompt_execution NOLOGIN;'
        Invoke-TransferSql $connection ('CREATE ROLE '+$role+' LOGIN NOINHERIT CREATEDB CREATEROLE NOSUPERUSER NOBYPASSRLS;GRANT quickprompt_migrator,quickprompt_app TO '+$role+' WITH SET TRUE, INHERIT FALSE;')
        $connection.Dispose();$builder.Username=$role
        $connection=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$connection.Open()
        Invoke-TransferSql $connection ('CREATE DATABASE transfer_fixture OWNER '+$role+';')
        $connection.Dispose();$builder.Database='transfer_fixture'
        $connection=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$connection.Open()
        Invoke-TransferSql $connection 'REVOKE CONNECT ON DATABASE transfer_fixture FROM PUBLIC;GRANT CONNECT,CREATE ON DATABASE transfer_fixture TO quickprompt_migrator;'
        if((Invoke-TransferScalar $connection 'SELECT system_user;') -cne ('sspi:'+$identity)){throw 'Synthetic Windows identity differs.'}
        if(-not (Get-PublicPostgresCapabilities $connection).tls){throw 'Synthetic client TLS not verified.'}
        if($port -eq $ports[0]){$source=$connection}else{$target=$connection}
    }
    $transaction=$source.BeginTransaction()
    try{
        Invoke-TransferSql $source @'
SET LOCAL ROLE quickprompt_migrator;
CREATE SCHEMA qp_identity AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_library AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_host AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_migrations AUTHORIZATION quickprompt_migrator;
CREATE SCHEMA qp_operations AUTHORIZATION quickprompt_migrator;
CREATE TABLE qp_identity.users(id uuid PRIMARY KEY,email text NOT NULL,password_hash text NOT NULL);
CREATE TABLE qp_library.prompts(id uuid PRIMARY KEY,owner_id uuid REFERENCES qp_identity.users,title text,template text);
CREATE TABLE qp_host.keys(id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,xml text);
CREATE TABLE qp_operations.executions(id integer PRIMARY KEY,"Status" character varying(24) NOT NULL);
INSERT INTO qp_operations.executions VALUES(1,'Completed'),(2,'Failed'),(3,'Cancelled'),(4,'Interrupted');
INSERT INTO qp_identity.users VALUES ('11111111-1111-1111-1111-111111111111','a@example.invalid','SYNTHETIC-HASH-A'),('22222222-2222-2222-2222-222222222222','b@example.invalid','SYNTHETIC-HASH-B');
INSERT INTO qp_library.prompts VALUES ('33333333-3333-3333-3333-333333333333','11111111-1111-1111-1111-111111111111','Español y comillas','Conceptualiza {{nombre_proyecto}}: propósito, tipo y ''restricciones''.'),('44444444-4444-4444-4444-444444444444','22222222-2222-2222-2222-222222222222','Otra biblioteca',E'Línea uno\n{{proposito}}');
INSERT INTO qp_host.keys(xml) VALUES ('<key>synthetic-only</key>');
ALTER TABLE qp_library.prompts ENABLE ROW LEVEL SECURITY;
ALTER TABLE qp_library.prompts FORCE ROW LEVEL SECURITY;
CREATE POLICY owner_only ON qp_library.prompts TO quickprompt_app USING(owner_id=nullif(current_setting('quickprompt.actor_id',true),'')::uuid);
GRANT USAGE ON SCHEMA qp_library TO quickprompt_app;
GRANT SELECT ON qp_library.prompts TO quickprompt_app;
RESET ROLE;
'@ $transaction
        $transaction.Commit()
    }finally{$transaction.Dispose()}
    $transaction=$source.BeginTransaction()
    try{
        Invoke-TransferSql $source 'SET LOCAL ROLE quickprompt_migrator;INSERT INTO qp_operations.executions VALUES(5,''Queued''),(6,''Running'');' $transaction
        if((Invoke-TransferScalar $source (Get-TransferPendingExecutionSql) $transaction) -cne '2'){throw 'Pending execution guard missed queued/running string states.'}
    }finally{$transaction.Rollback();$transaction.Dispose()}
    $checks.Add('Pending execution guard rejects Queued/Running persisted strings and ignores terminal states')
    $inventory=Get-TransferInventory $source
    $catalogSql=[IO.File]::ReadAllText((Join-Path $repository 'deploy/cloud/sql/transfer-catalog.sql'))
    $original=Invoke-TransferScalar $source $catalogSql
    $reader='qp_transfer_'+[Guid]::NewGuid().ToString('N')
    $sql=Get-TransferReaderSql $inventory $reader $reader $role
    $transaction=$source.BeginTransaction();try{Invoke-TransferSql $source $sql.prepare $transaction;$transaction.Commit()}finally{$transaction.Dispose()}
    $snapshot=$source.BeginTransaction([Data.IsolationLevel]::RepeatableRead)
    Invoke-TransferSql $source ('SET TRANSACTION READ ONLY;SET LOCAL ROLE '+(Quote-TransferIdentifier $reader)+";SET LOCAL TimeZone='UTC';SET LOCAL DateStyle='ISO,YMD';SET LOCAL extra_float_digits=1;") $snapshot
    if((Invoke-TransferScalar $source (Get-TransferPendingExecutionSql) $snapshot) -cne '0'){throw 'Drained execution guard rejected terminal string states.'}
    $snapshotId=Invoke-TransferScalar $source 'SELECT pg_export_snapshot();' $snapshot
    $rows=Invoke-TransferScalar $source (Get-TransferRowsSql $inventory) $snapshot
    $sequences=Invoke-TransferScalar $source (Get-TransferSequenceSql $inventory) $snapshot
    $pass=New-TransferPrivateFile (Join-Path $root 'empty.pgpass') $repository
    $dump=Join-Path $root 'fixture.dump'
    $dumpArgs=@('--host','127.0.0.1','--port',[string]$ports[0],'--username',$role,'--dbname','transfer_fixture','--role',$reader,'--snapshot',$snapshotId,'--enable-row-security','--inserts','--format=custom','--extra-float-digits=1','--no-password')
    foreach($schema in $inventory.schemas){$dumpArgs+=@('--schema',$schema.name)}
    $start=New-TransferNativeStart (Join-Path $NativeClientDirectory 'pg_dump.exe') $dumpArgs $pass $certificatePath
    Invoke-TransferNative $start $dump (Join-Path $root 'dump.stderr') $repository
    $snapshot.Rollback();$snapshot.Dispose();$snapshot=$null
    $transaction=$source.BeginTransaction();try{Invoke-TransferSql $source $sql.cleanup $transaction;$transaction.Commit()}finally{$transaction.Dispose()}
    $cleaned=Invoke-TransferScalar $source $catalogSql
    if($cleaned -cne $original){
        $beforeItems=foreach($item in (ConvertFrom-Json -InputObject $original -NoEnumerate)){ConvertTo-Json -InputObject $item -Depth 20 -Compress}
        $afterItems=foreach($item in (ConvertFrom-Json -InputObject $cleaned -NoEnumerate)){ConvertTo-Json -InputObject $item -Depth 20 -Compress}
        $difference=@(Compare-Object $beforeItems $afterItems)
        [IO.File]::WriteAllText((Join-Path $root 'synthetic-catalog-difference.json'),($difference|ConvertTo-Json -Depth 5))
        throw 'Source privileges did not return to baseline.'
    }
    $checks.Add('Shared-snapshot native pg_dump under NOLOGIN/NOBYPASSRLS reader; exact source cleanup')
    $cipher=Protect-TransferArchive $dump (Join-Path $root 'fixture.dump.dpapi') 'synthetic-transfer' $repository
    $checks.Add('DPAPI current-user encryption and roundtrip')
    Invoke-TransferSql $target $sql.createRole
    Invoke-TransferSql $target ('GRANT '+(Quote-TransferIdentifier $reader)+' TO '+(Quote-TransferIdentifier $role)+' WITH SET TRUE, INHERIT FALSE;')
    $restoreArgs=@('--host','127.0.0.1','--port',[string]$ports[1],'--username',$role,'--dbname','transfer_fixture','--role','quickprompt_migrator','--single-transaction','--exit-on-error','--no-owner','--enable-row-security','--no-password',$dump)
    $start=New-TransferNativeStart (Join-Path $NativeClientDirectory 'pg_restore.exe') $restoreArgs $pass $certificatePath
    Invoke-TransferNative $start (Join-Path $root 'restore.stdout') (Join-Path $root 'restore.stderr') $repository
    $transaction=$target.BeginTransaction()
    try{
        Invoke-TransferSql $target ('SET TRANSACTION READ ONLY;SET LOCAL ROLE '+(Quote-TransferIdentifier $reader)+";SET LOCAL TimeZone='UTC';SET LOCAL DateStyle='ISO,YMD';SET LOCAL extra_float_digits=1;") $transaction
        if((Invoke-TransferScalar $target (Get-TransferRowsSql $inventory) $transaction) -cne $rows -or (Invoke-TransferScalar $target (Get-TransferSequenceSql $inventory) $transaction) -cne $sequences){throw 'Synthetic native restore fingerprints differ.'}
    }finally{$transaction.Rollback();$transaction.Dispose()}
    $transaction=$target.BeginTransaction();try{Invoke-TransferSql $target $sql.cleanup $transaction;$transaction.Commit()}finally{$transaction.Dispose()}
    if((Invoke-TransferScalar $target $catalogSql) -cne $original){throw 'Restored privileges or schema differ.'}
    $checks.Add('Native single-transaction restore preserves all data, sequences and exact catalog/ACL')
    Invoke-TransferSql $target 'REVOKE CREATE ON DATABASE transfer_fixture FROM quickprompt_migrator;'
    # Exercise the rename cut with the CREATEDB operator, not the schema owner or superuser.
    $target.Dispose();$target=$null;$builder.Database='postgres'
    $control=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$control.Open()
    try{
        Invoke-TransferSql $control 'ALTER DATABASE transfer_fixture RENAME TO transfer_import;'
        Invoke-TransferSql $control 'CREATE DATABASE transfer_fixture;'
        $builder.Database='transfer_import'
        $target=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$target.Open()
        Invoke-TransferSql $target 'ALTER DATABASE transfer_fixture ALLOW_CONNECTIONS false;ALTER DATABASE transfer_fixture RENAME TO transfer_before;ALTER DATABASE transfer_before ALLOW_CONNECTIONS true;'
        $target.Dispose();$target=$null
        Invoke-TransferSql $control 'ALTER DATABASE transfer_import RENAME TO transfer_fixture;'
        $builder.Database='transfer_fixture'
        $target=[Npgsql.NpgsqlConnection]::new($builder.ConnectionString);$target.Open()
    }finally{$control.Dispose()}
    $checks.Add('Database rename promotion succeeds with non-superuser operator and SET-only migrator membership')
    $transaction=$target.BeginTransaction();try{
        Invoke-TransferSql $target "SET LOCAL ROLE quickprompt_app;SELECT set_config('quickprompt.actor_id','11111111-1111-1111-1111-111111111111',true);" $transaction
        if((Invoke-TransferScalar $target 'SELECT count(*) FROM qp_library.prompts;' $transaction) -ne '1'){throw 'Restored RLS isolation failed.'}
    }finally{$transaction.Rollback();$transaction.Dispose()}
    $checks.Add('Restored application RLS isolates the two synthetic owners')
}catch{$failure=$_.Exception.GetType().Name; $fixtureDiagnostic=@{line=$_.InvocationInfo.ScriptLineNumber;message=$_.Exception.Message}}
finally{
    if($snapshot){try{$snapshot.Rollback()}catch{};$snapshot.Dispose()}
    if($source){$source.Dispose()};if($target){$target.Dispose()};$key.Dispose()
    foreach($cluster in $started){
        if([IO.File]::Exists((Join-Path $cluster.data 'postmaster.pid'))){
            $stopOutput=& (Join-Path $NativeClientDirectory 'pg_ctl.exe') '-D' $cluster.data '-m' 'fast' '-w' '-t' '30' 'stop' 2>&1
            if($LASTEXITCODE -ne 0){$failure='OwnedClusterStopUnconfirmed'};$stopOutput=$null
        }
    }
}
$receipt=@{verdict=if($failure){'FAIL'}else{'PASS'};checks=$checks.ToArray();privateDataUsed=$false;identity='Current Windows SSPI principal only';tls='VerifyFull with fixture-only certificate file; no trust-store modification';fixtureRoot=$root;clustersStopped=$failure -ne 'OwnedClusterStopUnconfirmed';failure=$failure;syntheticDiagnostic=$fixtureDiagnostic}
$json=$receipt|ConvertTo-Json -Depth 5
if($ReceiptPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReceiptPath),$json)}
$json
if($failure){exit 1}
