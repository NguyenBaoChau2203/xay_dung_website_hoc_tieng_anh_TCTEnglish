#requires -Version 7.0
<#
.SYNOPSIS
  Local-only sample learning progress for learner "Anh Hoàng" (dashboard /Home/Index).

.PARAMETER Action
  apply  — upsert sample progress (idempotent)
  remove — delete sample rows and restore prior user streak/goal fields from receipt
  status — print current counts for the learner

.NOTES
  Touches only Docker MariaDB tctenglish_dev on 127.0.0.1:3307.
  Does not change application business logic. Does not commit or push.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('apply', 'remove', 'status')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$names = @('ConnectionStrings__DefaultConnection', 'TEMP', 'TMP')
$previous = @{}
foreach ($name in $names) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

Push-Location $repoRoot
try {
    $temp = Join-Path $repoRoot '.appdata/local/temp'
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $env:TEMP = $temp
    $env:TMP = $temp

    $values = @{}
    foreach ($line in Get-Content -LiteralPath '.appdata/local/database.env') {
        if ($line -match '^([^#=]+)=(.*)$') {
            $values[$Matches[1]] = $Matches[2]
        }
    }
    if ($values.MARIADB_DATABASE -cne 'tctenglish_dev' -or $values.MARIADB_USER -cne 'tct_dev' -or -not $values.MARIADB_PASSWORD) {
        throw 'Expected this checkout Docker database (tctenglish_dev / tct_dev).'
    }

    $env:ConnectionStrings__DefaultConnection =
        "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"

    & dotnet run --file scripts/seed-anh-hoang-dashboard.cs -- $Action $repoRoot
    if ($LASTEXITCODE -ne 0) {
        throw "seed-anh-hoang-dashboard $Action failed (exit $LASTEXITCODE)."
    }
}
finally {
    foreach ($name in $names) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process')
    }
    Pop-Location
}
