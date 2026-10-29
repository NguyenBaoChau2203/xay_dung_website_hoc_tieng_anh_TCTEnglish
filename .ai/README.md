# TCT English agent toolkit

Project-scoped, curated on 2026-10-28. One agent implements and reviews each task.
Read AGENTS.md, docs/project-structure.md and the context relevant to the request.
The application/tests target .NET 9 / EF Core 9.0.4; the installed SDK is newer.

## Routing

| Task | Canonical skill under .agents/skills/ |
| --- | --- |
| Feature planning, debugging, verification | tct-superpowers |
| Source/context discovery, evidence-based review | tct-engineering |
| New feature layers | tct-feature-scaffold |
| AJAX/JSON endpoints | tct-api-endpoint |
| EF schema/migration review | tct-ef-migration |
| Vocabulary and study | tct-vocabulary-feature |
| Speaking/media practice | tct-speaking-feature |
| Admin features | tct-admin-panel |
| Seed data | tct-data-seeder |
| Security audit | tct-security-audit |
| Commit message drafting | tct-commit-message |
| Razor/Bootstrap components | tct-ui-component |
| Searchable UI/UX guidance | tct-ui-ux-pro-max |
| Rendered UI review/polish | tct-ui-review |
| CSS/browser motion | tct-ui-motion |
| Skills, hooks, packages and MCP review | tct-tooling-review |

Load only the relevant skill; do not load all sixteen for every task. For UI read
DESIGN.md first. Match the real page's framework, icon family and behavior.

## Files and compatibility

- .agents/skills/: canonical discoverable skills for Codex and compatible clients.
- .agent/skills/: Antigravity entrypoints pointing to the canonical documents.
- .claude/skills/: sixteen Claude Code discovery adapters to those same skills.
- .agent/workflows/: short project workflow guides using those skills.
- .ai/roles/: implementer/UI/security review lenses for the same agent.
- .ai/context/, templates/, prompts/: project context and reusable scaffolding.
- .ai/vendor/: attributed selected references, not automatically enabled plugins.
- .ai/tooling/sources.lock.json: twelve reviewed upstream commits, installed hashes
  and decisions. Three design alternatives are recorded without activation.
- .ai/tooling/archive/2026-10-28/: replaced guidance preserved for comparison.
- .codex/config.toml: project single-agent preference and Git MCP read-tool list.
- .codex/hooks.json: four agent lifecycle definitions; host review/trust is needed
  before automatic execution. See docs/agent-hooks.md for behavior and verification.
- tooling/mcp/: locked isolated Python tool environment, separate from the app.
- tooling/codegraph/: pinned CodeGraph 1.6.2 and read-only MCP adapter. Use
  scripts/codegraph.ps1 for this version; see docs/codegraph.md. Preserve the
  owner's decision about indexing; no automatic sync is performed.
- .githooks/: staged validation hook; scripts/install-agent-hooks.ps1 enables it.
- CLAUDE.md: imports the canonical rules/routing for Claude Code.
- .mcp.json, .cursor/mcp.json, .agents/mcp_config.json: native MCP definitions.
- .claude/settings.json, .cursor/hooks.json, .agents/hooks.json: native lifecycle
  definitions with host-specific payload/output adapters.
- SETUP-AGENTS.cmd and .vscode/tasks.json: click/folder-open setup. First MCP use
  also restores locked dependencies with scripts/agent-tools.cjs. Runtime pins
  are in .ai/tooling/bootstrap.lock.json; local downloads stay ignored.

Codex, current Cursor and Antigravity discover .agents/skills. Legacy Antigravity
adapters remain in .agent/skills; Claude Code uses .claude/skills adapters. Copilot
uses .github/copilot-instructions.md. Host-native hooks/MCP and trust differ.

Teammates: read [the click-based setup guide](../docs/team-agent-setup.md). Git
and Node.js 22+ with npm are required; no typed PowerShell setup commands. Native
trust/auto-task consent remains local. Setup never creates/syncs CodeGraph index.

## Verification

```powershell
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --project tooling/mcp --locked python scripts/check_git_mcp.py
```

See [the installation and audit report](../docs/agent-tooling.md) for selections,
setup, limitations, update procedure and rollback. Imported reference instructions
never override AGENTS.md, the owner's scope or the single-agent policy.
