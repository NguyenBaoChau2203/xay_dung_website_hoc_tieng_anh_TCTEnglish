param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('SessionStart', 'PreToolUse', 'PostToolUse', 'Stop')]
    [string] $EventName
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# Inherit JSON stdin directly. Do not collect or evaluate any event fields.
# Reuse the already installed locked environment; never install at hook startup.
& node (Join-Path $PSScriptRoot 'agent-tools.cjs') hook codex $EventName
exit $LASTEXITCODE
