param([Parameter(Mandatory=$true)][string]$Contract,[Parameter(Mandatory=$true)][string]$Python,
    [Parameter(Mandatory=$true)][string]$SdkRoot,[Parameter(Mandatory=$true)][string]$LabRoot,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9-]+$')][string]$Candidate,
    [ValidatePattern('^[a-z0-9-]+$')][string]$FrozenCandidate='',
    [switch]$PrepareOnly)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$goal=Get-Content -Raw -LiteralPath $Contract | ConvertFrom-Json
if($goal.schema -ne 'ai-delivery-goal/1'){throw 'Unsupported goal contract'}
$fixtureSplit='development'
$fixtureSelection=Join-Path (Split-Path -Parent $Contract) ($goal.id+'.source-fixtures.json')
if(Test-Path -LiteralPath $fixtureSelection){
    $selection=Get-Content -Raw -LiteralPath $fixtureSelection|ConvertFrom-Json
    if($selection.schema -ne 'quickprompt-goal-source/1' -or $selection.goalId -ne $goal.id -or
        $selection.goalHash -ne $goal.hash -or $selection.fixtureSplit -notin @('development','validation')){throw 'Invalid goal-bound source fixture selection'}
    $fixtureSplit=$selection.fixtureSplit
}
$fixtureDataset=Join-Path $PSScriptRoot ('datasets/recovery-v1/'+$fixtureSplit+'.json')
$base=Get-Content -Raw -LiteralPath $fixtureDataset | ConvertFrom-Json
$directory=Join-Path (Get-Location) ('.playwright/closure-lab-20261021/goal-datasets/'+$Candidate)
if(Test-Path -LiteralPath $directory){throw 'Preserve prior diagnostic dataset'}
New-Item -ItemType Directory -Path $directory | Out-Null
if(Test-Path -LiteralPath $fixtureSelection){Copy-Item -LiteralPath $fixtureSelection -Destination (Join-Path $directory 'source-fixtures.json')}
$message=$goal.request
foreach($property in $goal.inputs.PSObject.Properties){$message+="`n"+$property.Name+': '+[string]$property.Value}
$browserFixture=Join-Path (Split-Path -Parent $Contract) ($goal.id+'.browser-fixture.json')
$fixture=$null;$web=$null
if(Test-Path -LiteralPath $browserFixture){
    $fixture=Get-Content -Raw -LiteralPath $browserFixture|ConvertFrom-Json
    if($fixture.schema -ne 'quickprompt-adapt-copy-fixture/1' -or $fixture.syntheticOnly -ne $true -or
        $fixture.goalId -ne $goal.id -or $fixture.goalHash -ne $goal.hash -or $fixture.request -cne $message -or
        $goal.context -ne 'selected_report' -or $goal.target -ne 'report' -or
        $fixture.source.title -cne $base.fixtures[0][0] -or $fixture.source.templateText -cne $base.fixtures[0][1] -or
        -not ($fixture.requiredText -is [array]) -or -not ($fixture.preservedText -is [array])){throw 'Browser fixture does not match the immutable mission and source'}
    $repo=(Get-Location).Path
    $web=[IO.Path]::GetFullPath((Join-Path $repo $fixture.webRoot))
    if(-not $web.StartsWith((Join-Path $repo '.playwright')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'A frozen frontend inside the project is required'}
    Copy-Item -LiteralPath $browserFixture -Destination (Join-Path $directory 'browser-fixture.json')
}
$scenario=@{id=$goal.id;family='user_goal';context=$goal.context;message=$message;required=@($goal.required);
    forbidden=@($goal.forbidden);outcomes=@($goal.outcomes);target=$goal.target;metamorphicGroup=$null}
if(@($goal.inputs.PSObject.Properties).Count){$scenario.expectedValues=$goal.inputs}
if($goal.PSObject.Properties.Name -contains 'providerId'){$scenario.expectedProviderId=$goal.providerId}
$dataset=@{version='lab-user-goal-blind-2';syntheticOnly=$true;split='diagnostic';fixtures=$base.fixtures;scenarios=@($scenario)}
$datasetPath=Join-Path $directory 'diagnostic.json'
[IO.File]::WriteAllText($datasetPath,($dataset | ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath Tests/nova/datasets/recovery-v1/thresholds.json -Destination (Join-Path $directory 'thresholds.json')
# Goals are an independent test oracle. Never configure the QA transport that
# sends Required/Forbidden/Outcomes to the model under test.
$reference=if($FrozenCandidate){@{schema='ai-delivery-candidate/1';candidate=$FrozenCandidate}}else{
    Get-Content -Raw -LiteralPath (Join-Path $LabRoot 'artifacts/closure-20261021/candidate-reference.json')|ConvertFrom-Json
}
if($reference.schema -ne 'ai-delivery-candidate/1' -or $reference.candidate -notmatch '^[a-z0-9-]{1,100}$'){throw 'Invalid frozen candidate reference'}
$plan=@{schema='quickprompt-goal-execution/1';goalId=$goal.id;goalHash=$goal.hash;
    contractSha256=(Get-FileHash -LiteralPath $Contract -Algorithm SHA256).Hash;
    candidate=$Candidate;frozenCandidate=$reference.candidate;dataset=$datasetPath;
    fixtureSplit=$fixtureSplit;fixtureDatasetSha256=(Get-FileHash -LiteralPath $fixtureDataset -Algorithm SHA256).Hash;
    modelReceivesOracle=$false;scope='INTERPRETATION_AND_SOURCE_RESOLUTION';uiExecution=$(if($fixture){'PLANNED'}else{'NOT_REQUESTED'})}
$plan|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $directory 'execution.json') -Encoding utf8
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $directory 'run-lab-goal.ps1')
if($PrepareOnly){Write-Output (Join-Path $directory 'execution.json');exit 0}
$parameters=@{Python=$Python;SdkRoot=$SdkRoot;Candidate=$Candidate;FrozenCandidate=$reference.candidate;
    DiagnosticDataset=$datasetPath;DiagnosticCases=@($goal.id)}
if($env:NOVA_EVAL_LAB_EVENTS){$parameters.Events=$env:NOVA_EVAL_LAB_EVENTS}
& (Join-Path $PSScriptRoot 'verify-core-recovery.ps1') @parameters
$runnerExit=$LASTEXITCODE
$report=Join-Path (Get-Location) ('docs/evidence/closure-lab-20261021/'+$Candidate+'/development.json')
if(-not(Test-Path -LiteralPath $report)){throw 'No blind evaluation receipt produced'}
# An explicit, goal-bound browser fixture opts this diagnostic into real UI.
# Expectations stay in the harness; only request and source enter the app.
if($fixture){
    $runtime=Join-Path $repo ('.playwright/closure-lab-20261021/'+$reference.candidate)
    $env:ASPNETCORE_ENVIRONMENT='Development'
    $env:NOVA_TEST_BUILD=Join-Path $runtime 'eval/QuickPrompt.Api.dll';$env:NOVA_TEST_WEBROOT=$web
    $env:NOVA_ADAPT_COPY_FIXTURE=Join-Path $directory 'browser-fixture.json'
    $env:NOVA_COPY_SCENARIOS='adapt_copy'
    $env:NOVA_COPY_EVIDENCE_ROOT='docs/evidence/closure-lab-20261021/'+$Candidate+'/ui'
    $env:NOVA_AGENT_OPTIONS=ConvertTo-Json -Compress -InputObject @('--QuickPrompt:Agent:Runtime=adaptive-core',
        ('--QuickPrompt:AdaptiveCore:Python='+$Python),('--QuickPrompt:AdaptiveCore:Worker='+$runtime+'/nova_worker.py'),
        ('--QuickPrompt:AdaptiveCore:SdkRoot='+$runtime+'/sdk'),'--QuickPrompt:Diagnostics:Lab:Enabled=true',
        '--QuickPrompt:Diagnostics:Lab:CaptureSyntheticContent=true',
        ('--QuickPrompt:Diagnostics:Lab:Directory='+$LabRoot+'/artifacts/closure-20261021/ui-copy-feed'))
    & node (Join-Path $PSScriptRoot 'clipboard-agent.cjs') --real-qwen
    $uiExit=$LASTEXITCODE
    $files=@(Get-ChildItem -LiteralPath (Join-Path $repo $env:NOVA_COPY_EVIDENCE_ROOT) -Recurse -File -Filter results.json)
    if($files.Count -ne 1){throw 'One browser receipt required'}
    $ui=Get-Content -Raw -LiteralPath $files[0].FullName|ConvertFrom-Json
    $result=Get-Content -Raw -LiteralPath $report|ConvertFrom-Json
    $interpretation=$result.rows[0]|ConvertTo-Json -Depth 30|ConvertFrom-Json
    $uiPass=$uiExit -eq 0 -and $ui.status -eq 'PASS' -and $ui.mission.goalId -eq $goal.id
    $result|Add-Member -NotePropertyName visibleUi -NotePropertyValue @{
        status=$(if($uiPass){'PASS'}else{'FAIL'});report=$files[0].FullName;
        sha256=(Get-FileHash -LiteralPath $files[0].FullName -Algorithm SHA256).Hash;
        fixtureSha256=(Get-FileHash -LiteralPath $env:NOVA_ADAPT_COPY_FIXTURE -Algorithm SHA256).Hash;
        webSha256=$ui.webSha256;checks=$ui.checks;runs=$ui.runs;scope='REAL_BROWSER_OUTCOME'}
    $result|Add-Member -NotePropertyName interpretationEvidence -NotePropertyValue $interpretation
    $result.rows[0].correct=$result.rows[0].correct -and $uiPass
    if(-not $uiPass){$result.rows[0].error='VISIBLE_UI_MISSION_FAILED'}
    $result.status=if($result.rows[0].correct -and $result.rows[0].effectScopePass){'DIAGNOSTIC_TASK_PASS'}else{'DIAGNOSTIC_TASK_FAIL'}
    $combined=Join-Path (Split-Path -Parent $report) 'mission.json'
    $plan.scope='SEMANTIC_AND_VISIBLE_UI';$plan.uiExecution=if($uiPass){'PASS'}else{'FAIL'}
    $plan|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $directory 'execution-result.json') -Encoding utf8
    $result|ConvertTo-Json -Depth 45|Set-Content -LiteralPath $combined -Encoding utf8
    $report=$combined
    if(-not $uiPass){$runnerExit=1}
}
# Existing running Lab has a fixed report template. Retain its filename for
# compatibility, but copy the unassisted production report without relabeling it.
$legacy=Join-Path (Get-Location) ('docs/evidence/skill-audit-20261021/'+$Candidate)
if(Test-Path -LiteralPath $legacy){throw 'Preserve prior Lab receipts'}
New-Item -ItemType Directory -Path $legacy|Out-Null
Copy-Item -LiteralPath $report -Destination (Join-Path $legacy 'assisted-development.json')
Copy-Item -LiteralPath (Join-Path $directory 'execution.json') -Destination (Join-Path $legacy 'execution.json')
exit $runnerExit
