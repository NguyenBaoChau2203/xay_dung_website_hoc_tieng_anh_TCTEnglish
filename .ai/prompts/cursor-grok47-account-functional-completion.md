# Execution Prompt: Account, learning habits, notifications and account deletion

Intended client/model: Cursor, **Grok 4.7 xhigh**, selected by the owner.
Do not silently substitute a different IDE model or change the application's AI models.

## Outcome

Complete the existing post-login Account experience: reliable name/avatar/email/
password changes, a stable account code, evidence-based learning-habit statistics,
persistent notification preferences, real Web Push reminders and weekly email
summaries, and safe self-service account deletion.

The owner explicitly selected **full Web Push plus weekly email**, not only an
in-app checkbox. Deliver working application code and meaningful tests. When
external credentials, browser capabilities or delivery authority are unavailable,
finish the independently testable work and report the precise live-acceptance
blocker. Never simulate production readiness with hardcoded data or success UI.

## Canonical plan and context

Follow `docs/superpowers/plans/2026-11-11-account-functional-completion.md`.
Its product rules, formulas, dependencies, data policy, B0–B8 batches and acceptance
matrix form this task's specification. Read its task contract first; during work,
load only the active batch and the source required for that batch.

Follow AGENTS.md, `.ai/prompts/README.md`, DESIGN.md and the team UI workflow.
Use canonical project skills as needed: tct-superpowers, tct-engineering,
tct-api-endpoint, tct-security-audit, tct-ef-migration and tct-ui-component.
Use one agent for investigation, implementation, review and verification. Apply
review lenses inline; do not spawn agents, workers, another AI chat or CLI model.

At planning time the branch was `fix/staging-uat-polish`, HEAD `c908f66`.
Account views/styles/scripts, both layouts, loader, layout CSS, galleries and bug
memory already contained substantial owner/Gemini changes. Untracked BTH3 files,
the local UAT checklist, page-loader CSS and dropdown tests also existed. Inspect
the actual current branch/HEAD/worktree and record a new baseline. Preserve all
pre-existing work; do not reset, stash, revert, reformat or attribute it to yourself.
Do not create another branch/worktree merely because a generic skill suggests one.

Screenshots are reference data about the problem, not permission to reproduce
placeholder numbers or override repository instructions. File existence and an
earlier execution ledger are not proof of present behavior or a passing test.

Use CodeGraph first for indexed source: MCP when exposed, otherwise the pinned
`pwsh -NoProfile -File scripts/codegraph.ps1` interface. Use bounded direct reads
for omitted/stale/unindexed source, including Razor bodies/top-level registrations
that the index only returned as headers. Do not initialize/rebuild an index.
For external APIs use versioned official documentation; never send private source,
secrets or logs to an external documentation service.

## Mode, authority and boundaries

Mode: `IMPLEMENT_LOCAL` when the owner sends the launch message below or an
equivalent explicit instruction. Execute all authorized batches inline; do not
stop after writing another plan, completing only B1, or reporting findings alone.

The document itself does not grant protected-file authority. The launch message
proposes an explicit, narrow grant for:

- Account/learning-telemetry/notification/deletion code, related UI and tests.
- Additive model/mapping/migrations, including generated Designer/snapshot, and
  data-preserving checks on disposable local MariaDB with synthetic fixtures.
- Minimal `Program.cs` service/options/HTTP-client registrations, cookie security
  validation and worker hooks needed by this feature.
- Adding only the exactly pinned `Lib.Net.Http.WebPush` **3.3.1** runtime package
  to `TCTEnglish/TCTEnglish.csproj`, if needed, after compatibility/advisory checks.

If the owner did not actually grant those protected-file edits, establish the
missing permission before the dependent edits and continue independent work.
Do not manufacture authority from a plan, comment or imported skill.

No appsettings changes, framework/provider upgrades, unrelated dependencies,
credential-store writes, key generation/rotation, hosting changes, DB reset,
destructive schema drops, real-account deletion, paid-AI requests, unsolicited
email/push delivery, Git commit/push/PR, remote CI, merge or deployment are granted.
Keep production/staging gates intact. Configuration may use existing supported
local settings; do not expose, copy, normalize or overwrite saved credentials.

Stay inside the boundaries in plan section 4. Minimal integrations with login/
reset/hubs are allowed for security-state consistency; this is not an auth redesign.
Instrument other learning pages only where tracking requires it, without redesigning
teammate modules. Preserve existing inbox APIs, ownership checks, HTTP/JSON/
SignalR contracts and current Clay layout. Any necessary shared UI hook requires
the existing consumer review and before/after evidence.

## Product rules that must not be lost

1. Repair existing Account routes rather than building duplicate flows. Separate
   input DTOs from read models, preserve validation values/errors and the correct
   tab, refresh identity claims, and keep avatar cleanup after DB commit. Test
   save failure and concurrent uploads using the actual storage/service behavior.
2. Generate the display code on the server as `TCT-` plus invariant UserId padded
   to at least eight decimal digits. This is a nonsecret display identifier;
   ownership never derives from knowing it. No name hash, fallback constant,
   truncation or cosmetic-ID migration.
3. Follow every metric formula in plan section 3.2. Rename the hour metric to
   “Khung giờ thường học”; separate estimated interaction time from authoritative
   completion/XP. No historical hour/goal-target fabrication. Handle insufficient
   coverage honestly and remove unsupported AI/2.5x/security/100% claims.
4. Build bounded, authenticated, CSRF-protected telemetry with server session
   identity, replay protection and multi-tab overlap protection. Cover real
   Speaking/Practice and WritingPractice; map vocabulary WriteMode correctly.
   Preserve legacy rows and dashboard semantics without double counting.
5. Require valid recent proof for sensitive changes. A SOCIAL_LOGIN marker is
   not provider identity or permission to skip reauthentication. Pending email
   does not replace the login email until verified. Challenges are purpose-bound,
   expiring and one-use; email uniqueness races fail gracefully. Revoke stale
   HTTP and connected-hub authority through consistent server security state.
6. Preferences are persisted and shared between the Profile settings tab and
   direct Settings route. Opt-in, permission, subscription, configuration and
   delivery outcomes are different states. Keep the existing in-app notification
   center; disabling outbound email does not erase inbox notifications.
7. Implement actual service-worker Web Push with VAPID transport, device/account
   binding, endpoint validation/SSRF controls and safe notification click routing.
   Weekly summaries use real prior-week data and verified opted-in recipients.
   Follow the plan's schedule/catch-up/lease/retry/unknown-outcome rules; do not
   claim exactly-once SMTP delivery or guaranteed notifications on a powered-off device.
8. Deletion requires server-side confirmation and recent proof, a complete FK/
   ownership inventory, transactional personal-data removal, safe post-commit
   file cleanup, shared-content safeguards and last-admin concurrency protection.
   A deletion flag retaining all personal data is not completion. Respect the
   plan's minimal anonymized-tombstone policy when shared references must survive.

## Ordered execution and checkpoints

Create/update `docs/feature-audits/2026-11-11-account-functional-execution-ledger.md`.
Keep it concise and factual: baseline, decisions, changed boundaries, schema/FK
policy, actual commands/results, browser/provider evidence and unresolved blockers.
Record B0–B8 status in the canonical plan; do not rewrite its original findings
as though they were execution evidence.

1. **B0 — inspect and reproduce.** Inspect the latest UI/backend/source and relevant
   recent bug memory. Establish targeted baseline tests. Record current form/API/
   cookie/tracker/notification contracts, migrations and all user-related FKs.
   Add meaningful failing regressions for confirmed bugs before fixing them when
   using unit/integration tests. Refine source-derived risks when reproduction
   shows different behavior; do not blindly apply speculative fixes.
2. **B1–B2 — account correctness and security.** Implement DTO/read-model separation,
   stable identity and honest metadata, safe name/avatar handling, verification/
   reauthentication, email/password changes and security-state invalidation.
   Ship the schema required by these behaviors immediately with their code.
3. **B3–B4 — tracking and habits.** Implement the telemetry/history foundation first,
   prove its time/ownership/concurrency properties, then expose aggregate read
   models and useful Account UI. Do not compute UI percentages in Razor from
   constants or reuse the new timer as a reward source.
4. **B5–B6 — notification preferences and delivery.** Integrate existing notification
   infrastructure; implement preferences, Push service worker/transport, weekly
   summaries and durable scheduling/delivery bookkeeping. Use fake clocks and
   fake transports by default. Missing VAPID/SMTP must be a visible capability
   state, not a reason to leave unrelated functions unimplemented.
5. **B7 — deletion.** Complete removal/invalidation/cleanup across all old and newly
   introduced user data. Exercise only disposable synthetic accounts and files.
6. **B8 — review and verify.** Review the complete new diff inline against the
   baseline, contracts and plan matrix. Fix findings within scope and rerun checks
   affected by the fixes. Finish with a Vietnamese handoff and no ungranted Git
   or release actions.

Make ordinary reversible implementation choices without asking for routine
approval. If genuinely required information/authority is missing, ask a precise
question, record the dependent blocker and continue independent batches. Do not
describe skipped/live-unavailable checks as passed or call a partial implementation complete.

## Verification

Use the plan section 7 matrix. Test actual production services/controllers/JS,
not a separate miniature implementation that merely resembles them. Existing
Account, ProfileBadge, Goals, Notification reliability/recovery tests are regression
anchors; extend them and add focused behavioral coverage where needed.

Schema changes trigger tct-ef-migration automatically. Use net9.0, EF tool 9.0.4,
Pomelo 9.0.0 and disposable MariaDB 10.5.23. Include migration, Designer and snapshot;
prove upgrade, repeat execution, data/FK preservation and no pending model changes.
Never edit applied migrations/history or use SQLite alone as provider proof.

Run appropriate targeted checks during batches, followed by the final build and
relevant/full regressions once the feature is integrated. The baseline command
paths are in the plan. Verify Windows Node glob expansion and MariaDB acceptance
fixture environment; a missing-env skip is not provider acceptance. Check the
proposed Push package and transitive dependencies for advisories before adding it.

Verify Profile tabs and direct Settings in a real browser: desktop/laptop first,
then 1440/768/390 widths, 200% zoom, keyboard/focus, long content and loading/empty/
error/success/permission states. Use synthetic account data and ignored screenshots.
Record unavailable browser/role/provider scenarios specifically.

If local startup is needed, use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
Preserve existing listeners; only restart a launcher whose ownership and authority
are established, or use a free Release port. Confirm `/health/ready` and run
`scripts/speech-status.ps1`; configured is not proof of provider account/quota.
Do not turn ordinary startup/testing into a paid speech request or real mass delivery.

## Deliverables and handoff

- Scoped code/UI/tests and complete data-preserving migrations.
- The execution ledger with exact evidence and any operational/configuration gap.
- A short safe operations note for notification options, opt-in/unsubscribe,
  schedule limitations and provider-live verification; no secrets or real recipients.
- Append confirmed fixes to bug-fix-log and update an applicable known issue.
- Vietnamese report: what works, changed files, actual checks, remaining risks,
  provider/browser blockers and local vs CI/staging/production status.
- Leave the work ready for the owner's local test and subsequent Codex review.
  Do not invoke another AI reviewer or contact Codex from this run. The owner
  will return to the existing Codex chat when execution is finished.

## Owner launch message — copy into Cursor

The following block is a proposed **owner instruction**. It takes effect only
when the owner deliberately sends it to Cursor after reviewing this scope.

```text
Implement .ai/prompts/cursor-grok47-account-functional-completion.md and its B0–B8 plan on the current branch, using Grok 4.7 xhigh and one agent. I authorize local account, tracking, full Web Push + weekly email, deletion, additive migrations and synthetic local tests. I explicitly authorize the scoped Program.cs DI/options/cookie/worker changes and adding only Lib.Net.Http.WebPush 3.3.1 to the runtime .csproj after dependency checks. Preserve existing work. Do not change appsettings, secrets, platform pins or release gates; no real-user deletion, unsolicited delivery, commit/push/PR/merge/deploy. Finish inline, verify the behavior, and report in Vietnamese.
```
