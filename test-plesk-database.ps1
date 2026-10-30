#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SiteRoot,
    [Parameter(Mandatory=$true)][string]$StateRoot
)
$ErrorActionPreference = 'Stop'
# Load the same path guards and helpers, with website/database deployment disabled.
. (Join-Path $PSScriptRoot 'deploy-plesk.ps1') -ManifestPath (Join-Path $PSScriptRoot 'release.json') -SiteRoot $SiteRoot -StateRoot $StateRoot -ValidateOnly
$config = [pscustomobject]@{}
foreach ($name in @('appsettings.json','appsettings.Production.json')) {
    $path = Join-Path $SiteRoot $name
    if (Test-Path -LiteralPath $path) { Merge-Config $config (Get-Content -Raw -LiteralPath $path | ConvertFrom-Json) }
}
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
$builder.set_ConnectionString([string]$config.ConnectionStrings.DefaultConnection)
$database = Db-Value $builder @('Database','Initial Catalog')
$server = Db-Value $builder @('Server','Host','Data Source')
if ($database -ne 'nhtctsnn_tctenglish' -or $server -notin @('103.28.36.90','127.0.0.1','localhost','::1')) { throw 'Unexpected database identity; no probe was run.' }
$username = Db-Value $builder @('User','User ID','UserID','Uid')
$password = Db-Value $builder @('Password','Pwd')
if (-not $username -or -not $password) { throw 'Hosting credentials are missing; no probe was run.' }
$port = Db-Value $builder @('Port')
if (-not $port) { $port='3306' }
$client = 'C:\Program Files (x86)\Plesk\MySQL\bin\mariadb.exe'
$dumpClient = 'C:\Program Files (x86)\Plesk\MySQL\bin\mariadb-dump.exe'
$optionsPath = Join-Path $StateRoot 'probe-client.cnf'
$dumpPath = Join-Path $StateRoot ('verified-backup-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '.sql')
$utf8 = New-Object Text.UTF8Encoding($false)
try {
    [IO.File]::WriteAllLines($optionsPath, @('[client]','host=127.0.0.1',"port=$port",('user=' + (Ini-Value $username)),('password=' + (Ini-Value $password)),'protocol=tcp'), $utf8)
    $sql = 'SELECT DATABASE(),VERSION(),@@lower_case_table_names; SELECT COUNT(*) FROM users; SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE(); SELECT MigrationId FROM __efmigrationshistory ORDER BY MigrationId; SHOW GRANTS FOR CURRENT_USER();'
    $metadata = @(& $client "--defaults-extra-file=$optionsPath" --batch --skip-column-names $database -e $sql 2> (Join-Path $StateRoot 'probe-query-errors.log'))
    if ($LASTEXITCODE -ne 0) { throw 'Read-only hosting database probe failed; inspect the private error log.' }
    & $dumpClient "--defaults-extra-file=$optionsPath" --single-transaction --quick --hex-blob --skip-lock-tables --routines --events --triggers "--result-file=$dumpPath" $database 2> (Join-Path $StateRoot 'probe-backup-errors.log')
    if ($LASTEXITCODE -ne 0 -or (Get-Item -LiteralPath $dumpPath).Length -lt 1000) { throw 'Hosting backup probe failed; no schema or website files were changed.' }
    $identity = ([string]$metadata[0]).Split("`t")
    $grantText = ($metadata | Where-Object { $_ -match '^GRANT ' }) -join ' '
    $hasDdl = $grantText -match 'ALL PRIVILEGES' -or ($grantText -match '\bALTER\b' -and $grantText -match '\bCREATE\b' -and $grantText -match '\bINDEX\b')
    Write-Json (Join-Path $StateRoot 'database-probe.json') @{
        CheckedAtUtc=[DateTime]::UtcNow.ToString('O'); Database=$identity[0]; ServerVersion=$identity[1]
        LowerCaseTableNames=$identity[2]; UserCount=[int]$metadata[1]; TableCount=[int]$metadata[2]
        AppliedMigrations=@($metadata | Where-Object { $_ -match '^\d{14}_[A-Za-z0-9_]+$' })
        GrantsIndicateSchemaPrivileges=[bool]$hasDdl; BackupBytes=(Get-Item -LiteralPath $dumpPath).Length
        BackupFile=(Split-Path -Leaf $dumpPath); BackupSha256=(Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash
        DatabaseOrWebsiteChanged=$false
    }
    Write-Output 'Read-only hosting metadata and a fresh private database backup were verified.'
} finally { if (Test-Path -LiteralPath $optionsPath) { [IO.File]::Delete($optionsPath) } }
