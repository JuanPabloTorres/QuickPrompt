# Startup evidence is observational, outside the immutable payload and credential
# store. Each attempt has its own file; concurrent launchers cannot overwrite it.
function New-CompanionStartupRecord([string]$Directory, [string]$ConfigPath) {
    try {
        $key=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($ConfigPath).ToUpperInvariant()))).ToLowerInvariant()
        $folder=Join-Path ([IO.Path]::GetFullPath($Directory)) $key
        $null=New-Item -ItemType Directory -Path $folder -Force
        if((Get-Item -LiteralPath $folder).LinkTarget){throw 'DIAGNOSTIC_DIRECTORY_LINK'}
        $attempt=[guid]::NewGuid().ToString('N')
        $record=@{Path=(Join-Path $folder ("launch-$attempt.json"));Data=[ordered]@{
            schema=1;kind='launcher-startup';attemptId=$attempt;configurationKey=$key;launcherPid=$PID;
            startedAtUtc=[DateTimeOffset]::UtcNow.ToString('O');updatedAtUtc=$null;phase='ENTERED';exitCode=$null;checks=@()
        }}
        Update-CompanionStartupRecord $record 'ENTERED'
        return $record
    } catch { [Console]::Error.WriteLine('No se pudo conservar el diagnóstico de arranque. Esto no autoriza ni impide la ejecución.'); return $null }
}

function Update-CompanionStartupRecord(
    [object]$Record,
    [ValidateSet('ENTERED','INVALID_PACKAGE_OR_CONFIGURATION','PREREQUISITES_BLOCKED','INVOKING_RUNTIME','RUNTIME_EXITED','RUNTIME_EXIT_UNCONFIRMED')][string]$Phase,
    [object[]]$Checks=@(), [Nullable[int]]$ExitCode=$null
) {
    if($null -eq $Record){return}
    $temporary=$null
    try {
        $ids=@('package-and-config','platform','dotnet','python','ollama','docker-model-runner')
        $codes=@('VALID','WINDOWS_READY','WINDOWS_REQUIRED_FOR_DPAPI','NETCORE_AND_ASPNETCORE_READY',
            'INSTALL_NET10_ASPNETCORE_RUNTIME_OR_CONFIGURE_DOTNET','PINNED_DEPENDENCIES_AND_IMPORTS_READY',
            'INSTALL_LOCKED_PYTHON_REQUIREMENTS_OR_CONFIGURE_PYTHON','MODEL_LISTED_NOT_INFERENCE_VERIFIED',
            'DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED','OLLAMA_API_REJECTED','OLLAMA_REPLY_TOO_LARGE',
            'QWEN3_8B_NOT_LISTED','OLLAMA_API_UNAVAILABLE','DMR_CONFIGURATION_NOT_READY','DMR_CONFIGURATION_INVALID',
            'DMR_API_REJECTED','DMR_REPLY_TOO_LARGE','DMR_API_UNAVAILABLE')
        $safeChecks=@(foreach($check in $Checks){
            if($check.id -cin $ids){[ordered]@{id=$check.id;passed=($check.passed -eq $true);
                code=$(if($check.code -cin $codes){$check.code}else{'CHECK_WITHOUT_SAFE_CODE'})}}
        })
        $Record.Data.phase=$Phase; $Record.Data.updatedAtUtc=[DateTimeOffset]::UtcNow.ToString('O')
        $Record.Data.exitCode=$ExitCode; $Record.Data.checks=$safeChecks
        $temporary=$Record.Path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
        [IO.File]::WriteAllText($temporary,($Record.Data | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary,$Record.Path,$true)
    } catch { [Console]::Error.WriteLine('No se pudo actualizar el diagnóstico de arranque. El resultado del componente no se ha sustituido.'); }
    finally { try{if($temporary -and [IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}catch{} }
}
