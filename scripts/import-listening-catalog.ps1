#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Stage','Snapshot','DryRun','Import')][string]$Action = 'DryRun',
    [switch]$Pilot,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
if ($Action -eq 'Import') {
    throw 'This Listening batch is superseded by Speaking. Import is disabled; use scripts/speaking-catalog.ps1. Historical baselines and receipts are preserved.'
}
$repo = Split-Path -Parent $PSScriptRoot
$state = Join-Path $repo '.appdata/local/reach-listening-import'
$output = Join-Path $state 'build'
$keys = @('ConnectionStrings__DefaultConnection','TEMP','TMP')
$before = @{}; foreach ($name in $keys) { $before[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
Push-Location $repo
try {
    $env:TEMP = Join-Path $repo '.appdata/local/temp'; $env:TMP = $env:TEMP
    if (-not $SkipBuild) {
        & dotnet build tools/ListeningCatalogImport/ListeningCatalogImport.csproj -c Release --output $output --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'Catalog runner build failed.' }
    }
    if ($Action -ne 'Stage') {
        $values = @{}
        foreach ($line in Get-Content -LiteralPath (Join-Path $repo '.appdata/local/database.env')) {
            if ($line -match '^([^#=]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
        }
        if ($values.MARIADB_DATABASE -cne 'tctenglish_dev' -or $values.MARIADB_USER -cne 'tct_dev' -or -not $values.MARIADB_PASSWORD) { throw 'Unexpected local Docker configuration.' }
        $env:ConnectionStrings__DefaultConnection = "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"
    }
    $arguments = @((Join-Path $output 'ListeningCatalogImport.dll'), $Action, $repo)
    if ($Pilot) { $arguments += '--pilot' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Catalog $Action failed; inspect the exact item above. No overwrite/reset is supported." }
} finally {
    foreach ($name in $keys) { [Environment]::SetEnvironmentVariable($name, $before[$name], 'Process') }
    Pop-Location
}
