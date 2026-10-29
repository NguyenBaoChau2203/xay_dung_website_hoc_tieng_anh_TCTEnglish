$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
# Compatibility entrypoint; use the same server-enforced reads as native clients.
& node (Join-Path $repoRoot 'scripts/agent-tools.cjs') mcp git
exit $LASTEXITCODE
