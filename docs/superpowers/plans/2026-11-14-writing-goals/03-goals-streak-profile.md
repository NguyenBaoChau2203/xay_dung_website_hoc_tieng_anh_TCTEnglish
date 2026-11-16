# 03 — Align confirmed study days, Goals and Profile

Model: GPT 6.1 Sol High. Local implementation/fixes when invoked.
Prerequisites: 01/02 handoffs. This is the cross-feature reward integration stage.

Execution status (2026-11-14): completed locally by one agent. Actual checks,
scope and browser limits are recorded in
`docs/feature-audits/2026-11-14-writing-goals/03-goals-streak-profile.md`.
Learning/UI field contract and bounded historical dry-run are adjacent files.
No remote actions, paid calls or historical reward rewrite were performed.

## Source map

GoalsService/IGoalsService, StreakService/IStreakService, BusinessDateHelper,
Notifications/LearningBusinessClock, Learning/CompletionCreditService,
LearningActivityTracker, LearningHabitService, GoalTargetHistoryWriter,
LearningNotificationIntentWriter and Notifications/LearningOutboxDispatcher.
AccountProfileService/ProfilePageViewModel, GoalsViewModel and dashboard consumers.
Models/UserDailyActivity, UserGoal, UserGoalTargetRevision, UserBadge and user streak
fields. Verify exact current paths/symbols through CodeGraph before editing.

## Ordered work

- [x] Inventory every visible Goals/Profile/dashboard statistic and its read/write
  source. Distinguish real counters, projections, unsupported claims and decorative
  artwork. Record the agreed `03-learning-contract.md` before implementation.
- [x] Verify the README learning contract: login/page visits alone never count;
  accepted persisted Writing practice can count before whole-exercise completion;
  replay on another business date can maintain study streak without another
  lifetime completion/20 XP grant. Use the existing server event from stage 02.
- [x] Keep first-completion counters, practice-day presence, goal attainment and
  XP separately modeled. Reuse current tables/events if sufficient. If a durable
  signal is missing, design the smallest additive schema/event change; do not
  infer accepted practice from browser time, login, an empty activity row or a
  failed/NeedsReview answer. Record honest historical availability.
- [x] Use a consistent injectable UTC+7 business clock across day writes, current
  streak reads, badges, notifications and target revisions. Persist UTC instants
  and business dates with explicit meanings; no request-local DateTime.Now hacks.
- [x] Verify same-day repeats, midnight boundaries, yesterday continuity, missed
  days, long absence, future/legacy timestamps and clock-based tests. Preserve
  longest streak; expired current streak must match on Goals and Profile without
  requiring a rewarding GET. Concurrent cross-feature events advance at most once.
- [x] Keep once-per-day streak bonus once and preserve the reward transaction/
  outbox unit. Distinguish 1062 duplicate keys from genuine failures/deadlocks;
  exercise retry and rollback on matching MariaDB with independent connections.
- [x] Split vocabulary weekly counts from general confirmed study-day presence
  and actual daily-goal-met state. Never mark all goals complete because one card
  was reviewed. Include Writing-only activity; units must remain meaningful.
- [x] Verify goal creation/update/pause 0/max 500/invalid inputs, five supported
  areas and unavailable areas. Overall percent averages positive-target skill
  percentages. Target edits must not rewrite past-day success, XP or streaks.
- [x] Align Profile summaries, habit window and badge recency with the actual
  business date. Keep earned badges monotonic and restricted to the current user.
- [x] Inventory historic missing rewards using only durable evidence. Produce a
  dry-run report and explicit reconciliation design; do not silently rewrite
  personal history. Stage 04 owns badge catch-up. Any broader history rewrite
  beyond additive safe repair needs a concrete owner decision.

## Required cases

Login only: 0; first accepted sentence today; accepted partial exercise; later-day
replay; same-day 100 replays; final completion once; wrong/review-only answers;
two tabs/cross-skill concurrency; 23:59:59→00:00:00 UTC+7; 16:59:59→17:00:00 UTC;
missed day→restart 1 with old longest retained; new account/no goal; paused target;
only Writing target; mixed-unit targets; target changed mid-day; yesterday's
history unchanged; forced credit/outbox failure; two users; anonymized/deleted user.

## Verification and handoff

- [x] Meaningful failing-then-passing regressions for confirmed defects/new events.
- [x] Relevant GoalsPhase1–7, GoalsCompletionRecovery, GoalsDeadlockPropagation,
  CompletionCreditAmbientTransaction, ProfileBadgeRead and notification tests.
- [x] Disposable MariaDB 10.5.23 multi-connection and upgrade/repeat/preservation
  proof for affected persistent/transaction changes; EF pending-model check clean.
- [x] Thin controllers and preserved existing JSON/access/antiforgery contracts.
- [x] Actual UI contracts recorded in `03-learning-contract.md`: field meaning,
  units, business-date display, known/unknown and null/zero semantics.

Write actual results/remaining limits in stage 03's ledger, update symptom-relevant
bug memory and report in Vietnamese. Keep Gemini's presentation changes. Do not
introduce new badges, 108 lessons or unrelated feature refactors here.
