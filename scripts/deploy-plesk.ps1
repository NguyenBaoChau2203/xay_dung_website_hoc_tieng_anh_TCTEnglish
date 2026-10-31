#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ManifestPath,
    [Parameter(Mandatory=$true)][string]$SiteRoot,
    [Parameter(Mandatory=$true)][string]$StateRoot,
    [switch]$ValidateOnly,
    # Local preflight only; never changes the enabled production release client.
    [string]$BackupClientPath = ''
)
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding($false)
$SiteRoot = [IO.Path]::GetFullPath($SiteRoot).TrimEnd('\')
$StateRoot = [IO.Path]::GetFullPath($StateRoot).TrimEnd('\')
$subscriptionRoot = Split-Path -Parent $SiteRoot
$privateRoot = Join-Path $subscriptionRoot 'private'

function Assert-ChildPath([string]$Root, [string]$Path) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped its expected release directory.' }
    $cursor = Split-Path -Parent $resolved
    while ($cursor -and $cursor.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Release paths must not traverse reparse points.' }
        $cursor = Split-Path -Parent $cursor
    }
    return $resolved
}
function Write-Json([string]$Path, $Value) { [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 24), $utf8) }
function Is-Persistent([string]$Relative) {
    return $Relative -match '^(wwwroot[\\/](uploads|images[\\/](avatars|cards|classes))([\\/]|$)|logs([\\/]|$)|\.appdata([\\/]|$)|app_offline\.htm$)'
}
function Copy-Tree([string]$Source, [string]$Destination) {
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File -Force) {
        $relative = $file.FullName.Substring($Source.TrimEnd('\').Length + 1)
        if (Is-Persistent $relative) { continue }
        $sourcePath = Assert-ChildPath $Source $file.FullName
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release files must not be links.' }
        $targetPath = Assert-ChildPath $Destination (Join-Path $Destination $relative)
        [IO.Directory]::CreateDirectory((Split-Path -Parent $targetPath)) | Out-Null
        [IO.File]::Copy($sourcePath, $targetPath, $true)
    }
}
function Merge-Config($Target, $Source) {
    foreach ($property in $Source.PSObject.Properties) {
        if ($property.Value -is [Management.Automation.PSCustomObject] -and $Target.PSObject.Properties[$property.Name]) {
            Merge-Config $Target.($property.Name) $property.Value
        } else { $Target | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value -Force }
    }
}
function Ini-Value([string]$Value) { return '"' + $Value.Replace('\','\\').Replace('"','\"').Replace("`r",'\r').Replace("`n",'\n') + '"' }
function Db-Value($Builder, [string[]]$Keys) {
    foreach ($key in $Keys) { if ($Builder.ContainsKey($key)) { return [string]$Builder[$key] } }
    return ''
}
function Assert-NoReparsePath([string]$Path) {
    $cursor = [IO.Path]::GetFullPath($Path)
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Local preflight paths must not traverse reparse points.'
        }
        $parent = Split-Path -Parent $cursor
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
}

if ((Split-Path -Leaf $SiteRoot) -ne 'httpdocs' -or -not (Test-Path -LiteralPath $SiteRoot -PathType Container)) { throw 'Expected the existing Plesk httpdocs directory.' }
Assert-ChildPath $privateRoot $StateRoot | Out-Null
$defaultBackupClient = 'C:\Program Files (x86)\Plesk\MySQL\bin\mariadb-dump.exe'
$backupClient = $defaultBackupClient
if (-not [string]::IsNullOrWhiteSpace($BackupClientPath)) {
    if (-not $ValidateOnly) {
        throw 'BackupClientPath override is only allowed for owned local preflight with -ValidateOnly.'
    }
    $resolvedClient = [IO.Path]::GetFullPath($BackupClientPath)
    $workspaceTmp = Join-Path (Split-Path -Parent $PSScriptRoot) '.tmp'
    foreach ($path in @($SiteRoot, $StateRoot, $resolvedClient)) {
        Assert-ChildPath $workspaceTmp $path | Out-Null
        Assert-NoReparsePath $path
    }
    $ownershipPath = Join-Path $subscriptionRoot '.tct-staging.json'
    if (-not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
        throw 'Local preflight requires an owned .tct-staging.json marker.'
    }
    Assert-NoReparsePath $ownershipPath
    $ownership = Get-Content -Raw -LiteralPath $ownershipPath | ConvertFrom-Json
    if ($ownership.schemaVersion -ne 1 -or $ownership.purpose -ne 'tct-plesk-preflight' -or [string]$ownership.runId -cnotmatch '^[a-z0-9_]{4,64}$') {
        throw 'Invalid local preflight ownership marker.'
    }
    if ((Split-Path -Leaf $resolvedClient) -ne 'mariadb-dump.exe') {
        throw 'BackupClientPath must point to mariadb-dump.exe.'
    }
    if (-not (Test-Path -LiteralPath $resolvedClient -PathType Leaf)) {
        throw 'BackupClientPath does not exist.'
    }
    $backupClient = $resolvedClient
}
[IO.Directory]::CreateDirectory($StateRoot) | Out-Null
$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
if ($ValidateOnly -or -not $manifest.enabled) {
    Write-Json (Join-Path $StateRoot 'preflight.json') @{
        CheckedAtUtc = [DateTime]::UtcNow.ToString('O'); PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        SiteExists = $true; BackupClientExists = (Test-Path -LiteralPath $backupClient)
        BackupClientPath = $backupClient
        HasProductionConfig = (Test-Path -LiteralPath (Join-Path $SiteRoot 'appsettings.Production.json'))
        DeploymentEnabled = $false; DatabaseOrWebsiteChanged = $false
    }
    Write-Output 'Plesk release preflight completed; deployment is disabled.'
    return
}

$repository = 'NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish'
if ($manifest.repository -ne $repository -or $manifest.commit -notmatch '^[0-9a-f]{40}$' -or $manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or [string]$manifest.runNumber -notmatch '^\d+$') { throw 'Invalid release identity.' }
$artifactUri = New-Object Uri($manifest.artifactUrl)
if ($artifactUri.Scheme -ne 'https' -or $artifactUri.Host -ne 'github.com' -or -not $artifactUri.AbsolutePath.StartsWith('/' + $repository + '/releases/download/')) { throw 'Only immutable HTTPS GitHub release assets are allowed.' }
if (-not (Test-Path -LiteralPath $backupClient)) { throw 'A database backup client is required before deployment.' }

$lock = $null
$offlinePath = Join-Path $SiteRoot 'app_offline.htm'
$migrationAttempted = $false
$offlineCreated = $false
$filesChanged = $false
$appBackup = $null
$runRoot = Assert-ChildPath $StateRoot (Join-Path $StateRoot ([string]$manifest.runNumber + '-' + $manifest.commit.Substring(0,12)))
try {
    $lock = [IO.File]::Open((Join-Path $StateRoot 'deploy.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $statePath = Join-Path $StateRoot 'current.json'
    if (Test-Path -LiteralPath $statePath) {
        $current = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
        if ([long]$manifest.runNumber -le [long]$current.runNumber) { Write-Output 'This release is already applied or superseded.'; return }
    }
    if (Test-Path -LiteralPath $offlinePath) { throw 'The site is already offline; operator recovery is required before a new release.' }
    if (Test-Path -LiteralPath $runRoot) { throw 'This release has an existing attempt; inspect its backup and state before retrying.' }
    [IO.Directory]::CreateDirectory($runRoot) | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $zipPath = Join-Path $runRoot 'release.zip'
    Invoke-WebRequest -UseBasicParsing -Uri $artifactUri.AbsoluteUri -OutFile $zipPath
    if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash -ne $manifest.sha256) { throw 'Release checksum did not match.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stageRoot = Join-Path $runRoot 'stage'
    [IO.Directory]::CreateDirectory($stageRoot) | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $zip.Entries) {
            $destination = Assert-ChildPath $stageRoot (Join-Path $stageRoot $entry.FullName)
            if ($entry.FullName.EndsWith('/')) { [IO.Directory]::CreateDirectory($destination) | Out-Null; continue }
            [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)
        }
    } finally { $zip.Dispose() }
    $application = Join-Path $stageRoot 'app'
    $migrationRoot = Join-Path $stageRoot 'migration'
    foreach ($required in @('TCTEnglish.exe','TCTEnglish.dll','web.config','appsettings.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $application $required))) { throw 'The application artifact is incomplete.' }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $migrationRoot 'efbundle.exe'))) { throw 'The migration bundle is missing.' }
    if (Test-Path -LiteralPath (Join-Path $application 'appsettings.Production.json')) { throw 'Production credentials must never come from an artifact.' }

    $effective = Get-Content -Raw -LiteralPath (Join-Path $application 'appsettings.json') | ConvertFrom-Json
    foreach ($name in @('appsettings.json','appsettings.Production.json')) {
        $hostConfig = Join-Path $SiteRoot $name
        if (Test-Path -LiteralPath $hostConfig) { Merge-Config $effective (Get-Content -Raw -LiteralPath $hostConfig | ConvertFrom-Json) }
    }
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.set_ConnectionString([string]$effective.ConnectionStrings.DefaultConnection)
    $database = Db-Value $builder @('Database','Initial Catalog')
    $server = Db-Value $builder @('Server','Host','Data Source')
    if ($database -ne 'nhtctsnn_tctenglish' -or $server -notin @('103.28.36.90','127.0.0.1','localhost','::1')) { throw 'Production database identity did not match the verified local hosting database.' }
    $username = Db-Value $builder @('User','User ID','UserID','Uid')
    $password = Db-Value $builder @('Password','Pwd')
    $port = Db-Value $builder @('Port')
    if (-not $port) { $port = '3306' }
    if (-not $username -or -not $password) { throw 'Production database credentials are missing.' }
    $builder['Server'] = '127.0.0.1'
    $effective.ConnectionStrings.DefaultConnection = $builder.get_ConnectionString()
    $effective | Add-Member -NotePropertyName Database -NotePropertyValue ([pscustomobject]@{ ServerVersion='10.5.23-mariadb'; ApplyMigrationsOnStartup=$false; SeedOnStartup=$false }) -Force
    Write-Json (Join-Path $application 'appsettings.Production.json') $effective
    Write-Json (Join-Path $migrationRoot 'appsettings.json') $effective
    $optionsPath = Join-Path $runRoot 'backup-client.cnf'
    $options = @('[client]', 'host=127.0.0.1', "port=$port", ('user=' + (Ini-Value $username)), ('password=' + (Ini-Value $password)), 'protocol=tcp')
    [IO.File]::WriteAllLines($optionsPath, $options, $utf8)
    $appBackup = Join-Path $runRoot 'previous-app'
    [IO.Directory]::CreateDirectory($appBackup) | Out-Null
    Copy-Tree $SiteRoot $appBackup

    [IO.File]::WriteAllText($offlinePath, '<html><body>He thong dang duoc cap nhat. Vui long quay lai sau it phut.</body></html>', $utf8)
    $offlineCreated = $true
    Start-Sleep -Seconds 5
    $dumpPath = Join-Path $runRoot 'database-before.sql'
    & $backupClient "--defaults-extra-file=$optionsPath" --single-transaction --quick --hex-blob --skip-lock-tables --routines --events --triggers "--result-file=$dumpPath" $database 2> (Join-Path $runRoot 'backup-errors.log')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $dumpPath) -or (Get-Item -LiteralPath $dumpPath).Length -lt 1000) { throw 'Database backup failed; release stopped before migration.' }
    $env:DOTNET_ENVIRONMENT = 'Production'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $migrationAttempted = $true
    Push-Location $migrationRoot
    try { & (Join-Path $migrationRoot 'efbundle.exe') *> (Join-Path $runRoot 'migration.log'); if ($LASTEXITCODE -ne 0) { throw 'Database migration failed; maintenance and backup have been retained.' } }
    finally { Pop-Location }

    $filesChanged = $true
    Copy-Tree $application $SiteRoot
    $wwwRoot = Assert-ChildPath $SiteRoot (Join-Path $SiteRoot 'wwwroot')
    [IO.Directory]::CreateDirectory($wwwRoot) | Out-Null
    $pendingMarkerPath = Assert-ChildPath $wwwRoot (Join-Path $wwwRoot 'tct-release.pending.json')
    $markerPath = Assert-ChildPath $wwwRoot (Join-Path $wwwRoot 'tct-release.json')
    # Pending marker is not the public success document; promote only after readiness.
    $markerPayload = @{ commit=$manifest.commit; runNumber=$manifest.runNumber }
    if ($null -ne $manifest.sha256 -and "$($manifest.sha256)".Length -gt 0) {
        $markerPayload.sha256 = [string]$manifest.sha256
    }
    Write-Json $pendingMarkerPath $markerPayload
    Remove-Item -LiteralPath $offlinePath
    $ready = $false
    for ($attempt=0; $attempt -lt 24; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri 'https://tctenglish.site/health/ready' -TimeoutSec 10
            if ($response.StatusCode -eq 200) { $ready=$true; break }
        } catch { }
        Start-Sleep -Seconds 5
    }
    if (-not $ready) { throw 'Application readiness failed after release.' }
    [IO.File]::Copy($pendingMarkerPath, $markerPath, $true)
    [IO.File]::Delete($pendingMarkerPath)
    Write-Json $statePath @{ commit=$manifest.commit; runNumber=$manifest.runNumber; completedAtUtc=[DateTime]::UtcNow.ToString('O'); backupDirectory=$runRoot }
    [IO.File]::Delete($optionsPath)
    Write-Output ('Release completed: ' + $manifest.commit)
} catch {
    if ($migrationAttempted) {
        [IO.File]::WriteAllText($offlinePath, '<html><body>Maintenance: release recovery is required.</body></html>', $utf8)
        if ($filesChanged -and $appBackup -and (Test-Path -LiteralPath $appBackup)) {
            try { Copy-Tree $appBackup $SiteRoot } catch { }
        }
    } elseif ($offlineCreated -and (Test-Path -LiteralPath $offlinePath)) {
        Remove-Item -LiteralPath $offlinePath
    }
    Write-Json (Join-Path $StateRoot 'last-failure.json') @{ failedAtUtc=[DateTime]::UtcNow.ToString('O'); migrationAttempted=$migrationAttempted; maintenanceRetained=$migrationAttempted; release=$manifest.commit; recoveryDirectory=$runRoot }
    throw 'Release failed. Inspect the private release logs and backup; no database rollback was attempted.'
} finally { if ($lock) { $lock.Dispose() } }
