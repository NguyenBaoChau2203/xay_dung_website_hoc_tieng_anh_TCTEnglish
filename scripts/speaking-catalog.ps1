#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Snapshot','Status','Stage','Retire','Publish','ImportLocalTest')][string]$Action='Status',[switch]$Pilot,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo '.appdata/local/speaking-catalog-transfer/build'
$names=@('ConnectionStrings__DefaultConnection','TEMP','TMP')
$previous=@{};foreach($name in $names){$previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
Push-Location $repo
try{
 $env:TEMP=Join-Path $repo '.appdata/local/temp';$env:TMP=$env:TEMP
 if(-not $SkipBuild){& dotnet build tools/SpeakingCatalogImport/SpeakingCatalogImport.csproj -c Release --output $output --verbosity quiet;if($LASTEXITCODE-ne 0){throw 'Speaking catalog build failed.'}}
 $values=@{};foreach($line in Get-Content .appdata/local/database.env){if($line-match '^([^#=]+)=(.*)$'){$values[$Matches[1]]=$Matches[2]}}
 if($values.MARIADB_DATABASE-cne 'tctenglish_dev'-or $values.MARIADB_USER-cne 'tct_dev'-or -not $values.MARIADB_PASSWORD){throw 'Unexpected Docker local configuration.'}
 $env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3307;Database=tctenglish_dev;User=tct_dev;Password=$($values.MARIADB_PASSWORD);SslMode=None;"
 $arguments=@((Join-Path $output 'SpeakingCatalogImport.dll'),$Action,$repo);if($Pilot){$arguments+='--pilot'}
 & dotnet @arguments
 if($LASTEXITCODE-ne 0){throw "Speaking catalog $Action failed. Preserve the checkpoint and resolve the reported source; never reset/overwrite."}
}finally{foreach($name in $names){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')};Pop-Location}
