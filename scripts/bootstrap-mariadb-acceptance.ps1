#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ContainerName,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$RootPassword,
    [string]$User = 'tct_acc',
    [string]$UserPassword = 'acc_user_2930',
    [int]$HostPort = 3308
)

$ErrorActionPreference = 'Stop'
if ($ContainerName -notmatch '^tctenglish-mariadb-acceptance-') {
    throw 'Container name must start with tctenglish-mariadb-acceptance-.'
}
if ($Database -notmatch '^tct_acceptance_[a-z0-9_]{4,64}$') {
    throw 'Database name must match tct_acceptance_<runid>.'
}
if ($RunId -notmatch '^[a-z0-9_]{4,64}$') {
    throw 'Run id must match [a-z0-9_]{4,64}.'
}
if ($HostPort -lt 3308 -or $HostPort -gt 3399) {
    throw 'Host port must be in 3308-3399.'
}

$labelJson = docker inspect -f '{{json .Config.Labels}}' $ContainerName 2>$null
if (-not $labelJson) {
    throw "Container $ContainerName was not found."
}
$labels = $labelJson | ConvertFrom-Json
if (-not $labels.'tct.acceptance' -or $labels.'tct.purpose' -ne 'final-acceptance-g03') {
    throw "Container $ContainerName is missing required tct.acceptance/tct.purpose labels."
}

$createSql = @"
CREATE DATABASE IF NOT EXISTS ``$Database`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$User'@'%' IDENTIFIED BY '$UserPassword';
GRANT ALL PRIVILEGES ON ``$Database``.* TO '$User'@'%';
FLUSH PRIVILEGES;
USE ``$Database``;
-- Owned acceptance DB only: recreate sentinel schema for this harness run.
DROP TABLE IF EXISTS ``__tct_acceptance_sentinel``;
CREATE TABLE ``__tct_acceptance_sentinel`` (
  id INT NOT NULL PRIMARY KEY,
  purpose VARCHAR(64) NOT NULL,
  run_id VARCHAR(64) NOT NULL,
  created_at_utc DATETIME(6) NOT NULL
);
INSERT INTO ``__tct_acceptance_sentinel`` (id, purpose, run_id, created_at_utc)
VALUES (1, 'final-acceptance-g03', '$RunId', UTC_TIMESTAMP(6));
SELECT purpose, run_id FROM ``__tct_acceptance_sentinel`` WHERE id = 1;
"@

$createSql | docker exec -i $ContainerName mariadb -uroot "-p$RootPassword"
Write-Output ("Bootstrapped {0} run={1} on {2}:{3}" -f $Database, $RunId, $ContainerName, $HostPort)
Write-Output "Set TCT_ACCEPTANCE_MARIADB and TCT_ACCEPTANCE_RUN_ID=$RunId before provider tests."
