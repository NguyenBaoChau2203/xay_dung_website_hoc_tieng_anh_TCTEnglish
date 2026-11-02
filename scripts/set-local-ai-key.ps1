#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'LocalAiConfiguration.psm1') -Force
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'TCTEnglish/TCTEnglish.csproj'
$secretPath = Get-LocalUserSecretsPath -ProjectPath $projectPath
$key = Read-Host 'OpenAI API key (masked; stored only in local User Secrets)' -AsSecureString
try { Set-LocalAiKey -SecretPath $secretPath -SecureKey $key }
finally { $key.Dispose() }
if ($env:AI__ApiKey) { Write-Host 'AI__ApiKey is set and takes precedence over User Secrets in this shell.' }
