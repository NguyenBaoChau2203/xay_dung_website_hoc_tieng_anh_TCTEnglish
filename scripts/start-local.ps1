#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BackupZip,
    [switch]$DatabaseOnly,
    [switch]$NoBrowser,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [ValidateRange(1024, 65535)]
    [int]$WebPort = 5100
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.appdata/local'
$composePath = Join-Path $repoRoot 'compose.local.yml'
Import-Module (Join-Path $PSScriptRoot 'LocalAiConfiguration.psm1') -Force
$secretPath = Get-LocalUserSecretsPath -ProjectPath (Join-Path $repoRoot 'TCTEnglish/TCTEnglish.csproj')
Assert-LocalAiEnvironment
$speechStatusScript = Join-Path $PSScriptRoot 'speech-status.ps1'

function Invoke-Checked {
    param([string]$Executable, [string[]]$Arguments)
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed (exit $LASTEXITCODE)." }
}

function Invoke-LocalSql {
    param([string]$Sql)
    $sqlOutput = & docker exec tctenglish-mariadb-local sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" exec mariadb --protocol=tcp -h127.0.0.1 -uroot --batch --skip-column-names "$MARIADB_DATABASE" -e "$1"' sh $Sql
    if ($LASTEXITCODE -ne 0) { throw 'Local database query failed.' }
    return $sqlOutput
}

function Test-PortListening {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne(500)) { return $false }
        $client.EndConnect($async)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

$localEnvKeys = @(
    'AI__ApiKey',
    'AI__BaseUrl',
    'AI__Model',
    'AI__ReasoningEffort',
    'ConnectionStrings__DefaultConnection',
    'Database__ServerVersion',
    'Database__ApplyMigrationsOnStartup',
    'Database__SeedOnStartup',
    'Billing__PendingPaymentCleanupWorkerEnabled',
    'Billing__PremiumExpiryWorkerEnabled',
    'ReconciliationWorker__Enabled',
    'ASPNETCORE_ENVIRONMENT',
    'DOTNET_ENVIRONMENT',
    'TEMP',
    'TMP'
)
$previousLocalEnv = @{}
foreach ($key in $localEnvKeys) {
    $previousLocalEnv[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}

function Restore-LocalRuntimeEnv {
    foreach ($key in $localEnvKeys) {
        if ($null -eq $previousLocalEnv[$key]) {
            # PowerShell converts an untyped $null to an empty string at this .NET overload.
            [Environment]::SetEnvironmentVariable($key, [NullString]::Value, 'Process')
        } else {
            [Environment]::SetEnvironmentVariable($key, $previousLocalEnv[$key], 'Process')
        }
    }
}

Push-Location $repoRoot
try {
    if (-not $DatabaseOnly -and (Test-PortListening -Port $WebPort)) {
        throw "Web port $WebPort is already in use. The existing process may have previous code or API settings. Stop its owning launcher or use -Configuration Release -WebPort <free-port>; no process was stopped."
    }
    New-Item -ItemType Directory -Path $localRoot -Force | Out-Null
    # Keep child-process build/audio scratch data on the checkout's drive.
    # Do not change the owner's global Windows TEMP/TMP settings.
    $localTempRoot = Join-Path $localRoot 'temp'
    New-Item -ItemType Directory -Path $localTempRoot -Force | Out-Null
    $env:TEMP = $localTempRoot
    $env:TMP = $localTempRoot
    & docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Starting Docker Desktop...'
        Invoke-Checked docker @('desktop', 'start', '--timeout', '60')
    }

    $envPath = Join-Path $localRoot 'database.env'
    if (-not (Test-Path -LiteralPath $envPath)) {
        $rootPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        $appPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        @("MARIADB_ROOT_PASSWORD=$rootPassword", 'MARIADB_DATABASE=tctenglish_dev', 'MARIADB_USER=tct_dev', "MARIADB_PASSWORD=$appPassword") |
            Set-Content -LiteralPath $envPath -Encoding utf8NoBOM
    }
    $localValues = @{}
    foreach ($line in Get-Content -LiteralPath $envPath) {
        if ($line -match '^([^#=]+)=(.*)$') { $localValues[$Matches[1]] = $Matches[2] }
    }
    if ($localValues.MARIADB_DATABASE -ne 'tctenglish_dev' -or $localValues.MARIADB_USER -ne 'tct_dev') {
        throw 'Unexpected local database configuration; refusing to change an existing setup.'
    }

    Invoke-Checked docker @('compose', '-f', $composePath, 'up', '-d', '--wait', '--wait-timeout', '180')
    Write-Host 'MariaDB: ready (127.0.0.1:3307 / tctenglish_dev)'
    $tableCount = [int](Invoke-LocalSql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE';")

    $storedBackup = Join-Path $localRoot 'backup.zip'
    if ($BackupZip) {
        $resolvedBackup = (Resolve-Path -LiteralPath $BackupZip).Path
        if ($resolvedBackup -ne $storedBackup -and $tableCount -eq 0) { Copy-Item -LiteralPath $resolvedBackup -Destination $storedBackup }
    }
    if ($tableCount -eq 0 -and (Test-Path -LiteralPath $storedBackup)) {
        $archive = [IO.Compression.ZipFile]::OpenRead($storedBackup)
        try {
            $entries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.sql', [StringComparison]::OrdinalIgnoreCase) })
            if ($entries.Count -ne 1 -or $entries[0].Length -gt 25MB) { throw 'Expected one SQL backup smaller than 25 MB.' }
            $sqlPath = Join-Path $localRoot 'restore.sql'
            $inputStream = $entries[0].Open()
            $outputStream = [IO.File]::Create($sqlPath)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
        } finally { $archive.Dispose() }
        Write-Host 'Restoring backup into the empty Docker development database...'
        Invoke-Checked docker @('cp', $sqlPath, 'tctenglish-mariadb-local:/tmp/tctenglish-restore.sql')
        & docker exec tctenglish-mariadb-local sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" mariadb --protocol=tcp -h127.0.0.1 -uroot "$MARIADB_DATABASE" < /tmp/tctenglish-restore.sql' *> (Join-Path $localRoot 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed. Inspect .appdata/local/restore.log; the database will not be overwritten on rerun.' }
        Invoke-Checked docker @('exec', 'tctenglish-mariadb-local', 'rm', '/tmp/tctenglish-restore.sql')
        @{ ArchiveSha256 = (Get-FileHash -LiteralPath $storedBackup -Algorithm SHA256).Hash; RestoredAtUtc = [DateTime]::UtcNow.ToString('O') } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $localRoot 'restored-backup.json') -Encoding utf8NoBOM
    } elseif ($BackupZip -and $tableCount -gt 0) {
        Write-Host 'Existing local database preserved; backup import is skipped.'
    }

    # Ordinary startup must never rewrite the credential store. In particular,
    # an EFS/ACL failure during replacement must not remove an owner's saved key.
    $localDatabaseEnvironment = @{
        'ConnectionStrings__DefaultConnection' = "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($localValues.MARIADB_PASSWORD);SslMode=None;"
        'Database__ServerVersion' = '10.5.23-mariadb'
        'Database__ApplyMigrationsOnStartup' = 'true'
        'Database__SeedOnStartup' = 'false'
        'Billing__PendingPaymentCleanupWorkerEnabled' = 'false'
        'Billing__PremiumExpiryWorkerEnabled' = 'false'
        'ReconciliationWorker__Enabled' = 'false'
    }
    foreach ($setting in $localDatabaseEnvironment.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($setting.Key, $setting.Value, 'Process')
    }
    Write-Host 'Local connection configured for child processes (127.0.0.1:3307 / tctenglish_dev); User Secrets preserved.'

    if ($DatabaseOnly) {
        Write-Host 'DatabaseOnly: MariaDB ready; speech cloud and web were not started.'
        return
    }

    # Legacy nonsecret settings may be normalized for this run only. Preserve
    # the saved store and explicit environment overrides, including empty keys.
    $runtimeAiValues = Read-LocalSecretStore -SecretPath $secretPath
    Convert-LegacyAiSettings $runtimeAiValues
    foreach ($name in @('BaseUrl', 'Model', 'ReasoningEffort')) {
        if ($null -eq [Environment]::GetEnvironmentVariable("AI__$name", 'Process') -and $runtimeAiValues.ContainsKey("AI:$name")) {
            [Environment]::SetEnvironmentVariable("AI__$name", [string]$runtimeAiValues["AI:$name"], 'Process')
        }
    }
    $runtimeAiValues = $null
    $ai = Initialize-LocalAiEnvironment -SecretPath $secretPath
    if ($ai.Configured) {
        Write-Host "AI: key configured | source=$($ai.Source) | model=gpt-6-luna | reasoning=low (account access not checked)."
    } else {
        Write-Host 'AI: key missing; web/DB still start. Speaking cloud ASR needs OPENAI_API_KEY or scripts/set-local-ai-key.ps1. No fake scores.'
    }

    $speechStatus = 'not-configured'
    $speechSource = 'missing'
    if (Test-Path -LiteralPath $speechStatusScript) {
        $speechJson = & $speechStatusScript
        if ($LASTEXITCODE -eq 0 -and $speechJson) {
            $speech = $speechJson | ConvertFrom-Json
            $speechStatus = [string]$speech.Status
            $speechSource = [string]$speech.KeySource
            Write-Host "Speech: $speechStatus | source=$speechSource | model=$($speech.Model) (live account/quota not verified; no paid startup request)."
        }
    }
    Write-Host 'FFmpeg is not started for ordinary web/mic PCM. For Admin audio import run: pwsh -NoProfile -File scripts/local-media.ps1 -Action Setup'

    Invoke-Checked dotnet @('tool', 'restore')
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Invoke-Checked dotnet @('ef', 'database', 'update', '--project', 'TCTEnglish', '--startup-project', 'TCTEnglish', '--configuration', $Configuration)
    Invoke-Checked dotnet @('run', '--project', 'TCTEnglish', '--launch-profile', 'http', '--configuration', $Configuration, '--no-build', '--', '--local-bootstrap=true')

    # Catch up approved content even when pull was already up-to-date or native
    # hooks were not installed yet. No credential-store write or web/ASR spawn.
    Invoke-Checked pwsh @('-NoProfile','-File',(Join-Path $PSScriptRoot 'shared-data.ps1'),'-Action','Sync','-Configuration',$Configuration,'-SkipBuild','-DatabaseReady')

    if (-not $NoBrowser) { Start-Process "http://localhost:$WebPort" }
    Write-Host "Summary => MariaDB: ready | Speech: $speechStatus ($speechSource) | Web: starting http://localhost:$WebPort"
    Write-Host 'Ctrl+C stops this web process only; MariaDB volume keeps running. Ordinary startup does not spawn a local ASR engine or call paid speech APIs.'
    try {
        Invoke-Checked dotnet @('run', '--project', 'TCTEnglish', '--launch-profile', 'http', '--configuration', $Configuration, '--no-build', '--', '--urls', "http://localhost:$WebPort")
    } finally {
        Restore-LocalRuntimeEnv
    }
} finally {
    Restore-LocalRuntimeEnv
    Pop-Location
}
