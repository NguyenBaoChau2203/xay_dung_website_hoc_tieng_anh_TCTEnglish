# Listening module: redesign, completion and hardening (2026-11-13)

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Phạm vi:** làm lại toàn bộ 2 trang Luyện nghe của người học (`/Home/Listening` và `/Home/Listening/Practice/{id}`) theo Clay chung của nhóm, đồng bộ với Bộ thẻ và Luyện tập.
> - **Sửa lỗi thật tìm thấy khi đọc code** (mục 3), nặng nhất là:
>   - bài nghe **không bao giờ được tính hoàn thành** từ giao diện, nên Mục tiêu/streak không nhận tín hiệu Listening;
>   - chấm quiz và lưu tiến độ **không kiểm tra quyền xem bài** (lộ đáp án bài riêng tư của người khác);
>   - danh sách bài luôn hiện "Mới" vì không nạp tiến độ.
> - **Bổ sung cho hoàn thiện:** tab Từ vựng, khối "Tiếp tục nghe", lọc theo trạng thái, thanh 3 bước tiến độ, phím tắt, thông báo cho khách chưa đăng nhập.
> - **Không đổi:** route, database (không migration), `Program.cs`, file chung, code của Châu (AI, Goals, YouTube transcript) và trang Admin.
> - **Cách chạy:** agent chạy liên tục L0 → L11, không hỏi lại. Mỗi phase phải qua đủ bộ kiểm tra G1–G15 (mục 8) mới commit và sang phase sau.
> - **Chưa có ảnh mockup** cho đợt này; giao diện được đặc tả bằng chữ ở mục 6.
>
> Phần chỉ dẫn cho agent viết bằng tiếng Anh theo `AGENTS.md`.

- **Run mode:** continuous L0 → L11 with no questions to the owner. Every phase must pass the full phase gate (section 8.1) before its commit and before the next phase starts.
- Owner / page owner: Huỳnh Phú Trọng. `README.md` assigns "Module Listening (Luyện nghe)" to him; `docs/superpowers/plans/2026-11-08-chau-ui-design-system-scope.md` lists Listening under "Trong's screens".
- Save as `docs/superpowers/plans/2026-11-13-listening-redesign.md`. Prompt: `.ai/prompts/claude-code-listening-redesign.md`.
- **Baseline expected** (verify at L0): `origin/master` at `d8c5773` (PR #135) or later. It contains the personal-sets, practice and Clay work (`study-kit.css`, `personal-sets-common.js`, `practice-core.js`).
- **Rules to read first:**
  - `AGENTS.md`;
  - `DESIGN.md` (Visual direction, Demo priority, Claymorphism implementation, Accessibility);
  - `docs/design-system/team-ui-workflow.md`;
  - `.ai/prompts/adopt-claymorphism-for-current-ui-task.md`;
  - the gallery `docs/design-system/preview.html`.
- **Reference implementations in this repo** (same owner, same conventions):
  - `wwwroot/css/study-kit.css` (Clay kit `k-*`);
  - `Views/Practice/Session.cshtml` + `wwwroot/js/practice-ui.js` (session shell, options, feedback card, summary);
  - `wwwroot/js/practice-core.js` + `scripts/tests/test_practice_core.cjs` (pure UMD logic with node tests);
  - `wwwroot/js/personal-sets-common.js` (`window.TctSets`: `fetchJson`, `toast`, `debounce`, `speak`, `safeStorage`, `el`, `pending`);
  - `TCTEnglish.Tests/PracticeIntegrationTests.cs` (integration test style).
- **Prior plans** (hooks, gates and notes that still apply): `docs/superpowers/plans/2026-11-11-study-clay.md`, `2026-11-11-study-ui-refresh.md`.

---

## 0. Skills to apply (load each when its step starts)

| Skill (`.agents/skills/…`) | Used for | Phases |
| --- | --- | --- |
| `tct-superpowers` | scoped plan, systematic diagnosis, evidence before "done" | all |
| `tct-engineering` | source-evidence investigation, API contract checks, diff review | L0, L1, L11 |
| `tct-security-audit` | access control, antiforgery, XSS review of B2/B3/B6/B14 | L0, L1, L11 |
| `tct-api-endpoint` | JSON endpoints called by fetch (EvaluateQuiz, SaveProgress, My/*) | L1 |
| `tct-feature-scaffold` | view model / service additions in the existing MVC shape | L1 |
| `tct-ui-ux-pro-max` | layout, hierarchy, typography and interaction choices before building each page | L3, L4, L6 |
| `tct-ui-component` | Razor partials, Bootstrap forms/modals, feedback states | L4–L9 |
| `tct-ui-motion` | transcript highlight, feedback, tab and ring motion, reduced motion | L7–L9 |
| `tct-ui-review` | rendered-page audit (hierarchy, a11y, responsive, states) | every gate (G8–G12), L11 |
| `tct-commit-message` | one local commit per phase | all |

Not used: `tct-ef-migration` (no schema change), `tct-admin-panel` (Admin is out of scope), `tct-speaking-feature`, `tct-vocabulary-feature`, `tct-data-seeder`.

---

## 1. Decisions (fixed, no owner input needed)

| ID | Decision |
| --- | --- |
| L-R1 | **Routes and endpoint contracts stay.** `/Home/Listening`, `/Home/Listening/Practice/{id}`, `POST /Home/Listening/EvaluateQuiz`, `SaveProgress/{lessonId}`, `My/Create`, `My/Delete`, `My/GenerateQuiz`, `Translate/{lessonId}` keep their URLs, verbs, antiforgery and JSON shapes. Responses may gain fields; none is removed or renamed. The AI chat, the RAG corpus and `GoalsPhase5IntegrationTests` depend on them. |
| L-R2 | **No migration, no `Program.cs` change.** `IListeningService` is already registered. Interface changes are additive (new optional parameter or overload). |
| L-R3 | **Completion rule (fixes B1):** a lesson is complete when every section **that has content** is done: transcript (if it has transcript lines), quiz (if it has questions), vocabulary (if it has vocab items). A section without content counts as done. The goals credit fires exactly once, as today. |
| L-R4 | **Quiz results are persisted by the server** in `EvaluateQuiz` (score = best of old and new). `SaveProgress` still accepts `quizCompleted` / `quizScore`, because an existing test pins that contract. The UI stops sending them. |
| L-R5 | **Access rule, one place:** a private helper in `ListeningService` decides whether a user may *use* a lesson: `IsPublished && (OwnerUserId == null \|\| OwnerUserId == userId)`, and a private lesson is not usable by a Standard-role owner (current behaviour). Every read **and** write path uses it. A failure returns "not found" (404), per AGENTS.md. |
| L-R6 | **AI writes to shared lessons need Admin.** `GenerateQuizFromTranscriptAsync` on a public lesson (`OwnerUserId == null`) is allowed for Admin only. Premium users keep it for their own lessons. `TranslateTranscriptAsync` keeps its current rule (documented in code as intended). |
| L-R7 | **UI system:** shared Clay tokens + the owner kit. Add `.tl-page` (listening) to the kit scope, so `k-*` components work on the listening pages. One new stylesheet `wwwroot/css/listening.css` replaces `listening-index.css` and `listening-practice.css`. The page stops loading `speaking.css` (another owner's file). |
| L-R8 | **JS:** pure logic in a new UMD module `wwwroot/js/listening-core.js` with node tests. DOM code uses `createElement` / `textContent` only. No `innerHTML` with data, no `alert` / `confirm`, no inline `<script>` logic, no `console.log`. Config goes through `<script type="application/json">`. Reuse `window.TctSets`. |
| L-R9 | **Copy is Vietnamese**, except English learning content and CEFR codes. New strings are only those quoted in this plan. Remove claims that are not true (e.g. "Update hàng tuần"). |
| L-R10 | **Out of scope:** Admin `ListeningManagement` views, `IYoutubeTranscriptService`, `IAiProviderClient`, `GoalsService`, `time-tracker.js`, `_Layout.cshtml`, shared CSS and partials, seed data content, and the import flow's inline AI quiz generation (kept; see 3, B7). |

---

## 2. Scope

**In scope**
- **Learner pages:**
  - `Views/Study/Listening.cshtml` (index);
  - `Views/Study/ListeningPractice.cshtml` (practice).
- **Backend, owner's module:**
  - `Services/ListeningService.cs`, `Services/IListeningService.cs`;
  - `ViewModels/ListeningViewModels.cs`;
  - the Listening actions in `Controllers/StudyController.cs` (lines ~107–225 only).
- **Assets:**
  - new `wwwroot/css/listening.css`;
  - new `wwwroot/js/listening-core.js`;
  - rewritten `wwwroot/js/listening-index.js` and `wwwroot/js/listening-practice.js`;
  - `wwwroot/css/study-kit.css` (scope extension only).
- **New partials:** `Views/Study/_ListeningLessonCard.cshtml`, `Views/Study/_ListeningMyLessonCard.cshtml`.
- **Tests:** new `TCTEnglish.Tests/ListeningIntegrationTests.cs`, new `scripts/tests/test_listening_core.cjs`.
- **Docs:** tracker row "Nghe", `docs/project-structure.md` if it lists the files, `.ai/context/bug-fix-log.md`, `.ai/context/known-issues.md`.

**Out of scope:** everything in L-R10, plus other actions in `StudyController.cs` and other owners' pages.

---

## 3. Findings to fix (confirm each at L0 with file:line before changing code)

| ID | Finding (evidence) | Fix | Phase |
| --- | --- | --- | --- |
| B1 | **Lessons never complete from the UI.** `SaveProgressAsync` sets `CompletedAt` only when `TranscriptCompleted && QuizCompleted && VocabReviewed` (`ListeningService.cs` ~363). No view or script ever sends `vocabReviewed` (grep: only the service, DTO and a Goals test). There is no vocabulary tab, although lessons have `VocabItems` (seed data, Admin `ManageVocab`). So `BuildListeningCompletionActivityUpdate` never fires for real users. | L-R3 + vocabulary tab (6.9) | L1, L9 |
| B2 | **`EvaluateQuizAsync` has no access check.** Any signed-in user can POST another user's private lesson id (or an unpublished lesson) and receive `CorrectAnswer` + `Explanation` for every question. | L-R5 | L1 |
| B3 | **`SaveProgressAsync` checks only `IsPublished`.** A user can create progress rows, and reach completion credit, on someone else's private lesson. | L-R5 | L1 |
| B4 | **Index never loads user progress.** `GetIndexViewModelAsync` does not fill `TranscriptCompleted` / `QuizCompleted` / `QuizScore`, so every card shows "Mới". | Load progress in one query; card status | L1, L4 |
| B5 | **Topic list leaks private topics.** `allTopics` filters `IsPublished` only, so the private topic "My Lessons" appears in everyone's filter. | Add `OwnerUserId == null` | L1 |
| B6 | **XSS sinks.** `expEl.innerHTML` with `ans.explanation` (AI- or admin-written); `toastEl.innerHTML = msg`; `feedback.innerHTML` with the server `message`; `Html.Raw` string interpolation of `YoutubeId` into a script. | L-R8; JSON config; `textContent` | L5–L9 |
| B7 | **Import waits for AI quiz generation** (`CreateOwnedLessonAsync` awaits `GenerateQuizFromTranscriptAsync`; the comment says "without blocking"). The request can take a long time with no UI state. | Keep the behaviour (L-R10). Fix the comment. Give the UI an honest long-running state (6.5). Record in `known-issues.md`. | L1, L5 |
| B8 | **Any Premium user can write AI quiz questions onto a public lesson** that has none (`GenerateQuizFromTranscriptAsync` allows `OwnerUserId == null`). Shared content changes without Admin review. | L-R6 | L1 |
| B9 | **Keyboard trap:** in dictation, `Tab` is hijacked to replay audio (`listening-practice.js` ~384). A keyboard user cannot leave the textarea. | New shortcuts (6.8); Tab moves focus normally | L8 |
| B10 | **`confirm()` / `alert()`** for delete and errors; inline `<script>` of ~130 lines in `Listening.cshtml`; `console.log` in `ListeningPractice.cshtml`. | Bootstrap modal, toasts, external JS | L5, L6 |
| B11 | **Design-system violations:** private `:root` palettes (`--li-*`, `--lp-*`), hex colours, own shadows and radii, emoji as icons, external placeholder image `placehold.co`, cross-owner dependency on `speaking.css`, inline `style` mutations from JS. | L-R7 | L3–L9 |
| B12 | **Filters ignore the server parameters.** The action accepts `level` / `topic`, but the UI always starts on "all" and does not reflect or update the URL. | URL-synced filters (6.3) | L4 |
| B13 | **Mixed language and broken labels:** "English Listening Lab", "lesson(s)", "Qs", "Listening" breadcrumb; an empty duration tag when `Duration` is null; the false "Update hàng tuần" chip. | L-R9 | L4, L6 |
| B14 | **Best score is overwritten** by a worse retry; the score is client-reported. | L-R4 | L1 |
| B15 | **Quiz markup is not accessible:** options are not grouped (`fieldset` / `legend`); unanswered questions are flagged with a 2-second inline outline only; tabs lack arrow-key support; the mode selector is clickable `div`s. | 6.7, 6.10 | L6–L9 |
| B16 | **Dead / duplicate code:** delete posts both `id` and `lessonId`; `LevelMeta` holds unused Bootstrap-icon classes and hex colours; `quizJson` (`LP_QUIZ`) is emitted but unused, if confirmed. | Remove what is confirmed unused inside the owner's files | L10 |

If a finding cannot be reproduced at L0, record the evidence in section 12 and skip its fix.

---

## 4. Backend specification (L1)

Follow TDD: write the failing tests in 4.5 first, then the code.

### 4.1 `ListeningService`: access helper (L-R5)

```csharp
// Returns the lesson id/owner/counts when the user may use the lesson, else null.
private async Task<LessonAccess?> GetUsableLessonAsync(int lessonId, int? userId, CancellationToken ct = default)
// record LessonAccess(int Id, int? OwnerUserId, int TranscriptCount, int QuizCount, int VocabCount);
```

- One `AsNoTracking` projection query; no `Include`.
- **Usable** when `IsPublished && (OwnerUserId == null || OwnerUserId == userId)`.
- **Private lesson + Standard-role owner** → null (same as `GetPracticeViewModelAsync` today).
- **Callers:**
  - `GetPracticeViewModelAsync` (keep its current result shape);
  - `EvaluateQuizAsync`;
  - `SaveProgressAsync`.

### 4.2 `EvaluateQuizAsync` (B2, B14, L-R4)

- **New signature** (additive): `Task<ListeningQuizResultViewModel?> EvaluateQuizAsync(ListeningQuizSubmitDto dto, int? userId = null, CancellationToken ct = default)`.
- **Controller:** pass the id from `TryGetCurrentUserId`. On failure of that call, return `Unauthorized`, as the other actions do.
- **Flow:**
  1. `GetUsableLessonAsync` → null means return null (controller → 404).
  2. Grade as today. Ignore answer keys that are not question ids of this lesson. Accept only `A`–`D`, case-insensitive.
  3. When `userId` has a value, upsert progress:
     - `QuizCompleted = true`;
     - `QuizScore = max(existing ?? 0, new)`;
     - `LastAccessedAt = now`;
     - then apply the completion rule (4.4).
  4. Return the result with the new field `BestScorePercent` (additive).
- Keep the `Passed` threshold (60).

### 4.3 `SaveProgressAsync` (B3)

- Replace the `AnyAsync(IsPublished)` check with `GetUsableLessonAsync`.
- Keep the selective update of all four DTO fields (pinned by `GoalsPhase5IntegrationTests.SaveListeningProgress_ReplayCompletion_DoesNotDoubleCountListeningActivity`).
- For `QuizScore` sent by a client:
  - clamp to 0–100;
  - keep the best score (`max`).
- Apply the completion rule (4.4).

### 4.4 Completion rule (B1, L-R3): one private method used by 4.2 and 4.3

```csharp
private static bool IsLessonComplete(UserListeningProgress p, LessonAccess lesson) =>
    (lesson.TranscriptCount == 0 || p.TranscriptCompleted) &&
    (lesson.QuizCount == 0 || p.QuizCompleted) &&
    (lesson.VocabCount == 0 || p.VocabReviewed) &&
    (lesson.TranscriptCount + lesson.QuizCount + lesson.VocabCount) > 0;
```

- Set `CompletedAt` once.
- Call `_goalsService.RecordLearningActivityAsync` only on the transition from not-completed to completed (existing `wasCompleted` logic).
- Keep the existing exception handling and logging.
- Check the test's seeded lesson (`SeedListeningReplayLessonAsync`): the test must still end with exactly one credit.

### 4.5 Index data (B4, B5) and view model additions

`GetIndexViewModelAsync`:
- **Topics:** add `l.OwnerUserId == null` to the `allTopics` query.
- **Progress:** when `userId` has a value, load progress for the listed public lessons **and** the user's private lessons in **one** query (`Where(p => p.UserId == userId && ids.Contains(p.LessonId))`). Fill the existing card fields.
- **New card fields** (`ListeningLessonCardViewModel`):
  - `bool IsCompleted` (from `CompletedAt != null`, or the 4.4 rule for old rows);
  - `string Status`: `"new" | "progress" | "done"`.
    - `done` = `IsCompleted`;
    - `progress` = a progress row exists and is not done;
    - `new` = no row.
  - `int StepsDone`, `int StepsTotal` (sections with content, 0–3).
- **New index fields** (`ListeningIndexViewModel`):
  - `int TotalLessons` (all published public lessons, **ignoring** the level/topic filter);
  - `int CompletedLessons`, `int InProgressLessons` (same population);
  - `ListeningLessonCardViewModel? ContinueLesson`: the usable lesson with the latest `LastAccessedAt` that is not completed (public or own);
  - `bool IsAuthenticated`, `bool CanImport` (role is Premium or Admin).
- **Query rules:** no queries in loops; all `AsNoTracking`; pass a `CancellationToken` where the signature allows.

`GetPracticeViewModelAsync` additions (`ListeningPracticeViewModel`):
- `bool IsOwnLesson`;
- `bool CanUseAi` (Premium/Admin, **and** (own lesson **or** Admin) for quiz generation);
- `bool CanTranslate` (the current translate rule);
- `bool IsAuthenticated`;
- `bool IsCompleted`.

The view must not derive permissions from `User.IsInRole` any more.

### 4.6 `GenerateQuizFromTranscriptAsync` (B8, L-R6)

- After loading the lesson: if `OwnerUserId == null` and the role is not Admin → `OperationResult.Invalid("Chỉ quản trị viên mới tạo được quiz cho bài nghe chung.")`.
- `OpenAiConsumerPipelineTests` calls it with `TestDataIds.AdminUserId` on a public lesson; that must stay green.
- Fix the misleading comment in `CreateOwnedLessonAsync` (B7); do not change its behaviour.

### 4.7 Controller (`StudyController.cs`, Listening actions only)

- `EvaluateListeningQuiz`: pass `userId`.
- `Listening`: no change in signature. Normalise `level` to the known keys (`A1`–`C1`, else null) before calling the service.
- No other action changes.

### 4.8 Tests: `TCTEnglish.Tests/ListeningIntegrationTests.cs`

Use `TestWebApplicationFactory`, `IntegrationTestClientHelper` and `TestDataIds`, seeding lessons like `SeedListeningReplayLessonAsync` does.

- **Access:**
  - anonymous and signed-in users get 200 on `/Home/Listening` and on a public lesson;
  - an unpublished lesson → 404;
  - another user's private lesson → 404;
  - the owner (Premium) → 200;
  - the owner downgraded to Standard → 404.
- **B2:** `EvaluateQuiz` for another user's private lesson → 404, and the body has no `correctAnswer`. Same for an unpublished lesson. For a usable lesson → 200 with per-question results.
- **B3:** `SaveProgress` on another user's private lesson → 404, and no progress row is created.
- **B1 / L-R3:**
  - a lesson with transcript + quiz + vocab completes only after all three;
  - a lesson with transcript only completes after `transcriptCompleted`;
  - a lesson with transcript + quiz completes after evaluating the quiz (no `vocabReviewed`);
  - `UserDailyActivities.ListeningCompletedCount` increases exactly once, and a replay does not add more.
- **L-R4 / B14:**
  - `EvaluateQuiz` persists `QuizCompleted` and the score;
  - a worse retry keeps the best score;
  - `BestScorePercent` is returned;
  - answers for foreign question ids are ignored.
- **B4:** after progress exists, the index HTML shows the lesson's "done" / "progress" status marker (assert a stable `data-status="done"` attribute).
- **B5:** a private lesson's topic is not in the topic filter of another user, nor of an anonymous visitor.
- **B8:** a Premium non-admin calling `My/GenerateQuiz` on a public lesson → 400 with the message; nothing is written. Use the stub AI provider; no paid calls.
- **Antiforgery:** each POST without the token → 400.
- **Page contract:**
  - both pages contain `class="tct-theme tct-page tl-page`, one `<h1>`, the antiforgery token, `time-tracker.js` with `data-feature="Listening"` (practice), `listening-core.js`, and the config JSON script;
  - no inline `onclick`;
  - the practice page of a lesson with vocab contains the vocabulary tab.

All existing suites must stay green, in particular `GoalsPhase5*`, `GoalsPhase2*`, `OpenAiConsumerPipeline*`, `AiDeterministicBaseline*`, `WebsiteGuideRetriever*`, `SharedData*`.

---

## 5. Pure logic: `wwwroot/js/listening-core.js` (L2)

- UMD, like `practice-core.js`: `module.exports` under Node, `window.TctListening.core` in the browser.
- No DOM access. Random steps take an injectable `rng`; export `mulberry32(seed)`.

| Function | Behaviour |
| --- | --- |
| `normalize(text)` | NFC, lowercase, `’` → `'`, strip punctuation except inner apostrophes and hyphens, collapse spaces, trim |
| `tokenize(text)` | `normalize` then split on spaces; empty → `[]` |
| `levenshtein(a, b)` | standard |
| `gradeDictation(input, answer)` | Word-level alignment (LCS over tokens) → `{ status, score, words }`. `words` = `[{ text, state: "ok" \| "near" \| "wrong" \| "missing" }]` in answer order. A typed word is `near` when `levenshtein ≤ 1` and the word has ≥ 4 letters. `score` = ok words / answer words (0–1). `status`: `correct` (score = 1), `near` (score ≥ 0.8), `wrong` otherwise. Blank input → `wrong` with all `missing`. |
| `pickBlanks(text, rng, opts)` | Chooses words to blank: only words with ≥ 3 letters that are not in a small stop list (`the, a, an, and, or, but, to, of, in, on, at, is, are, was, were, be, it, i, you, he, she, we, they`). About 1 blank per 5 words, at least 1 and at most 4. Never two adjacent blanks. Returns `{ parts: [{ type: "text" \| "blank", value, answer? }] }`. Keeps the original punctuation outside the blank. A line with no eligible word returns no blanks. |
| `gradeBlank(input, answer)` | `correct` when `normalize` equal; `near` when ≥ 4 letters and distance ≤ 1; else `wrong`. Blank → `wrong`. |
| `filterLessons(lessons, { level, topic, status, q })` | `lessons` = `[{ id, title, topic, level, status }]`. `q` matches title or topic, accent- and case-insensitive (strip Vietnamese diacritics with NFD). `"all"` or empty means no filter. Returns ids in the input order. |
| `paginate(ids, shown)` | → `{ visible, hasMore }` |
| `lineIndexAt(lines, time)` | `lines` = `[{ start, end }]` sorted. Returns the index whose range contains `time`. When there is no `end`, use the next `start`. Returns -1 before the first line. Uses binary search. |
| `formatClock(seconds)` | `"m:ss"`, or `"h:mm:ss"` from one hour |
| `requiredSteps({ transcript, quiz, vocab })` | Counts → the ordered list of required step keys (L-R3). |
| `isComplete(flags, required)` | Mirrors 4.4. |
| `parseFilterQuery(search)` / `buildFilterQuery(state)` | URL sync for `level`, `topic`, `status`, `q`. Unknown values fall back to `"all"`. |

**Node tests** (`scripts/tests/test_listening_core.cjs`, `node:test`, seeded):
- every row above, including:
  - Vietnamese diacritics in search;
  - "don't" / "well-known";
  - blank input;
  - a one-word line;
  - a line with only stop words;
  - determinism with a seed;
  - no adjacent blanks;
  - `lineIndexAt` at boundaries, in gaps, and after the last line;
  - `isComplete` for each content combination.
- The file must contain no `document`, `window.` (outside the UMD wrapper) or `fetch`.

---

## 6. UI specification

### 6.1 Shared rules

- **Wrapper:** `<div class="tct-theme tct-page tl-page tl-page--index">` / `tl-page--practice`.
- **Kit scope (L3):**
  - in `study-kit.css`, replace every `:is(.ts-page, .tp-page)` with `:is(.ts-page, .tp-page, .tl-page)`;
  - add `.tl-page` and `tl-hue-*` to the canvas rule and the hue blocks (`:is(.ts-hue-primary, .tp-hue-primary, .tl-hue-primary)`, and so on for the six hues);
  - no other change to that file.
- **Stylesheets on both pages, in order:** `study-kit.css`, then `listening.css`. Do not load `speaking.css`, `listening-index.css` or `listening-practice.css`.
- **`listening.css`:**
  - every selector starts with `.tl-page`;
  - values are `var(--tct-*)` only: no hex, `rgb()`, `:root`, `!important` or weight above 700;
  - depth uses only `--tct-clay-shadow*` tokens (plus token-coloured rings appended to one);
  - radii use tokens;
  - a `@media (forced-colors: active)` block mirrors the kit's;
  - all motion uses `--tct-dur*` / `--tct-ease` and is disabled under `prefers-reduced-motion: reduce`.
- **Components to reuse:** `_PageHeader`, `_StateMessage`, `.tct-badge--*`, `.tct-phonetic`, `.tct-btn-icon`, `.tct-clamp-2`, `.tct-break`, Bootstrap buttons/forms/modals, and the kit classes `k-card`, `k-panel`, `k-panel--quiet`, `k-soft`, `k-pillow`, `k-art-box`, `k-seg`, `k-progress`, `k-kbd`, `k-btn--soft`, `k-btn--success|warning|danger`, `k-btn--icon-surface`, `k-display`, `k-label`.
- **Icons:** Font Awesome 6.0.0 only (verify each exists). No emoji as icons.
- **Level hues** (text always accompanies colour):

| Level | Hue | Icon | Label | Description |
| --- | --- | --- | --- | --- |
| A1 | success | `fa-seedling` | "A1 · Sơ cấp" | "Tiếng Anh dễ, nói chậm" |
| A2 | info | `fa-leaf` | "A2 · Cơ bản" | "Nói rõ, từ vựng thông dụng" |
| B1 | primary | `fa-fire` | "B1 · Trung cấp" | "Tốc độ tự nhiên, chủ đề quen thuộc" |
| B2 | warning | `fa-bolt` | "B2 · Trung cao cấp" | "Vốn từ rộng hơn" |
| C1 | danger | `fa-rocket` | "C1 · Nâng cao" | "Nói nhanh, tự nhiên" |

- **Status badges:** new = `tct-badge--primary` "Mới"; progress = `tct-badge--warning` "Đang học"; done = `tct-badge--success` "Đã xong" (+ `fa-check`).
- **Thumbnails:**
  - `<img loading="lazy" decoding="async" alt="">` (decorative; the title is next to it), `width` / `height` set, `aspect-ratio: 16/9`, `object-fit: cover`;
  - a JS `error` listener swaps a failed image for a local placeholder: an element `.tl-thumb--empty` with `fa-headphones` on `--k-bg`;
  - remove `placehold.co`.
- **Laptop first (demo priority):** design and check at 1440×900 and 1366×768, then 1024 / 768 / 390 / 320 and 200% zoom.

### 6.2 Index: header and overview (`Listening.cshtml`)

```text
_PageHeader: Title "Luyện nghe"; Description "Nghe hội thoại thực tế, xem lời thoại, luyện chính tả và làm quiz."
section.tl-overview.k-panel (signed-in users only)
```

**Overview block** (same visual pattern as `.ts-overview` on `/Sets`: gradient `linear-gradient(135deg, var(--k-tint), var(--tct-surface) 60%)`, radius `--tct-radius-xl`):
- **Left:**
  - `h2` "Tiến độ nghe của bạn";
  - three `k-pillow` stats in a `<dl>`: "bài đã xong" = `CompletedLessons`, "đang học" = `InProgressLessons`, "tổng số bài" = `TotalLessons`;
  - a thin `k-progress` with `aria-label="Đã xong {CompletedLessons}/{TotalLessons} bài"`.
- **Right (`ContinueLesson != null`):**
  - a `k-card` link "Tiếp tục nghe" with the thumbnail (160px), title (`tct-clamp-2`), level badge, "{StepsDone}/{StepsTotal} phần";
  - a primary button "Nghe tiếp" (`fa-play`) → the practice URL.
- **Right (no lesson to continue, some lessons exist):** text "Chọn một bài bên dưới để bắt đầu.".
- **Anonymous visitors:**
  - no overview;
  - a `k-panel--quiet` notice: `fa-circle-info` "Đăng nhập để lưu tiến độ và thêm bài nghe của riêng bạn." + link "Đăng nhập" (existing login route).

### 6.3 Index: toolbar and filters

```text
div.tl-toolbar (role="search")
  search field (label visually hidden "Tìm bài nghe"; placeholder "Tìm theo tên bài hoặc chủ đề…"; type=search; maxlength 80)
  nav.k-seg (levels):   "Tất cả" | A1 | A2 | B1 | B2 | C1          (buttons, aria-pressed)
  div.k-seg (status):   "Tất cả" | "Chưa học" | "Đang học" | "Đã xong"   (signed-in only)
  div.tl-topics:        pills "Tất cả chủ đề" + each topic (buttons, aria-pressed); wraps; collapses behind "Xem thêm chủ đề" when more than 8
  p.tl-count aria-live="polite": "{n} bài nghe"
```

- **Initial state** comes from the server (`SelectedLevel`, `SelectedTopic`) and the URL (`status`, `q`), via `parseFilterQuery`.
- **Changes** update the URL with `history.replaceState` (`buildFilterQuery`) and filter client-side (`filterLessons`). No page reload.
- **Empty result:** `_StateMessage` "Không tìm thấy bài nghe phù hợp." / "Thử từ khóa khác hoặc bỏ bớt bộ lọc." + button "Xóa bộ lọc".
- **No lessons at all:** `_StateMessage` "Chưa có bài nghe nào." / "Nội dung đang được cập nhật.".

### 6.4 Index: level sections and lesson cards (`_ListeningLessonCard.cshtml`)

```text
section.tl-level[data-level] (one per level that has lessons, order A1→C1)
  header: icon square (48px, level hue, k-pillow) + h2 "{label}" + p description + count badge "{n} bài"
  ul.tl-grid (repeat(auto-fill, minmax(280px, 1fr)), gap --tct-space-lg)
    li[data-id][data-title][data-topic][data-level][data-status]
      article.k-card.tl-card > a (stretched link, aria-label "Mở bài nghe {Title}")
        .tl-card__thumb.k-art-box: img + status badge (top-left) + duration pill (bottom-right, only when Duration has a value; fa-clock)
        .tl-card__body:
          h3 title (tct-clamp-2, tct-break)
          p meta: topic (fa-tag) · "{QuizQuestionCount} câu hỏi" (when > 0) · "{VocabItemCount} từ" (when > 0)
          p speakers (fa-user-group; "{Speaker1Name}" + " và {Speaker2Name}" when present; omitted when both empty)
          .tl-card__steps (signed-in): k-progress--thin + visually-hidden "{StepsDone}/{StepsTotal} phần đã xong"; done lessons with a score show "Quiz {QuizScore}%"
  button.btn.btn-outline-primary "Xem thêm" (shows 6 more; hidden when nothing more; replaces the old pagination)
```

- 6 cards visible per level at first (`paginate`).
- A filter change resets to 6.
- A section with no visible card is hidden.

### 6.5 Index: "Bài nghe của tôi" (signed-in only; `_ListeningMyLessonCard.cshtml`)

Position: after the overview, before the toolbar.

```text
section.tl-mine aria-labelledby
  header: h2 "Bài nghe của tôi" + count badge "{n} bài" + (locked) tct-badge--warning "Cần Premium" (fa-lock)
  [CanImport] form#tlImport.k-panel (no action attribute; JS submits)
     label "Dán link YouTube" + input type=url (required, maxlength 300, placeholder "https://www.youtube.com/watch?v=…", aria-describedby hint)
     button.btn.btn-primary "Thêm bài nghe" (fa-plus)
     p#hint "Chỉ hỗ trợ video tiếng Anh có phụ đề."
     div#tlImportStatus role="status" aria-live="polite"
  [!CanImport] div.k-panel.tl-locked: fa-crown + "Thêm bài nghe từ YouTube là tính năng Premium." + a.btn.btn-primary "Nâng cấp Premium" (href = the existing /Account/Premium)
  grid of my-lesson cards, or _StateMessage "Chưa có bài nghe nào." / "Dán link YouTube ở trên để tạo bài luyện nghe riêng."
```

- **My-lesson card:** like 6.4, plus a "Riêng tư" badge (`fa-user-shield`) and the created date `dd/MM/yyyy`.
  - **Unlocked:** "Luyện ngay" (primary, small) and a delete icon button (`tct-btn-icon`, `aria-label "Xóa bài nghe {Title}"`).
  - **Locked** (`IsLocked`): the card is not a link; it shows "Nâng cấp để mở khóa" (`fa-crown`) → `/Account/Premium`.
- **Import flow** (`listening-index.js`):
  1. Validate that the field is not empty and matches a YouTube URL pattern. Inline error text "Link YouTube không hợp lệ.".
  2. Disable the form. Status: spinner + "Đang lấy phụ đề và tạo bài nghe… Việc này có thể mất đến một phút." (B7).
  3. `POST /Home/Listening/My/Create` as form data with the antiforgery token (same contract).
  4. **Success:** status "Đã thêm bài nghe." Then navigate to `/Home/Listening/Practice/{lessonId}`.
  5. **403 + `requiresUpgrade`:** status text from the server via `textContent`, plus a link "Nâng cấp Premium".
  6. **Other errors:** the server `message` via `textContent`, else "Không thêm được bài nghe. Hãy kiểm tra video có phụ đề tiếng Anh rồi thử lại.". Re-enable the form and keep the typed URL.
- **Delete flow:**
  - Bootstrap modal `#tlDeleteLesson`: title "Xóa bài nghe?", body "Bài “{Title}” và tiến độ học của bài sẽ bị xóa vĩnh viễn.", buttons "Hủy" | "Xóa bài nghe" (danger).
  - POST `My/Delete` with `lessonId` only.
  - **Success:** remove the card, update the count, toast "Đã xóa bài nghe.", show the empty state when it was the last card (no reload), and move focus to the section heading.
  - **Failure:** toast "Không xóa được bài nghe. Vui lòng thử lại.".

### 6.6 Practice: layout (`ListeningPractice.cshtml`)

```text
div.tct-theme.tct-page.tl-page.tl-page--practice.tl-hue-{level hue}
  _PageHeader: Title = lesson title; Breadcrumbs "Luyện nghe" (→ index) › title
  p.tl-meta: level badge · topic (fa-tag) · duration (fa-clock, when present) · speakers with country text (no flag emoji)
  [guest] notice (k-panel--quiet): "Bạn đang học ở chế độ khách. Đăng nhập để lưu tiến độ." + link "Đăng nhập"
  div.tl-practice (grid)
     aside.tl-stage (player column)
     section.tl-work (tabs column)
```

- **≥1200px:** `grid-template-columns: minmax(0, 5fr) minmax(0, 7fr)`, gap `--tct-space-xl`. `.tl-stage` is `position: sticky; top: calc(var(--tct-header-h) + var(--tct-space-md))`.
- **<1200px:** one column.
  - At ≤768px the player card is `position: sticky; top: var(--tct-header-h)` with a compact height (`max-height: 34vh` for the video), so the learner can type while listening.
  - Verify at 390 that the sticky player does not cover the inputs or the AI launcher.
- **At 1366×768:** the player, the controls and the first transcript lines must be visible without scrolling.

### 6.7 Practice: player column (`.tl-stage`)

```text
.k-panel.tl-player
  [YouTube] div.tl-video (aspect-ratio 16/9, radius --tct-radius-lg, overflow hidden) > iframe#youtube-player (same src pattern, enablejsapi=1; title = lesson title; loading="lazy")
  [audio]   .tl-audio: fa-headphones art + <audio id="lp-audio" controls preload="metadata">
  [none]    _StateMessage "Bài nghe chưa có âm thanh."
  .tl-controls (toolbar, aria-label "Điều khiển phát")
     button "Lùi 5 giây" (fa-rotate-left, k-btn--icon-surface)
     button play/pause (fa-play / fa-pause, 56px, btn-primary, aria-label switches "Phát" / "Tạm dừng")
     button "Tới 5 giây" (fa-rotate-right)
     toggle "Lặp câu" (fa-repeat, aria-pressed): loops the active transcript line
     speed: [CanUseSpeed] k-seg radios 0.5× 0.75× 1× 1.25× 1.5×  |  [else] "1×" + link "Mở khóa tốc độ với Premium" (fa-lock) → /Account/Premium
.k-panel.tl-steps (aria-label "Tiến độ bài nghe")
   ol with the required steps only (5: requiredSteps):
      "Nghe và đọc lời thoại" · "Học từ vựng" · "Làm quiz"
   each: state icon (fa-circle-check success when done; fa-circle regular when not) + label + visually-hidden "đã xong" / "chưa xong"; a button jumps to the matching tab
   [all done] .tl-done (k-soft success): fa-trophy + "Bạn đã hoàn thành bài nghe này!" + links "Bài nghe khác" (index)
.tl-keys (≥992px only): k-kbd hints — Space "phát / dừng", ← → "câu trước / sau", R "nghe lại câu", L "lặp câu"
```

- **Speed availability:** keep the current rule (Premium/Admin). Expose it as a view-model flag `CanUseSpeed` instead of `User.IsInRole` in the view.
- **Player adapter** (`listening-practice.js`): one object wrapping YouTube IFrame API or `<audio>`:
  - methods: `play`, `pause`, `toggle`, `seek`, `time`, `rate`, `onState`;
  - load `https://www.youtube.com/iframe_api` as today;
  - if the API fails to load within 8 s, show an inline notice "Không tải được trình phát YouTube. Bạn vẫn có thể đọc lời thoại và làm bài." and keep the page usable.
- **Shortcuts:**
  - ignored while focus is in an input, textarea, select or button that handles the key, and when Ctrl/Meta/Alt is held;
  - `Space` must not scroll the page when handled.

### 6.8 Practice: tabs and transcript modes (`.tl-work`)

- **Tabs:** real ARIA tabs.
  - `div[role=tablist][aria-label="Các phần của bài nghe"]` with `button[role=tab]` (`aria-selected`, `aria-controls`, roving `tabindex`, ←/→/Home/End).
  - Tabs: "Lời thoại" (count), "Từ vựng" (count; **omitted when the lesson has no vocab**), "Quiz" (count).
  - A done tab shows `fa-circle-check` + visually-hidden "đã xong".
  - Styled as `k-seg`.
  - The selected tab is kept in `location.hash` (`#loi-thoai`, `#tu-vung`, `#quiz`).
- **Transcript tab — mode switch:** `k-seg` radios with `fa` icons: "Nghe thường" (`fa-book-open`), "Chính tả" (`fa-pen`), "Điền từ" (`fa-spell-check`). Real `input[type=radio]`, not clickable `div`s.

**Mode "Nghe thường"**
- **Toolbar:**
  - toggle "Hiện bản dịch" (`aria-pressed`; disabled with a hint when no line has a translation);
  - toggle "Ẩn lời thoại" (`aria-pressed`; hides the English text so the learner listens first; each line shows "Bấm để hiện" as a button);
  - toggle "Tự cuộn theo âm thanh" (on by default; stored in `TctSets.safeStorage`).
  - `[CanTranslate]`, when some lines have no translation: button "Dịch bằng AI" (`fa-language`).
    - Calls `POST Translate/{lessonId}`.
    - **Pending:** "Đang dịch…".
    - **Success:** reload the transcript data (page reload is acceptable) with a toast "Đã dịch lời thoại.".
    - **Failure:** the server message via toast.
- **List:** `ol.tl-lines` inside `k-panel--quiet`.
  - Each `li` holds:
    - a `button.tl-line` (the whole line is the seek control, `aria-label "Nghe từ câu {n}"`);
    - the speaker tag (`tct-badge`, second speaker in info hue);
    - the English text (`lang="en"`, `--tct-text-lg`);
    - the Vietnamese line (muted, hidden unless toggled);
    - the start time (`formatClock`, tabular, muted).
  - Lines without timings are plain text with no seek button.
  - **Active line** (from `lineIndexAt`, polled with `requestAnimationFrame` while playing): `--tct-primary-tint` background + a 3px `--tct-primary` inline-start border + `aria-current="true"`. Auto-scroll keeps it in view with `scrollIntoView({ block: "nearest" })`, and only when the learner has not scrolled manually in the last 3 s.
- **Footer:** button "Đánh dấu đã nghe xong" (`fa-check`, primary). After saving it becomes disabled, with "Đã nghe xong lời thoại" (`fa-circle-check`). Guests see the button, but it only updates the local step state.

**Mode "Chính tả"** (dictation; fixes B9)
- **Header:** short rule text "Nghe từng câu rồi gõ lại." + a progress ring (CSS `conic-gradient` with `--p`; `role="img"` `aria-label "{p}% số câu đã kiểm tra"`).
- **One card per line** (`k-panel`):
  - number badge, speaker;
  - button "Nghe câu này" (`fa-play`; seeks and plays that line, stopping at its end);
  - `textarea` (label visually hidden "Gõ lại câu {n}", `lang="en"`, `autocomplete="off" autocapitalize="none" autocorrect="off" spellcheck="false"`);
  - buttons "Kiểm tra" (primary) and "Xem đáp án" (outline; counts as wrong).
- **Keys:** `Enter` = check, `Shift+Enter` = new line, `Alt+R` = replay the line. **`Tab` moves focus normally.** The hint text shows these.
- **Feedback** (from `gradeDictation`), `aria-live="polite"`:
  - a title: "Chính xác!" (success) / "Gần đúng" (warning) / "Chưa đúng" (danger), with an icon;
  - the answer words rendered as spans: ok = normal, near = warning underline, wrong/missing = danger underline;
  - a text legend, so colour is not the only signal;
  - the correct sentence.
- **Summary** when every line is checked: `k-soft` tiles "Đúng", "Gần đúng", "Sai" + button "Làm lại".
- State stays in memory only.

**Mode "Điền từ"** (fill in)
- **Blanks** from `pickBlanks`, seeded with `lessonId` + a retry counter (a retry gives different blanks).
- **Each line:**
  - the sentence with inline `input`s (width from the answer length, `aria-label "Từ còn thiếu {k} của câu {n}"`, same input attributes as above);
  - a "Nghe câu này" button.
- **Keys:** `Enter` in a blank checks that blank and moves to the next one.
- **Footer:** "Kiểm tra tất cả" (primary) and "Làm lại" (outline).
- **Result per blank:** success / warning / danger border + icon + visually-hidden text. Wrong blanks show the answer under the input.
- **Summary:** tiles "Đúng", "Sai".
- Lines with no eligible word are shown as plain text.
- **Both practice modes on a lesson without transcript:** `_StateMessage` "Bài nghe chưa có lời thoại.".

### 6.9 Practice: vocabulary tab (new; B1)

```text
ul.tl-vocab (grid minmax(260px, 1fr))
  li.k-card: [image when ImageUrl, k-art-box 16/9] + word (lang=en, --tct-text-xl 700) + speak button (TctSets.speak; hidden when unsupported; aria-label "Phát âm {word}")
             + definition + example (lang=en, muted, when present)
footer: button "Đã học xong từ vựng" (fa-check, primary) → saveProgress({ vocabReviewed: true }) → disabled "Đã học xong từ vựng"
```

- Images use the same lazy / `error` fallback as thumbnails. Only render `ImageUrl` values that are local or `https` URLs.
- The tab and the step are omitted when `VocabItems` is empty (L-R3).

### 6.10 Practice: quiz tab (B15)

- **Each question:**
  - `fieldset.k-panel` with `legend` "Câu {i}. {QuestionText}";
  - options are `label.tl-option` (Clay option style, as `.tp-option`) wrapping a real `input[type=radio][name="q{id}"]` (visually hidden; focus ring on the label via `:has(:focus-visible)`), with a key badge A–D.
- **Submit bar:** "Đã chọn {a}/{n}" + "Nộp bài" (primary).
  - With unanswered questions: do not submit. Mark each unanswered `fieldset` with `tl-q--missing` (danger border + text "Chưa chọn đáp án"). Move focus to the first one. Announce "Còn {k} câu chưa trả lời." in a live region.
- **Submit:** `POST EvaluateQuiz` (same body).
  - **Pending:** "Đang chấm…".
  - **Success:**
    - per question: the correct option gets success styling + `fa-circle-check` + "Đáp án đúng"; a wrong pick gets danger + `fa-circle-xmark` + "Bạn chọn"; the explanation appears below via **`textContent`** ("Giải thích: …"), omitted when empty;
    - inputs disabled;
    - a result panel: ring with `ScorePercent`, "Đúng {c}/{n}", the remark ("Đạt! Bạn đã vượt qua bài quiz." when `passed`, else "Chưa đạt. Nghe lại rồi thử lần nữa nhé."), "Điểm cao nhất: {BestScorePercent}%", button "Làm lại quiz".
  - The step state updates from the response. **Do not call `SaveProgress` for the quiz** (L-R4).
  - **Failure:** toast "Không chấm được bài. Vui lòng thử lại."; the button is re-enabled.
- **Already completed on load:** show the stored score in the result panel ("Điểm cao nhất: {QuizScore}%") with "Làm lại quiz"; the questions are answerable again.
- **Guests:** the tab shows the questions, and a notice "Đăng nhập để làm quiz và lưu điểm." replaces the submit bar (the endpoint requires sign-in).
- **No questions:**
  - `[CanUseAi]`: `_StateMessage` "Bài nghe chưa có quiz." + button "Tạo quiz bằng AI" (`fa-wand-magic-sparkles`). Pending "Đang soạn câu hỏi…"; success reloads; failure shows the server message in a toast (no `alert`).
  - Otherwise: "Bài nghe chưa có quiz.".

### 6.11 Practice: config and scripts

```html
<script type="application/json" id="tlConfig">{ lessonId, youtubeId, audioUrl, isAuthenticated, canUseSpeed, canTranslate, canUseAi,
  steps: { transcript, quiz, vocab }, progress: { transcriptCompleted, quizCompleted, quizScore, vocabReviewed, isCompleted },
  lines: [{ id, order, speaker, text, vi, start, end }], urls: { evaluate, save, translate, generateQuiz, index } }</script>
```

- Serialise with `System.Text.Json` and the default encoder. Do **not** use `UnsafeRelaxedJsonEscaping` or `Html.Raw` string interpolation. `Html.Raw` on the serialised JSON inside a `type="application/json"` script is fine.
- Quiz questions and vocab are server-rendered (no JSON copy).
- **Scripts, in order:**
  1. `time-tracker.js` (`data-feature="Listening"`, unchanged);
  2. `personal-sets-common.js`;
  3. `listening-core.js`;
  4. `listening-practice.js`.
- No inline logic, no `console.log`.
- **`saveProgress`:**
  - via `TctSets.fetchJson` (antiforgery header);
  - fire-and-wait with one retry;
  - on failure, one toast per session: "Không lưu được tiến độ. Kết quả trên trang vẫn được giữ.";
  - guests skip the call.

---

## 7. Files

| File | Change |
| --- | --- |
| `Services/ListeningService.cs`, `Services/IListeningService.cs` | Section 4 |
| `ViewModels/ListeningViewModels.cs` | Additive fields (4.5) |
| `Controllers/StudyController.cs` | Listening actions only (4.7) |
| `Views/Study/Listening.cshtml`, `ListeningPractice.cshtml` | Rewritten (section 6) |
| `Views/Study/_ListeningLessonCard.cshtml`, `_ListeningMyLessonCard.cshtml` | **New** |
| `wwwroot/css/listening.css` | **New** |
| `wwwroot/css/listening-index.css`, `listening-practice.css` | **Deleted** at L10, after a grep proves no other view references them |
| `wwwroot/css/study-kit.css` | Scope extension only (6.1) |
| `wwwroot/js/listening-core.js` | **New** (section 5) |
| `wwwroot/js/listening-index.js`, `listening-practice.js` | Rewritten |
| `TCTEnglish.Tests/ListeningIntegrationTests.cs`, `scripts/tests/test_listening_core.cjs` | **New** |
| `docs/design-system/team-ui-workflow.md` §6 | Row "Nghe": CSS `study-kit.css` + `listening.css`, status "Đã chuyển Clay 2026-11-13", owner Huỳnh Phú Trọng |
| `.ai/context/bug-fix-log.md`, `.ai/context/known-issues.md`, `docs/project-structure.md` | B1–B16 summary; B7 limitation; file list if that doc has one |

**Forbidden:**
- `Program.cs`, appsettings, `.csproj`, migrations, `Models/**`;
- `Services/AI/**`, `GoalsService*`, `IYoutubeTranscriptService*`;
- `time-tracker.js`, `personal-sets-common.js`, `practice-*.js`;
- `_Layout.cshtml`, `layout.css`, `site.css`, `tct-learning-ui.css`, `_PageHeader.cshtml`, `_StateMessage.cshtml`;
- `speaking.css`, `Areas/Admin/**`;
- other actions of `StudyController.cs`;
- existing tests (do not edit them to pass).

---

## 8. Phases and the phase gate

**Run mode: continuous.** Go from L0 to L11 in one run without stopping to ask the owner. A phase ends only when its gate (8.1) is fully green. Then write its gate record (8.3), commit, and start the next phase immediately.

| Phase | Work | Skills |
| --- | --- | --- |
| L0 | **Baseline.** Check branch/HEAD/tree; `git switch master`; verify it contains `d8c5773`; create `feature/listening-redesign`. Build; full Release tests; all node tests (record counts and warnings). Save this plan. Confirm each finding B1–B16 with file:line (section 12). Start the app. Take **before** screenshots of both pages (signed-in Standard, Premium and anonymous) at 1440×900, 1366×768 and 390. Record the hook inventory: the routes, the endpoint JSON shapes, and the strings asserted by existing tests. | superpowers, engineering, security-audit, ui-review |
| L1 | **Backend** (section 4). Write `ListeningIntegrationTests` first (failing), then the code. | api-endpoint, feature-scaffold, security-audit |
| L2 | **`listening-core.js`** + node tests (section 5) | engineering |
| L3 | **Kit scope extension** + `listening.css` foundation (canvas, hues, forced colours); both views switched to the new wrapper and stylesheets, with **minimal** markup, so the pages still work. Regression check of `/Sets*` and `/Practice*`. | ui-ux-pro-max, ui-component |
| L4 | **Index:** header, overview, toolbar/filters, level sections, lesson card partial; `listening-index.js` filters + "Xem thêm" (6.2–6.4) | ui-ux-pro-max, ui-component |
| L5 | **Index:** "Bài nghe của tôi", import and delete flows (6.5) | ui-component, api-endpoint |
| L6 | **Practice:** layout, header, player column, controls, steps, tabs shell, config JSON, guest notice, player adapter, shortcuts (6.6, 6.7, 6.11) | ui-ux-pro-max, ui-component |
| L7 | **Transcript tab:** normal mode, translation, active-line highlight, auto-scroll, mark done (6.8) | ui-component, ui-motion |
| L8 | **Dictation and fill-in modes** (6.8) | ui-component, ui-motion |
| L9 | **Vocabulary tab, quiz tab, AI quiz generation, completion state** (6.9, 6.10) | ui-component, ui-motion, security-audit |
| L10 | **Cleanup:** delete the old CSS (after grep), remove confirmed dead code (B16), docs, tracker, bug-fix log | engineering |
| L11 | **Full gate** on both pages, every state and role; final self-review against AGENTS.md, DESIGN.md and section 10 | ui-review, security-audit, engineering |

### 8.1 Phase gate (every phase, every item, in order)

| # | Check | Command / method | Pass when |
| --- | --- | --- | --- |
| G1 | Build | `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` | 0 errors; warnings ≤ L0 |
| G2 | Area tests | `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~Listening\|FullyQualifiedName~GoalsPhase5\|FullyQualifiedName~OpenAiConsumerPipeline\|FullyQualifiedName~CriticalFlow"` | 0 failed |
| G3 | Full tests | `dotnet test TCTEnglish.Tests -c Release` | 0 failed; passed ≥ L0 (+ new tests); skipped = L0 |
| G4 | Node tests | `node --test scripts/tests/*.cjs` | 0 failed; count ≥ L0 |
| G5 | Contracts | Re-check the L0 hook inventory: routes, endpoint JSON shapes, asserted strings | Nothing removed or renamed |
| G6 | Tokens-only + diff | `git diff --check` + the grep below | Prints nothing |
| G7 | Scope | `git diff --name-only <previous commit>` | Only section 7 files; no forbidden file |
| G8 | Render | Each touched page at **1440×900, 1366×768, 1024, 768, 390, 320** and 200% zoom; roles: anonymous, Standard, Premium; screenshots to `.tmp/listening/<phase>/`; read the console and network | No horizontal scroll, clipping or overlap; no console errors; no failed CSS/JS; Clay canvas covers the page; header/sidebar unchanged |
| G9 | Design match | Compare with section 6 and with the finished `/Sets` and `/Practice` pages (same kit look) | Matches the spec; differences recorded |
| G10 | Behaviour | The phase checklist (8.4) with mouse **and** keyboard only | All items pass |
| G11 | Accessibility | Tab through every control; visible focus; one `<h1>`; labels on icon buttons; ARIA tabs work with arrow keys; live regions announce; states visible in `forced-colors: active`; reduced motion removes motion; no keyboard trap | All pass |
| G12 | Contrast | Script the computed colours of new text/control pairs | Text ≥ 4.5:1 (large ≥ 3:1); boundaries/icons ≥ 3:1 |
| G13 | Regression | `/Sets`, `/Sets/{id}`, `/Practice`, one practice mode, `/Goals`, `/Speaking`, `/Home/Index`; an AI chat answer that links to `/Home/Listening` | Unchanged from L0 |
| G14 | Clay conformance | The grep below | Every `box-shadow` uses a `--tct-clay-*` / `--tct-shadow-*` token, `none`, or a token ring; no hard-coded radius px except `50%` |
| G15 | Security | Re-run the B2/B3/B8 tests; grep the listening views and JS for `innerHTML`, `alert(`, `confirm(`, `Html.Raw` (outside the JSON config), inline `on*=` handlers and `console.log` | Tests green; grep prints nothing unexpected |

**G6 / G14 / G15 greps:**

```powershell
$css = 'TCTEnglish/wwwroot/css/listening.css'
Select-String -Path $css -Pattern '#[0-9a-fA-F]{3,8}\b|rgba?\(|:root|!important|font-weight:\s*(8|9)00'
Select-String -Path $css -Pattern 'box-shadow:' | Where-Object { $_.Line -notmatch 'var\(--tct-(clay-|shadow-)|box-shadow:\s*none' }
Select-String -Path $css -Pattern 'border-radius:\s*\d+px'
Select-String -Path TCTEnglish/wwwroot/js/listening-*.js,TCTEnglish/Views/Study/Listening*.cshtml,TCTEnglish/Views/Study/_Listening*.cshtml -Pattern 'innerHTML|alert\(|confirm\(|console\.log|\son[a-z]+='
```

L0 runs G1, G3, G4 and G8 (before-screenshots) and records the baselines. L1 and L2 run G1–G7 and G15 (no UI yet). L11 runs everything on every page, role and state.

### 8.2 When the gate fails

- Fix the cause, then re-run the **whole** gate.
- Up to 3 attempts per failing item. Then:
  - **(a) Caused by this phase:** revert that element to its previous state, mark it "deferred" with the reason, and re-run the gate.
  - **(b) Outside scope** (an existing bug elsewhere, or the app/browser unavailable): record the evidence and mark it "blocked". Continue only when every in-scope item is green.
- Never skip, weaken, delete or edit an existing test.
- Never edit a forbidden file.
- Never record a skipped or blocked check as passed.
- **Stop the run** only if:
  - a green build or tests needs a forbidden file; or
  - the base state differs from this plan in a way that makes it unsafe.
  Finish the gate record and the report, then stop.
- If the session is interrupted, resume at the first phase without a gate record in section 12.

### 8.3 Gate record (write into section 12 before each commit)

```text
### Lk — <name>
- Commit: <hash>
- G1 build: pass (warnings n, L0 n)
- G2 area: passed x / failed 0 / skipped y
- G3 full: passed x / failed 0 / skipped y (L0 …)
- G4 node: passed x / failed 0
- G5 contracts: unchanged
- G6 tokens/diff: clean
- G7 scope: files …
- G8 render: pages × sizes × roles, .tmp/listening/Lk/
- G9 design: differences …
- G10 behaviour: items 1..n pass
- G11 a11y: …
- G12 contrast: pairs + ratios
- G13 regression: pass
- G14 clay grep: clean / justified …
- G15 security: tests + grep
- Attempts / deferred / blocked: …
```

### 8.4 Behaviour checklists (G10)

| Phase | Items |
| --- | --- |
| L1 | Via the test suite and manual `curl`-style checks with the old UI still in place: quiz evaluate and progress save still work for a public lesson; the old pages still render |
| L3 | Both listening pages load and work with the old scripts; `/Sets*` and `/Practice*` look identical to L0 |
| L4 | Level, status and topic filters, alone and combined; search with and without Vietnamese accents; URL updates and survives reload; `?level=B1&topic=…` deep link preselects; "Xem thêm" per level; empty-result state and "Xóa bộ lọc"; card status and progress match the database for a seeded user; overview numbers and "Nghe tiếp"; anonymous notice; broken thumbnail falls back |
| L5 | Standard user sees the locked panel (no form); Premium import: invalid URL message, duplicate video message, success navigates to the new lesson, long-running status visible (stub AI); delete: cancel, confirm, last-card empty state, focus return; locked private card for a downgraded user |
| L6 | YouTube lesson and audio lesson: play/pause, ±5 s, speed (Premium) and the locked speed link (Standard), loop line; shortcuts work and are ignored inside inputs; tabs with mouse, arrow keys, Home/End, and `#hash` deep links; steps jump to tabs; sticky player at 1366×768 and at 390; guest notice; YouTube API failure notice (block the script in DevTools) |
| L7 | Click a line to seek; active line follows playback; auto-scroll on/off and manual-scroll pause; show/hide translation; hide text and reveal a line; AI translate success and failure (stub); "Đánh dấu đã nghe xong" persists after reload; lines without timings |
| L8 | Dictation: correct, near, wrong, "Xem đáp án", Enter / Shift+Enter / Alt+R, **Tab leaves the textarea**, ring and summary, retry. Fill-in: Enter moves on, check all, wrong shows the answer, retry gives new blanks, lines without blanks |
| L9 | Vocabulary: speak, image fallback, mark done persists; lesson without vocab has no tab or step. Quiz: unanswered guard, submit, per-question feedback and explanation as text, best score kept after a worse retry, reload shows the stored score, guest notice, AI generate (stub) success/failure. **Completion:** a seeded user finishes all required sections → the done banner appears, the index shows "Đã xong", and `/Goals` shows the Listening signal increased by exactly one |
| L11 | Everything above, end to end, on a fresh browser profile, at 1366×768 first, for anonymous, Standard and Premium |

---

## 9. Verification summary (L11)

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/*.cjs
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext   # must report no changes
git diff --check
```

- Plus the G6 / G14 / G15 greps.
- **Browser:** `tct-ui-review` + `team-ui-workflow.md` §7, with synthetic accounts from `.appdata/local/access.json` (never print passwords).
- Screenshots stay in ignored `.tmp/listening/`.
- **AI:** use the stub provider in tests. Do not call a paid AI just to look at the UI. If a real key is not available, mark the live AI checks "not verified".

## 10. Definition of done

- [ ] B1–B16 are fixed or recorded as not reproducible; each fix has a test or a recorded browser check.
- [ ] Completion works end to end from the UI and credits Goals exactly once.
- [ ] No lesson content or answers are reachable for a lesson the user may not use.
- [ ] Both pages follow section 6, use the shared Clay language, and match `/Sets` and `/Practice` in look.
- [ ] Routes and endpoint contracts are unchanged; no migration; no forbidden file touched.
- [ ] `listening-core.js` is pure and covered by node tests; the UI JS has no `innerHTML` with data, `alert`, `confirm` or `console.log`.
- [ ] Tokens-only and Clay greps are clean; forced colours, reduced motion, keyboard and contrast are verified and recorded.
- [ ] All tests are green at or above the L0 counts; existing tests are unedited.
- [ ] Gate records for L0–L11 are complete; the tracker row and docs are updated; the Vietnamese report separates verified from not verified.

## 11. Risks

| Risk | Mitigation |
| --- | --- |
| The new completion rule credits Goals for old progress rows on the next save | Intended (those lessons were finished). The credit fires once per lesson. Record it in the report. |
| Extending the kit scope changes `/Sets` or `/Practice` | The change is selector lists only; G13 compares screenshots. |
| YouTube iframe API unavailable (network, blockers) | Fallback notice (6.7); the transcript and quiz stay usable. |
| Sticky player covers content on small screens | G8 at 390 and 320; `max-height: 34vh`; disable sticky under 480px height if needed (record). |
| Deleting the old CSS breaks another view | Grep all `Views/**` and `Areas/**` first; keep the file if any other consumer exists and record it. |
| Admin-only quiz generation on public lessons surprises Premium users | The button is hidden for them (`CanUseAi`); the server message explains. |

## 12. Execution notes (agent fills in; one gate record per phase)

- Branch / HEAD at L0: `feature/listening-redesign`, created from the already-fetched `origin/master` = `d8c5773` (PR #135). Local `master` was 22 commits behind and lacked the Clay kit, and the tree held an owner edit in `TCTEnglish/appsettings.Development.json` plus untracked `Claude outputs/`, so no pull was run (the prompt allows a pull only on a clean tree). Branching from `origin/master` gives the required base without a pull or data sync; startup catches up shared data. Neither file is staged.
- Baseline counts (tests / node / warnings): `dotnet test -c Release` passed 1395 / failed 0 / skipped 29 (total 1424); node 229 pass / 0 fail; build 0 errors, 43 warnings.
- Findings confirmed (B1–B16 with file:line) or not reproducible:
  - B1 confirmed: `ListeningService.cs:363` needs Transcript && Quiz && Vocab; no view or script sends `vocabReviewed`.
  - B2 confirmed: `ListeningService.cs:266-313` has no access check (questions are loaded by lesson id only).
  - B3 confirmed: `ListeningService.cs:324-325` checks `IsPublished` only.
  - B4 confirmed: `GetIndexViewModelAsync` (`:55-165`) never reads `UserListeningProgresses`.
  - B5 confirmed: `ListeningService.cs:89-95`, `allTopics` filters `IsPublished` only.
  - B6 confirmed: `listening-practice.js:803` (explanation), `:905` (toast), `Listening.cshtml:386` (feedback), `ListeningPractice.cshtml:615` (`Html.Raw` of YoutubeId); more `innerHTML` in `listening-practice.js` (270-995) and `listening-index.js:157`.
  - B7 confirmed: `ListeningService.cs:474-477` awaits quiz generation under a "without blocking" comment.
  - B8 confirmed: `ListeningService.cs:524` blocks only other owners, so any Premium user can write to a public lesson.
  - B9 confirmed: `listening-practice.js:384`, `Tab` is `preventDefault`-ed in dictation.
  - B10 confirmed: `Listening.cshtml:428,454,459` (`confirm`/`alert`), inline script at `Listening.cshtml:338`, `console.log` at `ListeningPractice.cshtml:622` and in `listening-practice.js`, `alert` at `listening-practice.js:986,992`.
  - B11 confirmed: `Listening.cshtml:21` loads `speaking.css`; `placehold.co` at `Listening.cshtml:142,273`; own palettes in the two old CSS files.
  - B12 and B15 confirmed by reading the views and scripts (filters start on "all"; div-based mode selector; no fieldset).
  - B13 confirmed: `Listening.cshtml:35,56,303`.
  - B14 confirmed: `ListeningService.cs:353-354` assigns the score directly.
  - B16: `LP_QUIZ` is emitted at `ListeningPractice.cshtml:620` and read at `listening-practice.js:32`; whether `quizData` is used is re-checked at L10. `LevelMeta` icon/accent have no view consumer found so far (re-check at L10). No other view loads `listening-index.css` or `listening-practice.css`.
  - **B17 (new, not in the plan):** anonymous visitors get 404 on every public lesson. `GetPracticeViewModelAsync` (`ListeningService.cs:186`) tests `lesson.OwnerUserId == userId`; for a public lesson and a guest that is `null == null`, and the role defaults to Standard, so it returns null. Reproduced on the running app: anonymous `GET /Home/Listening/Practice/1` = 404, Standard and Premium = 200. Fixed in L1 by the L-R5 helper (the private-owner rule applies only when `OwnerUserId` has a value).
- Hook inventory (routes, JSON shapes, asserted strings):
  - Routes: `GET /Home/Listening?level&topic`, `GET /Home/Listening/Practice/{id}`, `POST /Home/Listening/EvaluateQuiz` (JSON `{lessonId, answers:{qid:"A"}}`), `POST SaveProgress/{lessonId}` (JSON `{transcriptCompleted?, quizCompleted?, quizScore?, vocabReviewed?}`), `POST My/Create` (form `youtubeUrl`), `POST My/Delete` (form `lessonId`), `POST My/GenerateQuiz` (form `lessonId`), `POST Translate/{lessonId}`. All POSTs are antiforgery-protected.
  - Responses: EvaluateQuiz 200 `{success:true, data:{lessonId,totalQuestions,correctCount,scorePercent,passed,answers[]}}`, 400/404 `{error}`; SaveProgress 200 `{success:true}`, 400/401/404 `{error}`; Create 200 `{success,lessonId}`, 400 `{success:false,message}`, 403 `{success:false,message,requiresUpgrade:true}`; Delete 200 `{success:true}`, 404, 403 `{success:false,message}`; GenerateQuiz and Translate 200 `{success:true}`, 400 `{success:false,message}`, 404.
  - Pinned by existing tests: `GoalsPhase5IntegrationTests` (SaveProgress contract with exactly one Listening credit; the Goals page lists `value="Listening"`; the index must not contain "Listening hiện đang ở trạng thái tạm hoãn trong hệ thống Goals/XP"), `OpenAiConsumerPipelineTests` (Admin quiz generation and translation on a public lesson), `OpenAiLiveFeatureAcceptanceTests`, `AiRagCorpusBm25Tests` (private lesson text never enters the corpus), `SharedDataLifecycleTests`.
  - The AI chat and RAG corpus link to `/Home/Listening`. Login route `/Account/Login`; Premium route `/Account/Premium`.
- Local environment: dev app started with `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5100` (Docker MariaDB, `/health/ready` = ready). The dev DB has one public lesson (id 1). The only console/network error on the old index is the thumbnail `img.youtube.com/vi/9F8o8eH_Hms/hqdefault.jpg` returning 404 (data, not code). Before-screenshots (anonymous, Standard, Premium × index and practice/1 × 1440×900, 1366×768, 390×844) are in ignored `.tmp/listening/L0/`. No horizontal overflow at any of those sizes.
- Design differences from section 6 and why:
- `color-mix` uses:
- Contrast results:
- Browser coverage / not verified (incl. live AI):
- Final counts:

### L0 — Baseline
- Commit: 851d989
- G1 build: pass (warnings 43, L0 43)
- G3 full: passed 1395 / failed 0 / skipped 29
- G4 node: passed 229 / failed 0
- G8 render: before-screenshots only (3 roles × 2 pages × 3 sizes), `.tmp/listening/L0/`
- Other gates: not applicable at L0 (no code change).

### L1 — Backend
- Commit: faebc47
- Work: tests first (`ListeningIntegrationTests`, 23 cases) went red with 13 failures (B2, B3, B5, B14, B17, B4, the new view-model fields); then the service. `GetUsableLessonAsync` is the single access rule for `GetPracticeViewModelAsync`, `EvaluateQuizAsync` and `SaveProgressAsync`; `IsLessonComplete` and `ApplyProgressAsync` implement L-R3/L-R4 and the single Goals credit; index loads progress in one query and adds status, steps, overview numbers and "continue" lesson; quiz generation on a public lesson is Admin-only (L-R6); the import comment is corrected (B7); the controller passes the user id to `EvaluateQuiz` and normalises `level`. Interface changes are additive (optional `userId` / `CancellationToken`).
- G1 build: pass (0 errors, warnings 43, L0 43; a first draft produced 44, fixed by dropping a needless `?.`)
- G2 area: passed 61 / failed 0 / skipped 0
- G3 full: passed 1418 / failed 0 / skipped 29 (L0 1395 / 29; +23 new)
- G4 node: passed 229 / failed 0
- G5 contracts: unchanged (URLs, verbs, antiforgery, JSON shapes; `EvaluateQuiz` gains `data.bestScorePercent`, `SaveProgress` still accepts `quizCompleted`/`quizScore`; the pinned `GoalsPhase5` replay test is green)
- G6 tokens/diff: `git diff --check` clean for every file of this phase (the only hit is the owner's unstaged blank line in `TCTEnglish/appsettings.Development.json`, never staged); CSS grep not applicable (no CSS yet)
- G7 scope: `Services/ListeningService.cs`, `Services/IListeningService.cs`, `ViewModels/ListeningViewModels.cs`, `Controllers/StudyController.cs` (Listening actions only), `TCTEnglish.Tests/ListeningIntegrationTests.cs`; no forbidden file
- G8–G9, G11–G14: not applicable at L1 (no UI change). G10: behaviour covered by the HTTP integration tests (public lesson evaluate/save, old pages render for guest, Standard and Premium); no manual run at this phase.
- G15 security: B2, B3, B8 and antiforgery tests green; no UI JS/Razor touched this phase.
- Attempts / deferred / blocked: none. Test runs regenerate `docs/design-system/email-preview/*` (line endings); those regenerated files were restored with `git checkout` and are not part of any commit.

### L2 — listening-core.js
- Commit: cd86058
- Work: new pure UMD module `wwwroot/js/listening-core.js` (`window.TctListening.core` in the browser) with the section 5 functions plus `mulberry32`, and 28 node tests in `scripts/tests/test_listening_core.cjs`.
- G1 build: pass (0 errors, warnings 43, L0 43)
- G2/G3 full: passed 1418 / failed 0 / skipped 29 (unchanged from L1)
- G4 node: passed 257 / failed 0 (L0 229, +28)
- G5 contracts: unchanged (no server or view change)
- G6 tokens/diff: `git diff --check` clean for the two files; no CSS yet
- G7 scope: `TCTEnglish/wwwroot/js/listening-core.js`, `scripts/tests/test_listening_core.cjs`
- G8–G14: not applicable at L2 (no UI). G15: grep for `innerHTML|alert(|confirm(|console.log|on*=` in the new file prints nothing; the purity test forbids `document`, `window`, `fetch` and `localStorage`.
- Decisions inside the spec (recorded as differences):
  - `gradeDictation` also returns `extra` (typed words that matched no answer word). A perfect score with extra words is "near", not "correct", so "I like the red apples" does not pass for "I like red apples". The plan's thresholds are otherwise unchanged.
  - `lineIndexAt` treats a range as `start <= time < end`, so a gap between two lines and the time after the last line's end return -1; the page keeps the previous highlight when it gets -1. A last line without `end` stays active.
  - `requiredSteps` returns keys in page order (`transcript`, `vocab`, `quiz`). `parseFilterQuery` takes an optional list of known topics. `pickBlanks` also returns `blanks` (the count).
- Attempts / deferred / blocked: none.

### L3 — Kit scope extension and listening.css foundation
- Commit: f981a58
- Work: `study-kit.css` scope lists gained `.tl-page` (142 `:is()` lists, the canvas rule and the six hue blocks with `.tl-hue-*`; comments updated). Proof that nothing else changed: undoing the scope extension mechanically gives a file byte-identical to the L2 kit. New `wwwroot/css/listening.css` (page frame, local thumbnail placeholder `.tl-thumb--empty`, forced-colours and reduced-motion blocks). Both views are wrapped in `tct-theme tct-page tl-page tl-page--index|practice` and load `study-kit.css` then `listening.css`.
- Transitional decision: the old markup, scripts and old CSS (`listening-index.css`, `listening-practice.css`, and `speaking.css` on the index) stay loaded inside the new wrapper until L4 (index) and L6 (practice) replace the markup; dropping them earlier would unstyle markup the old scripts still need. Both pages stop loading the old files in those phases; the files are deleted at L10.
- G1 build: pass (0 errors, 43 warnings = L0)
- G2/G3 full: passed 1418 / failed 0 / skipped 29
- G4 node: passed 257 / failed 0
- G5 contracts: unchanged (markup inside the wrapper is unchanged, ids/classes the old scripts use are intact)
- G6 tokens/diff: the three `listening.css` greps print nothing; `git diff --check` clean for the phase files
- G7 scope: `study-kit.css`, `listening.css`, `Views/Study/Listening.cshtml`, `Views/Study/ListeningPractice.cshtml`
- G8 render: anonymous, Standard, Premium × both pages × 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640 and 720×450 (= 200% zoom of 1440): per-element overflow 0 everywhere, no failed CSS/JS. Console/network: the only entry is the dev lesson thumbnail `img.youtube.com/.../hqdefault.jpg` 404 on the index (data, as at L0). The anonymous practice page now renders (B17). Screenshots: `.tmp/listening/L3/`.
- G9 design: transitional (old look inside the Clay canvas); the full spec match starts at L4/L6.
- G10 behaviour: both pages load; the old scripts' markup is untouched; guests open public lessons.
- G11 a11y / G12 contrast: no new text or controls this phase; checked from L4.
- G13 regression: `/Sets`, `/Practice`, `/Home/Index`, `/Vocabulary/Index` render with no console/network errors at 1440×900 (Premium). `/Goals` logs two `jQuery is not defined` errors from the jquery-validation scripts; no file of this phase is loaded by `/Goals` and no L0 baseline of that page was captured, so this is recorded as not caused by this phase and not compared with L0. `/Speaking` and a pixel comparison of `/Sets*` and `/Practice*` were not run; the kit change is provably selector-list-only (see above).
- G14 clay grep: clean (no shadow or radius literals in `listening.css`).
- G15 security: nothing new (no JS or data path changed).
- Attempts / deferred / blocked: none.

### L4 — Index: overview, filters, level sections, cards
- Commit: 96622cb
- Work: `Listening.cshtml` rewritten (page header, signed-in overview with "Tiếp tục nghe", guest notice, toolbar with search/level/status/topic filters and a live count, level sections, empty states, "Bài nghe của tôi" markup, delete modal markup, JSON config); new partials `_ListeningLessonCard` and `_ListeningMyLessonCard`; `listening-index.js` rewritten (filters through `listening-core.js`, URL sync, "Xem thêm" per level with focus on the first new card, topic collapse above 8, thumbnail fallback); index styles added to `listening.css`; card view model gains `ThumbnailSrc` (https/local URL or a well-formed YouTube id, never an external placeholder) and `SpeakerText`; 5 new page-contract tests (`ListeningIntegrationTests` now 28).
- Decision: the controller now asks the service for every lesson and only uses `level`/`topic` as the initial selection (invalid values fall back to "all"). The plan's 4.7 passed the level to the service, which would leave no other level in the page to switch to client-side. The service filter parameters stay and are covered by a test.
- Decision: the import form and delete modal markup exist, but their scripts are L5 work, so the import and delete buttons do nothing in this commit (the old inline script was removed with the old view).
- G1 build: pass (0 errors, 43 warnings = L0)
- G2/G3 full: passed 1423 / failed 0 / skipped 29 (L0 1395 / 29; +28 Listening tests)
- G4 node: passed 257 / failed 0
- G5 contracts: unchanged (no endpoint, JSON shape or route touched)
- G6 tokens/diff: the three `listening.css` greps print nothing; `git diff --check` clean for phase files
- G7 scope: `Listening.cshtml`, `_ListeningLessonCard.cshtml`, `_ListeningMyLessonCard.cshtml`, `listening.css`, `listening-index.js`, `ListeningViewModels.cs`, `StudyController.cs` (Listening action), `ListeningIntegrationTests.cs`
- G8 render: anonymous, Standard, Premium × 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640, 720×450 (200% zoom): per-element overflow 0 in all 21 combinations; the only console/network entry is the dev lesson's thumbnail URL returning 404 (data). Screenshots: `.tmp/listening/L4/`.
- G9 design: matches plan 6.2–6.4 and the `/Sets` overview pattern. Differences: the level and status filters are `div[role=group]` of buttons with `aria-pressed` instead of `nav` (they filter, they do not navigate); the dev DB holds one lesson, so multi-lesson layouts were checked on a patched copy of the served HTML (below).
- G10 behaviour (mouse and keyboard, scripted over a document patched to 15 lessons in A1/B1/B2 with 11 topics; Standard and Premium): level, status and topic filters alone and combined; search with and without accents (cuoc song = cuộc sống); URL updates and survives reload; deep link `?level=b2&topic=Nope&status=zzz` preselects B2 and ignores the rest; "Xem thêm" per level (6 then 8) with focus on the first new card; empty result and "Xóa bộ lọc" (focus returns to the search field); topic collapse/expand; Space and Enter on filter buttons; a broken thumbnail is replaced by the local placeholder; card status and steps match the database for a seeded user (integration tests); overview numbers, "Tiếp tục nghe" and the anonymous notice (integration tests). The live "Tiếp tục nghe" and quiz-score rendering with real progress are re-checked at L9.
- G11 a11y: one h1; accessible names on every control; groups named; `aria-pressed`; polite live count; 16 Tab stops each with a visible focus indicator; no trap; forced-colors (cards keep a border, the pressed filter has a thicker border); reduced motion (transitions ~0s). Scripts: `a11y-index` (kept outside the repo).
- G12 contrast (computed, anonymous/Standard/Premium): page title 15.45, description 8.41, overview title 13.18, stat number 4.23 (24px bold = large text, needs 3), stat label 7.17, hint 7.17, section title 15.45, import label 17.1, hint 9.31, count 8.41, pressed filter 17.1, unpressed 8.30, level description 8.41, card title 17.1, card meta 9.31, status badge 6.84, level count badge 6.84, guest text 17.1, locked text 17.1. Not rendered with the dev data (checked at L9): continue block, duration pill, speakers, quiz score, card action.
- G13 regression: `/Sets`, `/Practice`, `/Home/Index` and `/Home/Listening/Practice/1` render with no console/network errors (Premium, 1440×900). `/Speaking` logs one `http 404` for a resource that is not loaded by any file of this module and was not captured at L0, so it is recorded as not compared. The AI-chat answer that links to `/Home/Listening` was not run (it needs a live or stubbed chat session); the link target still exists.
- G14 clay grep: clean.
- G15 security: grep for `innerHTML`, `alert(`, `confirm(`, `console.log`, inline `on*=` in the index JS and views prints nothing; `Html.Raw` appears only for the JSON config script; B2/B3/B8 tests green; all text goes through `textContent` or Razor encoding.
- Attempts / deferred / blocked: none. A first run of the behaviour script failed two items: a test-harness Enter key without a text payload, and SignalR websocket noise caused by the request interception; both were harness issues and the rerun passed.

### L5 — Index: "Bài nghe của tôi", import and delete
- Commit: 9095e0a
- Work: `listening-index.js` gained the import flow (client check with the new `isYoutubeUrl` in `listening-core.js`, locked form with the honest long-running status, success navigation, 403 `requiresUpgrade` with an upgrade link, fallback text that keeps the typed URL) and the delete flow (Bootstrap modal `#tlDeleteLesson`, POST of `lessonId` only, card removal, live count, toast, empty state on the last card, focus to the section heading). The page code restores focus to the delete button itself after the modal closes, because Bootstrap only does that for data-api triggers. Server text is always written with `textContent`. Core: `isYoutubeUrl` plus 1 node test (core suite is now 29).
- G1 build: pass (0 errors, 43 warnings = L0)
- G2/G3 full: passed 1423 / failed 0 / skipped 29 (unchanged; no server change this phase)
- G4 node: passed 258 / failed 0 (L0 229, +29)
- G5 contracts: unchanged (the scripts send the same form fields: `youtubeUrl`, `lessonId`, with the antiforgery header)
- G6 tokens/diff: clean. G7 scope: `listening-index.js`, `listening-core.js`, `test_listening_core.cjs`.
- G8 render: no markup or CSS change since L4 (L4 sweep applies); the screenshots of the two lesson cards before delete and the empty state after are in `.tmp/listening/L5/`.
- G9 design: matches plan 6.5 (copy quoted exactly).
- G10 behaviour (scripted in headless Chrome as Premium against the running app, 2 synthetic private lessons inserted for the Premium test account and removed again; row count back to 0, the owner's real account untouched): invalid link shows "Link YouTube không hợp lệ." and sends no request; a duplicate video returns the real server message "Video này đã có trong mục Bài nghe của tôi." with the form usable and the URL kept; long-running status text and locked form while the request is pending (server answer mocked with a 1.5 s delay); success navigates to the lesson; 403 `requiresUpgrade` shows the message and the "Nâng cấp Premium" link to `/Account/Premium`; other failure shows the fallback text and keeps the URL; delete modal opens with the exact title and body copy; a failed delete (mocked 500) closes the modal, shows the danger toast and keeps the card; cancel keeps the card; Enter opens the modal, Escape closes it, focus returns to the delete button; real delete removes the card, updates the count, shows the toast, deletes the DB row and focuses the heading; deleting the last card shows the empty state with no reload. The Standard locked panel and the downgraded owner's locked card are covered by integration tests. Not run: a real YouTube import (needs outbound network and an AI quiz call); the success path used a mocked server answer.
- G11 a11y: the form field has a visible label, `aria-describedby`, `aria-invalid` on error, an `role="alert"` error line and a polite status region; the modal is labelled by its title and traps focus (Bootstrap); no keyboard trap (Escape works).
- G12 contrast: import error 6.46, status 9.31, hint 9.31 (all ≥ 4.5).
- G13 regression: no change outside the two listening scripts. G14: clean (no CSS change).
- G15 security: grep for `innerHTML`, `alert(`, `confirm(`, `console.log`, inline handlers prints nothing in the listening scripts and views; the old `confirm`/`alert` calls are gone (B10); server messages go through `textContent` (B6).
- Attempts / deferred / blocked: 3 failures on the first run were fixed: focus restore after the modal (code fix) and a harness regex and a hard-coded lesson id (script fixes).

### L6 — Practice: layout, player, controls, steps, tabs, config, shortcuts
- Commit: f064d5d
- Work: `ListeningPractice.cshtml` rewritten (shared page header with breadcrumb, meta line, guest notice, player column, steps, shortcut hints, ARIA tabs, panels for transcript, vocabulary and quiz, JSON config, no inline logic); `listening-practice.js` rewritten (player adapter for the YouTube IFrame API or `<audio>` with an 8 s failure notice, play/±5 s/loop/speed controls, steps and tab checkmarks, roving-tabindex tabs with `#loi-thoai`/`#tu-vung`/`#quiz`, keyboard shortcuts that ignore inputs and focused controls, `saveProgress` through `TctSets.fetchJson` with one retry and one toast per visit); practice styles added to `listening.css`; the practice view model gains `SafeYoutubeId` and `SafeAudioUrl` (malformed ids and non-https/non-local URLs are never rendered); 4 new page-contract and hostile-text tests (`ListeningIntegrationTests` now 32). The transcript, dictation, fill-in, vocabulary and quiz panels are server-rendered but their scripts land in L7–L9.
- Decision: the stage column dissolves into the grid below 1200 px so only the player card is sticky on tablets and phones (a sticky stage would carry the steps panel along); at 768 px and below the video is capped at 34vh.
- Decision: the kit highlights a selected segment through `aria-pressed`/`aria-current`/`:checked`, not `aria-selected`; the selected-tab style and its forced-colours border live in `listening.css` (the kit stays untouched).
- G1 build: pass (0 errors, 43 warnings = L0)
- G2/G3 full: passed 1427 / failed 0 / skipped 29 (+4 over L4)
- G4 node: passed 258 / failed 0
- G5 contracts: unchanged (same POST targets; the page now sends `quizCompleted`/`quizScore` never, as L-R4 requires — the quiz sends happen in L9)
- G6 tokens/diff: the three `listening.css` greps print nothing; `git diff --check` clean for phase files
- G7 scope: `ListeningPractice.cshtml`, `listening-practice.js`, `listening.css`, `ListeningViewModels.cs`, `ListeningIntegrationTests.cs`
- G8 render: Premium, Standard and anonymous × `/Home/Listening/Practice/1` (public lesson) and the synthetic private lesson (only the Premium owner sees it) × 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640, 720×450: per-element overflow 0 in all 42 runs; the console/network entries are the synthetic lesson's test-audio URL, which only the behaviour script serves. At 1366×768 the player, controls and the first transcript line are visible without scrolling. Screenshots: `.tmp/listening/L6/`.
- G9 design: matches plan 6.6–6.8 shell. The transcript lines are unstyled raw lists until L7.
- G10 behaviour (scripted, real mouse and key events, Premium audio lesson and YouTube lesson, Standard, guest): tabs by mouse, ArrowLeft/Right with wrap, Home/End, roving tabindex, `#hash` deep links and an unknown hash; step buttons jump to and focus their tab; play/pause with the label and icon switching; ±5 s; back never below 0; Premium speeds 0.75×/1.5×; loop toggle; Space plays without scrolling, ignored with Ctrl and on a focused control; L toggles loop; YouTube lesson without vocab or quiz shows only the transcript step; the YouTube IFrame API loaded in this environment, and with its script blocked the exact failure notice appears after 8 s while the tabs keep working; Standard sees the fixed "Tốc độ 1×" and the Premium link; the guest notice has the exact copy and a login link; the stage is sticky at ≥1200 and the player card is sticky at 390 (top 72 px = header height, 270 px tall). Not run: the AI launcher offsets at 390 against the sticky player were only checked by screenshot (`.tmp/listening/L6/sticky-390-scrolled.png`).
- G11 a11y: one h1; named radiogroups and toolbar; tab/panel wiring (`aria-controls`/`aria-labelledby`); `aria-pressed` toggles; 25 Tab stops on the Premium page with a visible focus indicator on every one except the native `<audio>` element, which draws its own focus UI (a `:focus-visible` outline rule was added too); no trap; forced-colors (cards keep a border, the selected tab has a 2 px border); reduced motion (transitions ~0).
- G12 contrast (computed): page title 15.45, meta 8.41, level badge 6.84, steps title 17.1, step label 17.1, step icon (not done) 5.87 (non-text, needs 3), shortcut hints 8.41, selected tab 17.1, other tab 8.30, tab count 5.24, mode label 17.1, toggle 7.65, fixed speed 9.31, unlock link 7.65, guest text 17.1, player notice 7.09.
- G13 regression: unchanged pages not touched this phase; the index and the other study pages were rendered at L4/L5.
- G14 clay grep: clean. G15 security: greps clean (`Html.Raw` only for the two JSON configs); the hostile-text test proves title, speaker, line, translation, vocab and URLs are encoded or dropped and that the JSON config cannot close its script (B6: the old `Html.Raw` of `YoutubeId` is gone).
- Attempts / deferred / blocked: harness-only fixes on the first run (an unknown hash on a same-document navigation, a scroll offset, expected ABORTED/NAME_NOT_RESOLVED entries from the synthetic fixture); the selected-tab styling gap was a real finding and is fixed.
- Synthetic data: two private lessons for the Premium test account (`ZZ-FIX-AUDIO`, `ZZ-FIX-YT`) are in the dev DB for the L6–L9 checks and are removed at L9.

### L7 — Transcript tab, normal mode
- Commit: 21e9948
- Work: `listening-practice.js` gained click-to-seek (seek and play), the active line (`lineIndexAt` in a `requestAnimationFrame` loop while playing; class plus `aria-current`), auto-scroll that yields to the learner's own wheel/touch/key scrolling for 3 s and is remembered in `TctSets.safeStorage`, the ←/→/R shortcuts (previous, next, replay a line), line looping, "Hiện bản dịch", "Ẩn lời thoại" with a per-line "Bấm để hiện", "Dịch bằng AI" (pending, success toast then reload with the translations switched on, failure toast with the server message), the mode switch (panels for the two practice modes are filled in L8) and "Đánh dấu đã nghe xong" (step, tab checkmark, progress save; guests update the page only). The line list is a labelled, focusable scroll region (`role="region"`, `tabindex="0"`). Transcript styles added to `listening.css`.
- G1 build: pass (0 errors, 43 warnings = L0). G2/G3 full: passed 1427 / failed 0 / skipped 29. G4 node: passed 258 / failed 0. G5 contracts: unchanged (Translate and SaveProgress are called as before).
- G6 tokens/diff: clean. G7 scope: `ListeningPractice.cshtml`, `listening-practice.js`, `listening.css`.
- G8 render: 1366×768, 390×844 and 1024×768 with an active line and translations on, per-element overflow 0 (`.tmp/listening/L7/`); the earlier L6 sweep (3 roles × 7 sizes) covers the unchanged shell.
- G9 design: matches plan 6.8 normal mode (speaker badge, English at the large text size, muted Vietnamese, tabular time, a 3 px primary start border on the active line). A fixed 4.5 rem speaker column keeps the text aligned across rows.
- G10 behaviour (scripted against the running app, synthetic audio lesson with 8 lines, one untimed, four translated): untimed line has no seek button or time; click a line seeks to its start and plays; the active line follows playback and `aria-current` moves with it; ←, → and R; loop returns to the line start; auto-scroll keeps the active line inside the list, stays put for 3 s after a manual scroll, and the off state survives a reload; translation toggle (4 lines) and the mask toggle with per-line reveal and focus to the line's seek button; AI translate failure toast (server message), the pending label and disabled button, and the success path with the translations on after the reload (server answers mocked, no AI call was made); mark done disables the button, checks the step and tab, sends exactly one `transcriptCompleted` POST and leaves the lesson incomplete (vocab and quiz remain); a failing save is retried once, shows one toast and keeps the done state; a guest's mark done sends no request.
- G11 a11y: 26 Tab stops, each with a visible focus indicator (a seek row shows its ring on the row), no trap; the line list is a named focusable region; masked lines keep their "Nghe từ câu n" name; the active line is announced through `aria-current`; forced-colours gives the active line a 5 px start border.
- G12 contrast (computed): speaker badge 6.84 / 5.15 (info), translation 9.31 (8.41 on the active tint), time 9.31 (8.41), reveal link 7.65, mask dots 5.87 (decorative), mark-done button 5.28, English 17.1 (15.45 on the active tint).
- G13 regression: nothing outside the practice page changed. G14 clay grep: clean. G15 security: greps clean; line text, speaker and translation are written by the server as encoded text (hostile-text test from L6) and the scripts only read them with `textContent`.
- Attempts / deferred / blocked: none (all scripted checks passed on the first run).

### L8 — Dictation and fill-in modes
- Commit: (hash filled in at the L9 commit)
- Work: `listening-practice.js` builds both modes on first use with `createElement`/`textContent` only. Dictation (B9 fixed): one card per line with the number, speaker, "Nghe câu này" (plays that line and stops at its end), a textarea with a hidden label and the typing attributes, "Kiểm tra" and "Xem đáp án"; Enter checks, Shift+Enter breaks the line, Alt+R replays the line, and Tab is not handled, so focus moves on normally. Feedback from `gradeDictation` (title with icon, the answer words with dotted/solid underlines, a text legend, the correct sentence) in a polite live region; a conic-gradient progress ring with an accessible name; a summary with "Làm lại". Fill-in: blanks from `pickBlanks` seeded with the lesson id plus a retry counter, inline inputs sized to the answer, Enter checks a blank and moves to the next, "Kiểm tra tất cả", "Làm lại" (new blanks), a result icon plus hidden text per blank and the answer under wrong ones, a summary. State lives in memory only. Styles added to `listening.css`.
- Difference from the plan: both summaries show three tiles, "Đúng", "Gần đúng" and "Sai" (the plan listed two for fill-in); a one-letter slip is its own result in both modes, so counting it as wrong or right would hide it.
- Decision: the summary row is left-aligned so its "Làm lại" button does not sit under the AI launcher at the lower right.
- G1 build: pass (0 errors, 43 warnings = L0). G2/G3 full: passed 1427 / failed 0 / skipped 29. G4 node: passed 258 / failed 0. G5 contracts: unchanged (no request is sent by these modes).
- G6 tokens/diff: clean. G7 scope: `listening-practice.js`, `listening.css`.
- G8 render: Premium, dictation with four checked lines and fill-in after "Kiểm tra tất cả", at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640 and 720×450: per-element overflow 0 in all 14 runs. Screenshots: `.tmp/listening/L8/`. The role sweep of the page shell is the L6 one (the modes are identical for every role that can open the lesson).
- G9 design: matches plan 6.8 (rule text, ring, cards, feedback, summary).
- G10 behaviour (scripted, real mouse and key events, synthetic audio lesson): panel switching; exact answer ignoring case and punctuation = "Chính xác!"; a one-letter slip = "Gần đúng" with one dotted word and the legend; missing words = "Chưa đúng" with five missing words underlined and the correct sentence; "Xem đáp án" counts as wrong; the ring reads 13% after one line and 100% at the end and always announces itself; summary tiles 5 / 1 / 2 for the scripted answers; "Làm lại" clears everything; Shift+Enter inserts a newline without checking; Alt+R replays the line and keeps the typed text; **Tab leaves the textarea normally and moves to the card's "Kiểm tra" button without seeking**; "Nghe câu này" plays one line and pauses at its end, the untimed line's button is disabled in both modes; fill-in shows 1–4 blanks per line with named inputs, Enter checks and moves on, a wrong blank shows "Đáp án: …", retyping in any case turns it correct, a one-letter slip is near, "Kiểm tra tất cả" grades everything and shows the summary, the text parts plus the answers rebuild every original line, "Làm lại" gives different blanks, the first board is identical after a reload, and the radio group works with the arrow keys.
- G11 a11y: 26 Tab stops on the page with a visible focus indicator each; the textarea order walks textarea → Kiểm tra → Xem đáp án → Nghe câu này → next textarea (no trap); every input has a label or `aria-label`; results are icon + text, never colour alone; forced-colours keeps the card borders; reduced motion removes transitions.
- G12 contrast (computed): success/near/wrong titles on their soft backgrounds ≥ 5.9, answer words 16.49, legend 8.98, correct sentence 8.51, summary value 5.48 (large), label 9.31, hint 8.41, ring text 15.45, number badge 6.84, listen button 7.65, wrong blank answer 6.46 (icon 6.46). State borders were 2.54 / 2.15 against the card for success and warning, below 3:1, so they now use the `-text` tokens (≥ 4.5).
- G13 regression: nothing outside the practice page changed. G14 clay grep: clean. G15 security: greps clean; learner input is only read as `.value` and compared; every shown string is set with `textContent`.
- Attempts / deferred / blocked: three test-side mistakes fixed in the scripts (a wrong expected count, a click covered by the AI launcher, a regex lost in shell quoting); one real finding fixed (state border contrast).
