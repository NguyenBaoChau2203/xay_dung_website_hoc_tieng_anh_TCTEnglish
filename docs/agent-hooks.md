# TCT English agent lifecycle hooks

Current team setup (2026-10-29): see [team-agent-setup.md](team-agent-setup.md).
All four clients have native definitions and use scripts/agent-tools.cjs.
Codex/Claude share the canonical output contract; Cursor and Antigravity use
explicit field/event translations. Native trust remains local to each client.

## Why this follow-up exists

The first tooling pass implemented Git pre-commit checks and deliberately omitted
upstream automatic session hooks. That left the agent-lifecycle part of the
owner's request incomplete. The single-agent policy was not a reason to omit
local lifecycle checks. This follow-up adds project-specific handlers without
importing upstream worker agents or automatic installers.

## Installed definitions

Definitions: [.codex/hooks.json](../.codex/hooks.json),
[.claude/settings.json](../.claude/settings.json),
[.cursor/hooks.json](../.cursor/hooks.json), [.agents/hooks.json](../.agents/hooks.json).
Node entrypoint: [scripts/agent-tools.cjs](../scripts/agent-tools.cjs).
Shared Python checks: [scripts/agent_lifecycle.py](../scripts/agent_lifecycle.py).
Former PowerShell launchers remain compatibility wrappers; the native client
definitions do not require PowerShell.
The project sets features.hooks=true and keeps multi_agent=false.

| Event | Behavior | Limit |
| --- | --- | --- |
| SessionStart | Supplies concise project-policy/skill pointers at startup, resume, clear, compact or fork; snapshots toolkit content. | Does not copy the transcript or entire skill catalog into context. |
| PreToolUse | Denies direct spawn_agent/Agent/followup_task while multi_agent=false. | Does not approve other calls or rewrite shell commands; not every tool path is interceptable. |
| PostToolUse | After shell/apply_patch tools, validates toolkit content if changed. | Reports failures as context; does not undo edits or hide completed tool results. |
| Stop | Checks changed tooling or reuses verification for identical content. | Surfaces failures without forcing continuation loops, committing or claiming C# tests passed. |

The validator checks skills, references, source hashes, MCP config and reviewed
lifecycle definitions. Application changes still need build/tests from AGENTS.md.
Unchanged read-only operations remain quiet.

The SessionStart pointer now asks agents to reuse instructions already present,
load only the active plan step and count fresh CodeGraph source as read.
Context7 is routed conditionally to external-library documentation, not private
repository context. This is guidance, not a tool-call interceptor: hooks do not
force an agent to use an MCP or guarantee fewer billed tokens. The pointer is
shared through the existing native host output translations; no hook definition,
trust record, automatic index or remote server was added.
Owner-approved CodeGraph incremental auto-sync is selected separately by the
checkout-local policy in docs/codegraph.md; lifecycle hooks do not run CLI sync.

The Node fast guard denies direct delegation before Python is installed. Other
validation remains pending until setup/MCP bootstrap prepares the environment.
Cursor also has a native subagentStart denial gate. Antigravity PostToolUse returns
the required empty object; diagnostic feedback goes to stderr. Its Stop response
allows stopping, never forces continuation. These contracts are not copied blindly
from Codex. No hook auto-approves unrelated tool calls.

No hook starts another model or downloads dependencies. Commands are fixed local
scripts; tool_input is
never executed. The runner does not read transcripts, prompts, environment files
or appsettings. It stores session-key hashes, fingerprints and bounded validation
results in ignored .tmp/agent-hooks. Session IDs are hashed, not used as paths.
State writes are atomic; content changes during validation prevent caching that
result as verified.

## Activation and trust

The earlier Codex audit checked codex-cli 0.164.0 and hooks=true/multi_agent=false.
Current native commands are checked from an application subdirectory using
scripts/check_team_hooks.py with representative JSON and malformed inputs.
Script smoke tests do **not** prove that an IDE has trusted or automatically
invoked the current definitions on any teammate's machine.

Codex requires review/trust for non-managed hooks. New or changed definitions are
skipped until trusted. In the Codex CLI opened in this repo, use `/hooks`, inspect
the four project entries from .codex/hooks.json, and trust the reviewed definitions.
Reload/resume the intended client/session as needed. Do not run a second AI task
just to activate these hooks.

This is required by the
[official OpenAI Hooks guide](https://learn.chatgpt.com/docs/hooks#review-and-trust-hooks),
not an extra project approval rule. Trust storage and bypass flags were not changed.
**Automatic host execution remains unverified until that review.** The Git
pre-commit hook remains enabled independently of Codex hook trust.

## Verification

```powershell
uv run --no-project python -m unittest discover -s scripts/tests -v
uv run --no-project python scripts/agent_tooling.py
node --test scripts/tests/test_agent_setup.cjs
uv run --no-project python scripts/check_team_hooks.py
```

Tests cover delegation denial, unchanged-content silence, failure feedback, no
forced Stop loops, payload isolation, bounded session paths, changes during
validation and config review. Windows launcher smoke checks cover valid and
malformed JSON without echoing input. No application/dependency file changed.

Disable the project lifecycle handlers through /hooks or set features.hooks=false
and reload the host. Do not rewrite the trust database. Git core.hooksPath is
independent and unaffected by this setting.
