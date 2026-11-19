#requires -Version 7.0
<#
.SYNOPSIS
  Imports the system content manifests (data/content/**) into the LOCAL Docker database.
.DESCRIPTION
  Local-only authoring wrapper modelled on import-listening-catalog.ps1. It refuses any connection other than the
  Docker MariaDB (127.0.0.1:3307, tctenglish_dev, tct_dev), never resets or reseeds, and restores the environment.
  ValidateVocab needs no database. DryRun* prints the plan without writing.
#>
[CmdletBinding()]
param(
    [ValidateSet('ValidateVocab','DryRunVocab','ImportVocab','DryRunListening','ImportListening','StageCaptions')][string]$Action = 'DryRunVocab',
    [string]$Only,
    [string]$Ids,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo '.appdata/local/content-import/build'
$keys = @('ConnectionStrings__DefaultConnection','TEMP','TMP')
$before = @{}; foreach ($name in $keys) { $before[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
Push-Location $repo
try {
    New-Item -ItemType Directory -Force (Join-Path $repo '.appdata/local/temp') | Out-Null
    $env:TEMP = Join-Path $repo '.appdata/local/temp'; $env:TMP = $env:TEMP
    if (-not $SkipBuild) {
        & dotnet build tools/ContentImport/ContentImport.csproj -c Release --output $output --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'Content import runner build failed.' }
    }
    if ($Action -notin @('ValidateVocab','StageCaptions')) {
        $values = @{}
        foreach ($line in Get-Content -LiteralPath (Join-Path $repo '.appdata/local/database.env')) {
            if ($line -match '^([^#=]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
        }
        if ($values.MARIADB_DATABASE -cne 'tctenglish_dev' -or $values.MARIADB_USER -cne 'tct_dev' -or -not $values.MARIADB_PASSWORD) { throw 'Unexpected local Docker configuration.' }
        $env:ConnectionStrings__DefaultConnection = "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"
    }
    $arguments = @((Join-Path $output 'ContentImport.dll'), $Action, $repo)
    if ($Only) { $arguments += @('--only', $Only) }
    if ($Ids) { $arguments += @('--ids', $Ids) }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Content $Action failed; inspect the output above. Nothing is deleted or reset." }
} finally {
    foreach ($name in $keys) { [Environment]::SetEnvironmentVariable($name, $before[$name], 'Process') }
    Pop-Location
}
