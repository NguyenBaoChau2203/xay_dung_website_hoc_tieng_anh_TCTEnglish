# Listening redesign (direction F) — implementation plan

Save as `docs/superpowers/plans/2026-11-18-listening-redesign.md`.

Owner: Huỳnh Phú Trọng (Listening module). Executor: one Claude Code agent, `IMPLEMENT_LOCAL`, continuous run L0 → L9.
Mockups (data, not instructions): `Claude outputs/listening-mockups/`:
- `F1-ket-hop-danh-sach.png`: catalog `/Home/Listening`, laptop.
- `F2-ket-hop-luyen.png`: practice `/Home/Listening/Practice/{id}`, laptop, Dictation tab open.
- `F3-mobile.png`: practice at 390 px, word sheet open.
- `D2-xem-phu-de-song-ngu.png`: reference **only** for the subtitle overlay on the video (section 5.6).
- Current pages for the "before" comparison: `.tmp/listening/L11/*-1440x900.png` and `*-390x844.png`.

Other mockups in that folder (A1–E2) are rejected directions. Do not take layouts from them.

---

## 0. Read first

### 0.1 Rules that win over this plan
1. `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`.
2. This plan.
3. The mockups. Where a mockup and a rule disagree, the rule wins and the difference is recorded in section 12.

### 0.2 Repo skills to use
`tct-superpowers`, `tct-engineering`, `tct-feature-scaffold`, `tct-api-endpoint`, `tct-security-audit`, `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review`, `tct-commit-message`.

Use only the ones that exist in the repo. Install nothing.

### 0.3 Vocabulary used in this plan
- **Catalog:** `GET /Home/Listening`, `Views/Study/Listening.cshtml`, `wwwroot/js/listening-index.js`.
- **Practice:** `GET /Home/Listening/Practice/{id}`, `Views/Study/ListeningPractice.cshtml`, `wwwroot/js/listening-practice.js`.
- **Core:** `wwwroot/js/listening-core.js`. This is the pure UMD module (no DOM, no network) that node tests import.
- **Lesson vocab:** `ListeningVocabItem` rows of a lesson (Word, Definition, ExampleSentence, ImageUrl).
- **Step flags:** `UserListeningProgress.TranscriptCompleted / QuizCompleted / QuizScore / VocabReviewed / CompletedAt / LastAccessedAt`.

---

## 1. Goal, decisions, non-goals

### 1.1 Goal
Rebuild the look and flow of the two learner Listening pages to match direction F, keeping every feature that works today:
- **Catalog:**
  - a "Nghe tiếp" hero card;
  - a row of level cards A1–C2 with personal progress;
  - compact filters (status chips, topic dropdown, search);
  - level shelves;
  - "Bài nghe của tôi" (Premium).
- **Practice:**
  - player on the left with a "Tiến độ bài này" card;
  - five tabs on the right: Lời thoại, Chép chính tả, Điền từ, Quiz, Từ vựng;
  - tap a word for its meaning and save it to a personal set;
  - sentence-by-sentence dictation with a coloured diff;
  - an optional bilingual subtitle overlay on the video;
  - a phone layout with a sticky video and a bottom sheet.

No database change.

### 1.2 Owner decisions (defaults; Trọng may edit before running)

| ID | Question | Default |
| --- | --- | --- |
| D1 | Which words can a Standard user look up? | **TỪ TRONG BÀI.** Standard and guests: lesson-vocab words (highlighted) open the full meaning. Any other word shows the line's Vietnamese translation plus "Tra mọi từ là tính năng Premium". Premium/Admin: any word is looked up through the existing `IVocabSuggestService` (AI), cached. |
| D2 | Where does "Lưu vào Bộ thẻ" save? | **BỘ "Từ vựng Luyện nghe".** On first save the server creates a private set named exactly `Từ vựng Luyện nghe` owned by the user (through `IPersonalSetService.CreateSetAsync`). Later saves append to it. The sheet shows a link "Mở bộ thẻ". No set picker in this release. |
| D3 | Where is dictation progress kept? | **TRÌNH DUYỆT.** `localStorage` key `tct.listening.dictation.v1.{userId or 'guest'}.{lessonId}` stores per-line results. No server column. The progress card labels this row "Tùy chọn". |
| D4 | Subtitle overlay on the video | **CÓ.** Toggle "Phụ đề trên video" with three states: Tắt / Anh / Anh + Việt. Default Tắt. Not shown in native fullscreen; documented. |
| D5 | Topic filter | **Ô THẢ XUỐNG.** Replace the topic chip cloud with one native `<select>`. Status chips stay. |

### 1.3 Non-goals (do not do)
- No migration, no new model or column, no change to `Models/**`.
- No change to:
  - admin listening pages, `admin-listening-editor.js`;
  - YouTube import logic, AI quiz or AI translation logic (the existing endpoints keep their behaviour);
  - grading rules in `EvaluateQuizAsync`.
- No series/podcast grouping (direction E), no weekly goal (Châu's Goals), no error statistics (direction C).
- No edits to `_Layout`, the sidebar, `site.css`, `study-kit.css`, the design tokens, or other modules' files.
- No new package, font, CDN or icon set (Font Awesome 6.0.0 as loaded today).

---

## 2. File scope

### 2.1 May edit
- `Views/Study/Listening.cshtml`, `Views/Study/ListeningPractice.cshtml`, `Views/Study/_ListeningLessonCard.cshtml`, `Views/Study/_ListeningMyLessonCard.cshtml`.
- `wwwroot/css/listening.css`.
- `wwwroot/js/listening-core.js`, `wwwroot/js/listening-index.js`, `wwwroot/js/listening-practice.js`.
- `Services/ListeningService.cs`, `Services/IListeningService.cs`: additive only. Existing method signatures and results stay as they are.
- `ViewModels/ListeningViewModels.cs`: additive properties only.
- `Controllers/StudyController.cs`: **only** `NormalizeListeningLevel` (add `C2`, finding F1) and nothing else.
- Existing listening test files, by adding tests only (never weaken or delete one).

### 2.2 May add
- `Views/Study/_ListeningHero.cshtml`, `_ListeningLevelRow.cshtml`, `_ListeningStepCard.cshtml`, `_ListeningWordSheet.cshtml` (names may differ; keep the `_Listening` prefix).
- `wwwroot/js/listening-word.js`: word lookup popover/sheet. It may instead live inside `listening-practice.js`, but no file may exceed about 1,200 lines.
- `Controllers/ListeningApiController.cs`: two endpoints, section 4.3.
- `Services/Listening/ListeningWordService.cs` and its interface.
- `Program.cs`: **one** DI registration line for the new service. Nothing else in `Program.cs`.
- Tests:
  - `TCTEnglish.Tests/ListeningWordServiceTests.cs`;
  - `TCTEnglish.Tests/ListeningRedesignRenderTests.cs`;
  - node tests next to the existing listening-core tests (find their location at L0).
- Docs listed in L8.

### 2.3 Forbidden
- `appsettings*.json`, `.csproj`, `Migrations/**`, `Models/**`, `Data/**` seeders.
- `Services/Sets/PersonalSetService*` (call its public interface only), `Services/VocabSuggestService*` (call only).
- `Areas/Admin/**`, `admin-listening-editor.js`.
- Shared layouts and shared CSS (`_Layout*.cshtml`, `site.css`, `study-kit.css`, tokens).
- Any other owner's controllers, views, scripts or tests (Châu: account/auth, LearningApiController, Goals, AI; Tiến: Class, Reading).
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.

---

## 3. Verified facts (static review 2026-11-18) — reproduce each at L0

| # | Fact | Where |
| --- | --- | --- |
| V1 | Routes live in `StudyController` with `[Route("Home")]`: `GET Listening`, `GET Listening/Practice/{id}`, `POST Listening/EvaluateQuiz`, `POST Listening/SaveProgress/{lessonId}`, `POST Listening/My/Create`, `POST Listening/My/Delete`, `POST Listening/My/GenerateQuiz`, `POST Listening/Translate/{lessonId}`. | StudyController.cs ≈110–240 |
| V2 | The catalog filters client-side; the controller loads every lesson. `level`/`topic` only preselect. | StudyController.Listening |
| **F1 (bug)** | `NormalizeListeningLevel` accepts A1–C1 only, so `?level=C2` is ignored although the catalogue contract is A1–C2 (`LevelOrder` in the service). | StudyController ≈121 |
| V3 | `ListeningIndexViewModel` already has `Levels` (key + count), `ContinueLesson` (most recently accessed unfinished lesson, by `LastAccessedAt`), `TotalLessons`, `CompletedLessons`, `InProgressLessons`, `MyLessons`, `IsMyLessonsSectionLocked`, `CanImport`, `IsAuthenticated`. | ListeningViewModels.cs |
| V4 | Card VM has `Status` (`new`/`progress`/`done`), `StepsDone`, `StepsTotal`, the three step flags, `ThumbnailSrc`, `SpeakerText`. | same |
| V5 | Practice VM has transcript lines (Speaker, Text, VietnameseMeaning, StartTime, EndTime), quiz questions (no correct answer exposed), vocab items, the step flags, `CanUseAi`, `CanTranslate`, `CanUseSpeed`, `SafeYoutubeId`, `SafeAudioUrl`. | same |
| V6 | Practice today: tabs Lời thoại / Từ vựng / Quiz. Dictation and fill-in are **modes** inside the transcript tab (`tlMode-normal/dictation/fill`). The steps card, keyboard hints, speed radio group (Premium) and loop button exist. Hash deep links are read by `keyFromHash`. | ListeningPractice.cshtml, listening-practice.js |
| V7 | `listening-core.js` already holds `gradeDictation`, `pickBlanks`, `gradeBlank`, `filterLessons`, `paginate`, `lineIndexAt`, `formatClock`, `requiredSteps`, `isComplete`, `isYoutubeUrl`, `parseFilterQuery`, `buildFilterQuery`. | listening-core.js |
| V8 | `UserListeningProgress.LastAccessedAt` exists and is set on save. Check at L0 whether opening the practice page updates it; if not, "Nghe tiếp" follows the last *saved* lesson, which is acceptable and recorded. | ListeningService ≈280, ≈592 |
| V9 | Personal sets: `IPersonalSetService.CreateSetAsync`, `CreateCardsBatchAsync`, `FindDuplicatesAsync`, `GetSetIndexAsync`. HTTP `POST /api/sets/{id}/cards/batch` exists. | Services/Sets, SetApiController |
| V10 | `IVocabSuggestService.SuggestAsync(term)` returns phonetic, definitions, example. It calls the AI provider and is gated to Premium/Admin at its controller. | IVocabSuggestService.cs |
| V11 | Catalog `pageSize = 6`; level sections use `data-level-section`. | Listening.cshtml ≈39, ≈228 |

Unknowns to resolve at L0:
1. Where the node tests for `listening-core.js` live, and the command that runs them.
2. Whether a guest can open the practice page. Expected: yes, read-only, no saving.
3. The exact name rule of `CreateSetAsync`: length, duplicates, and whether a set needs a folder.
4. The shape of `CardInputDto` needed for a minimal card (Term, Definition, Examples).
5. Which existing tests render the two pages (`ListeningIntegrationTests`, `ListeningCatalogLevelTests`, …).
6. The local seed lesson ids that have a timed transcript, vocab and quiz. Use them for screenshots.

---

## 4. Server design (L2)

### 4.1 Fix F1
In `NormalizeListeningLevel`, accept `C2`. Add a test that `GET /Home/Listening?level=C2` preselects C2. Nothing else in `StudyController` changes.

### 4.2 View-model additions (additive only)
- `ListeningLevelMetaViewModel.CompletedCount` (int): public lessons of that level the user finished. Compute it in the service from the same snapshot that fills `CompletedLessons`, with no extra query per level. It is 0 for guests.
- `ListeningPracticeViewModel.VocabWords`: not needed. JS builds the lookup map from `VocabItems`, which are already in the page config.
- `ListeningPracticeViewModel.CanLookupAnyWord` (bool) = Premium or Admin (D1).
- `ListeningPracticeViewModel.WordSetUrl` (string?): the URL of the user's "Từ vựng Luyện nghe" set when it exists, otherwise null. Read-only lookup, no creation on GET.

### 4.3 New `ListeningApiController` (`[Route("api/listening")]`, `[AutoValidateAntiforgeryToken]`)

**`GET api/listening/word?lessonId=&term=`** (word lookup)
- **Input:**
  - `term`: trimmed, lower-cased for matching, 1–40 chars, letters plus `'` and `-` only (regex `^[\p{L}][\p{L}'\-]{0,39}$`). Otherwise 400 `{ error: "Từ không hợp lệ." }`.
  - `lessonId`: must be a lesson the caller may view, using the same rule as `GetPracticeViewModelAsync`. Otherwise 404.
- **Order:**
  1. **Lesson vocab match.** Case-insensitive; also match simple inflections (`-s`, `-es`, `-ed`, `-ing`, `-'s`) through a pure helper. On a match, return `{ source: "lesson", word, definition, example, phonetic: null }`. Allowed for everyone, guests included.
  2. **Otherwise, a Premium/Admin caller:** `IVocabSuggestService.SuggestAsync(term)`. Return `{ source: "dictionary", word, phonetic, definition (first, Vietnamese if available), example }`.
     - Cache the result in `IMemoryCache` for 24 h, keyed by the lower-case term.
     - Rate-limit to 30 lookups per user per 10 minutes with the in-memory limiter pattern the repo already uses (find it at L0). Over the limit: 429 `{ error: "Bạn tra từ hơi nhanh, thử lại sau ít phút." }`.
     - On AI failure: 502 `{ error: "Chưa tra được từ này. Thử lại sau." }`.
  3. **Otherwise (Standard or guest):** 200 `{ source: "locked", word, lineTranslationOnly: true }`. The UI then shows the line translation and the Premium hint (D1). Never call the AI for these callers.
- **Never** return another user's private lesson content.

**`POST api/listening/word/save`**, body `{ lessonId, term, definition, example }`
- `[Authorize]`; antiforgery header as the rest of the listening JS (`Sets.fetchJson` already sends it; verify).
- Validate:
  - `term` with the same regex;
  - `definition` 1–300 chars after trim;
  - `example` ≤ 300;
  - the lesson is viewable.
- Find the caller's own set named exactly `Từ vựng Luyện nghe`. If none exists, create it with `CreateSetAsync`: private, owned by the caller, no folder unless the service requires one (resolve at L0). Use `CreateCardsBatchAsync` with one card: Term, Definition, Examples=[example] when given, Topic = lesson topic, Cefr = lesson level.
- Duplicates: if the set already contains the term (case-insensitive, via `FindDuplicatesAsync` limited to that set, or a set-scoped query in the new service), return 200 `{ saved: false, duplicate: true, setUrl }`. The UI says "Từ này đã có trong bộ thẻ."
- Success: `{ saved: true, setUrl }`.
- Errors: 401 guest; 400 invalid; 404 lesson; 500 never leaks details.

All logic lives in `ListeningWordService` (interface `IListeningWordService`). The controller only maps results to HTTP. Register the service with one line in `Program.cs`, scoped.

### 4.4 Tests first (L2)
`ListeningWordServiceTests`:
1. Lesson vocab exact match → `lesson`.
2. Inflection match: `orders` → `order`.
3. Standard, non-vocab word → `locked`, and the AI fake is never called.
4. Premium, non-vocab word → `dictionary`; a second call within 24 h hits the cache (AI fake called once).
5. Rate limit → 429 after 30.
6. AI failure → 502 mapping.
7. Invalid terms (`""`, `<script>`, 41 chars, `123`) → 400.
8. Private lesson of another user → 404 for both endpoints.
9. Save creates the set once; a second word goes to the same set; duplicate → `duplicate: true`.
10. Save cannot target another user's set. There is no set id in the request; prove that a set with the same name owned by user B is never used for user A.
11. Guest save → 401. Missing antiforgery → 400.

The C2 test of 4.1 goes here too.

---

## 5. UI design

General rules for every screen:
- Clay tokens only (`--tct-*`, `--tct-clay-*`) and existing `k-*` study-kit classes under the `.tl-page` scope. No hex colours, inline `style`, inline `<script>` or inline handlers in the views. Config stays in the existing `<script type="application/json" id="tlConfig">`.
- Vietnamese UI copy exactly as written below. Lesson content stays English.
- Laptop first (1440×900, 1366×768), then 1024, 768 and 390.
- Every interactive element is keyboard-reachable with a visible focus ring. `prefers-reduced-motion` and `forced-colors` blocks in `listening.css`.
- Font Awesome classes `fa-solid`/`fa-regular`/`fa-brands` only (no `bi bi-`).

### 5.1 Catalog (F1)
Order from top:

1. **Header row.**
   - Left: title `Luyện nghe` with the subtitle `Nghe hội thoại thật, tra từ ngay trong lời thoại, chép chính tả và làm quiz.`
   - Right: the existing search box, placeholder `Tìm bài nghe, chủ đề…`.

2. **Hero card** (partial `_ListeningHero`).
   - **When `ContinueLesson` is set:**
     - thumbnail on the left (≈45%), with a duration badge and a play overlay;
     - chip `Nghe tiếp`, the title, and a meta line `{Level} · {Topic}`;
     - step chips:
       - `Nghe lời thoại` (done → success chip with a check);
       - `Chép chính tả {n}/{total}`: filled client-side from localStorage (D3), hidden when there is no record;
       - `Quiz {score}%`, or `Quiz` when not done;
       - `Từ vựng` (when the lesson has vocab);
     - primary button `Học tiếp` (to the practice page, plus the hash of the first unfinished step, section 5.3);
     - inline stats `{CompletedLessons} bài xong · {avg}% quiz TB`. Average of the stored `QuizScore` of finished public lessons; omit when there is none (compute in the service; additive property `AverageQuizScore` int?).
   - **When signed in but `ContinueLesson` is null:** the same card shows the first unfinished lesson of the lowest level that has one, with the chip `Bắt đầu` and the button `Bắt đầu nghe`. If every lesson is done, show `Bạn đã nghe hết các bài hiện có` with the button `Ôn lại bài đã xong`, which opens the done filter.
   - **Guest:** card text `Đăng nhập để lưu tiến độ và tiếp tục bài đang nghe.`, buttons `Đăng nhập` (existing login URL with `returnUrl=/Home/Listening`) and `Nghe thử một bài` (first A1 lesson).

3. **Level row** (partial `_ListeningLevelRow`).
   - Seven toggle cards: `Tất cả` + A1…C2. Each card shows the key, `{CompletedCount}/{LessonCount}` (signed in) or `{LessonCount} bài` (guest), and a thin progress bar (success colour when complete).
   - `role="radiogroup"` with `aria-checked` and arrow-key movement. The selected card has a primary outline.
   - Levels with 0 lessons render disabled with `0 bài`.
   - The selection syncs with `?level=` through the existing `buildFilterQuery` and `parseFilterQuery`.

4. **Filter row.**
   - Status chips `Mọi trạng thái` / `Chưa học` / `Đang học` / `Đã xong` (hidden for guests).
   - Spacer.
   - Label `Chủ đề:` with a native `<select>` (`Tất cả` + topics, D5) synced with `?topic=`.
   - When a filter yields nothing: an empty state with `Không có bài nào khớp bộ lọc.` and the button `Xóa bộ lọc`.

5. **Level shelves.**
   - **With `Tất cả`:** one section per level that has matches. Header `{Key} · {Label}` (existing labels) with a count chip `{done}/{total}` and a link `Xem cả {n} bài`, which selects that level. Shows up to 4 cards in one row (4 columns ≥1200 px, 3 at 1024, 2 at 768, 1 at 390).
   - **With one level selected:** a single grid of that level with the existing `paginate` (page size 8) and the button `Xem thêm bài`.
   - **Card (restyled `_ListeningLessonCard`):**
     - 16:9 thumbnail with a duration badge and a play overlay;
     - title (2 lines max);
     - meta row: level chip, topic, and a status chip on the right (`Đã xong` success, `Đang học` warning, nothing for new);
     - a progress bar for `progress` (`StepsDone/StepsTotal`).
     - The whole card is one link (no nested interactive elements). Keep the existing broken-thumbnail fallback.

6. **"Bài nghe của tôi"** section, kept below the shelves.
   - Header with a `Premium` chip and the link `+ Thêm từ YouTube`, which focuses the import box.
   - Cards as above, plus the existing delete control (keep the modal).
   - Import tile: a dashed card `Dán link YouTube`. Clicking it reveals the existing import form; keep all existing messages and upgrade handling.
   - Locked (Standard): one card `Nhập video YouTube để luyện nghe là tính năng Premium.` with the existing upgrade link.
   - Guest: hidden.

Remove the old "Tiến độ nghe của bạn" overview panel. Its numbers move into the hero and the level row. Keep any element id that tests or JS rely on, or update those tests/JS in the same phase.

### 5.2 Practice (F2) — layout
- Breadcrumb `Luyện nghe / {Level} / {Title}`. Title row with a level chip and a `{Topic} · {Duration}` chip.
- **Two columns** at ≥1200 px: left ≈48% (player + progress), right ≈52% (tabs). At <1200 px the columns stack: player, then tabs, then progress.
- **Left, player card:**
  - YouTube iframe or audio (unchanged elements and ids `tlYoutube`, `tlAudio`, `tlPlayerNotice`);
  - the subtitle overlay host (5.6);
  - control row: previous line, `−5s`, play/pause (large, primary), `+5s`, next line, spacer, speed segmented control (Premium as today; Standard sees `1×` and a lock tooltip `Đổi tốc độ là tính năng Premium`), loop-line toggle, subtitle toggle (5.6).
  - Keep the existing ids `tlBack`, `tlPlay`, `tlForward`, `tlLoop` and the radios `tlSpeed-*`. Add ids `tlPrevLine`, `tlNextLine`, `tlSubs`.
- **Left, "Tiến độ bài này" card** (partial `_ListeningStepCard`), one row per step:
  1. `Nghe hết lời thoại`: `Xong`, or `Chưa xong`.
  2. `Chép chính tả`: `{done}/{total} câu · {accuracy}%` from localStorage, labelled `Tùy chọn` (D3).
  3. `Làm quiz`: `{score}%`, or `0/{n}`.
  4. `Ôn từ vựng trong bài`: `{n} từ` or `Xong`.
  - Rows link to their tab. Steps without content are hidden (same rule as `requiredSteps`).
  - The existing completion banner `tlDone` stays under the card.
  - Keyboard hints move under the card as one muted line: `Space phát/dừng · ← → đổi câu · R nghe lại · L lặp câu`.

### 5.3 Practice — tabs
- Five tabs in a `k-seg` tablist: `Lời thoại`, `Chép chính tả`, `Điền từ`, `Quiz {n}`, `Từ vựng {n}`.
- Hide Quiz when there are no questions and no AI generation is possible (keep today's AI-generate empty state when `CanUseAi`). Hide Từ vựng when there are no items.
- **Hash deep links:** `#transcript`, `#dictation`, `#fill`, `#quiz`, `#vocab`.
  - Old links stay valid: `#transcript` (normal mode), `#vocab`, `#quiz`.
  - If the old JS wrote mode hashes or query keys, map them too (check `keyFromHash` at L0).
  - Arrow keys move between tabs (existing behaviour); Home/End work.
- The old mode radios (`tlMode-*`) are removed. Their panels become the new tabs. The existing dictation and fill logic is reused and restyled, not rewritten, except where 5.4 says so.

### 5.4 Tab contents
- **Lời thoại** (normal mode):
  - Toolbar: `Bản dịch` toggle (existing `tlToggleVi`), `Ẩn lời thoại` (`tlToggleHide`), `Tự cuộn` (`tlToggleScroll`), the AI translate button (`tlTranslate`, as today), and a right-aligned hint `Bấm vào từ để tra nghĩa`.
  - Lines: time (tabular numerals), speaker in bold primary colour, text, translation under it when shown. The active line gets a primary inset bar and a soft background.
  - Every word token is a `<button type="button" class="tl-word">`, built by JS from the text with `core.wordSpans` (new). Lesson-vocab words get a dotted underline.
  - Clicking a line's time plays from that line (existing). Clicking a word opens the word popover (5.5) and does **not** seek.
  - Footer: `Đánh dấu đã nghe xong` (existing `tlMarkDone`).
- **Chép chính tả**, one sentence at a time (F2):
  - Header `Câu {i}/{n}`, the line time range, and a loop chip.
  - A segmented progress strip of n pieces (done = success, wrong = danger, current = primary, rest = muted). Each piece is a button with `aria-label="Câu {i}: {trạng thái}"`.
  - A play button that plays the current line (`playTimed` with stop-at-end) and an animated level bar that is purely decorative (`aria-hidden`; static when reduced motion).
  - Textarea (`aria-label="Gõ lại câu bạn nghe được"`).
  - Keys: `Ctrl+Space` replays the line, `Tab` reveals the next letter as today if that exists (otherwise omit the hint), `Enter` checks (`Shift+Enter` = newline).
  - Buttons `Bỏ qua`, `Kiểm tra`.
  - **Result box** after checking:
    - title `Chính xác · 100%` (success), `Gần đúng · {p}%` (≥70, warning), or `Chưa đúng · {p}%` (danger);
    - the diff from `gradeDictation`, rendered as spans: correct = success text, wrong = struck danger chip, missing = success chip with underline;
    - the line translation when it exists;
    - buttons `Lưu "{word}"` (only when the line contains a lesson-vocab word, prefilled from it), `Thử lại`, `Câu tiếp theo`.
  - Results are saved to localStorage (D3) and restored on load. The button `Làm lại từ đầu` clears them after a confirm dialog in the page style (no `window.confirm`).
  - Lines without times: playback is disabled with the note `Câu này chưa có mốc thời gian, hãy nghe cả bài.`, and checking still works.
- **Điền từ:** the existing fill logic restyled to the same card language (summary tiles, blanks as inline inputs with clear correct/incorrect states, `Kiểm tra tất cả`, `Làm lại`).
- **Quiz:** one question per screen.
  - Header `Câu {i}/{n}` with a dot progress strip.
  - Options A–D as large radio cards.
  - `Câu tiếp theo` / `Câu trước`, and `Nộp bài` on the last question (all answers are still submitted together to `EvaluateQuiz`, unchanged).
  - After grading:
    - a result ring with the score;
    - a per-question review list (your answer, the correct answer, the explanation);
    - `Làm lại`.
  - Keep `tlQuizForm`, `tlQuizResult`, `tlQuizLive` semantics (live announcements).
  - The AI generate empty state is unchanged in behaviour.
- **Từ vựng:** cards of lesson vocab (word, speak button when speech synthesis exists, definition, example, image when present), plus the buttons `Lưu tất cả vào Bộ thẻ` (signed-in; calls save for each item sequentially, then shows a summary `Đã lưu {a} từ, {b} từ đã có sẵn.`) and `Đánh dấu đã ôn` (existing `tlVocabDone`).

### 5.5 Word popover / sheet (`_ListeningWordSheet` + `listening-word.js`)
- **Laptop:** a popover anchored under the clicked word (flip above when there is no room), max width 300 px, `role="dialog"`, `aria-labelledby` = the word.
  - Focus moves into it. `Esc` closes and returns focus to the word. Outside click closes it.
- **≤ 767 px:** the same content as a bottom sheet (F3) with a drag handle (decorative), a backdrop and a focus trap.
- **Content:**
  - the word, the phonetic when known, a speak button (speech synthesis, hidden when unsupported);
  - the definition;
  - the example (italic);
  - the button `Lưu vào Bộ thẻ` (primary) and the link `Mở bộ thẻ` after the first save (`WordSetUrl` or the returned `setUrl`).
- **States:**
  - loading skeleton (`Đang tra từ…`);
  - `locked` (D1): the line translation, the text `Tra mọi từ là tính năng Premium.` and the link `Nâng cấp Premium` (existing upgrade URL);
  - error (message from the server);
  - guest pressing save: `Đăng nhập để lưu từ vào Bộ thẻ.` with a login link (returnUrl = the current practice URL);
  - duplicate: `Từ này đã có trong bộ thẻ.`;
  - saved: a toast `Đã lưu "{word}" vào bộ "Từ vựng Luyện nghe".` via the existing toast host `tsToastHost`.
- Client cache: one `Map` per page, so a word is fetched once.
- Never insert server strings with `innerHTML`; use `textContent` and `Sets.el`.

### 5.6 Subtitle overlay (D4, idea from D2)
- The toggle `tlSubs` cycles `Phụ đề: Tắt` → `Phụ đề: Anh` → `Phụ đề: Anh + Việt`; the label shows the state, plus `aria-pressed` for not-off. Remember the choice in localStorage `tct.listening.subs.v1`.
- The overlay is a positioned layer inside the video frame, bottom-centred. English line on a dark translucent chip, Vietnamese line under it in the accent colour. Words in the overlay are `tl-word` buttons too (same popover). The overlay itself must not block the YouTube controls: put `pointer-events:none` on the layer and `auto` on the buttons only.
- It follows the active line from the existing `tick` loop and is hidden when no line is active or the lesson has no times.
- Not shown in native fullscreen; the toggle tooltip says `Phụ đề không hiện khi xem toàn màn hình.`
- Audio-only lessons: the overlay sits on the audio card as a caption panel instead.

### 5.7 Phone (F3, ≤ 767 px)
- The top bar of the page: a back arrow to the catalog, the title (ellipsis), and a level chip. The breadcrumb is hidden.
- The video is sticky at the top. The control row sits under it with large touch targets (≥44 px) and a speed button.
- Tabs become a horizontally scrollable pill row (no wrapping; fade edge).
- Transcript lines become cards (speaker · time on top, text, translation). The active line has a primary outline.
- The progress card moves to the end of the page.
- The word lookup uses the bottom sheet.
- The catalog at 390: the hero stacks (thumbnail on top), the level row scrolls horizontally, cards in one column, filters wrap onto two lines.
- No horizontal page scroll at any width ≥ 320.

---

## 6. Client logic (L1)

New pure functions in `listening-core.js`, each exported and unit-tested with node:test:

| Function | Contract |
| --- | --- |
| `wordSpans(text)` | Splits a line into `[{t:'word', v, key}, {t:'sep', v}]`. `key` = lower-case word without surrounding punctuation. Keeps apostrophes inside words (`don't`, `Anna's`). Never drops characters (joining all `v` returns the input). |
| `baseForms(key)` | Returns `[key, …simple de-inflections]` (`-'s`, `-s`, `-es`, `-ies→y`, `-ed`, `-d`, `-ing`, doubled consonant), unique, max 6. The server uses the same rule (mirror in C#; add a test that both agree on a shared fixture of 20 words). |
| `vocabIndex(items)` | `Map<key, item>` over the base forms of each vocab word (multi-word items such as `to go` are matched as phrases in `wordSpans` output, longest first). |
| `markVocab(spans, index)` | Flags spans (and phrase ranges) found in the index. |
| `dictationStore(storage, userKey, lessonId)` | `{ load(), save(i, result), clear(), summary(n) }`. Tolerates a missing or throwing storage (private mode). `summary` → `{done, correct, accuracy}`. Versioned key `tct.listening.dictation.v1.…`. |
| `levelSummary(levels)` | Totals for the `Tất cả` card and the bar widths (0–100, integers). |
| `nextStepHash(flags, counts)` | First unfinished step → `#transcript` / `#quiz` / `#vocab`, else `#transcript`. |
| `activeSubtitle(lines, time, mode)` | `{en, vi}` or null for overlay mode `off/en/both`. |

Node tests cover the edge cases: empty text, punctuation only, Vietnamese diacritics in translations (untouched), curly apostrophes, numbers, storage throwing, zero lessons, all done.

---

## 7. Phases

Each phase ends with the full gate (section 8). Commit only on green.

### L0 — Baseline and discovery
1. Pull the default branch per the prompt. Create `feature/listening-redesign`.
2. Build and run the full test suite and the node tests. Record the counts. They must never go down.
3. Resolve the unknowns of section 3 and reproduce V1–V11 and F1. Write the verdicts in section 12.
4. Before screenshots: catalog and practice for guest, Standard and Premium at 1440×900, 1366×768 and 390×844 into `.tmp/listening-redesign/before/`.
5. List every element id and `data-*` hook used by the listening JS and tests. These must survive or move together.

### L1 — Core logic
Add the functions of section 6 with tests first. No UI change yet.

### L2 — Server
Implement F1, the view-model additions (4.2, plus `AverageQuizScore`) and `ListeningApiController` + `ListeningWordService`, with the tests of 4.4 written first. Add the one DI line.

### L3 — Catalog UI (5.1)
Partials, the restyled card, the hero, the level row, filters, shelves and the "Bài nghe của tôi" restyle. Update `listening-index.js` (level radiogroup, topic select, shelves vs single grid, hero dictation chip). Add render tests to `ListeningRedesignRenderTests`:
- hero states (continue, start, all done, guest);
- seven level cards including C2;
- topic select options;
- locked "Bài nghe của tôi" for Standard;
- hidden for guests.

### L4 — Practice layout, player, progress card (5.2)
Restructure the view. Keep all player ids. Add previous/next line. Add the step card with the localStorage row. Render tests: step rows hidden when there is no content, ids present.

### L5 — Tabs and contents (5.3, 5.4)
- Five tabs and hash compatibility, with a test for each hash, old and new.
- Transcript word buttons.
- Dictation one-at-a-time with the diff and storage.
- Fill restyle.
- One-question quiz.
- Vocab tab with save-all.

### L6 — Word popover and sheet, subtitles (5.5, 5.6)
`listening-word.js`, the overlay and the toggle. Manually verify the Premium lookup against the AI. If no real AI key is configured locally, verify with the test fake and record "AI path not verified live".

### L7 — Phone, responsive and accessibility (5.7)
- All widths, 200% zoom, keyboard-only pass, screen-reader names.
- Contrast script on the new chips (success/warning/danger on soft backgrounds).
- forced-colors and reduced-motion checks.

### L8 — Docs
- The tracker row for Listening.
- `docs/known-issues.md`:
  - D3 local-only dictation;
  - the subtitles fullscreen limit;
  - the AI lookup cost and rate limit;
  - the `Program.cs` line.
- `bug-fix-log.md` (F1).
- `project-structure.md` (the new files).
- The README Listening section, only if it lists the old tabs (Trọng's section only).

### L9 — Full gate and end-to-end run (8.5), self-review against section 10, final report.

---

## 8. Gates and run rules

### 8.1 Phase gate G1–G15 (run all, in order)

| Gate | Check |
| --- | --- |
| G1 | `dotnet build` with no new warnings in touched files |
| G2 | Listening tests (filter by `Listening`) |
| G3 | Full test suite; counts ≥ L0 |
| G4 | Node tests; counts ≥ L0 |
| G5 | `has-pending-model-changes` → none; route list unchanged except the two new `api/listening` routes |
| G6 | Token grep: no hex, no `rgb(` in `listening.css` outside forced-colors; no inline `style=`, `<script>` (except the JSON config), `onclick=` in the listening views; `git diff --check` |
| G7 | File scope = section 2; `Program.cs` diff = one line; `StudyController` diff = `NormalizeListeningLevel` only |
| G8 | Render at 1440×900, 1366×768, 1024×768, 768×1024, 390×844 and 200% zoom for guest, Standard and Premium; console and network clean (no 4xx/5xx except intended) |
| G9 | Mockup comparison with F1/F2/F3 (and D2 for the overlay); differences listed |
| G10 | Behaviour checklist 8.4, mouse and keyboard |
| G11 | Accessibility: tablist, radiogroup, dialog/sheet focus, live regions, names |
| G12 | Scripted contrast ≥ 4.5:1 text, ≥ 3:1 UI parts |
| G13 | Regression: Home, Vocabulary sets, Reading, Writing, Admin listening pages render; existing listening flows (import, delete, AI quiz, translate) still respond as before |
| G14 | Consistency: no `bi bi-`, Vietnamese copy matches this plan, no English UI labels |
| G15 | Security: tests of 4.4 pass; no `innerHTML` with server data; antiforgery on POST; no private-lesson leak; rate limit active |

### 8.2 Stop conditions (the only reasons to stop early)
1. A change would need a forbidden file, a migration or a model change.
2. A gate item fails 3 times. Mark it deferred or blocked with evidence. Stop only if it blocks every later phase.
3. A verified fact turns out false in a way that changes the design (for example, guests cannot open the practice page, or `CreateSetAsync` cannot create a private set without a folder the user must pick).
4. Data risk: anything that would edit or delete real seeded lessons or another user's data.

### 8.3 Gate record (write into section 12 after each phase)
`Phase · date · commit · G1…G15 results (pass / fail→fixed / deferred with reason) · test counts · screenshots path · notes`.

### 8.4 Behaviour checklist (G10)
1. Catalog: selecting a level card filters and updates `?level=`. Reload keeps it. `?level=C2` works.
2. Topic select and status chips combine. The empty state's `Xóa bộ lọc` resets everything.
3. Hero `Học tiếp` opens the right tab hash. Guest buttons go to login with the returnUrl.
4. "Bài nghe của tôi": import still validates YouTube links, shows errors, opens the new lesson. Delete still asks and removes the card.
5. Practice: play/pause, ±5 s, previous/next line, loop line and speed (Premium) all work with YouTube and with audio.
6. Every hash deep link (old and new) opens the right tab. Arrow keys move between tabs.
7. Transcript: a word click opens the popover without seeking. A time click seeks. Auto-scroll, hide and translation toggles work.
8. Lookup states: lesson word (all roles), locked (Standard), dictionary (Premium), error, rate limit (simulate in a test, not live).
9. Save word: the first save creates the set; then append; duplicate message; guest login prompt; `Mở bộ thẻ` opens the set.
10. Dictation: check, diff colours, retry, next, skip, the progress strip. Reload restores the results. `Làm lại từ đầu` clears them. Untimed lines handled.
11. Fill-in and the quiz grade exactly as before. The quiz review shows explanations. Progress is saved (step card updates without reload).
12. Vocab tab: speak, save all summary, `Đánh dấu đã ôn`.
13. Subtitle toggle cycles three states and remembers. Overlay words open the popover. YouTube controls stay clickable.
14. Phone: sticky video, scrollable tabs, bottom sheet with focus trap, no horizontal scroll.

### 8.5 End-to-end run (L9)
Run with the synthetic accounts from `.appdata/local/access.json` (never print passwords):
1. **Guest** opens the catalog, then an A1 lesson, plays it, looks up a lesson word, gets the login prompt on save.
2. **Standard:**
   - finishes the transcript;
   - dictates 3 lines (one wrong);
   - does the quiz;
   - saves 2 lesson words (set created once);
   - sees the locked lookup on a non-vocab word;
   - goes back to the catalog: the hero shows this lesson with the right chips, and the level card count changes when the lesson completes.
3. **Premium:**
   - imports nothing new (no live YouTube import needed);
   - uses speed 0.75×;
   - turns subtitles on (Anh + Việt);
   - looks up a non-vocab word (live AI if a key exists, otherwise record "not verified live");
   - saves it.
4. Clean up:
   - delete the `Từ vựng Luyện nghe` sets created for the synthetic accounts through the normal set delete UI;
   - clear the listening localStorage keys;
   - restore any progress rows the run created only if a test helper exists for it; otherwise record them (progress of synthetic accounts is acceptable).

---

## 9. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| AI cost from word lookups | Premium only, 24 h cache, 30/10 min limit, lesson vocab first |
| Breaking existing deep links or tests that use ids | L0 inventory, hash compatibility tests, keep ids |
| Overlay blocking YouTube controls | pointer-events rule + manual check G10.13 |
| localStorage unavailable | `dictationStore` tolerates throwing storage; the UI simply does not restore |
| Personal-set rules (name, folder) differ from assumptions | Resolve at L0; stop condition 8.2.3 if the design must change |
| Large JS files | Split the word logic into `listening-word.js`; keep files ≤ ~1,200 lines |
| Shared `StudyController` | One-method change only, gate G7 |

---

## 10. Definition of done
- [ ] F1 fixed with a test. 4.4 tests pass. Node tests for every function in section 6.
- [ ] Catalog matches 5.1 for guest, Standard and Premium. Practice matches 5.2–5.6. Phone matches 5.7.
- [ ] All features of V6 still work. Old hash links still work.
- [ ] Save-to-set works with no access to other users' sets. Lookup respects D1.
- [ ] No migration, no model change, no forbidden file. `Program.cs` = one DI line. `StudyController` = one method.
- [ ] G1–G15 green for every phase (or deferred with evidence). Test counts ≥ L0.
- [ ] Before/after screenshots in `.tmp/listening-redesign/`.
- [ ] Docs updated (L8). Section 12 complete.

## 11. Commit plan
One local commit per phase via `tct-commit-message`, for example:
- `feat(listening): add word and dictation helpers to listening core`
- `feat(listening): add word lookup and save-to-set endpoints`
- `feat(listening): rebuild the listening catalog`
- …
- `docs(listening): record the redesign`

No push, no PR, no merge.

## 12. Execution record
### 12.1 L0 — baseline, verdicts and unknowns (2026-11-18)

**Base and branch**
- `master` = `origin/master` = `e6be0e1` after `pwsh scripts/pull-local.ps1` (exit 0; shared-data sync complete, 380 catalog releases checked, 4 record operations, personal data preserved).
- A local branch `feature/listening-redesign` already existed at `084830c` (the previous Listening run, an ancestor of `master`, no unique commit). It was **fast-forwarded** to `master` with `git merge --ff-only`; nothing was reset, deleted or forced.
- `.ai/prompts/claude-code-listening-redesign.md` already existed with the previous run's prompt (commit `851d989`). It was replaced by this run's prompt because the prompt's own "Save as" line names that path; the old text stays in Git history.

**Baselines (counts must never go down)**

| Check | Result |
| --- | --- |
| `dotnet build` (Debug, solution) | 0 errors, 40 warnings (all in other modules; none in Listening files) |
| Listening tests (`FullyQualifiedName~Listening`) | 145 passed, 0 failed, 2 skipped (MariaDB-gated) |
| Full .NET suite | 3497 passed, 0 failed, 54 skipped. Run in two groups with `-- xUnit.MaxParallelThreads=2` because of the 16 GB limit: classes A–L 2687 / 0 / 34, the rest 810 / 0 / 20 |
| Node tests | 514 passed, 0 failed. Command: `node --test "scripts/tests/*.cjs"` (a directory argument fails on Node 24) |
| `dotnet ef migrations has-pending-model-changes` | none |

**Verdicts**

| # | Verdict | Evidence / consequence |
| --- | --- | --- |
| V1 | Confirmed | All eight routes in `StudyController.cs` 113–244. |
| V2 | Confirmed | `GetIndexViewModelAsync(null, null, userId)`; `level`/`topic` only preselect. |
| F1 | Confirmed | `NormalizeListeningLevel` (line 127) accepts A1–C1 only, so `?level=C2` is dropped. |
| V3 | Confirmed | `ContinueLesson` only comes from rows of `UserListeningProgresses`, so a lesson appears after its first save. |
| V4 | Confirmed | |
| V5 | **Partly false** | The practice `tlConfig` carries lines and progress but **not** the vocabulary items. L5 adds a `vocab` array to the JSON config (view only) so JS can build the lookup map. |
| V6 | **Partly false** | Tabs, modes, step card and speed group exist as described. The current deep-link hashes are `#loi-thoai`, `#tu-vung` and `#quiz` (`HASH` map in `listening-practice.js`), not `#transcript`/`#vocab`. L5 accepts both the old Vietnamese hashes and the new English ones. |
| V7 | Confirmed | All listed helpers exist and stay unchanged. |
| V8 | Confirmed | `GetPracticeViewModelAsync` is read-only; only `SaveProgressAsync` writes `LastAccessedAt`. "Nghe tiếp" therefore follows the last **saved** lesson. Accepted, no server change. |
| V9 | Confirmed | `CreateCardsBatchAsync` already skips a term that exists in the target set (case-insensitive) and reports it in `Skipped`, so "duplicate" needs no extra query. |
| V10 | Confirmed | `SuggestAsync` also starts a Pixabay image request in parallel (images are ignored here). The Premium/Admin gate lives in its controller, so `ListeningWordService` enforces it itself. |
| V11 | Confirmed | `pageSize = 6`; sections use `data-level-section`. |

**Unknowns resolved**
1. Node tests: `scripts/tests/*.cjs` (`test_listening_core.cjs`, `test_listening_catalog_filters.cjs`, `test_listening_catalog_packages.cjs`); new file `test_listening_core_redesign.cjs`. Shared word-form fixture: `TCTEnglish.Tests/Fixtures/listening-base-forms.json`.
2. A guest **can** open the practice page (no `[Authorize]`; `Pages_PublicLesson_AreOpenToGuestsAndSignedInUsers`).
3. `CreateSetAsync`: trimmed name, 1–255 characters, no uniqueness rule, folder optional, visibility `Private` by default, at most 200 owned sets (`MaxOwnedSets`).
4. Minimal card: `Term` (≤255), `Definition` (≤1000), optional `Topic`, `Cefr`, `Examples[{Text, Vi}]` (max 6); batch size ≤30.
5. Render tests: `ListeningIntegrationTests` (`IndexPage_*`, `PracticePage_*`), `ListeningCatalogLevelTests`. They assert copy that this redesign replaces on purpose ("Tiến độ nghe của bạn", the guest sentence, the locked-panel sentence, the step order, the Quiz empty state, the speed unlock link). Each such assertion is updated in the phase that changes the copy, with an equivalent assertion for the new contract; none is deleted or weakened without a replacement. Every change is listed in that phase's record.
6. The dev DB has 91 lessons but only lesson 1 has content (2 untimed lines, 1 quiz, no vocabulary); the other 90 are unpublished shells. **No seeded lesson has a timed transcript plus vocabulary plus quiz.** Nine synthetic public lessons (ids 107–115, A1–C2, with timed lines, translations, vocabulary and quizzes) were inserted for verification, marked `TranscriptSource = 'tct-fixture-ll'`, and are deleted at L9. Their audio is `/audio/ll-fixture.wav`, served only to the test browser through DevTools request interception (no file is added to the repository).

**Element-id and hook inventory (must survive or move together with their tests)**
- Catalog JS: `tlConfig tlSearch tlLevels tlStatuses tlTopics tlTopicsToggle tlCount tlEmpty tlClearFilters tlLevelsHost tlImport tlImportUrl tlImportSubmit tlImportError tlImportStatus tlMineGrid tlMineCount tlMineEmpty tl-mine-title tlDeleteLesson tlDeleteBody tlDeleteConfirm`; hooks `data-level data-status data-topic data-id data-title data-level-section data-level-count data-level-more data-level-grid data-mine-id data-tl-delete`.
- Practice JS: `tlConfig tlYoutube tlAudio tlPlayerNotice tlBack tlPlay tlForward tlLoop tlSpeed-* tlLines tlLinesWrap tlToggleVi tlToggleHide tlToggleScroll tlTranslate tlMarkDone tlVocabDone tlQuizForm tlQuizResult tlQuizSubmit tlQuizCount tlQuizLive tlGenerateQuiz tlDone tlModePanel-*`; hooks `data-tl-tab data-tl-goto data-tl-speak data-step data-i data-start data-end data-qid data-key`.
- Tests rely on: `tl-page--index`, `tl-page--practice`, `tlConfig`, `data-id` + `data-status` (adjacent), `data-mine-id`, `data-tl-delete`, `tl-card--locked`, `tlImport`, `tlTab-vocab`, `data-step=*`, `name="tlSpeed"`, `tlGenerateQuiz`.

**Before screenshots:** `.tmp/listening-redesign/before/` — guest, Standard and Premium at 1440×900, 1366×768 and 390×844 for the catalog and practice lesson 107 (18 images; ignored by Git). The only console noise is one 404 for a YouTube thumbnail of the seeded lesson 1 (external; the page swaps it for the local placeholder).

**Gate record L0:** G1 pass · G2 pass (145/0/2) · G3 pass (3497/0/54) · G4 pass (514) · G5 pass (no pending changes) · G6–G7 not applicable (no source change) · G8 baseline captured · G9–G15 not applicable at baseline (nothing built yet).

### 12.2 L1 — core logic (2026-11-18)

- **Added to `listening-core.js`** (pure, no DOM/network): `wordKey`, `wordSpans`, `baseForms`, `vocabIndex`, `markVocab`, `dictationStore`, `levelSummary`, `nextStepHash`, `activeSubtitle` (section 6) and one extra pure helper, `dictationVerdict` (tone + percent for the result box: near from 70%, 100% only when exact), so the thresholds of section 5.4 are unit-tested instead of living in the DOM code. Existing exports are unchanged (the old purity test still passes: no `document`, `window`, `fetch` or `localStorage` in the code).
- **Decision:** a word span is letters only (inner apostrophes and hyphens allowed). Numbers and mixed tokens such as `3pm` stay separators, so the server never receives a term it must reject.
- **Tests first:** `scripts/tests/test_listening_core_redesign.cjs` (22 tests) was written red (22 failed: functions missing) and then went green. The shared word-form fixture `TCTEnglish.Tests/Fixtures/listening-base-forms.json` (20 words) is already read by the node test; the C# mirror in L2 must read the same file.
- **Gate record L1:** G1 pass (0 errors, no new warnings) · G2 pass (145/0/2) · G3 not re-run (no .NET source changed since the L0 baseline of 3497/0/54) · G4 pass (536 = 514 + 22; 0 failed) · G5 pass (no pending model changes; no route attribute changed) · G6 pass for the new code (no hex/rgb, no inline handler); the two pre-existing `style="--p:…"` in the old catalog views are removed in L3 · G7 pass (only `listening-core.js`, the new node test and the fixture changed; `Program.cs` and `StudyController.cs` untouched; `git diff --check` clean except the owner's untouched `appsettings.Development.json`) · G8–G13 not applicable (no UI change) · G14 pass · G15 not applicable (no endpoint yet).

### 12.3 L2 — server (2026-11-18)

- F1 fixed (`NormalizeListeningLevel` accepts C2; only that method changed in `StudyController`). `Program.cs`: one DI line for `IListeningWordService`.
- Additive: `IListeningService.GetWordContextAsync` (single access rule), view-model fields `AverageQuizScore`, `CompletedCount`, `CanLookupAnyWord`, `WordSetUrl`; new `ListeningWordService`, `ListeningWordForms` (C# mirror of `baseForms`, same shared fixture), `ListeningApiController` (`GET api/listening/word`, `POST api/listening/word/save`).
- Decisions: the rate limit (30 per 10 min per user) counts only lookups that reach the AI (cache hits are free); the limiter and cache are static inside the service because the plan allows one DI line only; the save term may be a phrase of up to five words (so "to go" can be saved), the lookup term stays one word.
- Tests first: `ListeningWordServiceTests` (34) written red (34 failed), then green. Listening tests 179 pass / 2 skip.
- Gate record L2: G1 pass · G2 pass (179/0/2) · G3 3530 passed, 1 failed in the full run: `WritingOptionalEngineContractTests.Evaluate_SharedBudget_StopsSlowCloudBeforeLaterEnginesFinish` (Writing module, timing-based); it passed 2/2 when rerun alone (7/7), so recorded as a load flake, not a Listening regression · G4 node pass · G5 pass (no pending model changes; two new routes only) · G6/G7 pass (Program.cs +1 line, StudyController 1 method) · G15 server part pass (antiforgery, 401 guest, 404 private lesson, no AI for Standard/guest, rate limit, no private-lesson leak).
- Not run for this phase: G8–G14 (no UI in L2).
