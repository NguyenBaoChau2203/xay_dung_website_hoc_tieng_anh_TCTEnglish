# 04 — Real awards, playful tiers and mystery visibility

Model: GPT 6.1 Sol High. Local implementation when invoked.
Prerequisite: 03-learning-contract.md and verified reward/metric behavior.
Use README.md's five labels and ten award definitions as the product proposal.

## Scope and order

- [x] Audit the current12 badge definitions and every metric→threshold→persisted
  UserBadge→notification→Goals/Profile projection path. Static catalog metadata
  is valid; hardcoded earned state/progress is not. Preserve IDs/codes/history.
- [x] Verify first-day, 3/7-day milestones, XP, Writing and other existing metrics
  can actually reach their thresholds. Do not change sibling learning semantics
  merely to make a demo badge unlock. Cover unsupported/unknown metric values.
- [x] Add durable tier metadata and secret visibility with coherent mappings for
  old badges. Separate stable tier keys from Vietnamese labels. Mystery remains
  a visibility group with underlying difficulty; it need not mean hardest.
- [x] Add the ten README awards with actual evaluators and exact criteria. Use
  current real metrics whenever possible. No timezone/night-owl/spending/random
  awards requiring nonexistent telemetry or unhealthy behavior.
- [x] Implement server projection redaction for locked mysteries. No hidden rule,
  full name, numeric threshold/current progress or revealing code in HTML/JSON,
  tooltip, aria text, data attributes, filters or next-badge recommendations.
  Unlocked entries reveal their real name/rule/date and appear on Profile.
- [x] Keep award inserts unique and transactional; concurrency grants one award
  and one durable intent. Preserve earned awards even when current streak drops.
  Compare recency using the same business clock on both read surfaces.
- [x] Define new-award catch-up for existing eligible users. Produce a dry-run
  candidate report with minimal personal data kept ignored; run bounded local
  reconciliation only through the reviewed domain path. Do not manufacture XP/
  study dates, grant from unsatisfied conditions, revoke old badges or write on
  ordinary GET. Prevent notification floods; identify retroactive awards honestly.
- [x] Use the existing HasData reference-catalog convention if still correct.
  Ship additive metadata/catalog migration, Designer and snapshot; preserve enum
  numbers, stable codes, IDs/FKs and existing UserBadges. Detect custom-ID/code
  conflicts safely rather than overwriting them. Do not edit old migrations.
- [x] Badges are not presently an allowlisted shared-content root. Deliver this
  reference catalog via reviewed EF migration, not a SQL dump or invented generic
  exporter. New Writing lessons still use canonical packages in stage 05.
- [x] Expose typed view contracts for stage 06, including stable group/label,
  availability/redaction state, known progress, earned date and units. Do not
  force Gemini to recompute eligibility or peek into entities/seed definitions.

## Boundary tests

For every new award: threshold − 1/threshold/threshold + 1; real earning action; repeat;
parallel requests; fresh context/reload; two-user isolation; notification intent
dedupe; broken streak after award; existing eligible user catch-up; unknown
history; locked mystery payload inspection; unlock/reveal; missing/invalid metric;
unknown future tier fallback; Profile awarded-only list. Confirm all old 12 awards
remain intact and old code/IDs do not change.

Test metadata/catalog upgrade and repeat on disposable MariaDB 10.5.23 using
representative existing users, grants, progress and custom-data collision cases.
Record row/relationship preservation. Pending-model check must be clean.

## Deliverables

Updated domain/service/ViewModels plus migration evidence; existing/new badge
regressions; `04-badges.md` ledger; `04-badges-contract.md` documenting actual
member names and payload examples without secret/user data. Note any difference
from the README proposal. Report in Vietnamese; keep final UI integration for 06.


## Execution ledger — 2026-11-14

Completed locally after stage03 on the existing fix branch; earlier work is
preserved. Full evidence and scope limits:
[04-badges ledger](../../../feature-audits/2026-11-14-writing-goals/04-badges.md)
and [actual frontend contract](../../../feature-audits/2026-11-14-writing-goals/04-badges-contract.md).

Broad application suite:361 pass,0 fail,24 gated skips. Final disposable MariaDB
10.5.23 suite:29 pass,0 fail,0 skips. Node25/25, app build and EF pending-model
check pass. All ten boundaries, real earning, replay, multi-user concurrency,
catch-up, notification dedupe, rollback and hidden payloads are covered.

Local migration preserved42 old grants. Explicit63-user dry-run found7 eligible
new awards; bounded apply created7 retroactive grants with no notifications and
repeat found0 candidates. Old grant IDs/dates and XP/streak/activity/credit totals
remain intact. Private receipts stay ignored. Final visual binding remains06;
current10 public Writing lessons require stage05/Admin additions for milestones
above10. No paid calls, protected config edits, commits or remote actions.
