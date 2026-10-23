param([Parameter(Mandatory=$true)][string]$Node)
$ErrorActionPreference='Stop'
$copyChoiceLease=[IO.File]::Open((Join-Path ([IO.Path]::GetTempPath()) 'QuickPrompt.Nova.SemanticModel.lease'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$copyChoiceExit=1
try {
    $env:NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE='1'
    & $Node (Join-Path $PSScriptRoot 'dual-copy-choice.cjs')
    $copyChoiceExit=$LASTEXITCODE
} finally {
    Remove-Item Env:NOVA_SEMANTIC_CAPTURE_EXCLUSIVE_LEASE -ErrorAction SilentlyContinue
    $copyChoiceLease.Dispose()
}
exit $copyChoiceExit
