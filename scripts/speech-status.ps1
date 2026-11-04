#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'LocalAiConfiguration.psm1') -Force

# Metadata-only speech configuration status. Never calls OpenAI or any network endpoint.
$secretPath = Get-LocalUserSecretsPath -ProjectPath (Join-Path $repoRoot 'TCTEnglish/TCTEnglish.csproj')
$secrets = Read-LocalSecretStore -SecretPath $secretPath
# The local launcher always starts Development. Read the same file layers before
# secrets/environment; never emit their contents (they may contain private values).
$fileSettings = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
$fileSources = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($fileName in @('appsettings.json', 'appsettings.Development.json')) {
    $settingsPath = Join-Path $repoRoot "TCTEnglish/$fileName"
    if (Test-Path -LiteralPath $settingsPath) {
        $layer = Read-LocalSecretStore -SecretPath $settingsPath
        foreach ($settingName in $layer.Keys) {
            $fileSettings[$settingName] = $layer[$settingName]
            $fileSources[$settingName] = $fileName
        }
    }
}

function Get-EffectiveSetting {
    param(
        [Parameter(Mandatory)][string]$EnvName,
        [Parameter(Mandatory)][string]$SecretKey,
        [string]$Default = $null
    )
    $envValue = [Environment]::GetEnvironmentVariable($EnvName, 'Process')
    if ($null -ne $envValue) { return [pscustomobject]@{ Value = $envValue; Source = $EnvName } }
    if ($secrets.ContainsKey($SecretKey)) {
        return [pscustomobject]@{ Value = [string]$secrets[$SecretKey]; Source = "User Secrets $SecretKey" }
    }
    if ($fileSettings.ContainsKey($SecretKey)) {
        return [pscustomobject]@{ Value = [string]$fileSettings[$SecretKey]; Source = $fileSources[$SecretKey] }
    }
    return [pscustomobject]@{ Value = $Default; Source = 'default' }
}

function Test-UsableKey([string]$key) {
    return -not [string]::IsNullOrWhiteSpace($key) -and $key -notmatch '\s' -and -not $key.StartsWith('AIza', [StringComparison]::Ordinal)
}

$enabledSetting = Get-EffectiveSetting -EnvName 'Learning__Speech__Enabled' -SecretKey 'Learning:Speech:Enabled' -Default 'true'
$enabled = $true
$configurationValid = $true
if ($null -ne $enabledSetting.Value) {
    if (-not [bool]::TryParse([string]$enabledSetting.Value, [ref]$enabled)) {
        $configurationValid = $false
        $enabled = $false
    }
}

$modelSetting = Get-EffectiveSetting -EnvName 'Learning__Speech__OpenAI__Model' -SecretKey 'Learning:Speech:OpenAI:Model' -Default 'gpt-transcribe'
$baseUrlSetting = Get-EffectiveSetting -EnvName 'Learning__Speech__OpenAI__BaseUrl' -SecretKey 'Learning:Speech:OpenAI:BaseUrl' -Default 'https://api.openai.com/v1/'
$baseUrl = [string]$baseUrlSetting.Value
$endpointValid = $false
try {
    if (-not [string]::IsNullOrWhiteSpace($baseUrl)) {
        $normalized = $baseUrl.Trim().TrimEnd('/') + '/'
        $uri = [Uri]$normalized
        $endpointValid = ($uri.Scheme -eq 'https' -or ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) -and
            [string]::IsNullOrEmpty($uri.UserInfo) -and [string]::IsNullOrEmpty($uri.Query) -and [string]::IsNullOrEmpty($uri.Fragment)
    }
} catch { $endpointValid = $false }

function Resolve-KeyAuthority {
    $envSpeech = [Environment]::GetEnvironmentVariable('Learning__Speech__OpenAI__ApiKey', 'Process')
    if ($null -ne $envSpeech) {
        return [pscustomobject]@{
            KeyPresent = (Test-UsableKey $envSpeech)
            Source = $(if (Test-UsableKey $envSpeech) { 'Learning__Speech__OpenAI__ApiKey' } else { 'Learning__Speech__OpenAI__ApiKey-invalid' })
            Explicit = $true
        }
    }
    if ($secrets.ContainsKey('Learning:Speech:OpenAI:ApiKey') -or $fileSettings.ContainsKey('Learning:Speech:OpenAI:ApiKey')) {
        $setting = Get-EffectiveSetting -EnvName 'Learning__Speech__OpenAI__ApiKey' -SecretKey 'Learning:Speech:OpenAI:ApiKey'
        $speechSecret = [string]$setting.Value
        return [pscustomobject]@{
            KeyPresent = (Test-UsableKey $speechSecret)
            Source = $(if (Test-UsableKey $speechSecret) { $setting.Source } else { "$($setting.Source)-invalid" })
            Explicit = $true
        }
    }

    $envAi = [Environment]::GetEnvironmentVariable('AI__ApiKey', 'Process')
    if ($null -ne $envAi) {
        return [pscustomobject]@{
            KeyPresent = (Test-UsableKey $envAi)
            Source = $(if (Test-UsableKey $envAi) { 'AI__ApiKey' } else { 'AI__ApiKey-invalid' })
            Explicit = $true
        }
    }
    if ($secrets.ContainsKey('AI:ApiKey') -or $fileSettings.ContainsKey('AI:ApiKey')) {
        $setting = Get-EffectiveSetting -EnvName 'AI__ApiKey' -SecretKey 'AI:ApiKey'
        $secretAi = [string]$setting.Value
        return [pscustomobject]@{
            KeyPresent = (Test-UsableKey $secretAi)
            Source = $(if (Test-UsableKey $secretAi) { $setting.Source } else { "$($setting.Source)-invalid" })
            Explicit = $true
        }
    }

    $openAi = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'Process')
    if ($null -ne $openAi) {
        return [pscustomobject]@{
            KeyPresent = (Test-UsableKey $openAi)
            Source = $(if (Test-UsableKey $openAi) { 'OPENAI_API_KEY' } else { 'OPENAI_API_KEY-invalid' })
            Explicit = $true
        }
    }

    return [pscustomobject]@{ KeyPresent = $false; Source = 'missing'; Explicit = $false }
}

$key = Resolve-KeyAuthority
$configured = $configurationValid -and [bool]$enabled -and $endpointValid -and [bool]$key.KeyPresent

$result = [ordered]@{
    Status         = $(if ($configured) { 'configured' } else { 'not-configured' })
    Configured     = $configured
    Enabled        = [bool]$enabled
    EnabledSource  = $enabledSetting.Source
    ConfigurationValid = $configurationValid
    KeyPresent     = [bool]$key.KeyPresent
    KeySource      = $key.Source
    Model          = $(if ([string]::IsNullOrWhiteSpace([string]$modelSetting.Value)) { 'gpt-transcribe' } else { ([string]$modelSetting.Value).Trim() })
    ModelSource    = $modelSetting.Source
    BaseUrl        = $(if ($endpointValid) { $uri.AbsoluteUri } else { '[invalid endpoint]' })
    BaseUrlSource  = $baseUrlSetting.Source
    EndpointValid  = $endpointValid
    VerifiedLive   = $false
    Note           = 'configured means Enabled+valid endpoint+usable key locally; account/model/quota still need owner live opt-in'
}
$result | ConvertTo-Json -Compress
