$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
# Compatibility wrapper; same conflict checks and both executable bits as setup.
$launcher = Join-Path $repoRoot 'scripts/agent-tools.cjs'
& node -e "require(process.argv[1]).installGitHook(process.argv[2]);" $launcher $repoRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not install the local Git hook path.' }
Write-Output 'Project pre-commit and post-merge hooks enabled; no global config changed.'
