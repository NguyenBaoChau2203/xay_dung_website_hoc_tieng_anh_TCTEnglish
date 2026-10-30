# Agent rules for database changes and PR releases

Date: 2026-10-30. Owner requested shared project rules so Cursor and Claude Code
handle database/release requirements even when a feature prompt omits them.
One agent implements and reviews; preserve existing working-tree changes.

- [x] Add the mandatory feature/schema/PR/release contract to AGENTS.md.
- [x] Make Cursor always apply a short adapter and keep Claude's AGENTS.md import.
- [x] Update the canonical migration skill and shared routing/reminders.
- [x] Review consistency, local links, unchanged deployment gate and existing edits.
- [x] Run tooling validation, relevant script tests and an application build.

Scope: instruction Markdown/MDC only. Do not generate migrations, change app or
workflow behavior, enable production deployment, or modify hosting/database data.
Rules guide agents; they are not a new enforced CI/branch-protection check.

Acceptance: any persisted EF model/mapping change prompts schema assessment;
schema changes include reviewed migration/generated metadata/snapshot in the same
PR and real MariaDB validation. Reports distinguish CI success from an actual
release. The owner reviews/merges team feature PRs unless separately authorized.

Verification: agent_tooling.py passed; scripts/tests 49/49 passed. Release app
build in an isolated artifacts directory passed (0 errors, 35 existing warnings).
EF 9.0.4 has-pending-model-changes reported no changes since the last migration.
GitHub deployment gate remains false. Existing Claude/Antigravity skill adapters
still link to the canonical migration skill; no duplicate skill implementation.
Cursor MDC and Claude imports match their official documented formats; this is
configuration verification, not a claim that native clients were launched/tested.

The seven instruction/plan files are isolated on codex/database-pr-agent-rules
for an owner-reviewed PR. Existing feature/tooling edits stay in the main working
tree; no application/config/workflow behavior changes are included in that PR.
