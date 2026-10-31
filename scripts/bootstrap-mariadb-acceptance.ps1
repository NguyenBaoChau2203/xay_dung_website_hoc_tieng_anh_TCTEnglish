#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ContainerName,
    [Parameter(Mandatory = $true)][string]$Database,
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$RootPassword,
    [string]$User = 'tct_acc',
    [string]$UserPassword,
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
if ([string]::IsNullOrWhiteSpace($UserPassword)) {
    $fromEnv = [Environment]::GetEnvironmentVariable('TCT_ACCEPTANCE_DB_PASSWORD')
    if ([string]::IsNullOrWhiteSpace($fromEnv)) {
        throw 'UserPassword or env TCT_ACCEPTANCE_DB_PASSWORD is required; do not rely on a shared default credential.'
    }
    $UserPassword = $fromEnv
}

function Assert-NativeSuccess {
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [Parameter(Mandatory = $true)]$ExitCode
    )
    if ($null -eq $ExitCode -or [int]$ExitCode -ne 0) {
        $shown = if ($null -eq $ExitCode) { 'null' } else { [string]$ExitCode }
        throw ("Native command failed during {0} (exit {1})." -f $Action, $shown)
    }
}

function Quote-CmdArg([string]$value) {
    if ($null -eq $value) { return '""' }
    if ($value -notmatch '[\s"&<>|^]') { return $value }
    return '"' + ($value -replace '"', '\"') + '"'
}

function Invoke-DockerText {
    param(
        [Parameter(Mandatory = $true)][string]$Action,
        [Parameter(Mandatory = $true)][string[]]$DockerArgs,
        [string]$StdInText
    )
    # Avoid Start-Process ArgumentList (it corrupts Go templates like {{json ...}}).
    # Avoid PowerShell pipelines into native commands (PS 5.1 drops exit codes).
    $stdoutFile = [System.IO.Path]::GetTempFileName()
    $stderrFile = [System.IO.Path]::GetTempFileName()
    $stdinFile = $null
    try {
        $quoted = ($DockerArgs | ForEach-Object { Quote-CmdArg $_ }) -join ' '
        if ($null -ne $StdInText) {
            $stdinFile = [System.IO.Path]::GetTempFileName()
            # UTF8 no BOM keeps mariadb client happy with ASCII SQL.
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($stdinFile, $StdInText, $utf8NoBom)
            cmd.exe /c "docker $quoted < `"$stdinFile`" > `"$stdoutFile`" 2> `"$stderrFile`""
        }
        else {
            cmd.exe /c "docker $quoted > `"$stdoutFile`" 2> `"$stderrFile`""
        }
        $code = $LASTEXITCODE
        $output = Get-Content -LiteralPath $stdoutFile -ErrorAction SilentlyContinue
        if ($code -ne 0) {
            $errText = Get-Content -LiteralPath $stderrFile -Raw -ErrorAction SilentlyContinue
            if ($errText) {
                Write-Error $errText
            }
        }
        Assert-NativeSuccess -Action $Action -ExitCode $code
        return $output
    }
    finally {
        Remove-Item -LiteralPath $stdoutFile, $stderrFile -Force -ErrorAction SilentlyContinue
        if ($stdinFile) {
            Remove-Item -LiteralPath $stdinFile -Force -ErrorAction SilentlyContinue
        }
    }
}

$labelJson = Invoke-DockerText -Action "docker inspect labels $ContainerName" -DockerArgs @('inspect', '-f', '{{json .Config.Labels}}', $ContainerName)
if (-not $labelJson) {
    throw "Container $ContainerName was not found."
}
$labels = $labelJson | ConvertFrom-Json
if (-not $labels.'tct.acceptance' -or $labels.'tct.purpose' -ne 'final-acceptance-g03') {
    throw "Container $ContainerName is missing required tct.acceptance/tct.purpose labels."
}
if ($labels.'tct.run' -ne $RunId) {
    throw ("Container {0} run label '{1}' does not match requested RunId '{2}'." -f $ContainerName, $labels.'tct.run', $RunId)
}

$portJson = Invoke-DockerText -Action "docker inspect ports $ContainerName" -DockerArgs @('inspect', '-f', '{{json .NetworkSettings.Ports}}', $ContainerName)
if (-not $portJson) {
    throw "Container $ContainerName has no published ports."
}
$ports = $portJson | ConvertFrom-Json
$binding = $ports.'3306/tcp'
if (-not $binding) {
    throw "Container $ContainerName does not publish 3306/tcp."
}
$matchedBinding = $false
foreach ($entry in @($binding)) {
    $ip = [string]$entry.HostIp
    $published = 0
    [void][int]::TryParse([string]$entry.HostPort, [ref]$published)
    if (($ip -eq '127.0.0.1' -or $ip -eq '' -or $ip -eq '0.0.0.0') -and $published -eq $HostPort) {
        if ($ip -eq '0.0.0.0') {
            throw "Container $ContainerName publishes $HostPort on 0.0.0.0; acceptance requires loopback 127.0.0.1."
        }
        $matchedBinding = $true
        break
    }
}
if (-not $matchedBinding) {
    throw ("Container {0} is not bound to 127.0.0.1:{1} for 3306/tcp." -f $ContainerName, $HostPort)
}

# Escape identifiers for SQL identifiers / string literals used below.
function Escape-SqlIdent([string]$value) {
    return ($value -replace '`', '``')
}
function Escape-SqlLiteral([string]$value) {
    return ($value -replace "'", "''")
}

$dbIdent = Escape-SqlIdent $Database
$dbLiteral = Escape-SqlLiteral $Database
$userLiteral = Escape-SqlLiteral $User
$passwordLiteral = Escape-SqlLiteral $UserPassword
$runLiteral = Escape-SqlLiteral $RunId

$probeSql = @"
SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = '$dbLiteral';
"@
$schemaProbe = Invoke-DockerText -Action "docker exec schema probe" -StdInText $probeSql -DockerArgs @('exec', '-i', $ContainerName, 'mariadb', '-N', '-uroot', "-p$RootPassword")

if ([string]::IsNullOrWhiteSpace(($schemaProbe | Out-String))) {
    $createSql = @"
CREATE DATABASE ``$dbIdent`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$userLiteral'@'%' IDENTIFIED BY '$passwordLiteral';
GRANT ALL PRIVILEGES ON ``$dbIdent``.* TO '$userLiteral'@'%';
FLUSH PRIVILEGES;
USE ``$dbIdent``;
CREATE TABLE ``__tct_acceptance_sentinel`` (
  id INT NOT NULL PRIMARY KEY,
  purpose VARCHAR(64) NOT NULL,
  run_id VARCHAR(64) NOT NULL,
  created_at_utc DATETIME(6) NOT NULL
);
INSERT INTO ``__tct_acceptance_sentinel`` (id, purpose, run_id, created_at_utc)
VALUES (1, 'final-acceptance-g03', '$runLiteral', UTC_TIMESTAMP(6));
SELECT purpose, run_id FROM ``__tct_acceptance_sentinel`` WHERE id = 1;
"@
    [void](Invoke-DockerText -Action "docker exec create owned acceptance DB" -StdInText $createSql -DockerArgs @('exec', '-i', $ContainerName, 'mariadb', '-uroot', "-p$RootPassword"))
}
else {
    $identitySql = @"
USE ``$dbIdent``;
SELECT purpose, run_id FROM ``__tct_acceptance_sentinel`` WHERE id = 1;
"@
    try {
        $identity = Invoke-DockerText -Action "docker exec read sentinel" -StdInText $identitySql -DockerArgs @('exec', '-i', $ContainerName, 'mariadb', '-N', '-uroot', "-p$RootPassword")
    }
    catch {
        throw ("Acceptance DB {0} exists but sentinel identity is missing; refuse overwrite. Use a new ephemeral DB." -f $Database)
    }
    if ([string]::IsNullOrWhiteSpace(($identity | Out-String))) {
        throw ("Acceptance DB {0} exists but sentinel identity is missing; refuse overwrite. Use a new ephemeral DB." -f $Database)
    }
    $parts = @(($identity | Out-String) -split '\s+' | Where-Object { $_ -and $_.Trim() })
    if ($parts.Count -lt 2) {
        throw ("Acceptance DB {0} sentinel identity could not be parsed; refuse overwrite." -f $Database)
    }
    $existingPurpose = $parts[0]
    $existingRun = $parts[1]
    if ($existingPurpose -ne 'final-acceptance-g03' -or $existingRun -ne $RunId) {
        throw ("Acceptance DB {0} sentinel purpose/run mismatch (found {1}/{2}); refuse overwrite." -f $Database, $existingPurpose, $existingRun)
    }

    $grantSql = @"
CREATE USER IF NOT EXISTS '$userLiteral'@'%' IDENTIFIED BY '$passwordLiteral';
GRANT ALL PRIVILEGES ON ``$dbIdent``.* TO '$userLiteral'@'%';
FLUSH PRIVILEGES;
"@
    [void](Invoke-DockerText -Action "docker exec grant owned acceptance user" -StdInText $grantSql -DockerArgs @('exec', '-i', $ContainerName, 'mariadb', '-uroot', "-p$RootPassword"))
}

Write-Output ("Bootstrapped {0} run={1} on {2}:{3}" -f $Database, $RunId, $ContainerName, $HostPort)
Write-Output "Set TCT_ACCEPTANCE_MARIADB and TCT_ACCEPTANCE_RUN_ID=$RunId before provider tests."
