#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $root '.appdata/local/staging'
$target = Join-Path $directory 'ai-key.json'
# A staging-only handoff. Never read/write the owner's local app User Secrets.
if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw 'Provision staging before entering its key.' }
if (Test-Path -LiteralPath $target) { throw 'Staging key handoff already exists. Ask the operator to consume it before entering another key.' }
$key = Read-Host 'OpenAI STAGING key (masked; separate from local/production)' -AsSecureString
$ptr = [IntPtr]::Zero
$stream = $null
$plain = $null
try {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($key)
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr).Trim()
    if ($plain.Length -lt 20 -or $plain -match '\s') { throw 'Invalid key input; no value was saved.' }
    $stream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $bytes = [Text.Encoding]::UTF8.GetBytes((@{ environment='Staging'; apiKey=$plain } | ConvertTo-Json -Compress))
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) }
    finally { [Array]::Clear($bytes,0,$bytes.Length) }
    Write-Host 'Staging-only private handoff saved. Tell the agent it is ready; do not paste the key into chat.'
} finally {
    if ($stream) { $stream.Dispose() }
    if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    $plain = $null
    $key.Dispose()
}
