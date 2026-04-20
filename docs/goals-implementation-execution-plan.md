# Goals Feature Implementation Execution Plan

> **Historical plan notice (read first):** this document is kept for implementation history.
> The current authoritative source for remaining/finalization work is
> `docs/goals-finalization-playbook.md`.

> The purpose of this document is to turn the Goals idea into a clear execution plan that any AI agent can follow step by step, reduce rework, reduce risk, and stay within the current repository boundaries.

## 1. Objective

Turn the `Goals` page from a placeholder into a real feature backed by real user data, with a working goal update workflow, daily progress tracking, and a safe path to expand into XP and badges after the MVP is stable.

This document prioritizes:

- Preserving the current repository architecture.
- Fixing the current product inconsistency between Dashboard and Goals.
- Splitting the work into small phases so agents can deliver safely.
- Giving each phase checkpoints, tests, and clear completion criteria.

## 2. Scope And Rules

### 2.1 In Scope

- `GoalsController` and `Views/Goals/Index.cshtml`
- `GoalsViewModel`
- `IGoalsService` / `GoalsService`
- Data tracking for daily goals and weekly activity
- Minimal XP integration into existing learning flows
- Badge system only after daily tracking is stable

### 2.2 Not In The First Phase

- Class leaderboard
- 90-day calendar heatmap
- Streak freeze
- Weekly report email
- Reward chest
- Heavy celebration/confetti UX

These should only be added after the MVP is using real data and regression tests are in place.

### 2.3 Mandatory Agent Rules

- Do not add new domain logic to `HomeController` if it can live in `GoalsController` or `Services/`.
- Do not pass EF entities directly to views.
- Every mutation must use anti-forgery protection consistent with the rest of the repository.
- Every user-scoped query must include ownership/current-user filtering.
- Prefer `AsNoTracking()` for read-only queries.
- Do not modify `Program.cs`, `appsettings.json`, or `.csproj` unless explicitly requested.
- If implementation requires DI registration in `Program.cs`, the agent must stop and clearly note that user confirmation is required.

## 3. Current Repository State The Agent Must Understand

### 3.1 Current Reality

- `TCTEnglish/Controllers/GoalsController.cs` currently only returns the view.
- `TCTEnglish/Views/Goals/Index.cshtml` currently hardcodes streak, XP, and badges.
- Dashboard already maps `Goal` in `HomeController`, but `Views/Home/Index.cshtml` still hardcodes `0`.
- `StreakService` already works and is a good foundation to reuse.
- `LearningApiController` already updates `LearningProgress` and streak, but does not yet track XP or daily activity.

### 3.2 Implementation Hazards To Avoid

- `LearningProgress.Status` currently uses `"Learning"`, `"Reviewing"`, and `"Mastered"`, so any badge/XP logic must not compare against lowercase `"mastered"` unless statuses are normalized first.
- The current Goals view does not render an anti-forgery token, so any AJAX POST must explicitly add a token to the DOM.
- The repository currently uses `DateTime.UtcNow` in streak logic. If Goals continues using UTC for daily boundaries, the user experience may feel incorrect for internal users.
- If a daily activity table is added with a unique `(UserId, ActivityDate)` constraint, insert/update logic must be safe under concurrent requests.

## 4. Recommended Delivery Order

Agents must follow the phases below in order. Do not jump to badges or gamification UI before the previous phase is complete.

### Phase 0 - Preflight And Context Setup

#### Objective

Align the agent with the current architecture before editing code.

#### Required Reading

- `AGENTS.md`
- `docs/project-structure.md`
- `docs/architecture-prioritized-backlog.md`
- `.ai/context/known-issues.md`
- `.ai/context/coding-conventions.md`
- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/Controllers/HomeController.cs`
- `TCTEnglish/Views/Home/Index.cshtml`
- `TCTEnglish/Controllers/LearningApiController.cs`
- `TCTEnglish/Services/StreakService.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`

#### Workflow

1. Confirm that Goals is still a placeholder and Dashboard is inconsistent.
2. Confirm the canonical namespace and the current `TCTVocabulary.*` legacy situation.
3. Confirm special constraints:
   - `Program.cs` must not be modified without explicit approval.
   - Risky migrations must not be introduced casually.
4. Record the implementation assumptions:
   - Phase 1 only makes the Goals page real and editable.
   - Phase 2 adds daily tracking.
   - Phase 3 adds badge work later.

#### Definition Of Done

- The agent has a short current-state summary.
- The agent has identified the files needed for the next phase.

### Phase 1 - Make The Goals Page Real Without Adding New Tables Yet

#### Objective

Remove all fake numbers from the Goals page and turn it into a real page backed by actual user state, even before `UserDailyActivity` exists.

#### Expected Outputs

- `GoalsController` is refactored to use `BaseController`
- `GoalsViewModel` exists
- Goals shows real streak and real goal values, with a reasonable fallback for activity UI
- A working POST flow exists to update `User.Goal`

#### Expected Files

- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/css/goals.css`
- Possibly `TCTEnglish/Services/IGoalsService.cs`
- Possibly `TCTEnglish/Services/GoalsService.cs`

#### Detailed Workflow

##### Step 1. Create A ViewModel For The Goals Page

Create a `GoalsViewModel` that contains only what Phase 1 needs:

- `CurrentStreak`
- `LongestStreak`
- `StreakMessage`
- `DailyGoal`
- `TodayProgressValue`
- `TodayProgressMax`
- `ProgressPercent`
- `WeeklyActivity`
- `ShowPlaceholderBadges`

Also split out supporting types:

- `GoalsWeekDayViewModel`
- Optionally `GoalsBadgeViewModel`

Notes:

- In Phase 1, `WeeklyActivity` may still be a temporary fallback or an empty-state model with clear messaging that activity tracking comes later.
- Do not leave `12 Ngay` or `30/50` hardcoded in the view.

##### Step 2. Create A Service Boundary For The Goals Page

Even if a quick fix is tempting, create `IGoalsService` and `GoalsService` in Phase 1 to avoid growing a fat controller.

`GoalsService` in Phase 1 should only:

- Read `User.Goal`
- Read `User.Streak`
- Read `User.LongestStreak`
- Read `User.LastStudyDate`
- Compute a safe display value for `CurrentStreak`
- Return either a DTO or the view model

Do not add badge business logic in this phase.

##### Step 3. Refactor `GoalsController`

Convert the controller to inherit from `BaseController` so it can use `GetCurrentUserId()`.

`Index()` workflow:

1. Get `userId` from `GetCurrentUserId()`
2. Call the service
3. Receive DTO/view model
4. `return View(model)`

`UpdateGoal(...)` workflow:

1. Add `[HttpPost]`
2. Add `[ValidateAntiForgeryToken]`
3. Get `userId` from `GetCurrentUserId()`
4. Validate the goal range
5. Delegate update logic to the service
6. Return either `Json` or a redirect depending on the chosen UI pattern

##### Step 4. Update The Goals View

Replace all hardcoded numbers with model bindings:

- `12 Ngay` -> `@Model.CurrentStreak`
- `30/50` -> real values
- streak message -> model value

The view should add:

- `@model TCTEnglish.ViewModels.GoalsViewModel` if namespace normalization is complete
- `@Html.AntiForgeryToken()` in an appropriate place if using AJAX POST
- A clear empty state for the weekly chart while real activity tracking is not ready

Do not:

- Use `prompt()` as the final production UX
- Keep fake badges unless they are explicitly labeled as coming soon

##### Step 5. Choose The Goal Update UI Pattern

The agent must choose one of these approaches and record it clearly in the PR summary.

Option A:

- Use a simple Bootstrap modal
- Submit using a normal POST form
- Easier to test and debug

Option B:

- Use AJAX POST
- Still must render and send the anti-forgery token
- Must provide clear success/error handling

Recommendation:

- Prefer Option A in Phase 1 because it is more stable, easier to test, and less JavaScript-heavy.

##### Step 6. Sync The Dashboard

Once the Goals page becomes real, update `Views/Home/Index.cshtml` to display `@Model.Goal` instead of hardcoded `0`.

This matters because:

- Dashboard currently sets the wrong expectation.
- Skipping this step leaves the product inconsistent.

#### Validation For Phase 1

The agent must verify:

1. An authenticated user opening `/Goals` sees real streak and goal values.
2. Updating the goal with a valid value succeeds.
3. Updating the goal with an out-of-range value is blocked or clamped according to the chosen rule.
4. Dashboard shows the same `Goal` value as the Goals page.
5. No fake numbers remain in `Views/Goals/Index.cshtml`.

#### Recommended Tests

- Unit test for goal validation if the service owns clamp/range logic
- Integration test for `GET /Goals`
- Integration test for `POST /Goals/UpdateGoal`

#### Definition Of Done

- BUG-006 is partially resolved in the sense that Goals is no longer a fake page.
- Dashboard and Goals now share one source of truth for `Goal`.

### Phase 2 - Add Daily Activity Tracking For The Weekly Chart

#### Objective

Introduce real per-day data so the weekly chart and daily goal progress are based on actual activity.

#### Expected Outputs

- A new entity exists to aggregate daily activity per user
- Goals reads the weekly chart from that table
- Daily XP/progress is no longer a fake fallback

#### Expected Files

- `TCTEnglish/Models/UserDailyActivity.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- A new migration

#### Recommended Data Shape

`UserDailyActivity` should contain:

- `Id`
- `UserId`
- `ActivityDate`
- `XpEarned`
- `CardsReviewed`
- `NewCardsLearned`
- `QuizzesCompleted`
- `SpeakingCompletedCount` if speaking should be tracked from day one
- `MinutesSpent` only if there is a trustworthy source for it

#### Detailed Workflow

##### Step 1. Create The Entity And Navigation Properties

Add the new entity and add navigation properties to `User`.

Required unique index:

- `(UserId, ActivityDate)`

The agent must explicitly note:

- This unique index is required to prevent duplicate daily rows for the same user.

##### Step 2. Decide The Day Boundary

This is a required technical decision.

The agent must explicitly choose one approach before coding:

Option A:

- Continue using UTC for `ActivityDate`
- Fastest and consistent with current code
- Risky for local user experience

Option B:

- Use a business-local day derived from the internal timezone
- Better fit for daily goal/streak behavior
- Requires a small date abstraction instead of direct `DateTime.UtcNow` calls

Recommendation:

- If broader refactoring is allowed, introduce a small business-date abstraction.
- If this phase must stay narrow and safe, keep UTC temporarily but record it as debt.

##### Step 3. Create The Migration

The agent must:

1. Create a migration for `UserDailyActivity`
2. Read the generated migration file
3. Confirm it does not contain `DropTable` or `DropColumn`
4. Stop immediately if dangerous operations appear

##### Step 4. Expand `GoalsService`

At this point `GoalsService` must:

- Load the last 7 days of data
- Build all 7 chart columns with `0` for missing days
- Compute `TodayProgressValue`
- Compute `ProgressPercent`

Notes:

- Use `AsNoTracking()` for display queries.
- Do not query one row per day in a loop if one query plus in-memory mapping will do.

##### Step 5. Update The Chart UI

Update `Views/Goals/Index.cshtml` and `goals.css` to:

- Render the chart from model data
- Show an empty state when all 7 days are zero
- Add tooltip/labels only after the data model is stable

#### Validation For Phase 2

The agent must verify:

1. A user with no activity sees a valid zero-state chart with no layout breakage.
2. A user with some activity sees the correct dates and values.
3. Daily goal progress uses today's real data.
4. Queries do not introduce unnecessary N+1 behavior.

#### Recommended Tests

- Unit test for weekly chart mapping logic
- Integration test for `GoalsService`/controller covering:
  - no activity
  - activity today only
  - activity in the middle of the week
  - day-boundary behavior if a date abstraction exists

#### Definition Of Done

- Weekly chart and daily progress now use real data.
- Goals no longer depends on fake activity values.

### Phase 3 - Integrate XP Into Existing Learning Flows

#### Objective

Make real learning actions update `UserDailyActivity` so the Goals page becomes a live reflection of usage.

#### Important Principle

Do not force all flow integrations into one large PR. Integrate flow by flow, starting with the highest-signal and easiest-to-measure paths.

#### Recommended Order

1. `LearningApiController`
2. Daily challenge flow in `HomeController`
3. Speaking flow if a clear completion event already exists

#### Detailed Workflow

##### Step 1. Move Activity Recording Into The Service Layer

Instead of leaving controllers to build daily activity rows directly, `GoalsService` should own a method such as:

- `RecordActivityAsync(...)`
- or `AwardXpAsync(...)`

That method must:

1. Resolve the business date
2. Find or create the `UserDailyActivity` row
3. Increment counters based on activity type
4. Increment XP if the activity type earns XP
5. Save safely under concurrent requests

##### Step 2. Integrate With `LearningApiController`

This is the most sensitive integration point.

Current controller behavior:

- Uses `ControllerBase`
- Reads user identity via `User.TryGetUserId(...)`
- Sets status to `"Learning"`, `"Reviewing"`, `"Mastered"`

Because of that, the agent must not copy old pseudo-code blindly.

The agent must:

1. Read the exact `masteryLevel -> status` logic
2. Determine which transitions count as:
   - regular review
   - first-time learning
   - mastered
3. Call `GoalsService` after `LearningProgress` is updated

Avoid:

- Incorrect lowercase comparisons against status strings
- Double-awarding XP for the same transition
- Tight controller-to-goals coupling

##### Step 3. Integrate With Daily Challenge

Only award XP when:

- a real submit path exists
- the answer is confirmed correct

Do not award XP when:

- the user reloads the page
- the daily challenge is simply re-rendered

If the challenge flow does not yet have a reliable submission endpoint, the agent must record it as a dependency and defer the integration.

##### Step 4. Integrate With Speaking

Only do this if there is a trustworthy "speaking practice completed" event in code.

If that event is unclear or missing, defer this step instead of guessing.

#### Required Concurrency Handling

With a unique `(UserId, ActivityDate)` index, the agent must choose one of these:

Option A:

- Transaction + re-read + retry when unique-key conflicts occur

Option B:

- A helper upsert pattern:
  1. try read
  2. if null, add
  3. save
  4. on unique violation, reload row and apply the increment

Recommendation:

- Option B is usually simpler for the current monolith.

#### Validation For Phase 3

The agent must test:

1. Reviewing a card increases XP exactly once.
2. Mastering a card does not double-count XP.
3. A correct daily challenge answer awards XP.
4. Two near-simultaneous requests on the same day do not create duplicate rows.

#### Recommended Tests

- Integration test for `LearningApiController`
- Service tests for `AwardXpAsync` / `RecordActivityAsync`
- If feasible, a concurrency-oriented service test

#### Definition Of Done

- The Goals page is now powered by real user activity.
- Learning actions update the weekly chart and today's progress correctly.

### Phase 4 - Badge System

#### Objective

Add badges only after activity tracking is stable.

#### Expected Files

- `TCTEnglish/Models/Badge.cs`
- `TCTEnglish/Models/UserBadge.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- A new migration
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`

#### Detailed Workflow

##### Step 1. Define Badge Types

Initially only use badges based on simple, reliable metrics:

- Streak
- LongestStreak
- TotalDaysActive
- TotalXp

Defer:

- Speaking-dependent badges if speaking metrics are not stable
- Status-string-dependent badges unless data is normalized first

##### Step 2. Decide How To Seed Badges

The agent has two options:

Option A:

- Seed in a migration

Option B:

- Seed through an idempotent seeder

Recommendation:

- If the repository already has a stable seeding pattern and no `Program.cs` change is needed, prefer migration-based inserts for a small fixed badge set.

##### Step 3. Implement Badge Award Logic

Do not award badges inside controllers.

`GoalsService` must:

1. Compute the user's current metrics
2. Load badges the user does not already own
3. Evaluate unlock conditions
4. Create new `UserBadge` rows
5. Save and return the newly unlocked badges

##### Step 4. Normalize Metrics

This step is critical to avoid subtle bugs:

- If counting `CardsMastered`, normalize `Status` handling or query using the exact casing used in the live code
- If old DB data might contain inconsistent casing, consider a case-insensitive comparison or a cleanup migration

##### Step 5. Render Badges In The UI

Only switch the Goals page to badge-driven UI when badge data is real.

UI requirements:

- Show unlocked vs locked states
- Show progress for locked badges
- Do not hardcode badge names in the view

If a celebration popup is wanted:

- Do it in a later sub-phase or a follow-up PR

#### Validation For Phase 4

The agent must verify:

1. A new user can unlock the first badge correctly.
2. Badges are never duplicated.
3. Locked badge progress is calculated correctly.
4. Logic is not broken by casing mismatches.

#### Recommended Tests

- Service tests for `CheckAndAwardBadgesAsync`
- Migration/seed idempotency tests if seeding logic is involved

#### Definition Of Done

- Badges display real data.
- No fake badges remain in the view.

### Phase 5 - Product Polish After Logic Is Stable

#### Objective

Add UI/UX polish only after the earlier logic phases are fully tested.

#### Allowed In This Phase

- Chart tooltips
- Better empty states
- Goal update toast
- Small `+XP` toast
- Light badge unlock highlight

#### Not Recommended Yet

- Confetti before the logic is well tested
- Large celebration modals
- Heavy animation that risks regressions

## 5. Required Decision Gates

Before each phase, the agent must stop and explicitly confirm the following in notes or PR summary.

### Gate A - Does This Require Editing `Program.cs`?

If yes:

- Stop.
- Record why it is necessary.
- Ask for user confirmation, because this repository forbids changing that file casually.

### Gate B - Does This Require A Migration?

If yes:

- Confirm the migration only adds, and does not drop.
- Read the generated migration before continuing.

### Gate C - Does This Require A Timezone / Business-Date Decision?

If yes:

- Record clearly whether the implementation uses UTC or a business-local day.
- Do not choose silently without documenting it.

### Gate D - Does This Depend On An Unclear Completion Event?

If yes:

- Do not guess.
- Record the dependency and defer the work.

## 6. Standard Workflow Template For Every PR / Agent Handoff

Every agent working on a phase should follow this workflow.

### Step 1. Read

Read:

- This document
- The files relevant to the current phase
- Existing tests that may be affected

### Step 2. Confirm Current State

Write a short note covering:

- What data the feature currently uses
- Which files will be changed in this step
- Whether any dependencies or decision gates apply

### Step 3. Implement Narrowly

Only implement the assigned phase.

Do not:

- Sneak in leaderboard work
- Quietly redefine day-boundary behavior without approval
- Refactor unrelated controllers

### Step 4. Verify

The agent must run:

- Build
- Relevant tests
- Basic rendering checks if the UI changed

### Step 5. Report Handoff

Every agent must leave:

- What objective was completed
- Which files changed
- Which tests were run
- Remaining risks
- Dependencies for the next phase

## 7. File List By Phase

### Phase 1

- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/wwwroot/css/goals.css`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/Views/Home/Index.cshtml`

### Phase 2

- `TCTEnglish/Models/UserDailyActivity.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/Migrations/*`

### Phase 3

- `TCTEnglish/Controllers/LearningApiController.cs`
- `TCTEnglish/Controllers/HomeController.cs`
- Potentially `TCTEnglish/Controllers/SpeakingController.cs`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- Related test files

### Phase 4

- `TCTEnglish/Models/Badge.cs`
- `TCTEnglish/Models/UserBadge.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/Migrations/*`

## 8. Full Feature Definition Of Done

The Goals feature is considered complete when all of the following are true:

- `/Goals` no longer renders fake data.
- The user can update the daily goal successfully.
- Dashboard and Goals display the same `Goal` value.
- Weekly activity chart is backed by real data.
- Learning actions create daily activity records.
- If badges ship, they are backed by real data and not hardcoded.
- Minimum regression coverage exists for reading/updating the goal and tracking activity.
- PR summaries explicitly record timezone assumptions and migration impact.

## 9. Recommended PR Split

Do not combine everything into one large PR. Recommended order:

1. PR 1: Real Goals page + goal editing + dashboard sync
2. PR 2: `UserDailyActivity` + weekly chart + daily progress
3. PR 3: XP tracking from `LearningApiController` and daily challenge
4. PR 4: Badge entities + badge service + badge UI
5. PR 5: Small UI polish

## 10. Suggested PR Summary Template For Agents

Agents can use this template during handoff:

```md
## Goals Phase Summary

### Scope
- Phase:
- Objective:

### Files changed
- ...

### What changed
- ...

### Validation
- Build:
- Tests:
- Manual checks:

### Risks / follow-up
- ...

### Decisions recorded
- Timezone/day-boundary:
- Migration impact:
- Deferred items:
```

## 11.1 Goals Handoff Addendum (2026-03-28)

### Gate A Record - `Program.cs` update happened and why

- `Program.cs` was intentionally updated to register `IGoalsService` ->
  `GoalsService` in DI.
- Reason: multiple runtime entry points (`GoalsController`,
  `LearningApiController`, and dashboard daily-challenge tracking in
  `HomeController`) now depend on goals activity recording and would fail
  constructor resolution without explicit DI registration.
- This was a cross-cutting wiring change, not new domain logic in startup.

### Gate D Record - Speaking integration deferred

- Phase 3 Step 4 (speaking integration) is deferred.
- Reason: the current speaking flow does not expose a single trustworthy
  completion event that is safe for XP/activity awarding.
- Follow-up requirement: define one server-verified speaking-completion signal
  before enabling goals XP integration for speaking.

## 11. Final Recommendation

If priority must be reduced, follow this order:

- First: remove fake data and implement the real goal update flow
- Second: add activity tracking
- Third: add XP and badges

Do not start with badges and animation before real daily activity exists, because that would create more fake state and make testing harder.
