#requires -Version 7.2
param([Parameter(Mandatory)][string]$EvidenceDirectory,[string]$BaselineDirectory)
$ErrorActionPreference='Stop'
$testRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('QuickPromptStartupFixture-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $temporary
$null=New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$results=[Collections.Generic.List[object]]::new()
$fixtureShell=(Get-Process -Id $PID).Path
# Only synthetic dependency functions and PS stubs run. No DLL, model, OAuth,
# credential store, server, process worker or service is ever invoked.
$dependencyStubs=@'
function Assert-CompanionPackage([string]$Root) { }
function Resolve-CompanionExecutable([string]$Configured,[string]$Name,[string]$ConfigDirectory) {
    if(($env:QUICKPROMPT_STARTUP_FIXTURE_CASE -eq 'python' -and $Name -eq 'python') -or
       ($env:QUICKPROMPT_STARTUP_FIXTURE_CASE -eq 'dotnet' -and $Name -eq 'dotnet')){throw 'SYNTHETIC_PRIVATE_VALUE'}
    return $Configured
}
function Test-CompanionDmr([uri]$Endpoint,[string]$PhysicalModel) {
    if($env:QUICKPROMPT_STARTUP_FIXTURE_CASE -eq 'provider'){return 'DMR_API_UNAVAILABLE'}
    return 'DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED'
}
'@
function Check([string]$Name,[scriptblock]$Body){& $Body;$results.Add([pscustomobject]@{name=$Name;status='PASS'})}
function Invoke-Fixture([string]$Case,[string]$Source,[switch]$Old){
    $folder=Join-Path $temporary ([guid]::NewGuid().ToString('N'));$null=New-Item -ItemType Directory -Path $folder
    Copy-Item -LiteralPath (Join-Path $Source 'Companion.ps1') -Destination $folder
    $helper=(Get-Content -LiteralPath (Join-Path $Source 'Companion.Package.ps1') -Raw)+"`n"+$dependencyStubs
    [IO.File]::WriteAllText((Join-Path $folder 'Companion.Package.ps1'),$helper)
    [IO.File]::WriteAllText((Join-Path $folder 'Resolve-NovaDotnet.ps1'),'param($DotnetPath,$RuntimeConfigPath); $DotnetPath')
    [IO.File]::WriteAllText((Join-Path $folder 'python.ps1'),'Write-Output ''{"schema":1,"ready":true}''; $global:LASTEXITCODE=0')
    [IO.File]::WriteAllText((Join-Path $folder 'runtime.ps1'),@'
[IO.File]::AppendAllText((Join-Path $PSScriptRoot 'invoked.txt'),"one`n")
if($env:QUICKPROMPT_STARTUP_FIXTURE_CASE -eq 'runtime-throw'){throw 'SYNTHETIC_RUNTIME_EXCEPTION'}
$global:LASTEXITCODE=if($env:QUICKPROMPT_STARTUP_FIXTURE_CASE -eq 'runtime-two'){2}else{0}
'@)
    $config=Get-Content -LiteralPath (Join-Path $testRoot 'packaging/companion/companion.dmr.connected.example.json') -Raw | ConvertFrom-Json -AsHashtable
    $config.python=Join-Path $folder 'python.ps1';$config.dotnet=Join-Path $folder 'runtime.ps1'
    if($Case -eq 'configuration'){$config.token='SYNTHETIC_PRIVATE_VALUE'}
    $configPath=Join-Path $folder 'companion.json';$config | ConvertTo-Json | Set-Content -LiteralPath $configPath
    $observation=Join-Path $folder 'observations'
    if($Case -eq 'storage'){[IO.File]::WriteAllText($observation,'synthetic regular file, never delete as a directory')}
    $cmd=if($Case -eq 'doctor'){'doctor'}else{'run'}
    $fixtureArguments=@('-NoProfile','-File',(Join-Path $folder 'Companion.ps1'),'-Command',$cmd,'-ConfigPath',$configPath)
    if(-not $Old){$fixtureArguments+=@('-StartupEvidenceDirectory',$observation)}
    $previousCase=$env:QUICKPROMPT_STARTUP_FIXTURE_CASE
    try {
        $env:QUICKPROMPT_STARTUP_FIXTURE_CASE=$Case
        & $fixtureShell @fixtureArguments *> (Join-Path $folder 'console.txt')
        $exitCode=$LASTEXITCODE
    } finally {$env:QUICKPROMPT_STARTUP_FIXTURE_CASE=$previousCase}
    $records=@(if(Test-Path -LiteralPath $observation -PathType Container){Get-ChildItem -LiteralPath $observation -Recurse -File -Filter 'launch-*.json'})
    $record=if($records.Count -eq 1){Get-Content -LiteralPath $records[0].FullName -Raw | ConvertFrom-Json}else{$null}
    return [pscustomobject]@{ExitCode=$exitCode;Record=$record;RecordFiles=$records;Invoked=(Test-Path -LiteralPath (Join-Path $folder 'invoked.txt'));
        Console=(Get-Content -LiteralPath (Join-Path $folder 'console.txt') -Raw);Folder=$folder}
}
try {
    if($BaselineDirectory){
        $old=Invoke-Fixture 'python' $BaselineDirectory -Old
        $baseline=[ordered]@{reproduced=($old.ExitCode -ne 0 -and $null -eq $old.Record -and -not $old.Invoked);
            exitCode=$old.ExitCode;startupRecords=$old.RecordFiles.Count;runtimeInvoked=$old.Invoked;
            pythonFailureCodeShown=$old.Console.Contains('INSTALL_LOCKED_PYTHON_REQUIREMENTS_OR_CONFIGURE_PYTHON')}
        $baseline | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'baseline.json')
        if(-not $baseline.reproduced){throw 'Missing startup-evidence baseline was not reproduced.'}
    }
    $source=Join-Path $testRoot 'packaging/companion'
    foreach($case in @('configuration','python','dotnet','provider')){
        Check "Blocked $case records its phase and never invokes runtime" {
            $r=Invoke-Fixture $case $source
            $phase=if($case -eq 'configuration'){'INVALID_PACKAGE_OR_CONFIGURATION'}else{'PREREQUISITES_BLOCKED'}
            if($r.ExitCode -ne 2 -or $r.Invoked -or $r.Record.phase -ne $phase -or $r.Record.exitCode -ne 2){throw 'Blocked startup did not preserve its contract.'}
            if($case -ne 'configuration'){
                $failed=@($r.Record.checks | Where-Object { -not $_.passed })
                if($failed.Count -ne 1 -or -not $r.Console.Contains($failed[0].code)){throw 'Specific safe prerequisite code is missing.'}
            }
            if(($r.Record | ConvertTo-Json -Depth 6).Contains('SYNTHETIC_PRIVATE_VALUE') -or $r.Console.Contains('SYNTHETIC_PRIVATE_VALUE')){throw 'Private fixture value leaked.'}
        }
    }
    foreach($case in @('runtime-zero','runtime-two')){
        Check "$case preserves native exit and performs one invocation" {
            $r=Invoke-Fixture $case $source;$expected=if($case -eq 'runtime-two'){2}else{0}
            if($r.ExitCode -ne $expected -or -not $r.Invoked -or $r.Record.phase -ne 'RUNTIME_EXITED' -or $r.Record.exitCode -ne $expected){throw 'Runtime exit was altered.'}
            if(@(Get-Content -LiteralPath (Join-Path $r.Folder 'invoked.txt')).Count -ne 1){throw 'Runtime was repeated.'}
            if(($r.Record | ConvertTo-Json -Depth 6).Contains('example.invalid') -or ($r.Record | ConvertTo-Json -Depth 6).Contains($r.Folder)){throw 'Configuration content or paths leaked into metadata.'}
        }
    }
    Check 'Interrupted invocation remains unconfirmed and does not repeat' {
        $r=Invoke-Fixture 'runtime-throw' $source
        if($r.ExitCode -eq 0 -or $r.Record.phase -ne 'RUNTIME_EXIT_UNCONFIRMED' -or $null -ne $r.Record.exitCode){throw 'Unconfirmed exit was misclassified.'}
    }
    Check 'Unavailable diagnostic storage cannot replace successful runtime exit' {
        $r=Invoke-Fixture 'storage' $source
        if($r.ExitCode -ne 0 -or -not $r.Invoked -or $null -ne $r.Record){throw 'Observational storage affected runtime.'}
    }
    Check 'Doctor stays offline and creates no run evidence' {
        $r=Invoke-Fixture 'doctor' $source
        if($r.ExitCode -ne 0 -or $r.Invoked -or $null -ne $r.Record -or -not $r.Console.Contains('PREREQUISITES_READY')){throw 'Doctor acquired run effects.'}
    }
    . (Join-Path $source 'Companion.Package.ps1')
    Check 'Concurrent attempt identities stay separate and unknown codes are scrubbed' {
        $directory=Join-Path $temporary 'records';$a=New-CompanionStartupRecord $directory (Join-Path $temporary 'operator.json')
        $b=New-CompanionStartupRecord $directory (Join-Path $temporary 'operator.json')
        Update-CompanionStartupRecord $a 'PREREQUISITES_BLOCKED' @([pscustomobject]@{id='python';passed=$false;code='SYNTHETIC_PRIVATE_VALUE'},[pscustomobject]@{id='private';passed=$false;code='PRIVATE'}) 2
        $safe=Get-Content -LiteralPath $a.Path -Raw
        if($a.Path -eq $b.Path -or -not (Test-Path -LiteralPath $b.Path) -or $safe.Contains('SYNTHETIC_PRIVATE_VALUE') -or $safe.Contains('"private"') -or -not $safe.Contains('CHECK_WITHOUT_SAFE_CODE')){throw 'Attempt or sanitization invariant failed.'}
    }
    Check 'A later evidence-write failure cannot throw into the caller' {
        $record=New-CompanionStartupRecord (Join-Path $temporary 'late-write') (Join-Path $temporary 'operator.json')
        $record.Path=Split-Path $record.Path -Parent
        Update-CompanionStartupRecord $record 'RUNTIME_EXITED' @() 0
    }
    [ordered]@{schema=1;passed=$results.Count;failed=0;scope='Synthetic PowerShell bootstrap; real Companion, credentials, OAuth, model and services never invoked';checks=$results.ToArray()} |
        ConvertTo-Json -Depth 5 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'startup-diagnostics.json')
} finally {
    $resolved=[IO.Path]::GetFullPath($temporary)
    $allowed=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected fixture cleanup target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
$global:LASTEXITCODE=0
