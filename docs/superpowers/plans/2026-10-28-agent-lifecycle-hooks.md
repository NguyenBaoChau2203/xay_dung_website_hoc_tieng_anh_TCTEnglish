# Complete the omitted agent lifecycle hooks

## Cause and scope

The original request covered agent hooks. The first implementation deliberately
excluded automatic session hooks and verified only a Git pre-commit hook. That
selection was too narrow; the single-agent policy does not prohibit lifecycle
checks. This follow-up completes the existing authorized tooling scope.

Use one agent. Do not modify application code, dependencies, secret settings,
hook trust storage or machine-wide config. Keep the existing Git hook and MCP.

## Design

Use the project's .codex/hooks.json, documented by OpenAI and supported by the
installed codex-cli 0.164.0 (features list reports hooks=true). Four command hooks:

| Event | Behavior |
| --- | --- |
| SessionStart | Provide concise pointers to current project policy/skills and snapshot the current tooling fingerprint. |
| PreToolUse | Deny direct spawn_agent/Agent/followup_task calls while project multi_agent is false. No automatic tool approvals or shell rewriting. |
| PostToolUse | After shell/edit tools, validate tooling only if its content changed. Give feedback without hiding the completed tool's result. |
| Stop | Verify changed tooling or reuse an exact-content cached result; warn about failures without forcing a continuation loop. |

State contains hashes and bounded validation results only, inside ignored .tmp.
Never read transcripts/prompts/credentials, execute payload commands, log tool
arguments or invoke another model. Atomic state writes; cache a result only if
the content fingerprint stayed stable during validation. Apply the same code on
Windows through a PowerShell launcher and elsewhere through uv/Python.

The hooks validate the toolkit, not arbitrary C#/Razor behavior. Build/test for
application changes remains required by AGENTS.md. Hook matching is a guardrail,
not a replacement for host permissions. Keep hook output short and unchanged
read-only operations quiet.

## Checklist

- [x] Explain the omission from the original implementation and inspect records.
- [x] Read current official hook schema/behavior and verify installed CLI support.
- [x] Add failing tests for context, policy, content caching and payload isolation.
- [x] Implement runner, portable launcher and four project hook definitions.
- [x] Extend toolkit checks and CI for lifecycle hook configuration.
- [x] Exercise real launcher with representative JSON events and negative cases.
- [x] Run tooling tests, build and relevant regression verification.
- [x] Correct original report/plan and document exact activation/trust status.
- [ ] Owner reviews/trusts exact hook definitions through Codex; verify automatic
  host invocation afterward. This required host action cannot be self-approved.

## Verification evidence

- 21/21 tooling tests passed, including eleven new lifecycle/config tests.
- Direct Windows launcher checks passed for all four events from TCTEnglish/,
  including denial JSON, no unrelated auto-approval, unchanged-content caching,
  malformed input rejection and absence of echoed private sentinel content.
- Source/index validator and actual Git pre-commit passed using a temporary
  index; a corrupted staged source was rejected. User staging was unchanged.
- Solution build passed with 0 warnings/0 errors. No application/test source or
  project dependencies changed; previously recorded .NET suite limits still apply.
- Hook definitions configured; automatic host execution is pending user trust.
- The existing CI unittest discovery automatically includes the new tests.

## Trust boundary

Non-managed hooks need user review/trust of their exact definitions in Codex.
Prepare concrete files and verified results before this user action. Never use
--dangerously-bypass-hook-trust or edit hook trust records. Script smoke tests
do not prove that the live host has trusted and invoked the hooks.

Source: https://learn.chatgpt.com/docs/hooks (checked 2026-10-28).
