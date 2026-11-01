#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Setup', 'Verify')][string]$Action = 'Setup',
    [switch]$Offline,
    [string]$MediaRoot = '',
    [string]$LockPath = '',
    [string]$ArchivePath = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $MediaRoot) { $MediaRoot = Join-Path $repoRoot '.appdata/local/media' }
if (-not $LockPath) { $LockPath = Join-Path $repoRoot 'tooling/media/artifacts.lock.json' }
$pin = Get-Content -LiteralPath $LockPath -Raw | ConvertFrom-Json
if ($pin.schemaVersion -ne 1 -or $pin.platform -ne 'win-x64' -or -not $IsWindows) { throw 'Pinned local FFmpeg bootstrap supports Windows x64 only.' }
function Assert-Hash([string]$path, [string]$expected) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expected) {
        throw 'Pinned FFmpeg artifact is missing or has a checksum mismatch. Do not use PATH as a substitute.'
    }
}
New-Item -ItemType Directory -Force -Path $MediaRoot | Out-Null
$lock = [IO.File]::Open((Join-Path $MediaRoot 'setup.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
try {
    $allPresent = $true
    foreach ($file in $pin.files) {
        if ([IO.Path]::GetFileName([string]$file.name) -cne $file.name) { throw 'Invalid artifact filename in media lock.' }
        $target = Join-Path $MediaRoot $file.name
        if (Test-Path -LiteralPath $target) { Assert-Hash $target $file.sha256 } else { $allPresent = $false }
    }
    if (-not $allPresent) {
        if ($Action -eq 'Verify') { throw 'Pinned FFmpeg is missing. Run scripts/local-media.ps1 -Action Setup.' }
        if (-not $ArchivePath) { $ArchivePath = Join-Path $MediaRoot 'ffmpeg-8.1.1-essentials_build.zip' }
        if (-not (Test-Path -LiteralPath $ArchivePath)) {
            if ($Offline) { throw 'Pinned FFmpeg archive unavailable offline.' }
            $download = Join-Path $MediaRoot ('download-' + [guid]::NewGuid().ToString('N') + '.tmp')
            try {
                Invoke-WebRequest -Uri $pin.archive.url -OutFile $download -TimeoutSec 180
                Assert-Hash $download $pin.archive.sha256
                if ((Get-Item -LiteralPath $download).Length -ne $pin.archive.bytes) { throw 'FFmpeg archive size mismatch.' }
                [IO.File]::Move($download, $ArchivePath, $false)
            } finally { if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force } }
        }
        Assert-Hash $ArchivePath $pin.archive.sha256
        if ((Get-Item -LiteralPath $ArchivePath).Length -ne $pin.archive.bytes) { throw 'FFmpeg archive size mismatch.' }
        $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
        try {
            foreach ($file in $pin.files) {
                $entry = $archive.GetEntry($file.entry)
                if ($null -eq $entry) { throw 'Missing locked FFmpeg archive entry.' }
                $temporary = Join-Path $MediaRoot ('extract-' + [guid]::NewGuid().ToString('N') + '.tmp')
                try {
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temporary, $false)
                    Assert-Hash $temporary $file.sha256
                    [IO.File]::Move($temporary, (Join-Path $MediaRoot $file.name), $true)
                } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
            }
        } finally { $archive.Dispose() }
    }
    foreach ($file in $pin.files) { Assert-Hash (Join-Path $MediaRoot $file.name) $file.sha256 }
    [pscustomobject]@{ Status = 'verified'; Version = $pin.version; ExecutablePath = [IO.Path]::GetFullPath((Join-Path $MediaRoot 'ffmpeg.exe')) }
} finally { $lock.Dispose() }
