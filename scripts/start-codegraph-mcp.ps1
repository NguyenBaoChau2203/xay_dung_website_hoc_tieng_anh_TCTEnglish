$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
# Compatibility entrypoint; first-use setup is shared, indexing stays disabled.
& node (Join-Path $repoRoot 'scripts/agent-tools.cjs') mcp codegraph
exit $LASTEXITCODE
