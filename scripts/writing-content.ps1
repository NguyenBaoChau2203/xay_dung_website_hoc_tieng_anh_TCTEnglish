#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('baseline','validate','dry-run','pilot','import','export','verify','roundtrip')][string]$Action,
    [ValidateSet('beginner','intermediate','advanced')][string]$Level='beginner'
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$names=@('ConnectionStrings__DefaultConnection','TEMP','TMP')
$previous=@{};foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
Push-Location $repoRoot
try {
    $temp=Join-Path $repoRoot '.appdata/local/temp';New-Item -ItemType Directory -Path $temp -Force|Out-Null
    $env:TEMP=$temp;$env:TMP=$temp
    if($Action -ne 'roundtrip'){
        $values=@{};foreach($line in Get-Content -LiteralPath '.appdata/local/database.env'){
            if($line -match '^([^#=]+)=(.*)$'){$values[$Matches[1]]=$Matches[2]}
        }
        if($values.MARIADB_DATABASE -cne 'tctenglish_dev' -or $values.MARIADB_USER -cne 'tct_dev' -or -not $values.MARIADB_PASSWORD){throw 'Expected this checkout Docker database.'}
        $env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"
    }
    & dotnet run --file scripts/writing-content.cs -- $Action $repoRoot $Level
    if($LASTEXITCODE -ne 0){throw "Writing content $Action failed; preserve baseline/receipts and investigate. No force/reset is supported."}
} finally {
    foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}
