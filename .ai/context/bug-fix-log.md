# TCT English — Bug Fix Log

After every bug fix, the agent **must append one entry** to this file.
This is a historical record of actual fixes — not a list of pending issues (see `known-issues.md`).

---

## How Agents Should Use This File

1. Read this file before fixing any bug.
2. Start from the newest entries and search for matching symptoms, stack traces, entities, controllers, or regression patterns.
3. Reuse a previous fix pattern only after confirming the same root cause exists in the current code.
4. If a previous entry influenced the solution, mention that relationship in `Notes`.
5. After the fix is verified, append one new entry below the marker in the "Fix History" section.

---

## Entry Format

```
### [BUG-ID or short description] — [fix date]

**Symptom**: What the user observed (HTTP code, broken UI, wrong data...)

**Root Cause**: Why it happened — which layer, file, or logic was wrong

**Solution**: What was changed and where — be specific

**Files Changed**:
- `path/to/file.cs` — line X: brief explanation

**Verification**: How the fix was confirmed (manual steps, automated test, regression check)

**Commit**: `fix(scope): message` — hash if available

**Notes**: Regression warnings, edge cases to watch, or related previous fixes (if any)
```

---

## Fix History

<!-- Agent: append new entries BELOW this line, newest first -->

### Goals phase-2 database apply was blocked by a missing migration designer artifact - 2026-04-04

**Symptom**: The app could be kept alive with compatibility fallback, but applying the intended SQL Server migration `20260404143000_AddUserDailyActivityAreaCountersPhase2` failed because EF reported `The migration '20260404143000_AddUserDailyActivityAreaCountersPhase2' was not found.`

**Root Cause**: The migration had its `.cs` file but was missing the generated `20260404143000_AddUserDailyActivityAreaCountersPhase2.Designer.cs`, so EF Core did not discover it as a valid migration. At the same time, the local `bin/*/TCTEnglish.dll` outputs were locked by a running process, which blocked a normal rebuild to refresh migration metadata before database update.

**Solution**: Restored the missing designer artifact for the phase-2 migration, then applied the schema change directly on SQL Server with an idempotent script that adds `VocabularyCompletedCount`, `WritingCompletedCount`, `ReadingCompletedCount`, and `ListeningCompletedCount` only when absent and records the matching row in `__EFMigrationsHistory`.

**Files Changed**:
- `TCTEnglish/Migrations/20260404143000_AddUserDailyActivityAreaCountersPhase2.Designer.cs` - restored the missing EF Core designer metadata so the migration exists in source as a complete pair again.
- `.ai/context/bug-fix-log.md` - recorded the database-apply follow-up and the missing-designer root cause.

**Verification**: Connected to the configured SQL Server and verified `COL_LENGTH('dbo.UserDailyActivities', ...) = 4` for all four phase-2 counters plus `COUNT(*) = 1` in `__EFMigrationsHistory` for `20260404143000_AddUserDailyActivityAreaCountersPhase2` with product version `10.0.2`.

**Commit**: Not created yet.

**Notes**: This database apply complements the earlier compatibility fallback in `GoalsService`; the site now has both the code-side safety net and the intended SQL schema. Local EF CLI rebuild/update is still blocked while a process keeps `bin\Debug\net10.0\TCTEnglish.dll` and `bin\Release\net10.0\TCTEnglish.dll` locked.

### Goals page crashed on legacy UserDailyActivities schema missing phase-2 counters - 2026-04-04

**Symptom**: Opening `/Goals` on a database that had not yet applied the latest additive `UserDailyActivities` rollout failed with `SqlException: Invalid column name 'VocabularyCompletedCount' / 'WritingCompletedCount' / 'ReadingCompletedCount' / 'ListeningCompletedCount'`.

**Root Cause**: `GoalsService` assumed the phase-2 daily-activity counter migration was already present everywhere. Both the Goals read path (`GetGoalsAsync` / badge metrics) and the activity write path (`RecordLearningActivityAsync`, streak XP row creation) materialized or updated the new columns directly through EF, so any environment still on the older table shape crashed at runtime instead of degrading safely.

**Solution**: Added schema detection plus compatibility-mode read/write helpers in `GoalsService` for `UserDailyActivities`. When the phase-2 counters are missing, the service now reads/writes only the legacy column set, defaults the missing counters to `0`, and logs an explicit warning that migration `20260404143000_AddUserDailyActivityAreaCountersPhase2` still needs to be applied for full metrics. Added regression coverage for loading `/Goals` and recording learning activity against a downgraded SQLite table that drops those four columns.

**Files Changed**:
- `TCTEnglish/Services/GoalsService.cs` - added `UserDailyActivities` schema detection, compatibility queries/commands, and fallback streak/activity persistence when the new counters are absent.
- `TCTEnglish.Tests/GoalsPhase2IntegrationTests.cs` - added regressions for `/Goals` page load and learning-activity persistence against a legacy `UserDailyActivities` schema.
- `TCTEnglish.Tests/GoalsPhase7IntegrationTests.cs` - added the missing `TCTVocabulary.Services` import so the test project can compile `BusinessDateHelper` references during verification.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase2IntegrationTests`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~GoalsPhase1IntegrationTests|FullyQualifiedName~GoalsPhase2IntegrationTests|FullyQualifiedName~GoalsPhase3IntegrationTests|FullyQualifiedName~GoalsPhase4IntegrationTests|FullyQualifiedName~GoalsPhase5IntegrationTests|FullyQualifiedName~GoalsPhase6IntegrationTests"` with workspace-local `.dotnet-home`; all passed.

**Commit**: Not created yet.

**Notes**: Full `FullyQualifiedName~GoalsPhase` still reports an unrelated existing phase-7 failure (`/Home/Writing/Practice` anti-forgery fetch returning `401` for the anonymous test client). The compatibility fallback keeps the site working on legacy schema, but missing phase-2 counters still need the real migration applied if the team wants complete vocabulary/writing badge metrics and backfilled reporting.

### Goals phase-7 reward hardening and request-path cleanup - 2026-04-04

**Symptom**: Final review for the cross-feature Goals rollout found two closure risks: speaking/writing completion rewards still depended on non-atomic completion-state transitions, and writing request paths could call `Database.MigrateAsync()` during normal user traffic even though startup already migrates the app.

**Root Cause**: `SpeakingController.SaveSpeakingProgress` and `StudyService.Writing.PersistWritingProgressAsync(...)` used read-then-write completion flips (`IsCompleted == false` to `true`) without an atomic guard on the transition that triggers XP. In parallel or replay-heavy conditions, that left room for the same logical completion to be rewarded twice. Separately, an older writing hotfix added a request-time schema guard that no longer matched the current startup-migration baseline.

**Solution**: Hardened both completion paths so XP/reward issuance is driven only by an atomic conditional update of the persisted completion row, removed request-time writing schema migration logic, added a focused replay regression for writing completion, and synchronized backlog/known-issues/playbook docs with an explicit `Close` decision for the rollout.

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` - replaced tracked completion flip logic with an atomic completion-row update so a speaking video completion only awards once.
- `TCTEnglish/Services/StudyService.Writing.cs` - removed `EnsureWritingSchemaReadyAsync()` request-path migration logic and changed exercise completion to an atomic transition before awarding writing rewards.
- `TCTEnglish.Tests/GoalsPhase7IntegrationTests.cs` - added replay regression coverage for writing completion so XP/progress stay single-award after the first completed submit.
- `docs/architecture-prioritized-backlog.md` - updated Goals backlog truth to reflect active vs deferred goal areas and reward-dedup coverage expectations.
- `.ai/context/known-issues.md` - updated the Goals drift warning and marked phase-7 closure/reward hardening as resolved/verified.
- `docs/goals-cross-feature-rollout-playbook.md` - recorded the explicit rollout close decision and verification snapshot.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoalsPhase'`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~Sprint1SmokeTests'` with workspace-local `.dotnet-home`; all commands exited `0`.

**Commit**: Not created yet.

**Notes**: The rollout is now closed for `Vocabulary`, `Speaking`, and `Writing`. `Reading`/`Listening` remain explicitly deferred and should stay that way until they gain real completion signals.

### Goals phase-2 mastered-retry test expected pre-streak-XP total — 2026-04-04

**Symptom**: `GoalsPhase2IntegrationTests.RecordLearningActivityAsync_MasteredCompletionCountsOnceAcrossRetries` failed with XP mismatch (`Expected: 20`, `Actual: 30`) after phase-6 streak XP rollout.

**Root Cause**: The phase-2 assertion still reflected pre-phase-6 behavior and did not include one-time streak XP (`StreakXpAwarded`) when streak increases on the business day.

**Solution**: Updated phase-2 test expectations to include streak XP in daily total and assert `StreakXpAwarded == 10` to keep the contract explicit.

**Files Changed**:
- `TCTEnglish.Tests/GoalsPhase2IntegrationTests.cs` — updated expected XP and added streak-XP assertion in mastered-retry regression.

**Verification**: Ran focused test `TCTEnglish.Tests.GoalsPhase2IntegrationTests.RecordLearningActivityAsync_MasteredCompletionCountsOnceAcrossRetries` (passed), then reran goals phase suites.

**Commit**: Not created yet.

**Notes**: Same regression family as prior phase-3 expectation updates after streak-XP integration; root cause is test contract drift, not service logic failure.

### Goals Phase4 modal contract test drifted after multi-goal input rename — 2026-04-04

**Symptom**: `GoalsPhase4IntegrationTests.GoalsPage_RendersGoalInputContract_ForAutofocusTarget` failed because it still asserted `name="GoalEditor.DailyGoal"`, while the UI had moved to the multi-goal model using `GoalEditor.TargetValue`.

**Root Cause**: Regression in test contract after Phase 1/2 model migration (`DailyGoal` -> `TargetValue` + `GoalArea`). The view and controller were already updated, but one phase-4 test still referenced the legacy field name.

**Solution**: Updated phase-4 test payload/assertions to use `GoalEditor.TargetValue` and include `GoalEditor.GoalArea` in invalid submit POST data so model binding and validation reflect current behavior.

**Files Changed**:
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` — replaced legacy `GoalEditor.DailyGoal` references with `GoalEditor.TargetValue` and aligned invalid-submit form payload.

**Verification**: Ran focused tests for `TCTEnglish.Tests.GoalsPhase4IntegrationTests.GoalsPage_RendersGoalInputContract_ForAutofocusTarget` and `TCTEnglish.Tests.GoalsPhase4IntegrationTests.UpdateGoal_InvalidSubmit_ReturnsOpenModalContractForReload` (both passed).

**Commit**: Not created yet.

**Notes**: Related to earlier modal/autofocus hardening entries; same class of issue (HTML contract drift) but different root cause (input name migration).

### Goals modal autofocus nhắm nhầm hidden anti-forgery field — 2026-03-30

**Symptom**: Khi mở modal chỉnh sửa mục tiêu trên trang `Goals`, con trỏ có thể focus vào hidden anti-forgery input thay vì ô nhập số mục tiêu, làm trải nghiệm create/edit kém ổn định.

**Root Cause**: `goals.js` dùng selector quá rộng (`input, select, textarea, button`) trong `shown.bs.modal`, nên phần tử đầu tiên có thể là `input[type="hidden"]` do `@Html.AntiForgeryToken()` render ra.

**Solution**: Ưu tiên focus thẳng `.goal-input`; nếu không có thì fallback sang selector loại trừ hidden input (`input:not([type='hidden'])`). Đồng thời thêm contract test HTML để giữ ổn định target field trong modal.

**Files Changed**:
- `TCTEnglish/wwwroot/js/goals.js` - đổi logic autofocus để ưu tiên `.goal-input` và loại trừ hidden input ở fallback.
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` - thêm test `GoalsPage_RendersGoalInputContract_ForAutofocusTarget` xác nhận modal có hidden anti-forgery field và ô nhập `.goal-input` cho `GoalEditor.DailyGoal`.

**Verification**: Chạy `dotnet test TCTEnglish.Tests --filter 'FullyQualifiedName~GoalsPhase' --no-restore` và không ghi nhận lỗi backend/data integrity mới trong bộ Goals phases.

**Commit**: Not created yet.

**Notes**: Đây là hardening UX/accessibility cho modal interaction; logic dữ liệu/service không thay đổi.

### Goals modal CTA không mở ổn định vì thiếu Bootstrap JS bundle toàn cục — 2026-03-30

**Symptom**: Trên trang `Goals`, các CTA dùng `data-bs-toggle="modal"` (nút đầu trang và empty-state) có thể không mở được modal editor trong runtime do layout chính chưa nạp Bootstrap JS.

**Root Cause**: `Views/Shared/_Layout.cshtml` chỉ nạp Bootstrap CSS mà chưa nạp `bootstrap.bundle.min.js`, nên các hành vi `data-bs-*` không được kích hoạt. `goals.js` cũng gọi qua global `bootstrap` thay vì API tham chiếu an toàn từ `window.bootstrap`.

**Solution**: Nạp Bootstrap JS bundle ở layout chung để tất cả màn dùng `data-bs-*` hoạt động ổn định, đồng thời harden `goals.js` để mở modal qua `window.bootstrap?.Modal` và không ném lỗi nếu API chưa sẵn sàng.

**Files Changed**:
- `TCTEnglish/Views/Shared/_Layout.cshtml` - thêm script CDN `bootstrap.bundle.min.js` trước `RenderSection("Scripts")`.
- `TCTEnglish/wwwroot/js/goals.js` - dùng `modalApi = window.bootstrap?.Modal` và gọi `modalApi.getOrCreateInstance(...)`.

**Verification**: Chạy `dotnet test TCTEnglish.Tests --filter "FullyQualifiedName~GoalsPhase1IntegrationTests|FullyQualifiedName~GoalsPhase5IntegrationTests" --no-restore` (11/11 passed), chạy `Sprint2SmokeTests` (15/15 passed), và `run_build` (passed).

**Commit**: Not created yet.

**Notes**: `scripts/encoding_guard.py` không tồn tại trong repo hiện tại nên không thể chạy encoding guard script.

### Goals progress card text wraps vertically and header CTA is cramped on small screens - 2026-03-30

**Symptom**: On the Goals page, the text inside the circular daily-progress widget could break into awkward vertical wrapping (for example `thẻ hôm nay` split across multiple lines), and the top `Chỉnh sửa mục tiêu` CTA felt cramped on narrower layouts.

**Root Cause**: The absolute-positioned `.percentage-label` inside the progress ring had no stable width constraints, so the label container could shrink too aggressively. The header row also used fixed `col-6` columns at all breakpoints, which reduced available space for title/CTA on smaller screens.

**Solution**: Added dedicated progress label classes and fixed sizing (`.goal-progress-value`, `.goal-progress-meta`, `.percentage-label`, `.circular-progress`) to keep the center text readable and stable. Updated the header row to responsive columns (`col-12 col-lg-6`) and improved mobile CTA behavior. Also adjusted no-goal caption copy to show `Mục tiêu ngày: Chưa đặt`.

**Files Changed**:
- `TCTEnglish/Views/Goals/Index.cshtml` - improved responsive header layout, progress-label markup, and no-goal daily target caption.
- `TCTEnglish/wwwroot/css/goals.css` - added sizing/typography rules for progress text and responsive tweaks for title + CTA.

**Verification**: Ran `run_build` (passed). Ran goals regression tests via `run_tests` with TypeName filters `TCTEnglish.Tests.GoalsPhase1IntegrationTests` and `TCTEnglish.Tests.GoalsPhase2IntegrationTests` (6/6 passed).

**Commit**: Not created yet.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Goals merge-safety, business-date badge alignment, and test-host migration isolation - 2026-03-30

**Symptom**: The branch was not merge-safe because stray AI chat migration artifacts sat in the workspace outside the Goals scope, goals-related tests failed during startup with `PendingModelChangesWarning`, and the recent badge highlight could drift across the local midnight boundary for SQL Server `datetime2` badge timestamps.

**Root Cause**: Untracked migration artifacts (`20260329111532_AddAiChatPhase1Data*`, `20260329164730___TempPendingInspection*`) introduced unrelated schema noise. The SQLite integration host still executed `Program` startup migration behavior, which is not valid for the test database setup. In addition, badge highlighting depended on timestamps that SQL Server materializes as `DateTimeKind.Unspecified`, so the business date had to be interpreted explicitly as stored UTC.

**Solution**: Removed the stray migration artifacts from the branch, replaced the EF migrator inside the SQLite test host with a test-only no-op implementation so startup no longer attempts the SQL Server migration chain, kept streak/badge sequencing inside `GoalsService`, and added regression coverage for SQL Server-style `datetime2` badge timestamps near the business-date boundary.

**Files Changed**:
- `TCTEnglish/Services/GoalsService.cs` - treated daily activity rows as stored business dates directly and kept badge recent-unlock evaluation on the UTC-aware helper path.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` - replaced the runtime migrator in the SQLite host with a no-op test implementation.
- `TCTEnglish.Tests/Infrastructure/NoOpMigrator.cs` - added a test-only EF Core migrator that makes startup `Database.Migrate()` a no-op.
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` - added a regression test for SQL Server `datetime2` badge timestamps near local midnight.
- `.ai/context/known-issues.md` - moved the goals business-date issue out of unresolved technical debt.
- `TCTEnglish/Migrations/20260329111532_AddAiChatPhase1Data.cs` - removed empty stray migration artifact.
- `TCTEnglish/Migrations/20260329111532_AddAiChatPhase1Data.Designer.cs` - removed empty stray migration artifact.
- `TCTEnglish/Migrations/20260329164730___TempPendingInspection.cs` - removed stray AI chat migration artifact.
- `TCTEnglish/Migrations/20260329164730___TempPendingInspection.Designer.cs` - removed stray AI chat migration snapshot artifact.

**Verification**: Ran `dotnet ef migrations has-pending-model-changes --project TCTEnglish/TCTEnglish.csproj --startup-project TCTEnglish/TCTEnglish.csproj --context DbflashcardContext --no-build`, then ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase4IntegrationTests` and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase5IntegrationTests`.

**Commit**: Not created yet.

**Notes**: This replaces the temporary controller-level badge refresh workaround with service-owned orchestration already present in the workspace, and it keeps `Program.cs` untouched by isolating the migration behavior inside the test host only.

### Streak badge unlock bị trễ một lượt học vì thứ tự cập nhật streak và badge — 2026-03-30

**Symptom**: Khi người dùng đạt mốc streak (ví dụ 3 ngày liên tiếp) trong request học hiện tại, badge streak có thể chưa mở khóa ngay, chỉ xuất hiện ở lượt học sau.

**Root Cause**: `LearningApiController.Record` gọi `IGoalsService.RecordActivityAsync(...)` (có refresh badge) trước khi gọi `IStreakService.UpdateStreakAsync(...)`, nên logic badge đọc `User.Streak` cũ trong cùng request.

**Solution**: Thêm API refresh badge tường minh trong goals service và gọi lại ngay sau khi cập nhật streak để đồng bộ trạng thái badge theo streak mới trong cùng request.

**Files Changed**:
- `TCTEnglish/Services/IGoalsService.cs` — thêm `RefreshBadgesAsync(int userId)`.
- `TCTEnglish/Services/GoalsService.cs` — triển khai `RefreshBadgesAsync(...)` dùng lại `RefreshUserBadgesAsync(...)`.
- `TCTEnglish/Controllers/LearningApiController.cs` — gọi `await _goalsService.RefreshBadgesAsync(currentUserId);` sau `UpdateStreakAsync(...)`.
- `TCTEnglish.Tests/GoalsPhase5IntegrationTests.cs` — thêm regression test `LearningRecord_ReachesThreeDayStreak_UnlocksStreakBadgeImmediately`.

**Verification**: `run_build` thành công. Chạy `GoalsPhase5IntegrationTests` bị chặn bởi lỗi nền workspace `PendingModelChangesWarning` (liên quan migration local chưa đồng bộ), không do thay đổi fix này.

**Commit**: Not created yet.

**Notes**: Fix này giữ nguyên boundary hiện tại (`LearningApiController` + `IGoalsService`), không thêm logic sang `HomeController`.

### Goals core rollout and challenge-token hardening restored after cleanup — 2026-03-28

**Symptom**: During commit cleanup, related updates around goals rollout documentation and daily-challenge token validation were dropped from working changes even though they were still required.

**Root Cause**: The workspace was intentionally trimmed to a narrow `goals core` commit scope, which discarded additional local updates that were planned for a follow-up commit.

**Solution**: Restored the dropped updates by reapplying token-based daily challenge validation flow (`HomeController` + dashboard views), startup migration block in `Program.cs` per team decision, XP reward toast in vocabulary study view, and documentation/context updates in known-issues and bug-fix log.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` - restored signed challenge token generation/validation and secure `CheckAnswer` flow.
- `TCTEnglish/ViewModels/DailyChallengeViewModel.cs` - restored `ChallengeToken` property.
- `TCTEnglish/Views/Home/Index.cshtml` and `TCTEnglish/Views/Home/_DailyChallenge.cshtml` - restored token-aware answer submission.
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - restored XP reward toast display.
- `TCTEnglish/Program.cs` - restored startup `Database.Migrate()` block.
- `.ai/context/known-issues.md` and `.ai/context/bug-fix-log.md` - restored context records.

**Verification**: Build/check verification should be run after reapplying all restored files.

**Commit**: Not created yet.

**Notes**: This entry documents restoration of intentionally removed local changes that were later confirmed as still required.

### Writing UI still showed mojibake Vietnamese text that looked like font corruption — 2026-03-28

**Symptom**: Learners still saw broken Vietnamese fragments (for example `TrÃ¢n trá»ng`, `ChÃºc may máº¯n`) in writing-related UI paths, which appeared as a font/display issue on the interface.

**Root Cause**: Legacy mojibake values could still flow from persisted writing sentence content into practice/hint rendering. Closing-line detection also contained explicit mojibake matching, confirming mixed-encoding data was still being tolerated but not normalized for display.

**Solution**: Added display normalization for known mojibake Vietnamese phrases in `StudyService.Writing` and applied it on writing sentence payload/hint lookup paths before returning view/API data. Also simplified closing-line detection to run on normalized text only.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - added `NormalizeVietnameseDisplayText(...)`, normalized sentence text in practice/hint pipelines, and updated closing-line detection to use normalized text.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression assertions to ensure writing page and writing practice JSON payload do not contain known mojibake fragments.

**Verification**: Ran `dotnet build` (passed). Ran `Sprint1SmokeTests` via filter `TypeName=TCTEnglish.Tests.Sprint1SmokeTests` (27/27 passed).

**Commit**: Not created - workspace already contains unrelated local changes.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this specific writing mojibake issue is not currently listed there.

### Writing smoke tests drifted from localized UI copy and AI evaluator rejected code-fenced JSON — 2026-03-28

**Symptom**: Writing smoke tests failed after recent writing updates because assertions still expected older English UI text (`0/14 completed`, `Closing hint`) while the live writing UI/hint copy had moved to Vietnamese. In addition, AI writing evaluation had a latent parsing risk: if the model returned JSON wrapped in markdown code fences, deserialization failed and silently forced rule-based fallback.

**Root Cause**: Test fixtures were not updated alongside localization changes in writing views/service responses. The AI evaluation parser expected raw JSON only and did not normalize fenced markdown payloads.

**Solution**: Updated writing smoke-test assertions to match the current localized writing output and added AI response normalization in `StudyService.Writing` to strip markdown code fences before JSON deserialization.

**Files Changed**:
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - aligned writing assertions with current Vietnamese writing copy.
- `TCTEnglish/Services/StudyService.Writing.cs` - added `NormalizeAiJsonPayload(...)` and applied it before deserializing AI evaluation output.

**Verification**: Ran `dotnet build` (passed). Ran `Sprint1SmokeTests` via test explorer filter `TypeName=TCTEnglish.Tests.Sprint1SmokeTests` (27/27 passed). Ran `git diff --check -- TCTEnglish/Services/StudyService.Writing.cs TCTEnglish.Tests/Sprint1SmokeTests.cs .ai/context/bug-fix-log.md` (passed; line-ending warning only).

**Commit**: Not created - workspace already contains unrelated local changes.

**Notes**: This follows the same writing hardening trend as previous entries by reducing fallback-only behavior when AI responses are structurally valid but wrapped in markdown. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice CTA sat below the fold and failure messaging overemphasized AI fallback - 2026-03-28

**Symptom**: On the `Writing Practice` page at `1280x720`, users landed on the exercise without seeing the textarea CTA immediately because the compose area sat too low. On small mobile widths, the shared header felt cramped because the search box copy was too long for the available space. When a writing sentence failed, the warning banner led with `AI feedback is unavailable...`, which made the screen feel broken instead of clearly telling the learner the sentence still needed revision.

**Root Cause**: The practice page still spent too much vertical space on pre-compose chrome and the workspace column needed a denser desktop treatment. The shared header only tightened spacing a little on mobile and did not shorten placeholder copy responsively. The writing client rendered rule-based evaluation copy directly from the server payload, so fallback-mode responses surfaced the AI-unavailable note inside the main failure banner instead of separating the fallback note from the learner-facing retry state.

**Solution**: Tightened the writing practice layout, widened the workspace column, reduced compose spacing for shorter desktop viewports, and kept the compose CTA visually prioritized. Improved the shared mobile header by adding a compact search placeholder plus tighter responsive spacing. Added a dedicated fallback note below the feedback banner and normalized rule-based evaluation copy in the client so the main banner now says the sentence needs another try while the AI-fallback explanation appears separately.

**Files Changed**:
- `TCTEnglish/Views/Shared/_Layout.cshtml` - added a stable search input id plus default/compact placeholder metadata for responsive header behavior.
- `TCTEnglish/wwwroot/js/layout.js` - swapped the search placeholder to `Search` on narrow screens.
- `TCTEnglish/wwwroot/css/layout.css` - tightened mobile header spacing, resized action buttons, and gave the search box more usable room.
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - added a dedicated feedback source note under the banner.
- `TCTEnglish/wwwroot/js/writing.js` - split rule-based fallback copy from retry/acceptance copy and rendered a separate fallback note when AI is unavailable.
- `TCTEnglish/wwwroot/css/writing.css` - widened the workspace column, tightened compose spacing, and styled the new fallback note.
- `TCTEnglish/Services/StudyService.Writing.cs` - aligned the non-AI retry title with the learner-facing `Try this sentence again` wording.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Shared/_Layout.cshtml TCTEnglish/wwwroot/js/layout.js TCTEnglish/wwwroot/css/layout.css TCTEnglish/Views/Study/WritingPractice.cshtml TCTEnglish/wwwroot/js/writing.js TCTEnglish/wwwroot/css/writing.css TCTEnglish/Services/StudyService.Writing.cs` (passed, with line-ending warnings only). Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors). Re-verified in a real browser with Playwright at `1280x720` and `390x844`, including a failed submit case that now shows `Try this sentence again` plus a separate fallback note.

**Commit**: Not created - workspace already contained unrelated local changes.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this specific UI/UX regression was not listed there.

### Writing Exercises header still looked broken after partial reordering - 2026-03-28

**Symptom**: Even after earlier header tweaks, the `Writing Exercises` page still looked visually broken because the back button and breadcrumb did not form a balanced top row and the title/filters row still felt misaligned.

**Root Cause**: The previous iteration grouped the back button with the title column, but the breadcrumb stayed in the controls column above the filters, which kept the whole header visually uneven.

**Solution**: Reworked the header into two explicit rows: a top bar with `Back` on the left and the breadcrumb on the right, followed by a second row with `Available Exercises` on the left and the filters on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - split the page header into a top bar and a separate title/filter row.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - added top-bar styling and rebalanced the title/filter row alignment.
- `.ai/context/bug-fix-log.md` - added this final header-layout correction record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): split header into topbar and controls`

**Notes**: This keeps the breadcrumb close to the filters while also preventing the back button from creating a visually empty standalone block. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises header split the back button onto a broken standalone row - 2026-03-28

**Symptom**: The `Writing Exercises` page header looked visually broken because the back button sat on its own row above the title, while the breadcrumb and filters were grouped separately on the right.

**Root Cause**: `WritingExercises.cshtml` rendered the back button in a standalone toolbar block before the main header layout instead of grouping it with the page title column.

**Solution**: Moved the back button into the left title column so the header now forms two balanced columns: `back button + title` on the left and `breadcrumb + filters` on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - merged the back button into the title column.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - removed the standalone toolbar styling and added a title-block layout for the left header column.
- `.ai/context/bug-fix-log.md` - added this layout correction record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): rebalance header layout`

**Notes**: This keeps the breadcrumb above the filters as requested, while removing the extra empty-looking top row. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises breadcrumb sat too far away from the filters - 2026-03-28

**Symptom**: On the `Writing Exercises` page, the `Writing > Beginner > Emails` breadcrumb appeared detached from the filter controls instead of sitting above them where users expected the context label.

**Root Cause**: `WritingExercises.cshtml` rendered the breadcrumb as a separate block above the page toolbar instead of grouping it with the right-side filter controls.

**Solution**: Moved the breadcrumb into the head controls column and placed it directly above the filters, then adjusted the layout CSS so the heading stays on the left while the breadcrumb and filters stack together on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - moved the breadcrumb into the filter controls block above the form.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - updated head alignment and added styling for the new controls column layout.
- `.ai/context/bug-fix-log.md` - added this layout fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): place breadcrumb above filters`

**Notes**: On narrower screens the controls column now expands full-width and left-aligns so the breadcrumb still stays close to the filters. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises page had no clear way back to level selection - 2026-03-28

**Symptom**: On the `Writing Exercises` page, users could see the breadcrumb but did not have an obvious dedicated button to return to the Writing screen and reselect level or content type.

**Root Cause**: `WritingExercises.cshtml` only rendered the breadcrumb plus filters and exercise cards, so the back-navigation affordance was too subtle for the page flow.

**Solution**: Added an explicit `Back to level and format` action above the exercise heading and styled it to match the current Writing blue theme.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - added a dedicated back button that returns to `Study/Writing` with the current level.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - added matching styles for the new back button.
- `.ai/context/bug-fix-log.md` - added this UX fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): add explicit back navigation`

**Notes**: The breadcrumb remains in place, but the main flow now has a much more visible return action. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing level selection reloaded the whole index page - 2026-03-28

**Symptom**: On the Writing index page, every level selection (`Beginner`, `Intermediate`, `Advanced`) triggered a full page reload before the user could continue to choose a content type.

**Root Cause**: The level cards in `Writing.cshtml` were plain navigation links back to the same `StudyController.Writing` action with a different `level` query string, so the level switch always round-tripped to the server.

**Solution**: Kept the links as a no-JavaScript fallback, but added client-side level switching for the Writing index page. The new script updates the selected card state, refreshes the selected-level helper copy, rewrites the content-type links to the chosen level, and syncs the URL with `history.replaceState(...)` without reloading the page.

**Files Changed**:
- `TCTEnglish/Views/Study/Writing.cshtml` - added data hooks for level cards and content links, plus the page-specific Writing index script reference.
- `TCTEnglish/wwwroot/js/writing-index.js` - added client-side level-selection behavior so changing levels no longer reloads the page.
- `.ai/context/bug-fix-log.md` - added this UX fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/Writing.cshtml TCTEnglish/wwwroot/js/writing-index.js` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-index): switch levels without page reload`

**Notes**: The server route still works as a fallback if JavaScript is unavailable, so direct links/bookmarks with `?level=...` remain valid. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing pages still missed the app's real brand blue - 2026-03-28

**Symptom**: Even after the lighter UI pass, the Writing screens still did not match the actual TCT site branding because the user wanted the blue logo/header color, not the temporary green palette. The `Writing` index screen also remained on an older dark theme.

**Root Cause**: The previous visual update optimized layout density and removed the dark feel, but it used a green accent family that did not match the app's canonical header/logo color (`#4255ff`). `writing-index.css` was also still on its own older dark/yellow treatment.

**Solution**: Re-themed all Writing learner pages (`Writing`, `Writing Exercises`, `Writing Practice`) to the app's brand-blue palette based on the shared header/logo color, replacing the remaining dark/yellow and green variants with blue surfaces, blue buttons, blue chips, blue progress styling, and blue status treatments.

**Files Changed**:
- `TCTEnglish/wwwroot/css/writing-index.css` - moved the Writing index page from the old dark/yellow theme to the shared blue brand palette.
- `TCTEnglish/wwwroot/css/writing.css` - switched the Writing Practice page from green accents to the app's blue brand palette.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - switched the Writing Exercises list and status badges to the same blue brand palette.
- `.ai/context/bug-fix-log.md` - added this branding follow-up record.

**Verification**: Ran `git diff --check -- TCTEnglish/wwwroot/css/writing-index.css TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): align writing screens with brand blue`

**Notes**: A hard refresh in the browser may be needed if cached CSS still shows the earlier palette. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice still used dark/yellow styling and cramped status chips - 2026-03-28

**Symptom**: After the first UI cleanup, the writing practice page still looked too dark compared with the main site theme, progress and accent surfaces still felt heavy, the inline highlighted text chip looked cramped, and the `New` status badge on the exercise list still appeared too tight and off-theme.

**Root Cause**: The first pass compacted the layout but kept a dark practice palette derived from the writing sub-theme. `writing.css` still centered its design around dark panels and yellow accents, and `writing-exercises.css` did not define a dedicated `new` badge style that matched the site's green direction.

**Solution**: Reworked the writing practice and exercise-list styling around a light green app-aligned palette, removed the dark page background treatment, converted yellow accents to green, reduced heading and panel typography further, improved inline sentence chip padding, and added a dedicated green `new` status badge style for exercise cards.

**Files Changed**:
- `TCTEnglish/wwwroot/css/writing.css` - switched the practice page from dark/yellow to light green surfaces, tightened text sizing, softened the progress area, and improved inline sentence chip padding.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - changed the exercise list to the same light green palette and added refined `new`, `in-progress`, and `completed` badge styling.
- `.ai/context/bug-fix-log.md` - added this follow-up incident record.

**Verification**: Ran `git diff --check -- TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): align practice and badges with green site theme`

**Notes**: Browser QA is still recommended for final visual tuning because this pass was validated through code/build checks only. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice layout was oversized, theme-drifted, and missed sentence state styling - 2026-03-28

**Symptom**: The writing practice page felt too large and vertically long, several top metadata chips and icons were hard to read against the page background, and completed or retry sentence states did not render consistently after submit.

**Root Cause**: `WritingPractice.cshtml` rendered the practice workspace without the shared writing-theme background used by the writing index screen, `writing.css` used larger spacing and a tall stacked feedback layout, and `writing.js` toggled `is-completed` and `is-retry` while the CSS only styled `is-submitted` and `is-editing`.

**Solution**: Re-themed the practice page to reuse the navy/gold writing palette, tightened panel spacing and vertical rhythm, made the source passage and feedback area more compact with internal scrolling and a denser feedback grid, added themed icons in the feedback cards, and aligned the JS/CSS sentence-state classes so completed, editing, and retry states render correctly.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - wrapped the page in a themed shell and updated the feedback cards to use compact, color-aligned icon labels.
- `TCTEnglish/wwwroot/css/writing.css` - rebuilt the practice-page styling around the shared writing palette, reduced oversized spacing, added compact scrollable panels, and styled the actual sentence-state classes used by the script.
- `TCTEnglish/wwwroot/js/writing.js` - aligned sentence state toggles with the CSS so completed, editing, and retry states show correctly in the passage.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingPractice.cshtml TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/js/writing.js` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): compact and retheme practice workspace`

**Notes**: Full browser QA is still recommended to fine-tune feel on real content lengths. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing closing-line hints failed on UTF-8 Vietnamese phrases - 2026-03-27

**Symptom**: Some writing-practice closing sentences did not return `Closing hint` even when the Vietnamese source line clearly contained a closing phrase like `Trân trọng` or `Chúc may mắn`.

**Root Cause**: `StudyService.Writing.IsClosingLine(...)` matched only mojibake string fragments (`TrÃ¢n trá»ng`, `ChÃºc may máº¯n`) and missed the proper UTF-8 Vietnamese forms.

**Solution**: Expanded closing-phrase detection to support both proper UTF-8 Vietnamese strings and legacy mojibake variants, then added smoke-test coverage that discovers a seeded closing sentence and verifies the hint endpoint returns `Closing hint`.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - updated closing-phrase detection logic in `IsClosingLine(...)`.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression assertion for closing-line hint title on seeded writing data.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `TCTEnglish.Tests.Sprint1SmokeTests.WritingJsonEndpoints_ReturnServiceBackedPayloads` (passed) and workspace build (successful).

**Commit**: `fix(writing): restore closing-hint detection for utf8 vietnamese`

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be executed.

### Writing practice leaked reference answers and skipped server-side evaluation - 2026-03-28

**Symptom**: The writing practice page embedded `EnglishMeaning` directly into the page payload, the browser compared answers locally, and submit moved to the next sentence without any dedicated backend evaluation or graded feedback.

**Root Cause**: The first writing slice had DB-backed reference answers and read-only writing endpoints, but no POST evaluation endpoint. `WritingPractice.cshtml` serialized `EnglishMeaning` into the client bootstrap payload, and `writing.js` treated submit as a local state transition instead of a server review.

**Solution**: Moved sentence evaluation into `StudyController` + `IStudyService`/`StudyService.Writing`, added `POST /Home/Writing/Practice/Evaluate` with anti-forgery, removed `EnglishMeaning` from practice page/client payloads, converted hint responses to non-answer hint text, and wired the frontend to submit each sentence to the server. The new service path performs a fast rule-based check first, then optionally calls OpenAI for meaning/grammar/naturalness/word-choice feedback when an API key is configured, with safe rule-based fallback when AI is unavailable.

**Files Changed**:
- `TCTEnglish/Controllers/StudyController.cs` - added the server-side writing evaluation endpoint while keeping the work inside the `StudyController` boundary.
- `TCTEnglish/Services/IStudyService.cs` - added the writing evaluation contract.
- `TCTEnglish/Services/StudyService.cs` - extended constructor dependencies for configuration, HTTP, and logging needed by AI-backed evaluation.
- `TCTEnglish/Services/StudyService.Writing.cs` - removed reference answers from practice DTOs, replaced hint output with safe hint text, and implemented rule-based + optional AI evaluation flow.
- `TCTEnglish/ViewModels/WritingIndexViewModel.cs` - added evaluation request/response models and removed `EnglishMeaning` from practice sentence view models sent to the client.
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - removed `englishMeaning` from the bootstrapped JSON, added anti-forgery markup, and updated the feedback copy for server-driven evaluation.
- `TCTEnglish/wwwroot/js/writing.js` - changed submit flow to call the new backend endpoint and only complete/auto-advance a sentence when the server returns pass.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression coverage for hidden answers, hint shape, antiforgery on evaluate, and evaluate JSON responses.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~Sprint1SmokeTests -p:OutputPath=D:\TCTEnglish\.tmp-tests\bin\Debug\net10.0\`. The focused smoke suite passed `27/27`. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(writing): evaluate practice sentences on the server`

**Notes**: When no OpenAI API key is configured, the feature falls back to a rule-based evaluation so the learner loop still works without leaking the teacher reference answer. The AI prompt still uses `EnglishMeaning` server-side only.

### Writing exercises crash with `Invalid object name 'WritingExercises'` - 2026-03-27

**Symptom**: Opening `/Home/Writing/Exercises` threw SQL Server exception `Invalid object name 'WritingExercises'` from `StudyService.LoadWritingExerciseCardsAsync`.

**Root Cause**: The writing feature shipped with new EF tables/migration, but runtime environments could execute writing queries before pending SQL Server migrations were applied.

**Solution**: Added a one-time SQL Server schema-initialization guard in `StudyService.Writing` that runs `Database.MigrateAsync()` before writing-table queries (`WritingExercises`, `WritingExerciseSentences`) execute.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - added `EnsureWritingSchemaReadyAsync()` (with `SemaphoreSlim` gate) and invoked it before all writing DB query entry points.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Built the solution and ran focused study smoke tests after the change; writing-service code compiles and no new compilation errors were introduced.

**Commit**: `fix(writing): apply pending migrations before writing queries`

**Notes**: This fix targets SQL Server only (`Database.IsSqlServer()`), so SQLite test setup (`EnsureCreated`) remains unaffected.

### Class admin mutation consistency, legacy auth route fallback, and coverage hardening - 2026-03-20

**Symptom**: After the controller/service split, Admin users could view private class detail but still could not kick members or remove shared folders through the legacy `/Home/*` class actions. `ClassChatHub` also still duplicated class-access checks locally, and the leftover `HomeController.Login/Register` actions pointed at missing Home views instead of the real account endpoints.

**Root Cause**: Authorization rules had drifted between the class detail ViewModel/UI, `ClassService` mutation methods, and the SignalR hub. At the same time, the refactor left behind legacy `HomeController` auth actions that no longer matched the moved view structure. Several of these seams were also missing direct regression tests.

**Solution**: Extended class mutation authorization so admin users can remove shared folders and kick class members consistently with the existing `CanManageClass` boundary, centralized SignalR class-access checks through `IClassService.CanAccessClassAsync(...)`, redirected legacy `/Home/Login` and `/Home/Register` to `AccountController`, and added focused regression coverage for admin class mutations, secondary study modes, save/unsave folder flows, and legacy auth-route redirects.

**Files Changed**:
- `TCTEnglish/Services/IClassService.cs` - expanded class mutation signatures to carry the admin authorization context explicitly.
- `TCTEnglish/Services/ClassService.cs` - aligned `CanRemove`, `RemoveFolderFromClassAsync`, and `KickMemberAsync` with admin manage-class permissions.
- `TCTEnglish/Controllers/ClassController.cs` - passed `IsAdminUser()` into the class mutation service calls.
- `TCTEnglish/Hubs/ClassChatHub.cs` - replaced the duplicated hub-local class access check with `IClassService.CanAccessClassAsync(...)`.
- `TCTEnglish/Views/Class/ClassDetail.cshtml` - exposed the Kick action whenever `CanManageClass` is true instead of owner-only.
- `TCTEnglish/Controllers/HomeController.cs` - redirected legacy Home auth endpoints to `AccountController` so old URLs remain safe.
- `TCTEnglish.Tests/Sprint2SmokeTests.cs` - added admin regression coverage for kicking members and removing shared folders.
- `TCTEnglish.Tests/CriticalFlowSqliteIntegrationTests.cs` - added coverage for secondary study-mode auth, legacy Home auth redirects, and save/unsave folder round-trips.
- `TCTEnglish.Tests/Sprint4SmokeTests.cs` - added a structural assertion that `ClassChatHub` requires constructor-injected `IClassService`.

**Verification**: Ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore` with workspace-local `DOTNET_CLI_HOME`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`, and `DOTNET_NOLOGO`; the suite passed `67/67`. The only remaining output was the existing `NU1900` warning because the environment could not reach the NuGet vulnerability feed.

**Commit**: `fix(class-auth): align admin mutations and legacy route fallbacks`

**Notes**: This continues the same authorization-alignment pattern as the 2026-03-19 SignalR/class-detail fix, but extends it to mutation flows and adds broader regression guards so HTTP and SignalR access rules stay in sync.

### Post-refactor documentation drift and EOF hygiene - 2026-03-20

**Symptom**: `git diff --check` still failed because `TCTEnglish/Views/Vocabulary/Study.cshtml` had a blank line at EOF, and the main architecture/context docs still described the pre-refactor structure (for example "no test project", `ChatController` living in `HomeController`, and legacy `ViewModel/` paths).

**Root Cause**: Documentation and hygiene drift accumulated after the controller/service/view-model refactor landed in multiple steps. The codebase moved faster than the supporting markdown/context files.

**Solution**: Removed the EOF blank line in `Study.cshtml`, rewrote the architecture backlog to match the current controller/service/test structure, created a fresh implementation plan for the remaining work, and updated `known-issues.md` plus this log so future agents see the post-refactor paths and priorities first.

**Files Changed**:
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - removed the extra trailing blank line so `git diff --check` no longer reports the EOF error.
- `.ai/context/known-issues.md` - replaced outdated unresolved items with the remaining confirmed issues and debt after the refactor.
- `.ai/context/bug-fix-log.md` - added a refactor-sync entry so older historical notes are read with the current controller/view/view-model layout in mind.
- `docs/post-refactor-followup-plan.md` - added the current post-refactor implementation plan.
- `docs/architecture-prioritized-backlog.md` - rewrote the English backlog around the current codebase state.
- `docs/architecture-prioritized-backlog.vi.md` - rewrote the Vietnamese backlog around the current codebase state.

**Verification**: Re-ran `git diff --check` after the hygiene fix and then validated the codebase snapshot against the live controllers, services, view models, views, DI registrations, and test project under `TCTEnglish.Tests`. Full build/test verification was run after the documentation sync to make sure the repo state still matched the updated docs.

**Commit**: `chore(docs): sync post-refactor backlog and repo hygiene`

**Notes**: Older entries from 2026-03-19 intentionally describe the pre-normalization layout from that moment in history. In the current codebase, the equivalent feature files now live under `Controllers/ClassController.cs`, `Controllers/FolderController.cs`, `Views/Class/`, `Views/Folder/`, and `ViewModels/`.

### Admin speaking video playlist Vietnamese messages mojibake - 2026-03-20

**Symptom**: The admin speaking video edit flow showed broken Vietnamese text in the playlist validation error and the success toast after saving, with visibly garbled multi-byte fragments instead of readable UI copy.

**Root Cause**: Two user-facing strings in `SpeakingVideoManagementController.cs` had been saved with mixed Windows-1252/UTF-8-decoded text. The broader investigation also confirmed that several docs/views only looked broken in the terminal, not on disk, because their UTF-8 bytes were still valid.

**Solution**: Replaced the two corrupted literals with clean Vietnamese UTF-8 strings and re-scanned the repo with a byte-level UTF-8 + repairability check so only genuinely broken text was edited.

**Files Changed**:
- `TCTEnglish/Areas/Admin/Controllers/SpeakingVideoManagementController.cs` - restored the playlist validation message and update success toast to proper Vietnamese UTF-8.

**Verification**: Ran a repo-wide Python scan over tracked/untracked text files and confirmed `INVALID_UTF8_COUNT=0` plus `REPAIRABLE_HIT_COUNT=0` after the fix. Also ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore` with workspace-local `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`; build succeeded with 9 pre-existing nullable warnings only. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(admin-speaking): normalize Vietnamese encoding in playlist messages`

**Notes**: `AGENTS.md`, `.agent/workflows/bug-investigation.md`, `.ai/context/bug-fix-log.md`, `.ai/context/known-issues.md`, `README.md`, `TCTEnglish/Views/Vocabulary/Study.cshtml`, `TCTEnglish/Views/Study/WriteMode.cshtml`, `TCTEnglish/Models/JsonVocabularySeeder.cs`, and `TCTEnglish/Views/Speaking/Practice.cshtml` were reviewed because terminal output looked suspicious, but they were already valid UTF-8 and were not auto-edited.

### Vocabulary study view Vietnamese text mojibake in comments and UI - 2026-03-20

**Symptom**: `Views/Vocabulary/Study.cshtml` showed broken Vietnamese text in comments, settings labels, study feedback messages, shuffle toasts, and the completion overlay. The mojibake also leaked into visible UI copy, making parts of the vocabulary study page hard to read.

**Root Cause**: The study view contained mixed-encoding text after recent edits. Some literals had been decoded through a Windows-1252/Latin-1 path instead of staying in UTF-8, so a single `.cshtml` file ended up with both valid Vietnamese strings and mojibake sequences.

**Solution**: Normalized the corrupted literals in `Study.cshtml` back to UTF-8 while preserving the current ViewModel-based refactor and anti-forgery updates already present in the working tree. Re-checked the entire `Views/Vocabulary/` folder with a targeted mojibake detection pass to confirm no repairable lines remained.

**Files Changed**:
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - restored Vietnamese comments and UI strings in the settings modal, study actions, result messages, shuffle messages, and completion overlay without reverting unrelated in-progress refactor changes.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore` with `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` pointed at the workspace-local `.dotnet-home`; build succeeded with pre-existing warnings only. Also ran a workspace-local Python scan over `TCTEnglish/Views/Vocabulary/*.cshtml` and confirmed `repairable_lines=0`. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(vocabulary-study): normalize Vietnamese text encoding in study view`

**Notes**: This was an encoding-normalization bug in the view layer, not a controller or EF issue. `known-issues.md` was not updated because this exact bug was not listed there.

### Dashboard random sampling broke SQLite tests after controller split - 2026-03-20

**Symptom**: `/Home/Index` returned HTTP 500 in the SQLite integration test environment, and the full suite failed on the authenticated dashboard smoke tests. The dashboard challenge endpoint also pointed to a partial that did not exist after the view move.

**Root Cause**: `HomeController` used `OrderBy(_ => Guid.NewGuid())` inside EF queries for dashboard folder suggestions and daily challenge distractors. That translation works on SQL Server but not on the SQLite provider used by the test host. At the same time, `Views/Home/Index.cshtml` still contained stale links from the pre-split controller layout and `GetDailyChallenge()` referenced `_DailyChallenge` without a matching partial file in the moved view structure.

**Solution**: Kept the SQL Server random-order branch, then added a provider-safe fallback that samples ordered ids in memory for non-SQL Server providers. Restored the missing `_DailyChallenge` partial, switched the dashboard view to render through that partial, and updated the remaining dashboard links so the moved class/folder/study pages still resolve through their new controllers.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` - added provider-aware random sampling helpers for dashboard folders and daily challenge wrong answers.
- `TCTEnglish/Views/Home/Index.cshtml` - updated stale controller links and rendered the daily challenge through the restored partial.
- `TCTEnglish/Views/Home/_DailyChallenge.cshtml` - added the partial returned by `GetDailyChallenge()`.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added coverage for the daily challenge partial endpoint.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~Sprint1SmokeTests`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~CriticalFlowSqliteIntegrationTests`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore` with `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` pointed at the workspace-local `.dotnet-home`; all commands passed, including the full 59-test suite.

**Commit**: `fix(home-dashboard): use provider-safe random sampling and restore challenge partial`

**Notes**: This is a provider-compatibility bug rather than a SQL Server production defect. `known-issues.md` was not updated because this specific issue was not listed there.

### Split folder/set controllers loaded owner resources before scope check - 2026-03-20

**Symptom**: Remaining Sprint 1 folder/set mutations in the split controllers could load a `Folder` or `Set` by id first and only then compare `UserId` or `OwnerId`, leaving an anti-IDOR gap in the data access pattern.

**Root Cause**: `FolderController.DeleteFolder`, `FolderController.UpdateFolderName`, `SetController.DeleteSet`, `SetController.RemoveSetFromFolder`, `SetController.EditSet` (GET/POST), and the adjacent `CreateSet` folder lookup used post-load ownership checks instead of embedding the owner/admin scope in the EF query itself.

**Solution**: Added controller-local scoped query helpers for manageable folders and sets, then switched the risky actions to fetch through those helpers so unauthorized users get `NotFound()` without loading another user's entity into the action flow. Added focused integration coverage for outsider denial paths and owner success paths.

**Files Changed**:
- `TCTEnglish/Controllers/FolderController.cs` - moved `DeleteFolder` and `UpdateFolderName` to owner/admin-scoped queries.
- `TCTEnglish/Controllers/SetController.cs` - moved `CreateSet` folder lookup, `DeleteSet`, `RemoveSetFromFolder`, and `EditSet` GET/POST to owner/admin-scoped queries.
- `TCTEnglish.Tests/FolderSetIdorRegressionTests.cs` - added focused anti-IDOR regression coverage for the touched folder/set actions.

**Verification**: Ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --filter FullyQualifiedName~FolderSetIdorRegressionTests --no-restore` with `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` set to the workspace-local `.dotnet-home`; 5/5 tests passed. Re-checked the touched controller actions and removed the post-load `folder.UserId != currentUserId` / `set.OwnerId != currentUserId` authorization pattern from this scope.

**Commit**: `fix(folder-set-security): embed owner scope in split controller queries`

**Notes**: This follows the same "scope the EF query before loading the private resource" pattern used in the recent class detail hardening. `known-issues.md` was not updated because this exact bug was not listed there.

### Admin không join được SignalR class group dù xem được ClassDetail — 2026-03-19

**Symptom**: Tài khoản Admin mở được trang chi tiết lớp nhưng không thể `JoinClass` vào SignalR group, dẫn tới chat realtime không nhận sự kiện trong lớp.

**Root Cause**: `ClassChatHub.JoinClass` chỉ cho phép `owner/member`, không áp dụng quyền `admin`; trong khi `HomeController.Classes.ClassDetail` cho phép xem private content với rule `owner/member/admin`.

**Solution**: Đồng bộ rule tại Hub bằng cách thay kiểm tra riêng trong `JoinClass` sang dùng `CanAccessClassAsync`, vốn đã bao gồm nhánh `Context.User.IsInRole(Roles.Admin)`.

**Files Changed**:
- `TCTEnglish/Hubs/ClassChatHub.cs` — line ~57: đổi check quyền trong `JoinClass` sang `CanAccessClassAsync` để khớp rule truy cập trang.

**Verification**: Build workspace thành công. Đối chiếu logic quyền truy cập giữa `ClassDetail` và `JoinClass` đã thống nhất cùng rule `owner/member/admin`.

**Commit**: `fix(signalr): align JoinClass authorization with class detail access`

**Notes**: Fix này cùng hướng với pattern “đồng bộ authorization giữa HTTP endpoint và SignalR hub” để tránh regression quyền truy cập theo role.

---

### ClassDetail read-scope leak and claim parsing cleanup — 2026-03-19

**Symptom**: Outsiders could open `/Home/ClassDetail/{id}` and the controller loaded `ClassMembers`, `ClassMessages`, and `ClassFolders` before permission was established; several Razor views also parsed `ClaimTypes.NameIdentifier` directly.

**Root Cause**: `HomeController.Classes.ClassDetail` used eager-loading for private navigations before checking owner/member/admin access, and `ClassDetail.cshtml`, `Class.cshtml`, and `FolderDetail.cshtml` depended on direct claim parsing instead of controller-provided view state.

**Solution**: Refactored `ClassDetail` to a two-step flow that loads only class metadata first, derives `isOwner/isMember/isAdmin`, and only then queries private members/messages/folders. Replaced direct claim parsing in touched views with dedicated ViewModel properties, switched the class list page to a typed page ViewModel, and added denial-path smoke tests for anonymous access, outsider privacy, and missing anti-forgery.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.Classes.cs` — refactored `ClassDetail`, projected class list rows, and extended `SearchClass` payload.
- `TCTEnglish/Controllers/HomeController.Folders.cs` — projected folder detail data into a view model with explicit permission flags.
- `TCTEnglish/ViewModel/ClassDetailViewModel.cs` — replaced entity-backed view data with explicit class/member/folder option models and permission flags.
- `TCTEnglish/ViewModel/ClassPageViewModel.cs` — added class list page models to remove claim parsing from Razor.
- `TCTEnglish/ViewModel/FolderDetailViewModel.cs` — added folder summary/set item models and `IsOwner/CanManage`.
- `TCTEnglish/Views/Home/ClassDetail.cshtml` — removed direct claim parsing, gated private modals/scripts, and rendered only sanitized outsider join state.
- `TCTEnglish/Views/Home/Class.cshtml` — switched to page view model and removed inline claim parsing.
- `TCTEnglish/Views/Home/FolderDetail.cshtml` — switched owner/manage checks to controller-provided view state.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` — added anonymous denial-path, outsider privacy, and anti-forgery regression coverage.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` — seeded private class data for denial-path assertions.

**Verification**: Ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore` and confirmed 15/15 tests passed; grep on the touched scope found no remaining `int.Parse(User.FindFirst...)`, `FindFirstValue(ClaimTypes.NameIdentifier)`, or synchronous `SaveChanges(` usages. `scripts/encoding_guard.py` was not present, so no encoding guard run was possible.

**Commit**: `fix(class-security): gate ClassDetail private reads and remove direct claim parsing`

**Notes**: This follows the same “authorize before loading private graph” pattern that should be reused for any future class/chat read endpoints. `CurrentUserIdExtensions` still owns the single centralized `FindFirstValue(ClaimTypes.NameIdentifier)` usage by design.

---

### Speaking Video 500 Error — 2026-03-15 (sample entry)

**Symptom**: `/Speaking/Video/{id}` throws `NullReferenceException`, HTTP 500

**Root Cause**: `SpeakingSentence` was loaded without `.Include(v => v.SpeakingVideo)` — EF did not load the navigation property, resulting in a null reference

**Solution**: Added `.Include(s => s.SpeakingVideo)` to the query in `SpeakingController`

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` — line ~45: added `.Include(s => s.SpeakingVideo)`

**Verification**: Opened `/Speaking/Video/{id}` again, confirmed HTTP 200, and checked other speaking video queries for the same missing navigation include pattern

**Commit**: `fix(speaking): add Include for SpeakingVideo navigation property`

**Notes**: Check other queries in the same controller for the same missing Include pattern

---
