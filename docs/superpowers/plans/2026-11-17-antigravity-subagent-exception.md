# Antigravity Gemini 3.8 Flash High subagent exception

Mode: IMPLEMENT_LOCAL. The owner authorizes a project-policy exception only for
Antigravity using Gemini 3.8 Flash High. Codex, Claude Code, Cursor and every other
host/model combination retain the single-agent rule. This Codex task runs inline.
Existing chatbot work is outside scope. No Git publication, application changes,
database operations, model switching or native trust changes are authorized.

## Evidence and implementation boundaries

- AGENTS.md is the canonical owner policy; shared prompt/skill/workflow entrypoints
  currently repeat an unconditional single-agent restriction.
- scripts/agent-tools.cjs fastHook and scripts/agent_lifecycle.py handle both the
  first-run and installed-runtime delegation gates. Keep .codex/config.toml and
  all native hook definitions unchanged.
- The [official Antigravity hook contract](https://antigravity.google/docs/hooks/)
  documents top-level modelName and native invoke_subagent/define_subagent tools.
  Accept only the explicit owner label Gemini 3.8 Flash High or its identifier
  spelling gemini-3.8-flash-high (case-insensitive, surrounding whitespace ignored).
  Model availability and actual IDE hook invocation are not verified here.
- Check model metadata on each call; never read tool arguments, prompts,
  transcripts or environment overrides to grant the exception. Missing, malformed,
  different or unrecognized model names retain denial. Do not cache permission.
- Native subagents inherit task scope and authority; the coordinating Antigravity
  agent remains responsible for integrating, reviewing and verifying their work.
  External AI chats/CLIs and model substitution remain outside this exception.
- Historical task prompts and vendor snapshots remain historical; current owner
  policy supersedes their blanket ban only for the authorized host/model pair.

## Checklist

- [x] Inspect canonical policy, relevant adapters, both gate implementations and
  the official host metadata contract. Preserve the dirty working tree.
- [x] Add meaningful failing checks for native delegation on the allowed model,
  other hosts/models, absent/malformed metadata, spoofed tool arguments, model
  changes between calls and native command output contracts.
- [x] Update canonical policy, active shared guidance and both hook paths.
- [x] Run focused regressions, tooling validation, full Python script suite,
  Node setup suite and exact native command smoke checks; review the scoped diff.

## Acceptance and handoff

Only Antigravity with the approved model passes the native subagent gate; this
returns ordinary allow/default handling and never auto_approve or changed tool
arguments. Codex multi_agent remains false. Other hosts and Antigravity with
unknown/different model metadata retain denial. Session pointers and current
shared guidance agree with AGENTS.md. Record exact check results and report in
Vietnamese, distinguishing local contract verification from live IDE behavior.

## Verification evidence

- Before implementation, the new Python allowed-model and per-call checks failed
  (7 failing subcases); the Node allowed-model test also failed on the blanket ban.
- After implementation, focused lifecycle checks passed (15 tests), and
  `node --test scripts/tests/test_agent_setup.cjs` passed all 11 tests.
- `uv run --no-project python scripts/agent_tooling.py`: PASS.
- `uv run --no-project python -m unittest discover -s scripts/tests -v`: 152 tests,
  149 passed and 3 skipped. Skips: one non-Windows rejection case on Windows and
  two CTC checks requiring the separate pinned alignment-only Docker runtime.
- `uv run --no-project python scripts/check_team_hooks.py`: PASS for all four
  native host commands through pwsh, lifecycle events, malformed input isolation
  and the host/model/native-tool matrix. No AI tool or subagent was executed.
- All 14 modified canonical skills passed skill-creator quick_validate.py using
  isolated `uv run --no-project --with PyYAML==6.0.3 python -X utf8`; the default
  runtime lacked PyYAML and Windows' default encoding required the UTF-8 flag.
  No project dependency or system skill file was changed for this check.
- Scoped diff review and `git diff --check`: PASS. Codex configuration and all
  native hook definitions have no diff; Codex multi_agent remains false.
- No application build/browser acceptance was needed for this policy/tooling
  change. Live Antigravity modelName delivery, trusted hook invocation and native
  subagent execution remain unobserved; open/reload that client with the selected
  model to consume the updated guidance. No commit, push or deployment performed.
