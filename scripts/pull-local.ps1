#requires -Version 7.0
[CmdletBinding()]
param([string]$Remote='origin',[string]$Branch='master')
$ErrorActionPreference='Stop'
if($Remote -notmatch '^[A-Za-z0-9._-]+$' -or $Remote.StartsWith('-') -or $Branch -notmatch '^[A-Za-z0-9_./-]+$' -or $Branch.StartsWith('-')){throw 'Invalid remote/branch.'}
$repoRoot=Split-Path -Parent $PSScriptRoot
$previous=[Environment]::GetEnvironmentVariable('TCT_SHARED_SYNC_IN_PROGRESS','Process')
Push-Location $repoRoot
try{
    $env:TCT_SHARED_SYNC_IN_PROGRESS='1'
    & git pull --ff-only $Remote $Branch
    if($LASTEXITCODE -ne 0){throw 'Pull failed or needs a merge/rebase. Existing changes were preserved; no database sync was attempted.'}
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'shared-data.ps1') -Action Sync
    if($LASTEXITCODE -ne 0){throw 'Code pull completed, but shared data did not sync. Resolve the reported problem and rerun scripts/shared-data.ps1 before testing.'}
}finally{
    if($null -eq $previous){[Environment]::SetEnvironmentVariable('TCT_SHARED_SYNC_IN_PROGRESS',[NullString]::Value,'Process')}else{$env:TCT_SHARED_SYNC_IN_PROGRESS=$previous}
    Pop-Location
}
