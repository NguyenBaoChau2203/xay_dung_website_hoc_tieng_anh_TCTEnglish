#Requires -Version 7
param(
    [Parameter(Mandatory = $true)]
    [string] $Script,

    [string] $BlendFile = ""
)

$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot $Script
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Script not found: $scriptPath"
}

$blender = $env:BLENDER_EXE
if (-not $blender) {
    $cmd = Get-Command blender -ErrorAction SilentlyContinue
    if ($cmd) { $blender = $cmd.Source }
}

if (-not $blender -or -not (Test-Path -LiteralPath $blender)) {
    Write-Host @"
[tico] Blender not found.
Install Blender, then either:
  1) Add blender.exe to PATH, or
  2) Set BLENDER_EXE to the full path.

Blocked phases: modeling / rigging / animation / GLB export.
Landing will keep using the welcome PNG fallback until tico-welcome.glb exists.
"@
    exit 3
}

$argsList = @("--background")
if ($BlendFile) {
    if (-not (Test-Path -LiteralPath $BlendFile)) {
        throw "Blend file not found: $BlendFile"
    }
    $argsList += $BlendFile
}
$argsList += @("--python", $scriptPath)

Write-Host "[tico] Running: $blender $($argsList -join ' ')"
& $blender @argsList
exit $LASTEXITCODE
