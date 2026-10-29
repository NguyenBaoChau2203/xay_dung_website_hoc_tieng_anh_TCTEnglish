$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$launcher = Join-Path $repoRoot 'tooling/codegraph/launch.cjs'
& node $launcher @args
exit $LASTEXITCODE
