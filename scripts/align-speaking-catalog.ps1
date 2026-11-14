#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Job)
$ErrorActionPreference='Stop'
if($Job-notmatch '^[A-Za-z0-9_-]{11}(?:-g10c[12]|-g10)?$'){throw 'Invalid exact alignment job.'}
$repo=Split-Path -Parent $PSScriptRoot
$manifest=Get-Content (Join-Path $repo 'data/speaking/reach-a1-c2-catalog.json') -Raw|ConvertFrom-Json
if(-not @($manifest.items|Where-Object {$_.youtubeId-ceq $Job.Substring(0,11)}).Count){throw 'The job is not a currently selected source.'}
$state=Join-Path $repo '.appdata/local/speaking-catalog-transfer'
if(-not(Test-Path -LiteralPath (Join-Path $state "alignment/$Job/input.json"))){throw 'Prepare measured alignment windows first; no model/request was started.'}
$image='mmcauliffe/montreal-forced-aligner@sha256:a4fa1c8004712be22ae6290720c412722fc1f6876bcb7d142a762a73d90796b3'
$sourceMount='type=bind,source='+$repo.Replace('\','/')+',target=/src,readonly'
$stateMount='type=bind,source='+$state.Replace('\','/')+',target=/data'
& docker run --rm --mount $sourceMount --mount $stateMount -e TORCH_HOME=/data/torch-cache $image python /src/tools/SpeakingCatalogImport/ctc_alignment.py $Job
if($LASTEXITCODE-ne 0){throw 'Forced alignment failed; preserve cache and inspect the exact reported job.'}
