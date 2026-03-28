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
