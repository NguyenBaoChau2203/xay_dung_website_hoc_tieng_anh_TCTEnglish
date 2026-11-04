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

    Update-LocalSecretStore -SecretPath $secretPath -Update {
    param($secrets)
    Convert-LegacyAiSettings $secrets
    $secrets['ConnectionStrings:DefaultConnection'] = "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($localValues.MARIADB_PASSWORD);SslMode=None;"
    $secrets['Database:ServerVersion'] = '10.5.23-mariadb'
    $secrets['Database:ApplyMigrationsOnStartup'] = 'true'
    $secrets['Database:SeedOnStartup'] = 'false'
    # Missing AI key must stay missing so the authorized OPENAI_API_KEY fallback can apply.
    # Preserve an existing explicit empty key: the resolver treats it as an override.
    foreach ($key in @('Authentication:Google:ClientId','Authentication:Google:ClientSecret','Authentication:Facebook:AppId','Authentication:Facebook:AppSecret','SmtpSettings:Password')) { if (-not $secrets.ContainsKey($key)) { $secrets[$key] = '' } }
    $secrets['Billing:PendingPaymentCleanupWorkerEnabled'] = 'false'
    $secrets['Billing:PremiumExpiryWorkerEnabled'] = 'false'
    $secrets['ReconciliationWorker:Enabled'] = 'false'
    }
    Write-Host 'Local connection saved to .NET User Secrets (127.0.0.1:3307 / tctenglish_dev).'

    if ($DatabaseOnly) {
        Write-Host 'DatabaseOnly: MariaDB ready; speech cloud and web were not started.'
        return
    }

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
