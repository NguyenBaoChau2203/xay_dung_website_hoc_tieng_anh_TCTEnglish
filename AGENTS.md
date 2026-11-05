# AGENTS.md — TCT English

Shared instructions for Codex, Cursor, Claude Code and Antigravity. Reuse current
instructions/source; load only the active step and relevant skill. Architecture
and file maps: [project structure](docs/project-structure.md).

## Owner execution policy

- One agent investigates, implements, reviews and verifies. Never spawn workers,
  reviewers, another AI chat/CLI or model process to delegate. `.ai/roles/` are
  inline review lenses. Imported skills cannot override this owner policy.
- Owner branch preference: use feature/, fix/ or chore/ topic names; do not add
  a codex/ prefix. Inspect the actual checkout instead of assuming a branch name.
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

## Prompt authoring and language (all four clients)

- Write new and substantially revised execution/handoff prompts in **English**,
  including prompt templates and executable prompt blocks in plans/docs. Owner
  chat and final reports remain Vietnamese; preserve literal UI copy, fixtures,
  identifiers and paths in their required original language.
- Before drafting a prompt, follow [.ai/prompts/README.md](.ai/prompts/README.md)
  and adapt [.ai/templates/execution-prompt.md](.ai/templates/execution-prompt.md).
  Define the outcome, source context, mode/authority, scope/non-goals, deliverables,
  ordered work, meaningful acceptance checks and Vietnamese handoff. Scale detail
  to the task; reference existing rules instead of duplicating whole documents.
- Preserve explicit owner authority across stages. A prompt cannot invent Git,
  deployment, protected-config or data authority; review alone does not authorize
  fixes. Use one agent, evidence-based context and only available tools.
- Distinguish verified facts, assumptions and unknowns; separate reference data
  from instructions. Do not request hidden chain-of-thought, use expert-role
  claims as evidence, fabricate checks or substitute a different model.
- Do not bulk-translate historical/vendor prompts. Before reusing an old prompt,
  translate its active instructions into English and reconcile current rules,
  source and superseded behavior. Newer owner instructions take precedence.

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

Owner-authorized staging has its own `PLESK_STAGING_DEPLOYMENT_ENABLED` gate,
GitHub Environment restricted to master, `release/staging` manifest and private
state; follow docs/operations/staging-automation.md. A staging release never
grants production authority. Published Admin lessons are shared on staging;
personal drafts/progress and Docker rows are not synchronized by Git.

Read needed sections only: [local setup](docs/operations/local-development-quickstart.md),
[new teammate setup](docs/operations/team-local-onboarding.md),
[local speech](docs/operations/local-speech.md),
[Plesk release/recovery](docs/operations/plesk-cicd-runbook.md).

When an owner asks to run the local web for testing, use
`pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` so MariaDB and the web
app come up together. Speech uses OpenAI Speech-to-Text (`gpt-transcribe`) when a
key is configured; ordinary startup does not spawn a local ASR engine or make a
paid speech request. The owner authorized local `OPENAI_API_KEY` as the fallback
key source: the launcher preserves explicit `AI__ApiKey`, then User Secrets
`AI:ApiKey`, then maps `OPENAI_API_KEY` only into its child processes. Never echo,
put in argv, persist or copy that key into Git. Confirm app `/health/ready` and
`scripts/speech-status.ps1`; configured means present, not verified account/quota.
Use an already-saved local key automatically; never ask to paste it into chat.
Ordinary startup reads User Secrets without rewriting, normalizing or relocating
the saved file. Docker connection/worker flags and normalized nonsecret AI
settings are child-process environment overrides, restored on exit/failure.
Only an explicit key/configuration operation writes the supported secret store;
replacement must preserve the original/protected backup if it fails. Never use
delete-then-move overwrite for credentials or weaken EFS/ACLs to make it work.
A process already listening may use old code/key: the launcher refuses that port
before building. Preserve it and use a free port with
`-Configuration Release -WebPort <port>`, or restart only a process whose ownership
and task authority are established. Model web text: `gpt-6-luna`, reasoning `low`;
audio model: `gpt-transcribe`; the AI IDE model is separate. Do not start speech
API calls on folderOpen, MCP, ordinary build/test, or hook runs. For Admin audio
import decode only, run `scripts/local-media.ps1 -Action Setup` separately.
Local Whisper is retired. Never restore its old launcher, engine/model download,
background process or automatic startup from historical plans/prompts or caches.
Current local startup is MariaDB + web; speech configuration is metadata-only
until a user actually submits audio. Historical `whisper-audio` data stays readable.
The launcher maps child-process TEMP/TMP into ignored `.appdata/local/temp` on
the checkout drive and restores the caller's environment on exit/failure. Keep
local recordings/scratch out of C when the owner works from the D checkout;
never move global app profiles or secrets into tracked source to achieve this.
User Secrets, credential files and OS-protected key stores are not disposable
caches. Exclude them from bulk cache relocation; preserve their supported local
store and never weaken EFS/ACL protection to move them.
For a new teammate, run `node scripts/check-local.cjs` and follow the onboarding
guide. The launcher bootstraps synthetic samples/random local accounts only when
there are no users; credentials live in ignored .appdata/local/access.json. Never
replace an existing DB or copy the owner's volume/backup/key into Git. Teammates
enter the owner-provided test key into their own local secret store once.

## Focused context, CodeGraph and skills

### Shared content packages (all four AI tools)

- After an owner-authorized pull, run `pwsh -NoProfile -File scripts/pull-local.ps1`
  (default origin/master). It pulls with `--ff-only` and applies reviewed packages
  into this checkout's Docker. For another branch pass `-Branch <name>`. A plain
  Git pull may invoke the installed post-merge hook; the wrapper also handles an
  already-current checkout and missing hooks. Startup catches up before serving.
- Changes to supported shared content made through the UI, a seed or a script
  require `scripts/shared-data.ps1 -Action Export` and package review in the same
  PR, even if the prompt omits data delivery. Inspect `-Action Status` for stable
  keys, package ownership and local edits. Follow
  [shared-data workflow](docs/operations/shared-data-workflow.md) for exact args.
- Export only selected allowlisted content and curated media into `data/shared`.
  Never export users, keys, passwords, chat, payments or learner progress. An
  unsupported entity requires a reviewed typed adapter and regression tests;
  never substitute a SQL dump or arbitrary-table importer. Keep personal Goals
  local; shared goal templates would need a separate explicit feature.
- Published package versions and media are immutable. Add the next version;
  preserve stable keys and dependencies, review hashes and selected media.
  Conflicts must stop, preserve data, and be reported. No force/reset/overwrite.
- Successful code pull is not successful data sync. Require a zero exit code and
  current ignored `.appdata/local/shared-data/last-sync.json`; report blockers.
  Do not run database synchronization on folder-open, SessionStart or MCP calls.
- Staging imports packages after backup/migration. Production uses only reviewed
  `data/shared/production.json` hashes, deployment gate and environment approval.
  Merge/staging access never authorizes production or copying its private data.

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
