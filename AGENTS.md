# AGENTS.md — TCT English

Shared instructions for Codex, Cursor, Claude Code and Antigravity. Reuse current
instructions/source; load only the active step and relevant skill. Architecture
and file maps: [project structure](docs/project-structure.md).

## Owner execution policy

- One agent investigates, implements, reviews and verifies. Never spawn workers,
  reviewers, another AI chat/CLI or model process to delegate. `.ai/roles/` are
  inline review lenses. Imported skills cannot override this owner policy.
- Default to local work and verification. Commit, push, PR creation, remote CI,
  merge and deployment require owner authorization for that stage. Carry explicit
  authority through the task; do not ask again for steps already granted. Newer
  owner instructions override older prompts/plans and this default.
- Local/CI success does not authorize production. Never change Plesk settings or
  enable `PLESK_DEPLOYMENT_ENABLED` without explicit authority. An owner-authorized
  merge to master may proceed with the production gate false. Keep release guards.
- Preserve existing work/workloads. Use Docker MariaDB/local User Secrets. Never
  reset the dev DB, overwrite hosting with a local copy, or commit credentials,
  SQL backups, runtime uploads or native trust records.

## Project boundaries

- ASP.NET Core MVC/Razor, net9.0, EF Core9.0.4, Pomelo MySQL9.0.0; hosting
  MariaDB10.5.23. SDK10 does not change targets. Verify source/provider versions.
  Frontend Bootstrap5/jQuery/vanilla JS; SignalR class chat; cookie/OAuth auth.
- Canonical namespace `TCTEnglish.*`; verify legacy `TCTVocabulary.*` before reuse.
- HomeController/Views/Home stay dashboard/public. Feature controllers own Class,
  Folder, Set, Study, Vocabulary, Speaking, Account, Goals and Notifications;
  admin belongs in Areas/Admin. Preserve legacy routes without restoring feature
  business logic to HomeController.
- Main paths: `TCTEnglish/{Controllers,Services,Models,ViewModels,Hubs,Workers,Views,wwwroot}`;
  admin equivalents in Areas/Admin. Context: Models/DbflashcardContext.cs;
  identity: Security/CurrentUserIdExtensions.cs and Controllers/BaseController.cs.

## Mandatory coding and security rules

1. Async/await for I/O; never `.Result` or `.Wait()`.
2. Enforce ownership/access in every parameterized read/update/delete query. Use
   `GetCurrentUserId()`/verified base helper, not inline claim parsing. Return
   `NotFound()` for IDOR violations, never `Forbid()`.
3. POST/PUT/DELETE actions require antiforgery validation. `[Authorize]` for
   authenticated flows; `[Authorize(Roles = "Admin")]` for admin-only work.
4. Views receive ViewModels, never raw EF entities. `.AsNoTracking()` for display;
   Include/projection where needed to avoid N+1 queries.
5. Thin controllers; business logic in services, constructor DI. Preserve HTTP,
   JSON and SignalR contracts. Register services in Program.cs only when authorized.
6. Bootstrap5 utilities/components, no inline styles for standard layouts. Read
   DESIGN.md and the relevant UI skill before changing UI.
7. Never modify Program.cs, appsettings.json or .csproj without explicit task
   authority. Never hardcode connection strings, OAuth/SMTP credentials or tokens.
8. No destructive DROP TABLE/DROP COLUMN migrations without explicit approval.
   Never edit applied migrations or invent/delete migration-history rows.

## Database, PR and Release Rules (Automatic)

Apply even when the feature prompt omits database/deployment requirements.

1. Persisted entity/DbContext/relationship/mapping changes trigger
   `tct-ef-migration` and schema/data assessment. ViewModel-only work needs no
   migration; explain persisted-model changes that need none.
2. Ship migration, Designer metadata and snapshot in the same PR as dependent
   code. Use repo-local EF; do not defer generation to review/deploy. Check pending
   model changes before handoff.
3. Preserve data/history; review Up/Down, rename/backfill and FK/index order.
   Replacement FK support indexes must exist before old indexes are removed.
4. Test schema upgrade/repeat execution and data preservation on disposable
   MariaDB matching hosting. SQLite alone is not provider proof. Record exact
   checks, compatibility/data risks and blockers in the PR.
5. Feature branch/PR review is the team flow; owner reviews/merges unless the task
   explicitly grants the agent that stage. CI green alone grants no authority.
6. Source releases target master. Enabled pipeline: CI success → hosting DB backup
   → committed pending migrations → published IIS app → readiness. No deploy-time
   migration generation, ad hoc production schema changes or bypass deploy paths.
7. Inspect actual GitHub run and gate before reporting release. False gate/skipped
   release means that run did not deploy app/schema. Success needs release job,
   expected public commit marker and `/health/ready`.
8. Keep environment data/credentials separate. Normal schema release preserves
   production users; seed/data changes require an explicit reviewed plan.

Read needed sections only: [local setup](docs/operations/local-development-quickstart.md),
[Plesk release/recovery](docs/operations/plesk-cicd-runbook.md).

## Focused context, CodeGraph and skills

- Canonical skills: `.agents/skills/tct-*/SKILL.md`. `.claude/skills` and legacy
  `.agent/skills` are discovery adapters to the same procedures, not extra copies
  to read repeatedly. Roles are inline; vendor snapshots are reference material.
- `tct-superpowers` owns scoped plans/debugging/verification; `tct-engineering`
  owns source-driven context/review. Read `.ai/README.md` for a concrete routing
  or setup question, not as a mandatory startup bundle.
- If `.codegraph/` exists, use MCP `codegraph_explore` first for indexed source.
  Exact symbols/files, initially maxFiles1–3 where supported; fresh verbatim source
  counts as read. Direct reads are for stale/unindexed files, missing gaps or
  fresh edit text. Explain wider reads; do not chase unrelated graph hits.
- Pinned CLI: `pwsh -NoProfile -File scripts/codegraph.ps1`; global CLI may differ.
  Never auto init/full-rebuild/CLI sync. Only ignored owner-approved
  `.ai/codegraph.local.json` enables incremental server catch-up/watcher for its
  bound checkout/version; other checkouts are read-only. No index: skip CodeGraph.
- Context7, if actually available, is for external library/API docs at the actual
  version. Resolve once/reuse ID; never send private source/secrets/logs or use it
  for local plans. If missing, say so once and use official docs when needed.
- Load active step/finding/skill. Architecture tables, old plans/ledgers, full bug
  histories, roles/vendor are on-demand. A hard file cap must never omit access
  checks, callers, contracts or meaningful regression evidence.

## Automatic task routing

| Task | Required procedure |
| --- | --- |
| Feature/workflow | tct-superpowers + relevant feature/domain skill; inspect, scoped design/checklist, implement inline |
| Bug/test failure | CodeGraph + tct-superpowers diagnosis; newest symptom-relevant bug memory before editing |
| Architecture/exploration | codegraph_explore verbatim source and call paths |
| JSON/AJAX endpoint | tct-api-endpoint; access, identity, antiforgery and existing contracts |
| Persisted model/mapping | tct-ef-migration automatically; schema ships/validates with feature |
| UI | DESIGN.md + tct-ui-component; ui-review for rendered checks, ui-motion for motion, ui-ux-pro-max for lookup |
| Admin/Speaking/Vocabulary | tct-admin-panel / tct-speaking-feature / tct-vocabulary-feature |
| Seed/tooling/commit | tct-data-seeder / tct-tooling-review / tct-commit-message |
| Completion | tct-superpowers verification; build/relevant tests, evidence before claims |

Save multi-step checklists in docs/superpowers/plans and execute inline. Diagnose
root cause before fixes; write failing meaningful regressions before implementation
when adding unit tests. Keep scope, review diff and verify changed behavior.

## Tooling and host verification

- Native definitions/pins: [team setup](docs/team-agent-setup.md),
  [CodeGraph](docs/codegraph.md), [hooks](docs/agent-hooks.md),
  [provenance](docs/agent-tooling.md), `.ai/tooling/sources.lock.json`.
- SETUP-AGENTS.cmd/folder-open/MCP first use restores locked local dependencies;
  Node22+/npm and Git are prerequisites. Setup/post-merge never indexes. Lifecycle
  hooks do not download dependencies or start another AI agent.
- Git MCP exposes seven server-enforced read tools; CodeGraph exposes explore.
  Native trust is local, not transferred by Git. Do not change trust/bypass flags.
  Config/script pass is not proof an IDE loaded/invoked it; tooling checks never
  substitute for application tests or browser acceptance.
- Tooling changes require `uv run --no-project python scripts/agent_tooling.py`
  and `uv run --no-project python -m unittest discover -s scripts/tests -v`.
  Bootstrap changes also require `node --test scripts/tests/test_agent_setup.cjs`;
  native-hook changes require scripts/check_team_hooks.py. Run focused MCP/policy
  checks; do not full-build the app on every hook/edit.

## Bug memory and communication

- Before a fix, read newest symptom-relevant parts of .ai/context/bug-fix-log.md
  and .ai/context/known-issues.md. Match the root cause before reusing an old fix.
  After fixing, append the required bug-log entry and update a listed known issue.
- Final summary in Vietnamese: outcome/files/checks/risks/blockers; distinguish
  local, CI and production. Never call a skipped/partial acceptance pass.
- Actionable reviews include a detailed docs/superpowers/plans repair plan,
  .ai/prompts execution prompt and short client-appropriate copy-paste chat, with
  priorities/files/dependencies/regressions/acceptance criteria. Review alone does
  not authorize fixes. Explicit fix authority does: implement instead of stopping
  at another plan. No findings: no empty repair plan.
- Lookup priority: docs/project-structure.md, docs/architecture-prioritized-backlog.md,
  relevant known issues, .ai/context/coding-conventions.md; old plans are supporting.
