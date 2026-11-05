#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Sync','Status','Validate','Export','Approve','MapOwner')][string]$Action='Sync',
    [string]$Kind, [int]$EntityId, [string]$Key, [string]$PackageId,
    [int]$Version, [string]$OwnerAlias,
    [ValidateSet('Debug','Release')][string]$Configuration='Release',
    [switch]$SkipBuild,
    [switch]$DatabaseReady
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$state=Join-Path $repoRoot '.appdata/local/shared-data'
$catalog=Join-Path $repoRoot 'data/shared'
$keys=@('ConnectionStrings__DefaultConnection','Database__ServerVersion','Database__ApplyMigrationsOnStartup','Database__SeedOnStartup','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','TCT_SHARED_SYNC_IN_PROGRESS','TEMP','TMP')
$previous=@{};foreach($name in $keys){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$lock=$null
function Checked([string]$Exe,[string[]]$Arguments){& $Exe @Arguments;if($LASTEXITCODE -ne 0){throw "Shared-data $Action failed (exit $LASTEXITCODE). Code may already be pulled; data sync has NOT completed. Resolve the reported conflict/prerequisite and rerun scripts/shared-data.ps1."}}
Push-Location $repoRoot
try{
    New-Item -ItemType Directory -Path $state -Force|Out-Null
    $lock=[IO.File]::Open((Join-Path $state 'sync.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $env:TCT_SHARED_SYNC_IN_PROGRESS='1'
    $temp=Join-Path $repoRoot '.appdata/local/temp';New-Item -ItemType Directory -Path $temp -Force|Out-Null
    $env:TEMP=$temp;$env:TMP=$temp
    $env:DOTNET_ENVIRONMENT='Development';$env:ASPNETCORE_ENVIRONMENT='Development'
    $env:Database__ServerVersion='10.5.23-mariadb';$env:Database__ApplyMigrationsOnStartup='false';$env:Database__SeedOnStartup='false'
    if($Action -notin @('Validate','Approve')){
        if(-not $DatabaseReady){Checked 'pwsh' @('-NoProfile','-File',(Join-Path $PSScriptRoot 'start-local.ps1'),'-DatabaseOnly')}
        $envFile=Join-Path $repoRoot '.appdata/local/database.env'
        $values=@{};foreach($line in Get-Content -LiteralPath $envFile){if($line -match '^([^#=]+)=(.*)$'){$values[$Matches[1]]=$Matches[2]}}
        if($values.MARIADB_DATABASE -cne 'tctenglish_dev' -or $values.MARIADB_USER -cne 'tct_dev' -or -not $values.MARIADB_PASSWORD){throw 'Expected this checkout''s Docker local database; no remote target is supported.'}
        $env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"
    }
    $projectRoot=Join-Path $repoRoot 'TCTEnglish'
    $cliOutput=if($SkipBuild){Join-Path $projectRoot "bin/$Configuration/net9.0"}else{Join-Path $state "build/$Configuration"}
    if(-not $SkipBuild){Checked 'dotnet' @('build','TCTEnglish/TCTEnglish.csproj','-c',$Configuration,'--output',$cliOutput,'--verbosity','quiet','/p:WarningLevel=0')}
    $cliDll=Join-Path $cliOutput 'TCTEnglish.dll'
    if(-not (Test-Path -LiteralPath $cliDll)){throw 'CLI build is missing. Rerun without -SkipBuild; no database operation was started.'}
    function Invoke-Cli([string[]]$Arguments){
        Push-Location $projectRoot
        try{Checked 'dotnet' (@($cliDll)+$Arguments)}finally{Pop-Location}
    }
    if($Action -notin @('Validate','Approve')){
        Invoke-Cli @('--shared-data=migrate','--shared-target=local',"--shared-root=$catalog")
        if($Action -eq 'Sync' -and -not $DatabaseReady){
            # New teammates need the existing guarded first-user bootstrap too.
            # It skips any database that already has users; never replaces data.
            Invoke-Cli @('--local-bootstrap=true')
        }
    }
    $operation=if($Action -eq 'MapOwner'){'map-owner'}else{$Action.ToLowerInvariant()}
    $argsList=@("--shared-data=$operation",'--shared-target=local',"--shared-root=$catalog","--shared-webroot=$(Join-Path $repoRoot 'TCTEnglish/wwwroot')","--shared-owners=$(Join-Path $state 'owners.json')")
    if($Action -eq 'Export'){$argsList+=@("--shared-kind=$Kind","--shared-id=$EntityId","--shared-key=$Key","--shared-package=$PackageId")}
    if($Action -eq 'Approve'){$argsList+=@("--shared-package=$PackageId","--shared-version=$Version")}
    if($Action -eq 'MapOwner'){$argsList+=@("--shared-alias=$OwnerAlias","--shared-id=$EntityId")}
    Invoke-Cli $argsList
    if($Action -eq 'Sync'){@{completedAtUtc=[DateTime]::UtcNow.ToString('O');catalogSha256=(Get-FileHash -LiteralPath (Join-Path $catalog 'catalog.json') -Algorithm SHA256).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $state 'last-sync.json') -Encoding utf8NoBOM}
}finally{
    if($lock){$lock.Dispose()}
    foreach($name in $keys){if($null -eq $previous[$name]){[Environment]::SetEnvironmentVariable($name,[NullString]::Value,'Process')}else{[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}}
    Pop-Location
}
