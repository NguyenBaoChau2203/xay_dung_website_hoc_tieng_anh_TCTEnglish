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
$ProgressPreference = 'SilentlyContinue'
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
function Invoke-OwnedStagingImportRecovery([string]$SiteRoot, [string]$StateRoot, $Plan) {
    # Explicit reviewed recovery only: never a generic unlock/offline bypass.
    if ((Split-Path -Leaf $SiteRoot) -cne 'staging.tctenglish.site' -or (Split-Path -Leaf $StateRoot) -cne 'tctenglish-staging-state') { throw 'Recovery requires the isolated staging roots.' }
    $subscription = Split-Path -Parent $SiteRoot
    if ([IO.Path]::GetFullPath((Split-Path -Parent $StateRoot)) -ine [IO.Path]::GetFullPath((Join-Path $subscription 'private'))) { throw 'Recovery state escaped the subscription private directory.' }
    Assert-NoReparsePath $SiteRoot
    Assert-NoReparsePath $StateRoot
    if ([string]$Plan.runNumber -notmatch '^[1-9][0-9]{0,17}$' -or $Plan.commit -notmatch '^[0-9a-f]{40}$' -or $Plan.previousCommit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid reviewed recovery identity.' }
    $offline = Join-Path $SiteRoot 'app_offline.htm'
    if (-not (Test-Path -LiteralPath $offline)) { return }
    $oldRoot = Assert-ChildPath $StateRoot (Join-Path $StateRoot ([string]$Plan.runNumber + '-' + $Plan.commit.Substring(0,12)))
    Assert-NoReparsePath $oldRoot
    $owner = Get-Content -Raw -LiteralPath (Join-Path $oldRoot 'owner.json') | ConvertFrom-Json
    $current = Get-Content -Raw -LiteralPath (Join-Path $StateRoot 'current.json') | ConvertFrom-Json
    if ($owner.environment -cne 'Staging' -or $owner.commit -cne $Plan.commit -or [long]$owner.runNumber -ne [long]$Plan.runNumber -or $current.commit -cne $Plan.previousCommit) { throw 'Recovery owner/current marker does not match the reviewed failed run.' }
    $backup = Assert-ChildPath $oldRoot (Join-Path $oldRoot 'previous-app')
    $dump = Assert-ChildPath $oldRoot (Join-Path $oldRoot 'database-before.sql')
    Assert-NoReparsePath $backup
    Assert-NoReparsePath $dump
    if (-not (Test-Path -LiteralPath $dump) -or (Get-Item -LiteralPath $dump).Length -lt 1000) { throw 'Recovery requires the preserved database backup.' }
    $migration = Get-Content -Raw -LiteralPath (Join-Path $oldRoot 'migration.log')
    $applied = @([regex]::Matches($migration, "Applying migration '([^']+)'") | ForEach-Object { $_.Groups[1].Value })
    $approved = @('20261113112346_AddSpeakingAssessmentComponentScores','20261113125249_AddSpeakingVideoVisits')
    if ($migration -notmatch 'Done\.' -or ($applied -join '|') -cne ($approved -join '|')) { throw 'Recovery schema evidence is not the reviewed additive nullable-metrics/visits upgrade.' }
    $marker = Get-Content -Raw -LiteralPath (Join-Path $SiteRoot 'wwwroot\tct-release.json') | ConvertFrom-Json
    if ($marker.environment -cne 'Staging' -or $marker.commit -cne $Plan.previousCommit) { throw 'Recovery would reopen an unverified application version.' }
    $files = @(Get-ChildItem -LiteralPath $backup -Recurse -File -Force)
    if ($files.Count -eq 0) { throw 'Recovery application backup is empty.' }
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($backup.TrimEnd('\').Length + 1)
        $live = Assert-ChildPath $SiteRoot (Join-Path $SiteRoot $relative)
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or -not (Test-Path -LiteralPath $live) -or ((Get-Item -LiteralPath $live).Attributes -band [IO.FileAttributes]::ReparsePoint) -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne (Get-FileHash -LiteralPath $live -Algorithm SHA256).Hash) { throw 'Recovery requires an unchanged previous application; preserve maintenance for operator review.' }
    }
    $allowedOffline = @('<html><body>He thong dang duoc cap nhat. Vui long quay lai sau it phut.</body></html>', '<html><body>Maintenance: release recovery is required.</body></html>')
    if ($allowedOffline -cnotcontains ([IO.File]::ReadAllText($offline).Trim())) { throw 'Recovery does not own this maintenance file.' }
    $expectedExe = Assert-ChildPath $oldRoot (Join-Path $oldRoot 'stage\app\TCTEnglish.exe')
    $imports = @(Get-CimInstance Win32_Process -Filter "Name='TCTEnglish.exe'" | Where-Object { $_.CommandLine -like '*--shared-data=sync*' -and $_.CommandLine -like '*--shared-target=staging*' })
    $owned = @($imports | Where-Object { $_.ExecutablePath -ieq $expectedExe -and $_.CommandLine.Contains($oldRoot) })
    if ($imports.Count -ne $owned.Count -or $owned.Count -gt 1) { throw 'Recovery found a different or ambiguous staging import; no process was stopped.' }
    foreach ($process in $owned) { Stop-Process -Id $process.ProcessId -ErrorAction Stop }
    # Closing the owned process releases its uncommitted import transaction.
    # Wait for the original release parent to retain maintenance and release its
    # OS lock; never delete a lock file or remove another deployment's lock.
    $recoveryLock = $null
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        try { $recoveryLock = [IO.File]::Open((Join-Path $StateRoot 'deploy.lock'), [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $recoveryLock) { throw 'Original release lock is still held; maintenance retained.' }
    try {
        $current = Get-Content -Raw -LiteralPath (Join-Path $StateRoot 'current.json') | ConvertFrom-Json
        if ($current.commit -cne $Plan.previousCommit) { throw 'Current release changed during recovery; maintenance retained.' }
        $failure = Get-Content -Raw -LiteralPath (Join-Path $StateRoot 'last-failure.json') | ConvertFrom-Json
        if ($failure.release -cne $Plan.commit -or $failure.maintenanceRetained -ne $true -or [IO.Path]::GetFullPath([string]$failure.recoveryDirectory) -ine $oldRoot) { throw 'Original parent has not confirmed its failed-release cleanup.' }
        $remaining = @(Get-CimInstance Win32_Process -Filter "Name='TCTEnglish.exe'" | Where-Object { $_.ExecutablePath -ieq $expectedExe })
        if ($remaining.Count -ne 0) { throw 'Owned import still exists; maintenance retained.' }
        if ($allowedOffline -cnotcontains ([IO.File]::ReadAllText($offline).Trim())) { throw 'Maintenance ownership changed during recovery.' }
        $preserved = Assert-ChildPath $oldRoot (Join-Path $oldRoot 'recovered-app-offline.htm')
        if (Test-Path -LiteralPath $preserved) { throw 'Recovery receipt already exists; inspect the previous recovery.' }
        Move-Item -LiteralPath $offline -Destination $preserved
        Write-Output 'Reviewed staging import recovery completed; previous application preserved and backups retained.'
    } finally { $recoveryLock.Dispose() }
}
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
function Remove-OwnedRunTree([string]$RunRoot, [string]$Name) {
    if ($Name -notin @('stage','previous-app')) { throw 'Unexpected release cleanup target.' }
    Assert-ChildPath $StateRoot $RunRoot | Out-Null
    Assert-NoReparsePath $RunRoot
    $owner = Get-Content -Raw -LiteralPath (Join-Path $RunRoot 'owner.json') | ConvertFrom-Json
    if ($owner.environment -cne 'Staging' -or $owner.commit -cnotmatch '^[0-9a-f]{40}$' -or [string]$owner.runNumber -notmatch '^\d+$' -or (Split-Path -Leaf $RunRoot) -cne ([string]$owner.runNumber + '-' + $owner.commit.Substring(0,12))) { throw 'Staging cleanup ownership mismatch.' }
    $target = Assert-ChildPath $RunRoot (Join-Path $RunRoot $Name)
    Assert-NoReparsePath $target
    if (Test-Path -LiteralPath $target) {
        foreach ($item in Get-ChildItem -LiteralPath $target -Recurse -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Release cleanup refuses links.' }
        }
        Remove-Item -LiteralPath $target -Recurse -Force
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

if ((Split-Path -Leaf $SiteRoot) -ne 'staging.tctenglish.site' -or -not (Test-Path -LiteralPath $SiteRoot -PathType Container)) { throw 'Expected the existing staging.tctenglish.site directory.' }
Assert-NoReparsePath $SiteRoot
Assert-NoReparsePath $StateRoot
Assert-ChildPath $privateRoot $StateRoot | Out-Null
if ((Split-Path -Leaf $StateRoot) -ne 'tctenglish-staging-state') { throw 'Expected the dedicated staging state directory.' }
$defaultBackupClient = 'C:\Program Files (x86)\Plesk\MySQL\bin\mariadb-dump.exe'
$backupClient = $defaultBackupClient
[IO.Directory]::CreateDirectory($StateRoot) | Out-Null
$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
if ($manifest.environment -cne 'Staging') { throw 'Only a Staging manifest is accepted.' }
if ($ValidateOnly -or -not $manifest.enabled) {
    Write-Json (Join-Path $StateRoot 'preflight.json') @{
        CheckedAtUtc = [DateTime]::UtcNow.ToString('O'); PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        SiteExists = $true; BackupClientExists = (Test-Path -LiteralPath $backupClient)
        BackupClientPath = $backupClient
        HasStagingConfig = (Test-Path -LiteralPath (Join-Path $SiteRoot 'appsettings.Staging.json'))
        DeploymentEnabled = $false; DatabaseOrWebsiteChanged = $false
    }
    Write-Output 'Plesk release preflight completed; deployment is disabled.'
    return
}

# Hosting-only settings, never taken from CI or a source checkout.
if (-not $SettingsPath) { $SettingsPath = Join-Path $StateRoot 'hosting.json' }
Assert-ChildPath $StateRoot $SettingsPath | Out-Null
Assert-NoReparsePath $SettingsPath
try { $settings = Get-Content -Raw -LiteralPath $SettingsPath | ConvertFrom-Json }
catch { throw 'Staging hosting settings could not be read; credential details omitted.' }
if ($settings.environment -cne 'Staging' -or $settings.siteHost -cne 'staging.tctenglish.site') { throw 'Staging hosting identity did not match.' }
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
try { $builder.set_ConnectionString([string]$settings.connectionString) }
catch { throw 'Staging connection configuration is invalid; details omitted.' }
$database = Db-Value $builder @('Database','Initial Catalog')
$server = Db-Value $builder @('Server','Host','Data Source')
if ($database -cne 'nhtctsnn_tctenglish_staging' -or $server -notin @('127.0.0.1','localhost','::1')) { throw 'Staging database identity did not match; refusing any release writes.' }
$username = Db-Value $builder @('User','User ID','UserID','Uid')
$password = Db-Value $builder @('Password','Pwd')
$port = Db-Value $builder @('Port')
if (-not $port) { $port = '3306' }
if ($username -cne 'nhtctsnn_tct_stage' -or -not $password -or $port -ne '3306') { throw 'Expected the isolated staging database user/port.' }
if (-not $settings.portalUsername -or -not $settings.portalPassword) { throw 'Staging portal credentials are missing.' }
$portalHeader = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$settings.portalUsername + ':' + [string]$settings.portalPassword)) }
$builder['Server'] = '127.0.0.1'
$stagingConnection = $builder.get_ConnectionString()

$repository = 'NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish'
if ($manifest.repository -ne $repository -or $manifest.commit -notmatch '^[0-9a-f]{40}$' -or $manifest.sha256 -notmatch '^[0-9a-fA-F]{64}$' -or [string]$manifest.runNumber -notmatch '^\d+$') { throw 'Invalid release identity.' }
$artifactUri = New-Object Uri($manifest.artifactUrl)
$expectedAssetPath = '/' + $repository + '/releases/download/staging-' + $manifest.runNumber + '-' + $manifest.commit.Substring(0,12) + '/tctenglish-release.zip'
if ($artifactUri.Scheme -ne 'https' -or $artifactUri.Host -ne 'github.com' -or $artifactUri.AbsolutePath -cne $expectedAssetPath -or $artifactUri.Query -or $artifactUri.UserInfo) { throw 'Only the expected immutable staging GitHub release asset is allowed.' }
if (-not (Test-Path -LiteralPath $backupClient)) { throw 'A database backup client is required before deployment.' }

$lock = $null
$offlinePath = Join-Path $SiteRoot 'app_offline.htm'
$migrationAttempted = $false
$offlineCreated = $false
$filesChanged = $false
$appBackup = $null
$previousEnvironment = @{}
foreach ($key in @('DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','ConnectionStrings__DefaultConnection','Database__ServerVersion','Database__ApplyMigrationsOnStartup','Database__SeedOnStartup')) {
    $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$runRoot = Assert-ChildPath $StateRoot (Join-Path $StateRoot ([string]$manifest.runNumber + '-' + $manifest.commit.Substring(0,12)))
if ($manifest.recoverImport) { Invoke-OwnedStagingImportRecovery $SiteRoot $StateRoot $manifest.recoverImport }
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
    Write-Json (Join-Path $runRoot 'owner.json') @{ environment='Staging'; commit=$manifest.commit; runNumber=$manifest.runNumber }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $zipPath = Join-Path $runRoot 'release.zip'
    Invoke-WebRequest -UseBasicParsing -Uri $artifactUri.AbsoluteUri -OutFile $zipPath -TimeoutSec 900
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
    if ($manifest.sharedDataSchema -eq 1 -and (-not (Test-Path -LiteralPath (Join-Path $application 'App_Data\shared-data\catalog.json')) -or -not (Test-Path -LiteralPath (Join-Path $application 'App_Data\shared-data\production.json')))) { throw 'Shared-data artifact is incomplete; stopped before backup/migration.' }
    if (@(Get-ChildItem -LiteralPath $application -Filter 'appsettings.*.json').Count) { throw 'Environment configurations must never come from an artifact.' }

    $effective = Get-Content -Raw -LiteralPath (Join-Path $application 'appsettings.json') | ConvertFrom-Json
    foreach ($name in @('appsettings.json','appsettings.Staging.json')) {
        $hostConfig = Join-Path $SiteRoot $name
        if (Test-Path -LiteralPath $hostConfig) { Merge-Config $effective (Get-Content -Raw -LiteralPath $hostConfig | ConvertFrom-Json) }
    }
    $effective.ConnectionStrings.DefaultConnection = $stagingConnection
    $effective | Add-Member -NotePropertyName Database -NotePropertyValue ([pscustomobject]@{ ServerVersion='10.5.23-mariadb'; ApplyMigrationsOnStartup=$false; SeedOnStartup=$false }) -Force
    $effective | Add-Member -NotePropertyName AllowedHosts -NotePropertyValue 'staging.tctenglish.site' -Force
    Write-Json (Join-Path $application 'appsettings.Staging.json') $effective
    Write-Json (Join-Path $migrationRoot 'appsettings.json') $effective
    # Preserve hosting environment variables (including the staging API key), not
    # the old hosting module/binary configuration. Never print XML with secrets.
    $hostWebConfig = Join-Path $SiteRoot 'web.config'
    [xml]$oldWeb = Get-Content -Raw -LiteralPath $hostWebConfig
    [xml]$newWeb = Get-Content -Raw -LiteralPath (Join-Path $application 'web.config')
    $oldAsp = $oldWeb.SelectSingleNode('//system.webServer/aspNetCore')
    $newAsp = $newWeb.SelectSingleNode('//system.webServer/aspNetCore')
    if (-not $oldAsp -or -not $newAsp) { throw 'Staging IIS hosting configuration is missing.' }
    $oldEnvironment = $oldAsp.SelectSingleNode('environmentVariables')
    $newEnvironment = $newAsp.SelectSingleNode('environmentVariables')
    if ($newEnvironment) { [void]$newAsp.RemoveChild($newEnvironment) }
    if ($oldEnvironment) { [void]$newAsp.AppendChild($newWeb.ImportNode($oldEnvironment, $true)) }
    else { [void]$newAsp.AppendChild($newWeb.CreateElement('environmentVariables')) }
    $newEnvironment = $newAsp.SelectSingleNode('environmentVariables')
    foreach ($item in @(@('ASPNETCORE_ENVIRONMENT','Staging'), @('DOTNET_ENVIRONMENT','Staging'), @('ConnectionStrings__DefaultConnection',$stagingConnection), @('Database__ApplyMigrationsOnStartup','false'), @('Database__SeedOnStartup','false'))) {
        $node = $newEnvironment.SelectSingleNode("environmentVariable[@name='$($item[0])']")
        if (-not $node) { $node = $newWeb.CreateElement('environmentVariable'); $node.SetAttribute('name',$item[0]); [void]$newEnvironment.AppendChild($node) }
        $node.SetAttribute('value',$item[1])
    }
    $newWeb.Save((Join-Path $application 'web.config'))
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
    $env:DOTNET_ENVIRONMENT = 'Staging'
    $env:ASPNETCORE_ENVIRONMENT = 'Staging'
    $env:ConnectionStrings__DefaultConnection = $stagingConnection
    $env:Database__ServerVersion = '10.5.23-mariadb'
    $env:Database__ApplyMigrationsOnStartup = 'false'
    $env:Database__SeedOnStartup = 'false'
    $migrationAttempted = $true
    Push-Location $migrationRoot
    try { & (Join-Path $migrationRoot 'efbundle.exe') *> (Join-Path $runRoot 'migration.log'); if ($LASTEXITCODE -ne 0) { throw 'Database migration failed; maintenance and backup have been retained.' } }
    finally { Pop-Location }

    $sharedRoot = Join-Path $application 'App_Data\shared-data'
    if (Test-Path -LiteralPath (Join-Path $sharedRoot 'catalog.json')) {
        Push-Location $application
        try {
            & (Join-Path $application 'TCTEnglish.exe') '--shared-data=sync' '--shared-target=staging' "--shared-root=$sharedRoot" "--shared-webroot=$(Join-Path $SiteRoot 'wwwroot')" "--shared-owners=$(Join-Path $StateRoot 'shared-data-owners.json')" *> (Join-Path $runRoot 'shared-data.log')
            if ($LASTEXITCODE -ne 0) { throw 'Shared-data conflict/import failed; maintenance and backup retained. Inspect private shared-data.log.' }
        } finally { Pop-Location }
    } elseif ($manifest.sharedDataSchema -eq 1) { throw 'This release requires the reviewed shared-data artifact.' }

    $filesChanged = $true
    Copy-Tree $application $SiteRoot
    $wwwRoot = Assert-ChildPath $SiteRoot (Join-Path $SiteRoot 'wwwroot')
    [IO.Directory]::CreateDirectory($wwwRoot) | Out-Null
    $pendingMarkerPath = Assert-ChildPath $wwwRoot (Join-Path $wwwRoot 'tct-release.pending.json')
    $markerPath = Assert-ChildPath $wwwRoot (Join-Path $wwwRoot 'tct-release.json')
    # Pending marker is not the public success document; promote only after readiness.
    $markerPayload = @{ commit=$manifest.commit; runNumber=$manifest.runNumber; environment='Staging' }
    if ($null -ne $manifest.sha256 -and "$($manifest.sha256)".Length -gt 0) {
        $markerPayload.sha256 = [string]$manifest.sha256
    }
    Write-Json $pendingMarkerPath $markerPayload
    Remove-Item -LiteralPath $offlinePath
    $ready = $false
    for ($attempt=0; $attempt -lt 24; $attempt++) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri 'https://staging.tctenglish.site/health/ready' -Headers $portalHeader -MaximumRedirection 0 -TimeoutSec 10
            if ($response.StatusCode -eq 200 -and ($response.Content | ConvertFrom-Json).status -eq 'ready') { $ready=$true; break }
        } catch { }
        Start-Sleep -Seconds 5
    }
    if (-not $ready) { throw 'Application readiness failed after release.' }
    [IO.File]::Copy($pendingMarkerPath, $markerPath, $true)
    [IO.File]::Delete($pendingMarkerPath)
    Write-Json $statePath @{ commit=$manifest.commit; runNumber=$manifest.runNumber; completedAtUtc=[DateTime]::UtcNow.ToString('O'); backupDirectory=$runRoot }
    [IO.File]::Delete($optionsPath)
    Write-Output ('Release completed: ' + $manifest.commit)
    # Successful runs retain SQL/logs and only the newest previous-app. Never
    # prune failed/recovery runs or the subscription's shared Recycle Bin.
    try {
        Remove-OwnedRunTree $runRoot 'stage'
        [IO.File]::Delete((Assert-ChildPath $runRoot $zipPath))
        if ($current -and $current.backupDirectory -and $current.backupDirectory -ne $runRoot) {
            Remove-OwnedRunTree ([string]$current.backupDirectory) 'previous-app'
        }
    } catch { Write-Warning 'Staging is ready; owned temporary cleanup needs operator attention.' }
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
} finally {
    if ($lock) { $lock.Dispose() }
    if ($optionsPath -and (Test-Path -LiteralPath $optionsPath)) { [IO.File]::Delete($optionsPath) }
    foreach ($key in $previousEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], 'Process') }
}
