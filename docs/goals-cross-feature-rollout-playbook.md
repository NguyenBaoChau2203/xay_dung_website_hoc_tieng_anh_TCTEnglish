# Goals Cross-Feature Rollout Playbook

> Execution playbook for expanding `Goals` from a single vocabulary target into
> a real cross-feature goals, XP, badge, and streak system.
>
> The intent is that a future AI agent can be told only:
> `Read docs/goals-cross-feature-rollout-playbook.md and execute Phase X`
> without needing a long task prompt each time.

## 1. Purpose

This playbook defines the phased implementation path for:

- Multi-goal support on the `Goals` page
- XP rewards beyond vocabulary
- Feature-specific badges for `Speaking`, `Writing`, `Reading`, `Listening`
- Streak XP + streak badges
- Final review, regression verification, and closure criteria

This document is execution-oriented. Each phase tells the agent what to read,
which local skills/workflows matter, what to implement, how to verify, and what
must be ready for the next phase.

## 2. Minimal Prompt To Use This File

Use a short prompt like this:

```text
@workspace #solution
Read docs/goals-cross-feature-rollout-playbook.md and execute Phase <PHASE_NUMBER>.
Task: <short task note if needed>
Area hint: <optional, use the suggested hint from this playbook>
```

The agent should then follow this playbook as the main execution contract.

## 3. Global Agent Contract

For every phase, the agent must follow this contract.

### 3.1 Required repo reads

Read these first, in order:

1. `AGENTS.md`
2. `docs/project-structure.md`
3. `docs/architecture-prioritized-backlog.md`
4. `.ai/context/known-issues.md`
5. `.ai/context/coding-conventions.md`

Treat these as supporting or historical only:

- `docs/implementation_plan.md` if it exists
- `docs/goals-implementation-execution-plan.md`
- `docs/goals-finalization-playbook.md`
- `docs/branch-handoff-architecture-hardening.md`

### 3.2 Boundary rules

- Use the area hint first to identify the correct post-refactor controller,
  service, view-model, and view boundary.
- Do not add new domain logic to `HomeController`.
- Do not add new feature screens to `Views/Home/`.
- Keep work inside the correct feature boundary unless the work is explicitly
  cross-cutting.
- Prefer extending existing registered services such as `IGoalsService`,
  `IStudyService`, and `IStreakService` before introducing new DI registrations.
- If a phase truly requires a brand-new DI registration and therefore a
  `Program.cs` change, stop and ask the user before editing `Program.cs`.

### 3.3 Editing and safety rules

- Inspect existing local changes before editing and do not overwrite unrelated
  work.
- Read only the relevant files in `.agent/skills/`, `.agent/workflows/`, and
  `.ai/context/`, then follow those rules.
- Preserve UTF-8. If Vietnamese UI/text is touched, avoid encoding regressions.
- Run `python scripts/encoding_guard.py` only if it exists. Do not assume it
  exists in this repo.
- Do not modify `Program.cs`, `appsettings.json`, or any `.csproj` unless the
  user explicitly asked for it.
- Follow repo core rules: ViewModels only, thin controllers, async I/O,
  anti-IDOR, anti-CSRF, `GetCurrentUserId()` from `BaseController`,
  `NotFound()` for ownership violations.
- For namespaces/imports, follow the conventions already used in touched files
  and verify legacy `TCTVocabulary.*` references before changing them.

### 3.4 Bug-fix rule

If any phase turns into a bug fix or regression fix:

- Read `.ai/context/bug-fix-log.md` and `.ai/context/known-issues.md`
- Reuse an old fix pattern only if the root cause matches
- Update those files after verification if relevant

### 3.5 Required finish format

At the end of every phase, the agent must finish with a concise Vietnamese
summary covering:

- Result
- Files changed
- Checks run
- Skills/workflows used
- Remaining risks or blockers

## 4. Current Baseline Snapshot

Before implementation, assume this baseline until code inspection proves
otherwise:

- `Goals` is still modeled as a single numeric daily goal through `User.Goal`
  and a single `DailyGoal` in the view model/service.
- `Goals` activity tracking currently fits vocabulary first, with only limited
  counters in `UserDailyActivity`.
- Vocabulary already awards XP through `LearningApiController`.
- `Speaking` currently stores per-sentence practice scores, but not a stable
  server-verified video-completion event.
- `Writing` has practice/evaluation flow, but durable per-user progress is not
  fully persisted yet and exercise status can still be placeholder-derived.
- `Reading` and `Listening` may still be placeholder pages and must not be
  treated as real completion flows unless verified in code.
- Streak updates can happen from more than one entry point, so streak reward
  logic must stay centralized to avoid double-awards.

## 5. Suggested Area Hints By Phase

Use these short area hints when invoking an agent:

| Phase | Suggested area hint |
| --- | --- |
| 0 | `Goals / Services / Tests` |
| 1 | `Goals / ViewModels / Views/Goals / Models` |
| 2 | `Goals / Services / Models / API` |
| 3 | `Speaking / Goals / API / Services` |
| 4 | `Study/Writing / Goals / API / Services` |
| 5 | `Study/Reading-Listening / Goals / Views / Services` |
| 6 | `Goals / Badges / Streak / Services` |
| 7 | `Goals / Tests / Docs / Review` |

## 6. Relevant Local Skills, Workflows, and Templates

Only read the relevant items for the current phase.

### Skills

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/api-endpoint/SKILL.md`
- `.agent/skills/ui-component/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/speaking-feature/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md`
- `.agent/skills/data-seeder/SKILL.md`

### Workflows

- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/db-migration-flow.md`
- `.agent/workflows/code-review-flow.md`
- `.agent/workflows/bug-investigation.md`

### Templates

- `.ai/templates/controller.cs.md`
- `.ai/templates/service.cs.md`
- `.ai/templates/viewmodel.cs.md`
- `.ai/templates/ef-entity.cs.md`
- `.ai/templates/razor-view.cshtml.md`

## 7. Phase Overview

| Phase | Name | Primary outcome |
| --- | --- | --- |
| 0 | Preflight | Lock baseline, scope, boundaries, and risks |
| 1 | Multi-Goal Foundation | Convert `Goals` into a multi-goal feature |
| 2 | Reward Ledger Foundation | Generalize activity and XP tracking |
| 3 | Speaking Integration | Add safe speaking completion + rewards |
| 4 | Writing Integration | Add writing persistence + rewards |
| 5 | Reading/Listening Gate | Implement safely or defer explicitly |
| 6 | Badges And Streak Expansion | Add feature badges and streak rewards |
| 7 | Review And Closure | Full review, test, docs sync, close/no-close |

---

## Phase 0 - Preflight

### Objective

Confirm the real baseline, lock the boundaries, and prevent the agent from
guessing over stale docs.

### Read first

- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/UserDailyActivity.cs`
- `TCTEnglish/Models/Badge.cs`
- `TCTEnglish/Controllers/LearningApiController.cs`
- `TCTEnglish/Controllers/SpeakingController.cs`
- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Services/StudyService.Writing.cs`
- `TCTEnglish/Views/Study/Reading.cshtml`
- `TCTEnglish/Views/Study/Listening.cshtml`
- `TCTEnglish.Tests/GoalsPhase*.cs`

### Relevant skills/workflows

- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/db-migration-flow.md`

### Agent tasks

1. Inspect local changes and note files already modified by the user.
2. Confirm whether `Goals` is still single-goal, whether `Reading` and
   `Listening` are placeholders, and whether `Writing` still lacks durable
   user progress.
3. Confirm whether the current branch already contains partial work for any
   later phase.
4. Produce a short phase-local implementation note before editing:
   - baseline summary
   - affected files
   - whether schema changes are needed
   - whether any later phase dependency blocks safe work now
5. Decide whether the phase can be done without touching `Program.cs`.

### Verification

- Run only baseline inspection commands/tests needed to confirm current state.
- Do not start implementation if the baseline is still unclear.

### Definition of done

- The agent has confirmed the real code baseline.
- The agent has identified the exact files and boundaries for the next phase.
- The agent has recorded any blocker that would make later phases unsafe.

### Prepare the next phase

- Decide whether Phase 1 will introduce a new `UserGoal`-style entity or an
  equivalent additive model.
- Decide how legacy `User.Goal` will be handled during transition.

---

## Phase 1 - Multi-Goal Foundation

### Objective

Turn `Goals` from one vocabulary-only daily number into a multi-goal feature
that can represent separate targets for `Vocabulary`, `Speaking`, `Writing`,
`Reading`, and `Listening`.

### Read first

- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- `TCTEnglish/Controllers/GoalsController.cs`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/ViewModels/GoalsViewModel.cs`
- `TCTEnglish/Views/Goals/Index.cshtml`
- `TCTEnglish/Controllers/HomeController.cs`
- `TCTEnglish.Tests/GoalsPhase*.cs`

### Relevant skills/workflows

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/ui-component/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md` if schema changes are needed
- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/db-migration-flow.md` if schema changes are needed

### Agent tasks

1. Introduce an additive model for multiple goals:
   - preferred direction: a dedicated `UserGoal` entity/table
   - include `UserId`, `GoalArea`, `TargetValue`, `IsActive`, timestamps
2. Keep the change additive and migration-safe:
   - do not remove `User.Goal` in this phase
   - preserve backward compatibility until the new model is stable
3. Refactor `GoalsViewModel` and related input models so the page supports:
   - multiple goal cards
   - add/edit per goal area
   - clear area/unit labels
4. Update `GoalsController` and `GoalsService` to read/write the new goal model.
5. Update `Views/Goals/Index.cshtml` to present:
   - one card per active goal
   - create/edit UI for goal area + daily target
   - no fake progress labels
6. Keep `HomeController` stable:
   - if dashboard needs one goal value, use the vocabulary goal or an explicit
     summary rule
   - do not add new domain logic there
7. Prefer extending `IGoalsService` instead of introducing a brand-new service.
8. If a schema migration is required, keep it additive only:
   - add tables/columns/indexes only
   - no `DropTable`, no `DropColumn`

### Verification

- Build/update integration coverage for:
  - create first goal
  - edit existing goal
  - create multiple goal areas
  - invalid submit keeps modal/editor state
  - ownership/anti-forgery still hold
- Run focused `Goals` integration tests.

### Definition of done

- The `Goals` page can represent multiple daily goals.
- Vocabulary is no longer the only goal type.
- Legacy single-goal behavior is preserved safely during rollout.

### Prepare the next phase

- Define which activity counters are needed for each goal area.
- Decide whether progress should be tracked per day only, or also per
  completion source entity.

---

## Phase 2 - Reward Ledger Foundation

### Objective

Generalize activity tracking so XP and progress can be awarded by more than
vocabulary, without scattering reward rules across controllers.

### Read first

- `TCTEnglish/Models/UserDailyActivity.cs`
- `TCTEnglish/Models/DbflashcardContext.cs`
- `TCTEnglish/Services/GoalsActivityUpdate.cs`
- `TCTEnglish/Services/GoalsActivityRecordResult.cs`
- `TCTEnglish/Services/IGoalsService.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/Controllers/LearningApiController.cs`
- `TCTEnglish/Services/StreakService.cs`

### Relevant skills/workflows

- `.agent/skills/api-endpoint/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md` if schema changes are needed
- `.agent/workflows/db-migration-flow.md`

### Agent tasks

1. Extend the daily activity ledger so it can hold area-specific counters such
   as:
   - vocabulary reviews/completions
   - speaking video completions
   - writing exercise completions
   - reading exercise completions
   - listening exercise completions
2. Keep the `(UserId, ActivityDate)` uniqueness/race-handling model intact.
3. Move reward policy toward service ownership:
   - keep XP rules centralized in `GoalsService` or a tightly related helper
     under `Services/`
   - avoid duplicating reward constants inside multiple controllers
4. Preserve anti-double-award behavior:
   - rewards should fire only on valid first-time transitions or first-time
     completions
   - same request retried twice must not silently double-award
5. If schema changes are required:
   - additive only
   - no destructive operations
   - review migration carefully before applying

### Verification

- Add or extend tests for:
  - daily activity upsert race safety
  - XP increments exactly once for the same event
  - invalid/negative activity updates are rejected
  - `Goals` progress reads the new counters correctly

### Definition of done

- The reward ledger is no longer vocabulary-only in design.
- XP/progress logic is centralized enough for later speaking/writing phases.

### Prepare the next phase

- Define the exact server-verified completion signal for speaking.
- Decide which speaking metric counts as “goal progress” and “XP trigger”.

---

## Phase 3 - Speaking Integration

### Objective

Enable speaking goals, XP, and badges using a real server-verified completion
event, not a guess based on client-only behavior.

### Read first

- `TCTEnglish/Controllers/SpeakingController.cs`
- `TCTEnglish/Models/UserSpeakingProgress.cs`
- `TCTEnglish/Models/SpeakingVideo.cs`
- `TCTEnglish/Models/SpeakingSentence.cs`
- `TCTEnglish/ViewModels/SpeakingPracticeViewModel.cs`
- `TCTEnglish/Views/Speaking/Practice.cshtml`
- `TCTEnglish/wwwroot/js/speaking.js`
- `docs/goals-implementation-execution-plan.md`

### Relevant skills/workflows

- `.agent/skills/speaking-feature/SKILL.md`
- `.agent/skills/api-endpoint/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md` if new persistence is needed
- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/db-migration-flow.md` if schema changes are needed

### Agent tasks

1. Define one trustworthy server-verified speaking completion rule.
   Examples are acceptable only if verified against current code:
   - complete enough sentences in a video
   - reach a minimum score threshold
   - award only once per user/video completion state
2. Add persistence for video-level completion if sentence-level progress alone
   is insufficient.
3. Update the speaking save-progress flow so it can:
   - detect when a video becomes completed for the first time
   - call the centralized reward/progress path
   - avoid XP farming per sentence retry
4. Connect speaking completions to:
   - multi-goal progress
   - daily XP
   - feature-specific badges
5. Keep controllers thin:
   - business logic belongs in services
   - controller only validates, authorizes, delegates, returns

### Verification

- Add tests for:
  - score save still works
  - a video completion is counted once
  - replaying or re-saving a completed video does not double-award XP
  - another user cannot affect someone else’s speaking completion state

### Definition of done

- Speaking has a stable completion event that can safely drive goals and XP.
- Speaking rewards are not farmable through repeated low-level retries.

### Prepare the next phase

- Decide whether writing needs per-sentence progress, per-exercise progress, or
  both to support real completion status.

---

## Phase 4 - Writing Integration

### Objective

Add durable per-user writing progress so writing goals, XP, badges, and status
labels become real instead of placeholder-derived.

### Read first

- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Services/IStudyService.cs`
- `TCTEnglish/Services/StudyService.Writing.cs`
- `TCTEnglish/ViewModels/WritingIndexViewModel.cs`
- `TCTEnglish/Views/Study/Writing.cshtml`
- `TCTEnglish/Views/Study/WritingExercises.cshtml`
- `TCTEnglish/Views/Study/WritingPractice.cshtml`
- `TCTEnglish/wwwroot/js/writing.js`
- `docs/architecture-prioritized-backlog.md`

### Relevant skills/workflows

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/api-endpoint/SKILL.md`
- `.agent/skills/ui-component/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md` if schema changes are needed
- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/db-migration-flow.md` if schema changes are needed

### Agent tasks

1. Add durable writing progress for each user:
   - preferred direction: per-exercise progress plus per-sentence completion if
     required by the UI
2. Replace fake or hardcoded exercise status with real state:
   - `new`
   - `in-progress`
   - `completed`
3. Update writing evaluation flow so a sentence passing for the first time can
   update durable progress.
4. Award writing XP only when the exercise crosses a trustworthy completion
   threshold, not on every failed attempt or repeated submit.
5. Connect writing completions to:
   - multi-goal progress
   - daily XP
   - writing badges
6. Keep controller and service boundaries clean:
   - `StudyController` remains orchestration-only
   - persistence and reward logic live in services

### Verification

- Add tests for:
  - sentence pass updates durable progress
  - exercise status moves from `new` to `in-progress` to `completed`
  - first completion awards XP once
  - re-opening or replaying a completed exercise does not duplicate rewards

### Definition of done

- Writing no longer relies on placeholder user status.
- Writing goals and rewards are based on real persistence.

### Prepare the next phase

- Re-check whether `Reading` and `Listening` now have real content/progress
  flows in the branch, or remain placeholders.

---

## Phase 5 - Reading And Listening Enablement Gate

### Objective

Either integrate `Reading` and `Listening` safely, or defer them explicitly
without introducing fake state.

### Read first

- `TCTEnglish/Controllers/StudyController.cs`
- `TCTEnglish/Views/Study/Reading.cshtml`
- `TCTEnglish/Views/Study/Listening.cshtml`
- Any related models/services if real content exists in the branch
- `docs/architecture-prioritized-backlog.md`

### Relevant skills/workflows

- `.agent/skills/feature-scaffold/SKILL.md` if real implementation exists
- `.agent/skills/ui-component/SKILL.md` for disabled-state UI
- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/new-feature-flow.md`

### Agent tasks

1. Confirm whether each module has:
   - real content data
   - a real completion endpoint or persistence flow
   - an authoritative per-user progress signal
2. If a module is real enough:
   - integrate it into the same reward pipeline as other areas
   - add its goal progress counters
   - add its badges
3. If a module is not real enough:
   - do not fake completion
   - do not fabricate XP events
   - do not pretend a placeholder page is a learning completion
   - explicitly mark the area as deferred in code/docs/UI as appropriate
4. If both modules are still placeholders, this phase is still valid:
   - the correct outcome is an explicit defer gate, not a fake implementation

### Verification

- If implemented: add focused tests for completion, XP, and ownership safety.
- If deferred: verify the `Goals` UI does not promise unavailable behavior.

### Definition of done

One of these must be true:

- `Reading` and/or `Listening` are integrated using real completion signals
- or the phase ends with an explicit, documented defer gate and no fake state

### Prepare the next phase

- Finalize badge definitions and streak reward rules based on whichever feature
  areas are truly available.

---

## Phase 6 - Badges And Streak Expansion

### Objective

Expand badges and streak rewards so each real feature area has its own
achievement path, and streak earns both badges and XP safely.

### Read first

- `TCTEnglish/Models/Badge.cs`
- `TCTEnglish/Models/UserBadge.cs`
- `TCTEnglish/Models/UserDailyActivity.cs`
- `TCTEnglish/Services/GoalsService.cs`
- `TCTEnglish/Services/IStreakService.cs`
- `TCTEnglish/Services/StreakService.cs`
- `TCTEnglish/Controllers/HomeController.cs`
- `TCTEnglish/Controllers/LearningApiController.cs`
- `TCTEnglish.Tests/GoalsPhase*.cs`

### Relevant skills/workflows

- `.agent/skills/security-audit/SKILL.md`
- `.agent/skills/ef-migration/SKILL.md` if badge schema changes are needed
- `.agent/skills/data-seeder/SKILL.md` if seed strategy changes are needed
- `.agent/workflows/db-migration-flow.md` if schema changes are needed

### Agent tasks

1. Expand badge support to include feature-specific achievement paths for the
   real modules enabled so far.
2. Prefer additive badge model evolution:
   - add new metric types or equivalent metadata
   - keep existing badge data valid
3. Add streak XP that is awarded exactly once when streak increases on a new
   business day.
4. Keep streak orchestration centralized:
   - if `HomeController` remains a streak entry point, it must call service
     orchestration only
   - do not add fresh domain logic into `HomeController`
5. Seed or configure new badges safely:
   - prefer additive EF data seeding or migration-safe inserts
   - do not touch `Program.cs` for seeding unless the user explicitly asks
6. Ensure reward ordering is correct:
   - activity recorded
   - streak updated
   - streak XP evaluated
   - badges refreshed
   - response state consistent in the same request

### Verification

- Add tests for:
  - streak increase awards XP once
  - same-day repeat activity does not re-award streak XP
  - feature badges unlock immediately after threshold is reached
  - existing badges are not duplicated

### Definition of done

- Streak has real XP and badge behavior.
- Each real learning area has its own badge path.
- Badge unlock timing is correct in the same request where thresholds are met.

### Prepare the next phase

- Build the final review checklist based on what was truly implemented versus
  what was intentionally deferred.

---

## Phase 7 - Review And Closure

### Objective

Perform a full implementation review, regression pass, documentation sync, and
 a close/no-close decision.

### Read first

- `.ai/context/known-issues.md`
- `.ai/context/bug-fix-log.md` if regressions were fixed
- `docs/architecture-prioritized-backlog.md`
- `docs/goals-implementation-execution-plan.md`
- `docs/goals-finalization-playbook.md`
- all touched source files
- all touched tests

### Relevant skills/workflows

- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/code-review-flow.md`
- `.agent/workflows/bug-investigation.md` if failures appear

### Agent tasks

1. Review the whole change set for:
   - boundary violations
   - security regressions
   - reward duplication risk
   - stale placeholder UI
   - stale docs
2. Run the relevant regression suite for the touched areas.
3. Update docs/context if behavior changed:
   - `docs/architecture-prioritized-backlog.md` if product/architecture truth changed
   - `.ai/context/known-issues.md` if a new unresolved issue remains
   - `.ai/context/bug-fix-log.md` if a bug/regression was fixed
4. Produce an explicit close/no-close decision.

### Review checklist

- `Goals` supports multiple goal areas without breaking ownership/anti-forgery
- Vocabulary still works
- Speaking rewards are server-verified and non-farmable
- Writing rewards are persistence-backed and non-farmable
- Reading/listening are either real or explicitly deferred
- Streak XP is not double-awarded
- Badges are not duplicated
- Docs do not claim placeholder behavior that no longer matches code

### Verification

Run the most relevant build/test/security checks available, such as:

- Focused `Goals` integration tests
- New speaking/writing/reward tests introduced by these phases
- Targeted build for touched projects
- Focused manual verification for UI flows if applicable

### Definition of done

The work is closable only if all of these are true:

1. No phase introduced fake progress or fake reward state.
2. The implemented modules have real completion signals.
3. XP and badge awarding are deduplicated.
4. Security checks for ownership and anti-forgery still pass.
5. Tests for the changed areas pass, or any failures are explicitly explained.
6. Deferred areas are documented honestly.

### Close/no-close rule

- Close the rollout only if the review checklist and definition of done are
  satisfied.
- Do not close if `Reading`/`Listening` are still placeholders but the UI
  claims they are active goal/reward sources.
- Do not close if writing or speaking can still award XP repeatedly for the
  same completion.

### Phase 7 Outcome Snapshot - 2026-04-04

- **Decision**: `Close`
- **Active goal/reward areas**: `Vocabulary`, `Speaking`, `Writing`
- **Deferred areas**: `Reading`, `Listening` remain intentionally deferred
  until real completion signals exist
- **Review closeout**:
  - speaking/writing completion rewards use atomic completion transitions so
    replayed completion requests do not re-award XP/goal progress
  - writing request paths no longer run schema migration logic during normal
    user traffic
  - phased integration coverage includes replay protection for writing
    completion
- **Verification snapshot**:
  - `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`
  - `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoalsPhase'`
  - `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~Sprint1SmokeTests'`

---

## 8. Final Notes For Future Agents

- Do not skip straight to badge polish before the underlying progress signal is
  real.
- Do not implement `Reading` or `Listening` rewards by guessing.
- Prefer fewer real feature areas over many fake ones.
- If a phase cannot be finished safely, stop, document the blocker, and leave
  the code truthful rather than “visually complete”.
