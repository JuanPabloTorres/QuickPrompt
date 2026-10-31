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
    $dmrExample=Get-Content -LiteralPath (Join-Path $root 'packaging/companion/companion.dmr.example.json') -Raw
    Check 'Explicit v2 DMR configuration preserves logical profile and immutable physical binding' {
        $dmrExample | Set-Content -LiteralPath $configPath
        $dmr=Read-CompanionConfiguration $configPath
        if($dmr.version -ne 2 -or $dmr.provider -cne 'docker-model-runner' -or $dmr.thinking -ne $false -or $dmr.endpoint -cne 'http://127.0.0.1:12434/'){throw 'DMR selection changed.'}
    }
    Check 'Legacy v1 cannot smuggle a provider field' {
        Write-TestConfiguration {param($c) $c.provider='docker-model-runner'}
        Reject {Read-CompanionConfiguration $configPath}
    }
    $connectedExample=Get-Content -LiteralPath (Join-Path $root 'packaging/companion/companion.dmr.connected.example.json') -Raw
    Check 'Explicit v3 connected mode has no ignored job limit' {
        $connectedExample | Set-Content -LiteralPath $configPath
        $value=Read-CompanionConfiguration $configPath
        if($value.version -ne 3 -or $value.sessionMode -cne 'connected' -or $value.Contains('maximumJobs')){throw 'Connected configuration mixed lifetime contracts.'}
        $arguments=@(Get-CompanionSessionArguments $value 'run')
        if($arguments.Count -ne 2 -or $arguments[0] -cne '--session-mode' -or $arguments[1] -cne 'connected'){throw 'Connected run arguments differ.'}
        foreach($command in @('status','pair','disconnect')) {
            if(@(Get-CompanionSessionArguments $value $command).Count -ne 0){throw 'Non-run command received session or job arguments.'}
        }
    }
    Check 'Older bounded formats retain their original job budgets' {
        foreach($source in @($example,$dmrExample)) {
            $source | Set-Content -LiteralPath $configPath
            $value=Read-CompanionConfiguration $configPath
            $arguments=@(Get-CompanionSessionArguments $value 'run')
            if($arguments.Count -ne 2 -or $arguments[0] -cne '--max-jobs' -or $arguments[1] -cne '12'){throw 'Bounded budget changed.'}
            if(@(Get-CompanionSessionArguments $value 'status').Count -ne 0){throw 'Status received ignored budget.'}
        }
    }
    foreach($case in @('maximum-jobs','bounded-mode','missing-mode','nonstring-mode','wrong-provider')) {
        Check ('connected-'+$case) {
            $data=$connectedExample | ConvertFrom-Json -AsHashtable
            switch($case) {
                'maximum-jobs' {$data.maximumJobs=1}
                'bounded-mode' {$data.sessionMode='bounded'}
                'missing-mode' {$data.Remove('sessionMode')}
                'nonstring-mode' {$data.sessionMode=$true}
                'wrong-provider' {$data.provider='ollama'}
            }
            $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
            Reject {Read-CompanionConfiguration $configPath}
        }
    }
    Check 'V2 remains strict and cannot acquire connected policy implicitly' {
        $data=$dmrExample | ConvertFrom-Json -AsHashtable; $data.sessionMode='connected'
        $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
        Reject {Read-CompanionConfiguration $configPath}
    }
    foreach($case in @('wrong-provider','unpinned-model','scheme-in-physical','wrong-port','thinking-string','overlong-physical','missing-physical','unknown-field')) {
        Check ('dmr-'+$case) {
            $data=$dmrExample | ConvertFrom-Json -AsHashtable
            switch($case) {
                'wrong-provider' {$data.provider='ollama'}
                'unpinned-model' {$data.physicalModel='docker.io/ai/qwen3:latest'}
                'scheme-in-physical' {$data.physicalModel='http://registry/model@sha256:'+('a'*64)}
                'wrong-port' {$data.endpoint='http://127.0.0.1:11434/'}
                'thinking-string' {$data.thinking='false'}
                'overlong-physical' {$data.physicalModel=('a'*169)+'/model@sha256:'+('a'*64)}
                'missing-physical' {$data.Remove('physicalModel')}
                'unknown-field' {$data.token='synthetic-never-accepted'}
            }
            $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
            Reject {Read-CompanionConfiguration $configPath}
        }
    }
    function New-DmrMetadata {
        return ,@(@{ModelID=($dmrExample | ConvertFrom-Json).physicalModel.Split('@')[1]; Backend='llama.cpp'; Mode='completion';
            Config=@{'context-size'=12288;'runtime-flags'=@('--parallel','1');llamacpp=@{'reasoning-budget'=0}}})
    }
    Check 'Thinking is opt-in in v2 and v3 and projects only to run' {
        foreach($profileExample in @($dmrExample, (Get-Content -LiteralPath (Join-Path $root 'packaging/companion/companion.dmr.connected.example.json') -Raw))) {
            $data=$profileExample | ConvertFrom-Json -AsHashtable
            if($data.thinking -ne $false){throw 'An existing example enabled thinking.'}
            $data.thinking=$true; $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
            $parsed=Read-CompanionConfiguration $configPath
            if(@(Get-CompanionThinkingArguments $parsed 'run') -join ' ' -cne '--thinking true'){throw 'Explicit opt-in was not projected.'}
            foreach($command in @('status','pair','disconnect')) {
                if(@(Get-CompanionThinkingArguments $parsed $command).Count -ne 0){throw 'A non-run command acquired inference options.'}
            }
            $parsed.thinking=$false
            if(@(Get-CompanionThinkingArguments $parsed 'run') -join ' ' -cne '--thinking false'){throw 'The nonthinking profile changed.'}
        }
        $data=$example | ConvertFrom-Json -AsHashtable; $data.thinking=$true
        $data | ConvertTo-Json | Set-Content -LiteralPath $configPath
        Reject {Read-CompanionConfiguration $configPath}
    }
    foreach($case in @(@{thinking=$false;budget=0;ready=$true},@{thinking=$false;budget=-1;ready=$true},
        @{thinking=$true;budget=-1;ready=$true},@{thinking=$true;budget=0;ready=$false},
        @{thinking=$false;budget=1;ready=$false},@{thinking=$true;budget=$true;ready=$false})) {
        Check ('DMR doctor thinking='+$case.thinking+' budget='+$case.budget) {
            $metadata=New-DmrMetadata; $metadata[0].Config.llamacpp['reasoning-budget']=$case.budget
            $actual=Test-CompanionDmrMetadata $metadata ($dmrExample | ConvertFrom-Json).physicalModel $case.thinking
            if(($actual -ceq 'DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED') -ne $case.ready){throw 'Reasoning capability contract failed.'}
        }
    }
    Check 'DMR doctor metadata does not claim inference when identity and limits match' {
        $metadata=New-DmrMetadata
        if((Test-CompanionDmrMetadata $metadata ($dmrExample | ConvertFrom-Json).physicalModel) -cne 'DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED'){throw 'Metadata contract failed.'}
    }
    foreach($case in @('wrong-digest','array-digest','wrong-backend','wrong-mode','wrong-context','wrong-reasoning','reasoning-boolean','parallelism','numeric-runtime-flag','extra-record','missing-config')) {
        Check ('dmr-metadata-'+$case) {
            $metadata=New-DmrMetadata
            switch($case) {
                'wrong-digest' {$metadata[0].ModelID='sha256:'+('0'*64)}
                'array-digest' {$metadata[0].ModelID=@()}
                'wrong-backend' {$metadata[0].Backend='unknown'}
                'wrong-mode' {$metadata[0].Mode='embedding'}
                'wrong-context' {$metadata[0].Config['context-size']=8192}
                'wrong-reasoning' {$metadata[0].Config.llamacpp['reasoning-budget']=1}
                'reasoning-boolean' {$metadata[0].Config.llamacpp['reasoning-budget']=$false}
                'parallelism' {$metadata[0].Config['runtime-flags']=@('--parallel','2')}
                'numeric-runtime-flag' {$metadata[0].Config['runtime-flags']=@('--parallel',1)}
                'extra-record' {$metadata+= $metadata[0]}
                'missing-config' {$metadata[0].Remove('Config')}
            }
            if((Test-CompanionDmrMetadata $metadata ($dmrExample | ConvertFrom-Json).physicalModel) -cne 'DMR_CONFIGURATION_NOT_READY'){throw 'Invalid metadata was accepted.'}
        }
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
