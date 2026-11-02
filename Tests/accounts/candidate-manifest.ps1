#requires -Version 7.2
# Pure filesystem/Git fixture. No product accounts, database, network or provider.
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script = Join-Path $project 'scripts/New-QuickPromptCandidate.ps1'
$fixture = Join-Path $project ('.playwright/candidate-tests/' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $fixture 'apps/web')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $fixture 'Data')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $fixture 'private')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $fixture '.playwright/artifact')) | Out-Null
'{"version":"1.0.0"}' | Set-Content -LiteralPath (Join-Path $fixture 'apps/web/package.json')
'initial source' | Set-Content -LiteralPath (Join-Path $fixture 'main.cs')
'public source, not runtime data' | Set-Content -LiteralPath (Join-Path $fixture 'Data/Repository.cs')
'private/' | Set-Content -LiteralPath (Join-Path $fixture '.gitignore')
'SYNTHETIC_SECRET_MUST_NOT_APPEAR' | Set-Content -LiteralPath (Join-Path $fixture '.env')
'SYNTHETIC_SECRET_MUST_NOT_APPEAR' | Set-Content -LiteralPath (Join-Path $fixture 'appsettings.Production.json')
'SYNTHETIC_SECRET_MUST_NOT_APPEAR' | Set-Content -LiteralPath (Join-Path $fixture 'private/credential.json')
'synthetic binary bytes' | Set-Content -LiteralPath (Join-Path $fixture '.playwright/artifact/app.dll')
'SYNTHETIC_SECRET_MUST_NOT_APPEAR' | Set-Content -LiteralPath (Join-Path $fixture '.playwright/artifact/signing.pem')
& git -C $fixture init --quiet
& git -C $fixture add -- main.cs Data/Repository.cs apps/web/package.json .gitignore
& git -C $fixture add --force -- .env appsettings.Production.json
& git -C $fixture -c user.name='QuickPrompt QA Fixture' -c user.email='qa@example.invalid' commit --quiet -m 'Synthetic manifest fixture'
if ($LASTEXITCODE -ne 0) { throw 'The isolated Git fixture was not created.' }

function Assert-Candidate([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Expect-CandidateRejection([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert-Candidate $rejected 'An unsafe candidate input was accepted.'
}

$first = & $script -RepositoryRoot $fixture -CandidateId stable -Mode Begin | ConvertFrom-Json
$second = & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Begin | ConvertFrom-Json
Assert-Candidate ($first.sourceSha256 -eq $second.sourceSha256) 'Identical source inventories were not deterministic.'
$baseline = Get-Content -LiteralPath (Join-Path $fixture '.playwright/candidates/stable/baseline.json') -Raw | ConvertFrom-Json
Assert-Candidate ($baseline.sources.files.Count -eq 4) 'Allowed source was omitted or ignored/private files entered the inventory.'
Assert-Candidate ($baseline.sources.excludedFiles -ge 2) 'Explicit private source exclusions were not recorded.'

$receiptPath = Join-Path $fixture '.playwright/run.trx'
$stamp = [DateTimeOffset]::UtcNow.ToString('O')
"<TestRun><Times start='$stamp' finish='$stamp'/><ResultSummary><Counters total='2' executed='2' passed='1' failed='1'/></ResultSummary></TestRun>" | Set-Content -LiteralPath $receiptPath
$result = & $script -RepositoryRoot $fixture -CandidateId stable -Mode Complete -ArtifactDirectory '.playwright/artifact' -EvidenceFiles '.playwright/run.trx' | ConvertFrom-Json
$manifestText = Get-Content -LiteralPath (Join-Path $fixture '.playwright/candidates/stable/candidate.json') -Raw
$manifest = $manifestText | ConvertFrom-Json
Assert-Candidate ($result.status -eq 'SOURCE_STABLE_REVIEW_REQUIRED') 'Unchanged sources did not retain the source-stable status.'
Assert-Candidate ($manifest.releaseVerdict -eq 'NOT_ASSESSED') 'A failed receipt was promoted to a release PASS.'
Assert-Candidate ($manifest.evidence[0].counters.failed -eq 1) 'A failed test count was hidden.'
Assert-Candidate ($manifest.evidence[0].window -eq 'WITHIN_BASELINE_WINDOW') 'The actual receipt window was not compared.'
Assert-Candidate ($manifest.artifacts.Count -eq 1 -and $manifest.artifacts[0].path.EndsWith('/app.dll')) 'Private artifact keys entered the inventory.'
Assert-Candidate (-not $manifestText.Contains('SYNTHETIC_SECRET_MUST_NOT_APPEAR')) 'Private fixture content was serialized.'

Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId stable -Mode Begin }
Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete -ArtifactDirectory $env:TEMP }
Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete -EvidenceFiles 'main.cs' }
Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete -ArtifactDirectory '.' }
'{"token":"SYNTHETIC_SECRET_MUST_NOT_APPEAR"}' | Set-Content -LiteralPath (Join-Path $fixture '.playwright/credentials.json')
Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete -EvidenceFiles '.playwright/credentials.json' }
'<!DOCTYPE TestRun [<!ENTITY forbidden "not evaluated">]><TestRun/>' | Set-Content -LiteralPath (Join-Path $fixture '.playwright/invalid.trx')
Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete -EvidenceFiles '.playwright/invalid.trx' }

'changed source' | Set-Content -LiteralPath (Join-Path $fixture 'main.cs')
$changed = & $script -RepositoryRoot $fixture -CandidateId comparison -Mode Complete | ConvertFrom-Json
Assert-Candidate ($LASTEXITCODE -eq 2 -and $changed.status -eq 'SOURCE_CHANGED_REVALIDATE') 'Changed sources did not invalidate the validation window.'

# A link/junction must not hash files from a second directory, even when Git lists it.
[IO.Directory]::CreateDirectory((Join-Path $fixture '.playwright/outside')) | Out-Null
'fixture link content' | Set-Content -LiteralPath (Join-Path $fixture '.playwright/outside/linked.cs')
if ([OperatingSystem]::IsWindows()) {
    New-Item -ItemType Junction -Path (Join-Path $fixture 'linked') -Target (Join-Path $fixture '.playwright/outside') | Out-Null
    Expect-CandidateRejection { & $script -RepositoryRoot $fixture -CandidateId linked -Mode Begin }
}
[ordered]@{ status = 'passed'; scope = 'ISOLATED_CANDIDATE_METADATA_ONLY'; checks = @('deterministic allowed inventory','ignored and private source exclusion','artifact key exclusion','no private content serialization','failed test counters preserved','source window checked','source changes require revalidation','existing evidence cannot be overwritten','path and receipt guards','private receipts and DTD rejected','junction rejected on Windows'); fixture = [IO.Path]::GetRelativePath($project, $fixture).Replace('\','/') } | ConvertTo-Json
