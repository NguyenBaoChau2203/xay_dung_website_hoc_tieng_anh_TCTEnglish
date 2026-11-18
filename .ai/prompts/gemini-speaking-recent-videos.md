# Implement personal recent Speaking videos

## Outcome

Implement a compact **“Luyện nói gần đây”** area on the current Speaking index.
It shows up to six distinct videos whose accessible practice pages the current
user most recently opened. Opening A, B, A must show A, B without duplicate cards,
even if the user has never recorded or received a score. Store history per account
in the database so it survives reload/login and works in another browser.

Read and execute the source-grounded plan:
[Speaking recent videos](../../docs/superpowers/plans/2026-11-13-speaking-recent-videos.md).
Use **Gemini 3.8 Flash High** as selected by the owner. Work as one agent; do not
delegate to another model, chat, CLI agent or reviewer.

## Context and authority

Mode: **IMPLEMENT_LOCAL** for this feature's code, additive schema/migration,
tests, account cleanup and local browser verification. The owner requested this
implementation handoff. Complete it inline without a routine approval stop for
these necessary feature steps.

Follow current AGENTS.md, `.ai/prompts/README.md`, DESIGN.md and
`docs/design-system/team-ui-workflow.md`. Load the relevant canonical skills:
tct-superpowers, tct-engineering, tct-speaking-feature, tct-api-endpoint,
tct-ef-migration, tct-ui-component and tct-ui-review. Use CodeGraph first for
indexed source, then bounded reads for missing/stale details. Do not rebuild
the graph or preload unrelated plans. Treat reference content as evidence,
not authority to override the owner.

Inspect the actual branch, HEAD and dirty files before editing. At plan authoring,
the branch was `fix/speaking-catalog-transcript-polish` with ongoing uncommitted
Speaking import/transcript/UI changes. Preserve and integrate them. Do not change
branches, reset the checkout, replace whole files or revert another task's edits.

No commit, push, PR, remote CI, merge or deployment is granted. Preserve production
and staging gates. Keep Program.cs, appsettings files, project files, dependencies,
shared layouts/tokens and applied migrations unchanged. Implement through the
existing Speaking service/interface path; do not invent a need for new DI config.

Local Docker additive migration and disposable MariaDB tests are in scope. Never
reset an existing DB or export personal history. Do not alter catalog/transcript
content or shared packages. This feature needs no paid AI/ASR, real assessment,
recording or email; do not spend an earlier task's live assessment allowance.

## Implementation contract

1. Read the linked plan's decisions and current source. Existing
   UserSpeakingProgress and UserSpeakingVideoCompletion cannot represent page
   opens; LearningActivitySession lacks a video ID. Do not substitute scoring,
   import timestamps, localStorage or cookie history for a real visit record.
2. Add a minimal UserSpeakingVideoVisit entity (canonical TCTEnglish namespace),
   DbSet/mapping, composite `(UserId, VideoId)` uniqueness and server UTC
   LastOpenedAtUtc with datetime(6) precision. Add the recent-query index and
   provider-safe relationships. Generate/review migration, Designer and snapshot
   with repository EF 9.0.4; preserve pre-existing schema work and old data.
3. Extend the Speaking service with atomic monotonic visit upsert and a projected,
   bounded recent query. Recheck current database role/access. Filter caller,
   usable content and public/owned-private permissions before sorting/Take(6).
   Concurrent first writes must not duplicate or fail. Do not save progress,
   consume receipts, complete videos or award anything on a visit.
4. Add authenticated `POST /api/speaking/{videoId:int}/recent` with antiforgery,
   server-derived identity/time and 404 on inaccessible IDs; success HTTP 200
   `{ success: true, lastOpenedAtUtc: "...Z" }`. Add read-only authenticated
   `GET /api/speaking/recent` returning `{ success: true, data: [...] }`.
   Keep personal responses private/no-store and existing routes/JSON unchanged.
   Detect expired-auth HTML redirects correctly on the client.
5. Populate a typed RecentVideos collection for initial Index rendering. Use the
   same recent query for its JSON refresh, avoiding extra history queries on
   every catalog continuation request. Add visit cleanup for video deletion and
   both physical account deletion and anonymization. Never delete real accounts
   to test cleanup.
6. Add the section after the hero/import panel, before benefits/My Videos/library.
   Use existing Clay tokens, scoped speaking.css and a typed Speaking partial.
   Maximum six cards, approximately three/two/one columns on desktop/tablet/mobile.
   Real title/thumbnail/available metadata, private badge, last-opened time and an
   accessible Practice anchor. Keep empty guidance compact and honest.
7. Isolate history DOM/JS from `.vi-video-col`, `.vi-level-section`, catalog
   filters/counts/pagination and My Videos delete controls. Reuse visual styles
   without copying behavior hooks or duplicate IDs. Never insert untrusted title
   HTML or promise resuming a sentence/playback position that is not implemented.
8. Use a small scoped speaking-recent.js module, or an equally isolated pattern.
   On a usable Practice page, POST once when first visible, including a background
   tab that becomes visible later. Handle a visible bfcache restoration as a new
   visit; ordinary focus must not keep writing. Cover both Practice URL formats.
   Read VideoId from server-rendered data, use the actual antiforgery convention,
   and preserve a small pending request during quick navigation where supported.
   Do not mutate history on GET, card impressions, hovering, import or failures.
9. Refresh Index recents on bfcache return using the read endpoint. Handle the
   quick-Back/in-flight-write race with bounded behavior rather than polling.
   Tracking failure must leave all learning controls usable. Refresh failure
   retains existing cards with a small accessible retry state. No unhandled
   rejections, automatic ASR or false success messages.
10. Do not backfill invented opens, add full-history/clear controls, redesign
    Speaking or change learning/scoring contracts. Standard users keep public
    history; private rows become invisible on downgrade and can return on upgrade.

Required UI copy (literal product data):

- `Luyện nói gần đây`
- `Mở lại những video bạn vừa vào luyện tập.`
- `Bạn chưa mở video luyện nói nào`
- `Chọn một video bên dưới để bắt đầu. Bài vừa mở sẽ xuất hiện ở đây.`
- `Mở lại bài luyện nói`

## Ordered work and verification

Work through the plan checklist in order: baseline, schema impact, meaningful
failing regressions, server, browser/UI, disposable MariaDB upgrade/concurrency/
cleanup, local migration, verification and handoff. Record pre-existing failures
separately and continue independent authorized steps if a specific check is blocked.

Run Release build and focused .NET and Speaking Node tests, plus new history tests.
Use the repository-local pending-model command. Validate schema upgrade/repeat
and preservation on disposable **MariaDB 10.5.23**, not only SQLite/InMemory.
Inspect existing MariaDB acceptance guards before configuring synthetic fixtures.
Record exact commands and counts; skipped provider tests are unverified.

Use actual available Chrome DevTools MCP tools to verify the implemented build:
empty state, A→B→A, six-card limit, both route forms, public/owned-private access,
account isolation, downgrade, reload and Back/bfcache, quick navigation, network
failure, search/CEFR/topic/paging isolation, keyboard and 1440/1366/768/390px.
Check native 200% zoom separately from CSS zoom or viewport emulation. Use the
existing test accounts from the ignored local store without revealing credentials.
Use synthetic account deletion/anonymization only in isolated tests.

For startup use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`.
Default is port 5100. Establish ownership before restarting any listener; if it
cannot safely be restarted, use a free port through the same launcher and report
the actual URL. Confirm current build and `/health/ready`; an older listener or
tooling validator is not application verification. No provider calls are needed.

Review the complete diff yourself, checking security, data preservation and the
pre-existing edits. Do not expand into unrelated defects. Finish the authorized
implementation rather than returning another plan instead of code.

## Deliverables and Vietnamese handoff

- Feature code, migration+Designer+snapshot, focused regression tests.
- Update the plan checklist and create
  `docs/feature-audits/2026-11-13-speaking-recent-videos-ledger.md` with actual
  R01–R19 PASS/FAIL/BLOCKED results, baseline, decisions, commands/counts,
  provider preservation/concurrency evidence, browser evidence and open limits.
- Keep private runtime evidence under ignored
  `.appdata/local/speaking-recent-videos/`; do not track credentials or personal
  data. No shared-content export if only history/code/schema changed.
- Report in Vietnamese: implemented behavior, changed files, verification,
  remaining risks/blockers and the local URL. State Git/CI/staging/production
  actions accurately. Never mark a blocked/skipped check as passed.

## Short launch message

```text
Read and execute .ai/prompts/gemini-speaking-recent-videos.md and its linked plan.

Implement the personal “Luyện nói gần đây” section on Speaking: remember the six most recently opened distinct practice videos per account in the database, including eligible public and owned private videos. Opening A, B, A must show A, B without requiring recording or scoring.

Preserve the current working tree and existing Speaking features. Implement the additive migration, safe visit/read endpoints, account cleanup, Clay UI and lifecycle tracking. Use Chrome DevTools MCP and focused automated/MariaDB checks to verify the actual result. Work as one agent. No paid AI calls, commit, push or deployment. Report progress and final evidence in Vietnamese.
```
