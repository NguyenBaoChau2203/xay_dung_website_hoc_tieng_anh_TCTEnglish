# 07 — Review, fix and verify the combined local result

Model: GPT 6.1 Sol High. Local review plus in-scope fixes when invoked.
Prerequisites: 01–06 complete with actual handoffs, including all three 05 batches.
Single agent; remote release stages require a separate owner instruction.

## Stage 07 execution checklist (current checkout)

Local fixes and available verification are finished. Final evidence and the
numbered owner manual sequence are in
`docs/feature-audits/2026-11-14-writing-goals/07-integrated-acceptance.md`.
Native browser 200% zoom remains unverified; paid/live providers and remote
release stages were not run. This is not an all-environments acceptance claim.

- [x] Review prior contracts, combined diff, all three content batches and preserved baselines.
- [x] Run initial Release suite, 41 established Node checks, 29 isolated MariaDB provider checks, content verify/rerun, canonical Validate/Sync twice and EF pending-model check.
- [x] P2: repair the reproduced badge-filter keyboard gap (Home/End), and normalize stale/unknown filter state on pageshow; add failing browser-state regressions before changing production JS.
- [x] P2: bind the Writing practice header to persisted response status/attempt/completion totals, including another tab's completed sentences; verify failure/retry does not fabricate progress.
- [x] P2: link Goals modal labels to preserved JS IDs; failing rendered regression, browser accessible names and focus return verified.
- [x] P2: distinguish paused targets from unconfigured/zero-percent progress; six failing rendered regressions then green, browser verified.
- [x] P2: fix measured mobile dashboard legend overlap and completed Writing history overflow using scoped layout changes; browser before/after verified.
- [x] Verify Writing practice/completion/replay, errors, Goals/Profile, real-cookie account isolation/Back, mystery reveal and 24 route/viewport cases; explicitly record unavailable native zoom.
- [x] Verify all 108 canonical Writing packages together on a fresh MariaDB recipient, repeat execution and preservation.
- [x] Recheck initial full-suite failures without weakening assertions; final full 1,575 pass/50 gated skip, focused98, Node46, provider29+1. Initial transient root causes remain unproven.
- [x] Update contracts, content matrix, bug memory and Vietnamese numbered manual handoff.
- [ ] Native browser 200% zoom on the owner's real Chrome; tool shortcuts did not change DPR/viewport, so this check is not passed.

## Review and repair

- [x] Inspect actual branch/diff and previous ledgers. Reconcile frontend fields
  with backend projections, migrations, canonical packages and new catalog rules.
  Record a scoped repair checklist for real findings; implement authorized fixes.
- [x] Verify security and state transitions, history preservation, clock semantics,
  model/snapshot consistency, reward atomicity/idempotency, error recovery,
  mystery redaction, trusted content provenance and old-data compatibility.
- [x] Check source for unsupported numeric/earned placeholders in changed UI;
  manually distinguish harmless static labels, thresholds and decorations from
  fabricated learner progress. A string grep alone is not an audit conclusion.

## End-to-end scenarios

| Scenario | Check |
| --- | --- |
| New user logs in, opens Goals/Profile | No unearned XP, streak or awards |
| Accepted partial Writing practice | Confirmed study day under stage 03 contract; no false full completion |
| Complete exercise once | Owned saved progress, one completion credit, correct goal/XP/badge/intent |
| Repeat/reload/concurrent final submit | No duplicate lifetime credit/award/notification |
| Repeat on a later day | Practice-day streak behavior is correct; completion credit remains unique |
| Midnight and missed day | UTC+7 rules match on Goals/Profile; longest record preserved |
| Target update/pause/mixed skills | Correct units/overall percentage; old-day history unchanged |
| Every new award boundary | Below/exact/above threshold, persisted grant and visible Profile state |
| Mystery locked→earned | No pre-unlock leak; real award reveals details once |
| User A→logout→B in same browser | No draft, progress, badge or recent-history leakage |
| Private/unpublished/wrong IDs | Existing access/404/antiforgery contracts hold |
|108 lessons across18 cells | Six net-new public lessons/cell; usable full bilingual exercise |
| Reimport and fresh recipient | No duplicates; old personal data and immutable package bytes preserved |
| Local optional provider outage/offline/429 | No fake pass/award; clear recoverable state |

Use fake providers/controlled clocks for deterministic checks. No live paid calls
or OS clock changes to simulate several days. Do not rewrite real user's study
history to create demonstration badges; fixtures belong to disposable/owned tests.

## Completion checks

- [x] Release build and relevant/full application test suite after integration.
  Run established Node Writing/Goals/draft/status suites. Record actual totals.
- [x] EF pending-model check clean; every persistent change includes migration,
  Designer/snapshot and MariaDB 10.5.23 upgrade/repeat/data-preservation evidence.
- [x] Validate all exported packages, immutable history and repeat sync. Current
  last-sync catalog hash matches. Exported content excludes personal data.
- [x] Chrome DevTools MCP/real-browser role/state/resolution checks completed;
  native 200% zoom and microphone/cloud checks are labeled honestly if unavailable.
- [x] Every backend/final UI contract and content matrix updated after fixes.
  Append actual bug memory for fixed defects and retire matching known issues.
- [x] Diff is focused and contains no credentials, scratch, personal records,
  unrelated code, package rewrites or protected config changes without authority.

Write `07-integrated-acceptance.md` with findings/fixes/checks/limits and a numbered
manual test route for the owner: Writing selection→list→practice→resume→Goals
target/study-day→badge→Profile→account switch. Include expected outcomes and safe
test fixtures. Report in Vietnamese; state local/CI/staging/production separately.
Do not call an unrun/blocked/skipped check passed or deploy the local result.
