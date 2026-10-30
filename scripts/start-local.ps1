#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BackupZip,
    [switch]$DatabaseOnly,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.appdata/local'
$composePath = Join-Path $repoRoot 'compose.local.yml'
$secretPath = Join-Path $env:APPDATA 'Microsoft/UserSecrets/9acc1ae4-070a-4aed-98d7-63b06155d80e/secrets.json'

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

Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Path $localRoot -Force | Out-Null
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

    $secrets = if (Test-Path -LiteralPath $secretPath) { Get-Content -Raw -LiteralPath $secretPath | ConvertFrom-Json -AsHashtable } else { @{} }
    $secrets['ConnectionStrings:DefaultConnection'] = "Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($localValues.MARIADB_PASSWORD);SslMode=None;"
    $secrets['Database:ServerVersion'] = '10.5.23-mariadb'
    $secrets['Database:ApplyMigrationsOnStartup'] = 'true'
    $secrets['Database:SeedOnStartup'] = 'false'
    foreach ($key in @('Authentication:Google:ClientId','Authentication:Google:ClientSecret','Authentication:Facebook:AppId','Authentication:Facebook:AppSecret','SmtpSettings:Password','AI:ApiKey')) { if (-not $secrets.ContainsKey($key)) { $secrets[$key] = '' } }
    $secrets['Billing:PendingPaymentCleanupWorkerEnabled'] = 'false'
    $secrets['Billing:PremiumExpiryWorkerEnabled'] = 'false'
    $secrets['ReconciliationWorker:Enabled'] = 'false'
    New-Item -ItemType Directory -Path (Split-Path -Parent $secretPath) -Force | Out-Null
    $secrets | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $secretPath -Encoding utf8NoBOM
    Write-Host 'Local connection saved to .NET User Secrets (127.0.0.1:3307 / tctenglish_dev).'

    if ($DatabaseOnly) { return }
    Invoke-Checked dotnet @('tool', 'restore')
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    Invoke-Checked dotnet @('ef', 'database', 'update', '--project', 'TCTEnglish', '--startup-project', 'TCTEnglish')
    if (-not $NoBrowser) { Start-Process 'http://localhost:5100' }
    Invoke-Checked dotnet @('run', '--project', 'TCTEnglish', '--launch-profile', 'http')
} finally { Pop-Location }
