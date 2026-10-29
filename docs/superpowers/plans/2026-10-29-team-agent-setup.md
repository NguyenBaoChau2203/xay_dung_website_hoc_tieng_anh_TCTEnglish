# Shared agent setup for the TCT English team

Owner authorization: add/edit project files so Codex, Claude Code, Cursor and
Antigravity share the curated toolkit without teammates typing PowerShell setup
commands. Execute and review inline with one agent.

## Design

- Keep AGENTS.md and .agents/skills as the canonical content. Add small Claude
  skill adapters and a CLAUDE.md import; use native discovery in other clients.
- Share a Node launcher for first-use, version-locked dependency restoration and
  MCP startup. Download a pinned uv binary with a reviewed SHA-256 only if needed.
  Keep runtime files in ignored project folders; require installed Node and Git.
- Add native MCP/hook files for all four clients. Enforce the seven Git read tools
  in the server, including rejection of direct calls to unadvertised write tools.
- Translate hook events/payloads/outputs per host; keep fast delegation checks
  independent of dependency installation. No payload commands, trust changes,
  forced Stop loops, transcript reads or global configuration edits.
- Supply a Windows double-click setup file and an IDE folder-open task. Native
  trust/automatic-task consent remains with each teammate. Git pull alone cannot
  execute a fresh clone's setup; no remote post-pull executable is smuggled in.
- Bootstrap installs runtime dependencies and the local Git pre-commit hook, but
  never init/index/sync CodeGraph. A new machine reports a missing index clearly.
- Preserve existing user files and all previous modernization work. Do not change
  application source, project targets, secrets or database schema.

## Checklist

- [x] Inspect actual project files and current official client documentation.
- [x] Add meaningful bootstrap/hook/config and read-only MCP regressions.
- [x] Implement idempotent first-use setup, concurrency locking and click entrypoint.
- [x] Add/validate host adapters, MCP definitions and hook translations.
- [x] Document teammate onboarding, first-run consent, offline failures and indexing.
- [x] Verify a clean simulated teammate checkout and repeated setup without downloads.
- [x] Run tooling checks/tests, actual MCP smoke checks, solution build and app tests.
- [x] Review the full diff inline, encoding/staged checks; report tested limits.

## Evidence and limitations

Official host formats reviewed on 2026-10-29:
https://code.claude.com/docs/en/skills
https://code.claude.com/docs/en/hooks
https://code.claude.com/docs/en/mcp
https://cursor.com/docs/skills
https://cursor.com/docs/hooks
https://cursor.com/docs/mcp
https://antigravity.google/docs/skills
https://antigravity.google/docs/hooks/
https://antigravity.google/docs/mcp/
https://code.visualstudio.com/docs/debugtest/tasks#_run-behavior

CodeGraph does not index the newly added tooling scripts. Use bounded direct
source reads for these files; do not re-index to make this task possible.
Actual automatic invocation in teammates' installed client versions is separate
from protocol/script tests and cannot be certified from this machine.

Verified so far: 41 Python tests and 9 Node tests pass; native hook commands for
four hosts/four events plus malformed-input isolation pass through PowerShell
from a subdirectory. Actual Git MCP exposes exactly seven reads and rejects a
direct git_add call. Actual CodeGraph source query passes with DB/WAL data hash
unchanged. Fresh Windows checkout (spaces in path, uv absent) installed in 30.1s;
offline repeat skipped installers and no index was created. SETUP-AGENTS.cmd and
the installed post-merge hook both run successfully. Staged validation/negative
hash test and full diff whitespace pass using a separate Git index. The real
user index is unchanged. Solution build passes with zero warnings/errors.

Application verification: the initial full run stopped making progress after 523
passing tests and was terminated at the ten-minute bound. It produced
.tmp/tooling-modernization/team-test-results/team-agent-setup.trx (aborted).
A fresh full run with --blame-hang-timeout 90s --blame-hang-dump-type none passed
557/557, zero failed/skipped, reported test duration 1m18s. Its result is
.tmp/tooling-modernization/team-test-results/team-agent-setup-diagnostic.trx.
No application or C# test source changed. The first hang's cause was not proven
or repaired; do not treat the successful retry as a fix for test nondeterminism.

Final limits: Windows x64 runtime/setup and native command contracts were tested.
Automatic execution/trust in teammates' actual client versions, macOS/Linux/ARM
runtime installations and GitHub-hosted CI were not run here. The index task was
reviewed but deliberately not executed; the owner has not requested re-indexing.
No commits/pushes or user/global trust/configuration changes were made.
