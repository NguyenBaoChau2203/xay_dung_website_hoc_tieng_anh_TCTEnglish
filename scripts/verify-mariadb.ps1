#requires -Version 7.0
[CmdletBinding()]
param([string]$Container = 'tct-ci-mariadb', [string]$Database = 'tct_ci')
$ErrorActionPreference = 'Stop'
if ($Container -notmatch '^tct-(ci|migration-check)-[a-z0-9-]+$' -and $Container -ne 'tct-ci-mariadb') { throw 'Only a disposable TCT migration-check container is allowed.' }
if ($Database -notmatch '^tct_(ci|migration_check)$') { throw 'Only a disposable migration-check database is allowed.' }

function Invoke-Sql([string]$Sql) {
    $output = & docker exec $Container sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" mariadb --protocol=tcp -h127.0.0.1 -uroot --batch --skip-column-names "$MARIADB_DATABASE" -e "$1"' sh $Sql
    if ($LASTEXITCODE -ne 0) { throw 'MariaDB regression query failed.' }
    return $output
}
function Update-Schema([string]$Target) {
    $arguments = @('ef','database','update','--project','TCTEnglish','--startup-project','TCTEnglish','--configuration','Release','--no-build')
    if ($Target) { $arguments += $Target }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'MariaDB migration regression failed.' }
}

if ((Invoke-Sql 'SELECT DATABASE();') -ne $Database) { throw 'Unexpected database; refusing to run regression.' }
if ([int](Invoke-Sql 'SELECT @@lower_case_table_names;') -ne 1) { throw 'Initialize the test datadir with lower_case_table_names=1.' }
if ([int](Invoke-Sql 'SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();') -ne 0) { throw 'Regression requires an empty disposable database.' }

Update-Schema '20260510020134_AddReadingUserTranslation'
Invoke-Sql "INSERT INTO users (UserID,Email,PasswordHash,FullName,Role,Status,CreatedAt) VALUES (900001,'ci-user@example.test','not-a-login-password','Migration regression','Standard',0,UTC_TIMESTAMP());" | Out-Null
$before = Invoke-Sql 'CHECKSUM TABLE users;'
Update-Schema ''
$after = Invoke-Sql 'CHECKSUM TABLE users;'
if ($before -ne $after) { throw 'Upgrade changed the baseline users table.' }
$historyBeforeRetry = @(Invoke-Sql 'SELECT MigrationId FROM __efmigrationshistory ORDER BY MigrationId;')
Update-Schema ''
$historyAfterRetry = @(Invoke-Sql 'SELECT MigrationId FROM __efmigrationshistory ORDER BY MigrationId;')
if (@(Compare-Object $historyBeforeRetry $historyAfterRetry).Count -gt 0) { throw 'Repeated migration changed history.' }
$expectedMigrations = @(Get-ChildItem -LiteralPath 'TCTEnglish/Migrations' -Filter '*.cs' | Where-Object { $_.Name -match '^\d{14}_' -and $_.Name -notlike '*.Designer.cs' }).Count
if ($historyAfterRetry.Count -ne $expectedMigrations) { throw 'Expected all source migrations to be applied.' }
Write-Host "MariaDB regression passed: baseline upgrade, FK indexes, preserved user checksum, repeat execution ($($historyAfterRetry.Count) migrations)."
