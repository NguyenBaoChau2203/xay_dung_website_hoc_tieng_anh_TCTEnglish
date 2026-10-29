@AGENTS.md
@.ai/README.md

The imported files are the canonical project rules and routing table. Project
skills under .claude/skills are small discovery adapters to .agents/skills/tct-*.
Read the canonical skill before following its scripts or relative references.
Use .ai/roles as inline review lenses with the same agent. Never delegate to
subagents, another chat or another AI CLI session.

Native MCP configuration is in .mcp.json; native lifecycle hooks are in
.claude/settings.json. First MCP use restores locked runtime dependencies.
Workspace/server trust remains the user's decision. See docs/team-agent-setup.md.
Do not initialize, re-index or sync CodeGraph automatically.
