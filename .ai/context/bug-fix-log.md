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
