#requires -Version 7.2
param([string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $root 'packaging/companion/Companion.Package.ps1')
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('QuickPromptPackageTest-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $temporary
$results = [Collections.Generic.List[object]]::new()
function Check([string]$Name,[scriptblock]$Body) {
    & $Body
    $results.Add([pscustomobject]@{name=$Name;status='PASS'})
}
function Reject([scriptblock]$Body) {
    $rejected=$false; try { & $Body | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Expected explicit rejection.' }
}
$configPath = Join-Path $temporary 'config.json'
$example = Get-Content -LiteralPath (Join-Path $root 'packaging/companion/companion.example.json') -Raw
function Write-TestConfiguration([scriptblock]$Change) {
    $data=$example | ConvertFrom-Json -AsHashtable
    & $Change $data
    $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
}
try {
    Check 'Explicit valid configuration has no developer paths' {
        Write-TestConfiguration { param($c) }
        $value=Read-CompanionConfiguration $configPath
        if ($value.capability -ne 'nova' -or $value.python -ne '' -or $example -match 'C:|Users|Projects') { throw 'Nonportable defaults.' }
    }
    Check 'Examples preserve separate NOVA twelve-job and prompt one-job profiles' {
        $nova=$example | ConvertFrom-Json
        $prompts=Get-Content -LiteralPath (Join-Path $root 'packaging/companion/companion.prompts.example.json') -Raw | ConvertFrom-Json
        if($nova.capability -ne 'nova' -or $nova.maximumJobs -ne 12 -or $prompts.capability -ne 'prompts' -or $prompts.maximumJobs -ne 1 -or $nova.slot -eq $prompts.slot){throw 'Existing profile batch configuration changed.'}
    }
    foreach($case in @('http-origin','origin-credentials','origin-path','remote-model','model-path','model-credentials','invalid-slot','invalid-capability','zero-jobs','too-many-jobs','fractional-jobs','secret-field','nullable-python')) {
        Check $case {
            Write-TestConfiguration { param($c)
                switch($case) {
                    'http-origin' {$c.server='http://example.invalid/'}
                    'origin-credentials' {$c.server='https://user:private@example.invalid/'}
                    'origin-path' {$c.server='https://example.invalid/other'}
                    'remote-model' {$c.endpoint='http://192.0.2.1:11434/'}
                    'model-path' {$c.endpoint='http://127.0.0.1:11434/api/chat'}
                    'model-credentials' {$c.endpoint='http://user:private@127.0.0.1:11434/'}
                    'invalid-slot' {$c.slot='../other'}
                    'invalid-capability' {$c.capability='admin'}
                    'zero-jobs' {$c.maximumJobs=0}
                    'too-many-jobs' {$c.maximumJobs=51}
                    'fractional-jobs' {$c.maximumJobs=1.5}
                    'secret-field' {$c.token='synthetic-must-not-be-accepted'}
                    'nullable-python' {$c.python=$null}
                }
            }
            Reject { Read-CompanionConfiguration $configPath }
        }
    }
    Check 'Both capabilities and fifty jobs use the same validated contract' {
        Write-TestConfiguration {param($c) $c.capability='prompts';$c.maximumJobs=50}
        if((Read-CompanionConfiguration $configPath).maximumJobs -ne 50){throw 'Valid explicit limit rejected.'}
    }
    Check 'Literal IPv6 loopback is valid without enabling a remote endpoint' {
        Write-TestConfiguration {param($c) $c.endpoint='http://[::1]:11434/'}
        if((Read-CompanionConfiguration $configPath).endpoint -ne 'http://[::1]:11434/'){throw 'IPv6 loopback changed.'}
    }
    $runtimeConfig=Join-Path $temporary 'runtimeconfig.json'
    '{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"},{"name":"Microsoft.AspNetCore.App","version":"10.0.14"}]}}' | Set-Content -LiteralPath $runtimeConfig
    $fakeDotnet=Join-Path $temporary 'synthetic-dotnet.ps1'
    Check 'NETCore alone cannot satisfy Companion ASPNETCore requirement' {
        'param($mode); $global:LASTEXITCODE=0; "Microsoft.NETCore.App 10.0.14 [synthetic]"' | Set-Content -LiteralPath $fakeDotnet
        Reject { & (Join-Path $root 'scripts/Resolve-NovaDotnet.ps1') -DotnetPath $fakeDotnet -RuntimeConfigPath $runtimeConfig }
    }
    Check 'Older installed patch cannot satisfy runtimeconfig' {
        'param($mode); $global:LASTEXITCODE=0; "Microsoft.NETCore.App 10.0.13 [synthetic]"; "Microsoft.AspNetCore.App 10.0.13 [synthetic]"' | Set-Content -LiteralPath $fakeDotnet
        Reject { & (Join-Path $root 'scripts/Resolve-NovaDotnet.ps1') -DotnetPath $fakeDotnet -RuntimeConfigPath $runtimeConfig }
    }
    Check 'Both required frameworks accept an existing compatible runtime' {
        'param($mode); $global:LASTEXITCODE=0; "Microsoft.NETCore.App 10.0.14 [synthetic]"; "Microsoft.AspNetCore.App 10.0.14 [synthetic]"' | Set-Content -LiteralPath $fakeDotnet
        $resolved=& (Join-Path $root 'scripts/Resolve-NovaDotnet.ps1') -DotnetPath $fakeDotnet -RuntimeConfigPath $runtimeConfig
        if($resolved -ne $fakeDotnet){throw 'Wrong selected runtime.'}
    }
    Check 'Missing package manifest is rejected before dispatch' { Reject { Assert-CompanionPackage $temporary } }
    $receipt=[pscustomobject]@{schema=1;scope='Synthetic package/config/runtime contract only';passed=$results.Count;failed=0;checks=$results.ToArray();modelCalls=0;workersStarted=0;servicesStarted=0;cleanMachineVerified=$false}
    if($EvidenceDirectory){$null=New-Item -ItemType Directory -Path $EvidenceDirectory -Force;$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'package-contract.json')}
    $receipt | ConvertTo-Json -Depth 5
} finally {
    $resolved=[IO.Path]::GetFullPath($temporary)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    if(-not $resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cleanup target.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
