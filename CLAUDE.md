@AGENTS.md
@.ai/README.md

Apply AGENTS.md's "Database, PR and Release Rules (Automatic)" even when the
feature prompt omits database/deploy work. Schema changes need their migration
in the same PR; CI success is distinct from a verified production release.

The imported files are the canonical project rules and routing table. Project
skills under .claude/skills are small discovery adapters to .agents/skills/tct-*.
Read the canonical skill before following its scripts or relative references.
Use .ai/roles as inline review lenses with the same agent. Never delegate to
subagents, another chat or another AI CLI session.

Native MCP configuration is in .mcp.json; native lifecycle hooks are in
.claude/settings.json. First MCP use restores locked runtime dependencies.
Workspace/server trust remains the user's decision. See docs/team-agent-setup.md.
Do not initialize/full-rebuild CodeGraph automatically. Incremental server sync
requires valid owner-approved `.ai/codegraph.local.json`; other checkouts stay
read-only. This ignored local approval is not transferred by Git.
