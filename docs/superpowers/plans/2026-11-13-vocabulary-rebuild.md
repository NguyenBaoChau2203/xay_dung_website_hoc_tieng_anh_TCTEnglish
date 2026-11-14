# System Vocabulary: full rebuild of pages and learning flow (2026-11-13)

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Phạm vi:** làm lại **toàn bộ** phần Từ vựng hệ thống (`/Vocabulary/*`), cả giao diện lẫn chức năng, theo hướng đơn giản nhất cho người học: **chọn khóa → bấm "Học tiếp" → học một bài 10 từ trong khoảng 5 phút**.
> - **Từ 6 trang rối còn 3 màn:**
>   - trang Từ vựng (danh sách khóa + "Bài tiếp theo");
>   - trang Khóa học (danh sách bài + tất cả từ);
>   - màn Học 3 bước: Làm quen → Luyện tập → Kết quả.
> - **Bỏ 4 nút tự chấm** (Khó/Tốt/Dễ/Hoàn hảo). Hệ thống tự chấm theo kết quả làm bài rồi xếp lịch ôn.
> - **Giữ nguyên:** địa chỉ trang cũ (test và chatbot đang dùng), cách ghi tiến độ và điểm Mục tiêu (API của Châu), database (không migration), `Program.cs`, file chung.
> - **1 ô cần bạn quyết (D1, mục 1.2):** có cho agent viết thêm nội dung từ vựng không. Hiện cả hệ thống chỉ có **92 từ**; bộ tên "1000 từ thông dụng" có 20 từ, "3000 từ Oxford A1" có 10 từ.
> - **Cách chạy:** liên tục V0 → V11, không hỏi lại. Mỗi phase phải qua đủ bộ kiểm tra G1–G15 (mục 8) mới commit và sang phase sau.
> - **Mockup đã duyệt:** 7 ảnh ở `Claude outputs/vocabulary-mockups/`.
>
> Phần chỉ dẫn cho agent viết bằng tiếng Anh theo `AGENTS.md`.

- **Run mode:** continuous V0 → V11 with no questions to the owner. Every phase must pass the full phase gate (section 8.1) before its commit and before the next phase starts.
- Owner / page owner: Huỳnh Phú Trọng. `README.md`: "Module Vocabulary (học từ vựng hệ thống)" and "quản trị học liệu từ vựng".
- Save as `docs/superpowers/plans/2026-11-13-vocabulary-rebuild.md`. Prompt: `.ai/prompts/claude-code-vocabulary-rebuild.md`.
- **Mockups (visual contract), drawn 2026-11-13:** 7 PNGs in the untracked folder `Claude outputs/vocabulary-mockups/` at the repo root. Never commit them.

| Mockup | Page |
| --- | --- |
| `01-tu-vung.png` | `/Vocabulary` home |
| `02-khoa-hoc.png` | course page |
| `03-hoc-lam-quen.png` | lesson, step 1 (meet the word) |
| `04-hoc-luyen-tap.png` | lesson, step 2 (practise, correct answer) |
| `05-hoc-ket-qua.png` | lesson, step 3 (result) |
| `06-mobile-tu-vung.png` | home at 390px |
| `07-mobile-lam-quen.png` | step 1 at 390px |

- **The mockups are static HTML with copied values and sample data.** Implement them with tokens and real data. Where a mockup pixel and a token or rule disagree, the rule wins (record it in section 12).
- **Baseline expected** (verify at V0): `master` at `d8c5773` (PR #135) or later.
  - The Listening redesign (`feature/listening-redesign`) may or may not be merged. This plan does not depend on it.
- **Rules to read first:**
  - `AGENTS.md`;
  - `DESIGN.md` (Visual direction, Demo priority, Claymorphism implementation, Accessibility);
  - `docs/design-system/team-ui-workflow.md`;
  - `.ai/prompts/adopt-claymorphism-for-current-ui-task.md`.
- **Reference code by the same owner** (reuse, do not fork):
  - `wwwroot/css/study-kit.css` (Clay kit `k-*`), `wwwroot/css/practice.css`;
  - `Views/Practice/Session.cshtml` + `wwwroot/js/practice-ui.js` (`window.TctPractice.ui`: `options`, `feedbackPanel`, `typingInput`, `diff`, `termLine`, `meaningLine`, `visual`, `speakButton`, `keyHints`, `ring`, `stateMessage`, `onKeys`);
  - `wwwroot/js/practice-core.js` (`window.TctPractice.core`: `shuffle`, `mulberry32`, `checkTyping`, `pickDistractors`, `choiceOptions`, `levenshtein`, `textKey`);
  - `wwwroot/js/personal-sets-common.js` (`window.TctSets`);
  - `scripts/tests/test_practice_core.cjs`, `TCTEnglish.Tests/PracticeIntegrationTests.cs`.

---

## 0. Research summary and skills

### 0.1 What the reference products do (basis for the design)

Sources reviewed 2026-11-13: a Memrise/Duolingo comparison (fluentu.com) and flashcard-app comparisons. The patterns taken:

| Pattern (source) | Applied here |
| --- | --- |
| Ready-made themed courses the learner just picks (Memrise, Duolingo) | Courses = system sets, grouped by collection (folder) |
| Short lessons, about 5 minutes, about 5 new words per sitting (Memrise) | Lessons of about 10 words, taught in two rounds of 5 |
| Words introduced one at a time, then practised at once with multiple choice and typing (Memrise, Duolingo) | Step 1 "Làm quen", step 2 "Luyện tập" |
| The lesson's words are visible before starting (Memrise) | Word chips on the current lesson row |
| One obvious next action; progress visible per course | "Học tiếp" on the home page, the course page and the result screen |
| Spaced repetition decides what to review; wrong words come back sooner (Memrise, Anki) | Automatic grading mapped onto the existing `LearningApi/record` schedule |
| "I already know this" to skip easy words (Memrise, Duolingo placement) | "Tôi đã biết từ này" in step 1 |

Not taken, to stay simple: locked sequential units, virtual currency, leaderboards, speed-review timers, mnemonics video.

### 0.2 Repo skills to apply (load each when its step starts)

| Skill (`.agents/skills/…`) | Used for | Phases |
| --- | --- | --- |
| `tct-superpowers` | scoped plan, TDD, systematic diagnosis, evidence before "done" | all |
| `tct-engineering` | source-evidence investigation, API contract checks, diff review | V0, V1, V11 |
| `tct-vocabulary-feature` | Vocabulary/Set/Card/learning-progress conventions | V1, V2, V7 |
| `tct-feature-scaffold` | view model / service additions in the MVC shape | V1 |
| `tct-api-endpoint` | the fetch calls to `LearningApi/record` | V7 |
| `tct-security-audit` | access rules for system sets, antiforgery, XSS review | V1, V11 |
| `tct-ui-ux-pro-max` | layout, hierarchy and interaction choices before each page | V3–V6 |
| `tct-ui-component` | Razor partials, Bootstrap forms/modals, feedback states | V4–V8 |
| `tct-ui-motion` | card, feedback, progress and result motion; reduced motion | V6–V8 |
| `tct-ui-review` | rendered-page audit | every gate, V11 |
| `tct-data-seeder` | only if D1 = CÓ (V10) | V10 |
| `tct-commit-message` | one local commit per phase | all |

### 0.3 Popular external skills (considered 2026-11-13)

| External skill | What it adds | Decision |
| --- | --- | --- |
| `obra/superpowers` (brainstorm → plan → TDD → verify) | Process discipline | **Already vendored** in this repo (`.ai/vendor/superpowers`, wrapped by `tct-superpowers`). Use the repo version. |
| `anthropics/skills` → `frontend-design` | Pushes for distinctive, non-generic UI | **Optional lens only.** `tct-ui-ux-pro-max` + DESIGN.md decide; a personal copy may be consulted for layout ideas, but it must not override tokens, Clay or Vietnamese copy. |
| `anthropics/skills` → `webapp-testing`; `lackeyjb/playwright-skill` | Drive a real browser for checks | **Optional.** The repo already has a browser-review workflow (`tct-ui-review`, `team-ui-workflow.md` §7). Use a personal copy only as a helper to run G8–G12 faster. |
| `OthmanAdi/planning-with-files` | Plan state kept in markdown | **Not needed.** This plan's section 12 gate records do that. |

**Rule:** do **not** add, vendor or install any external skill into the repository (`.agents/`, `.claude/`, `.ai/vendor/`, `tooling/`). Those are shared tooling files and need the owner of tooling. A skill installed at the user level on the owner's machine may be used as an optional lens. `AGENTS.md` and this plan win over any imported skill.

---

## 1. Decisions

### 1.1 Fixed decisions

| ID | Decision |
| --- | --- |
| V-R1 | **Legacy URLs keep working and return 200**, because tests, Goals and the AI chat use them: `/Vocabulary`, `/Vocabulary/Index`, `/Vocabulary/FolderDetail?folderId=`, `/Vocabulary/Detail?setId=`, `/Vocabulary/Topics?setId=`, `/Vocabulary/TopicDetail?setId=[&topic=]`, `/Vocabulary/Study?setId=[&topic=][&mode=review]`. They render the new views (section 5.1). Non-system sets and folders still return 404. |
| V-R2 | **Progress is recorded only through the existing `POST /api/LearningApi/record`** (`{ cardId, masteryLevel, timestamp }`, antiforgery). That endpoint is not edited. It owns the review schedule and the Goals/XP/streak credit (pinned by `GoalsPhase3IntegrationTests`, `Sprint1/2SmokeTests`, `CriticalFlowSqliteIntegrationTests`). |
| V-R3 | **No migration, no `Program.cs` change.** Lessons are computed from `Card.Topic` and card order. `IStudyService` is already registered; its changes are additive, and old methods are removed only when a grep proves nothing else calls them. |
| V-R4 | **Three screens:** home, course, lesson (section 6). `FolderDetail`, `Topics` and `TopicDetail` render the home or course view in a filtered state; they have no separate layout. |
| V-R5 | **Automatic grading replaces the four self-rating buttons.** One `record` call per word, when the word finishes (section 4.4). |
| V-R6 | **Lessons are never locked.** The learner may start any lesson; the UI only recommends the next one. |
| V-R7 | **UI system:** shared Clay tokens + the owner kit. Add `.tv-page` to the kit scope. One new stylesheet `wwwroot/css/vocabulary.css` replaces `vocabulary-system.css`. The lesson screen also reuses `practice.css` components and `practice-ui.js` parts. |
| V-R8 | **JS:** pure logic in a new UMD module `wwwroot/js/vocabulary-core.js` with node tests. DOM code uses `createElement` / `textContent` only; no `innerHTML` with data, no `alert` / `confirm`, no inline handlers or inline logic, no `console.log`. Config through `<script type="application/json">`. |
| V-R9 | **Copy is Vietnamese**, except English learning content and CEFR codes. New strings are only those quoted in this plan. Show **real counts** everywhere (never a number implied by a set's name). |
| V-R10 | **Out of scope:** Admin `SetManagement`; `LearningApiController`; `GoalsService`; the personal-sets and practice pages (except the additive kit scope change); `_Layout.cshtml`; shared CSS and partials; `JsonVocabularySeeder.cs`; the legacy `/Home/Study*` pages. |

### 1.2 Owner decision

| ID | Question | If CÓ | If KHÔNG / blank |
| --- | --- | --- | --- |
| D1 | Có cho agent **viết thêm nội dung từ vựng** vào `wwwroot/data/system-vocabulary.json` không? Hiện có 92 từ trong 8 bộ. | Phase V10 runs: additive content only (section 7), with an automated validator. The content is AI-written and must be reviewed by the owner before it is published. | V10 is skipped. The UI ships with the current 92 words and shows the real counts. |

D1 =

---

## 2. Scope

**In scope**
- **Controller:** `Controllers/VocabularyController.cs`.
- **Service:**
  - `Services/StudyService.Vocabulary.cs`, `Services/IStudyService.cs` (Vocabulary members only);
  - new `Services/Vocabulary/VocabularyLessonBuilder.cs`.
- **View models:** `ViewModels/VocabularyPageViewModels.cs` (rewritten; keep any type still used elsewhere).
- **Views:**
  - `Views/Vocabulary/Index.cshtml`, `Detail.cshtml`, `Study.cshtml` (rewritten);
  - new partials `_CourseCard.cshtml`, `_LessonRow.cshtml`, `_WordRow.cshtml`;
  - `FolderDetail.cshtml`, `Topics.cshtml`, `TopicDetail.cshtml`, `_MasteryButtons.cshtml` deleted once unused.
- **Assets:**
  - new `wwwroot/css/vocabulary.css`;
  - new `wwwroot/js/vocabulary-core.js`, `vocabulary-home.js`, `vocabulary-course.js`, `vocabulary-lesson.js`;
  - `wwwroot/css/study-kit.css` (scope extension only);
  - `wwwroot/css/vocabulary-system.css` deleted after a grep proves no other consumer.
- **Tests:**
  - new `TCTEnglish.Tests/VocabularyIntegrationTests.cs`, `VocabularyLessonBuilderTests.cs`;
  - new `scripts/tests/test_vocabulary_core.cjs`;
  - (D1) `SystemVocabularyDataTests.cs`.
- **Docs:** tracker row "Từ vựng", `bug-fix-log.md`, `known-issues.md`, `project-structure.md` if it lists the files.
- **(D1 only)** `wwwroot/data/system-vocabulary.json`.

---

## 3. Findings (confirm at V0 with file:line; fix or record)

| ID | Finding (evidence) | Handling |
| --- | --- | --- |
| F1 | **Content is tiny and the names mislead.** `system-vocabulary.json` has 3 folders, 8 sets, **92 cards**. "1000 từ tiếng Anh thông dụng" has 20 cards; "3000 từ vựng Oxford A1" has 10. | UI shows real counts (V-R9). Content expansion is D1. Renaming a set is **not** done here (the seeder matches sets by name; a rename would create a second set and strand learner progress). Record it for the owner. |
| F2 | **62 cards hotlink images from a third-party shopping CDN** (`down-vn.img.susercontent.com`). They can disappear or change. | Images load lazily with a local fallback tile (6.1). Record in `known-issues.md`. |
| F3 | **Dead call:** `Study.cshtml` posts to `/api/LearningApi/important`, which does not exist. | Removed with the rewrite. |
| F4 | **Six pages, about 6,500 lines of Razor with inline `<style>` and `<script>`, plus a 4,741-line stylesheet** with its own palette, hex colours and shadows. | Rewritten on tokens and the kit (V-R7). |
| F5 | **Inline `onclick` handlers** (`recordMastery('hard')`), `localStorage` settings, keyboard handlers scattered in the view. | V-R8. |
| F6 | **Four self-rating buttons** (Khó / Tốt / Dễ / Hoàn hảo) ask the learner to judge themselves on every card; "Dễ" on a first view marks a word "Mastered" without any check. | V-R5. |
| F7 | **Made-up numbers:** `EstimatedMinutes = CardCount * 2`; raw `#tags` shown as content. | Removed. Level is derived from the tags (5.3). |
| F8 | **`POST /api/LearningApi/record` does not check that the user may view the card** (any card id is accepted). The file belongs to another owner. | **Not changed here.** Record it in the report for Châu. The new client only ever sends ids of system cards it was given. |
| F9 | **Old progress vocabulary differs from personal sets** (`Learning` / `Reviewing` / `Mastered` here; `CardStatus` there). | Keep the stored values. The UI shows three states (5.4). |

---

## 4. Learning model (the product rules)

### 4.1 Terms

| Term | Meaning in code |
| --- | --- |
| Collection | a system `Folder` (owner = the user with email `system@tct.local`) |
| Course ("khóa") | a system `Set` |
| Lesson ("bài") | a computed group of cards in a course (4.2) |
| Learned ("đã học") | the user has a `LearningProgress` row for the card |
| Known ("đã thuộc") | `Status == "Mastered"` |
| Due ("cần ôn") | a progress row with `NextReviewDate <= UtcNow` |

### 4.2 Lessons: `VocabularyLessonBuilder` (pure, static, unit-tested)

```csharp
public static IReadOnlyList<LessonPlan> Build(IReadOnlyList<LessonCard> cards, int size = 10, int minTail = 4)
// LessonCard(int CardId, string? Topic); LessonPlan(int Number, string Topic, int Part, int PartCount, IReadOnlyList<int> CardIds)
```

1. Order the cards by `CardId`.
2. Normalise the topic (trim; blank → `"Từ vựng chung"`).
3. Group by topic, in the order of each topic's first card.
4. Split each group into parts of `size`. A last part with fewer than `minTail` cards is merged into the previous part (so a part holds 4–13 cards). A group with fewer than `minTail` cards stays as one part.
5. Number the lessons from 1 across the whole course.

- **Title:** `"Bài {Number} · {Topic}"`, plus `" · phần {Part}"` when `PartCount > 1`.
- **Lesson state:** `done` (every card learned), `progress` (some), `new` (none).
- **Next lesson** of a course: the first lesson that is not `done`; none → the course is complete.
- **A course with no cards** has no lessons.

### 4.3 The lesson session (client flow; logic in `vocabulary-core.js`)

**Session kinds**

| Kind | Started by | Words | Intro step |
| --- | --- | --- | --- |
| `learn` | "Học" / "Học tiếp" on a lesson that is not done | the lesson's cards that are **not learned yet** | yes |
| `relearn` | "Ôn lại" on a done lesson | all the lesson's cards | no |
| `review` | "Ôn n từ" (course) or "Ôn n từ đến hạn" (home) | due cards, oldest `NextReviewDate` first, at most 20 | no |

**Rounds:** the words are taught in rounds of 5 (`ROUND_SIZE`). A last round with fewer than 3 words is merged into the previous one.

**Each round**
1. **Làm quen** (`learn` only): one card per word, in order: picture or tile, term, IPA, speak, meaning, first example with its translation.
   - "Tiếp tục" goes to the next word.
   - "Tôi đã biết từ này" marks the word **known**: it is recorded as `easy` and skipped in step 2.
2. **Luyện tập**, pass A, then pass B, over the round's remaining words (shuffled):
   - **Pass A — choose:**
     - `learn` / `relearn`: the term is shown; choose the meaning (4 options).
     - `review`: the meaning is shown; choose the term.
   - **Pass B — type:** the meaning is shown; type the term (with the "Gợi ý" button and the letter diff from `practice-ui`).
   - A wrong answer adds 1 to the word's `misses` and re-queues the word at the end of the same pass.
   - "close" from `checkTyping` counts as correct and is shown as "Gần đúng!".
   - Fewer than 2 distractors available → pass A is skipped for that word.
3. When a word has passed both passes, it is **finished**: grade it (4.4) and record it once.

After the last round: **Kết quả**.

**Other rules**
- **Progress bar:** finished words / session words.
- **Counter:** "{finished}/{total}".
- **Leaving mid-session:** recorded words stay recorded; nothing else is stored. A confirm modal appears only when at least one answer was given and the session is unfinished.
- **Distractors:** `pickDistractors` over the session words plus the pool sent by the server (5.5).

### 4.4 Automatic grading → `masteryLevel` (V-R5)

| Session kind | Condition | `masteryLevel` sent | Shown in the result |
| --- | --- | --- | --- |
| `learn` | "Tôi đã biết từ này" | `easy` | "Đã biết" |
| `learn` | misses ≤ 1 | `good` | "Đã nhớ" |
| `learn` | misses ≥ 2 | `hard` | "Cần ôn lại" |
| `relearn`, `review` | misses = 0 | `easy` | "Đã nhớ" |
| `relearn`, `review` | misses = 1 | `good` | "Đã nhớ" |
| `relearn`, `review` | misses ≥ 2 | `hard` | "Cần ôn lại" |

- `misses` counts wrong answers of that word across both passes.
- The existing endpoint turns these into the schedule: `hard` → due in 1 minute; `good` → 1 h / 12 h / 24 h; `easy` → 1 / 3 / 7 days and "Mastered".
- **Recording:**
  - `POST /api/LearningApi/record` with `{ cardId, masteryLevel, timestamp: new Date().toISOString() }` and the antiforgery header (use `TctSets.fetchJson`);
  - on failure, retry once at once; then keep the word in a retry list that is flushed before the result screen;
  - one toast per session if anything still failed: "Không lưu được tiến độ của một số từ. Hãy kiểm tra mạng rồi học lại các từ đó.";
  - the result marks unsaved words with "Chưa lưu".
- **XP:** sum `xpEarned` from the responses. The result shows "+{xp} XP" only when the sum is above 0. Do not invent other reward numbers.

---

## 5. Backend specification (V1, V2)

Follow TDD: write the failing tests (5.6) first.

### 5.1 Routes (`VocabularyController`, `[Authorize]` as today)

| Action | Query | Renders |
| --- | --- | --- |
| `Index` | — | `Index` (home) |
| `FolderDetail` | `folderId` | `Index` with only that collection; breadcrumb "Từ vựng › {folder}". Not a system folder → 404. |
| `Detail` | `setId` | `Detail` (course), tab "Bài học" |
| `Topics` | `setId` | `Detail`, tab "Bài học" |
| `TopicDetail` | `setId`, `topic?` | `Detail`, tab "Tất cả từ", filtered to `topic` when given |
| `Study` | `setId`, `lesson?`, `topic?`, `mode?` | `Study` (lesson session) |
| `Review` (**new**) | `setId?` | `Study` in `review` kind: due cards of that course, or of all system courses |

**`Study` resolution**
- `mode=review` → `review` kind for that course.
- `lesson=n` (1-based) → that lesson; out of range → the next lesson.
- `topic=…` (legacy) → the first lesson of that topic.
- Nothing → the next lesson of the course.
- The lesson is done → `relearn`; otherwise `learn`.
- **Always 200 for a system set, with the antiforgery token rendered**, including the empty states (no cards; course complete; nothing due). Tests fetch the token from this page.
- The legacy `index` parameter is accepted and ignored.
- Keep `TrackVocabularySetViewAsync` on `Detail` and `Study`.

### 5.2 `IStudyService` additions (in `StudyService.Vocabulary.cs`)

```text
GetVocabularyHomeAsync(int userId, int? folderId = null)                          -> VocabularyHomeViewModel?      // null: folderId is not a system folder
GetVocabularyCourseAsync(int setId, int userId)                                   -> VocabularyCourseViewModel?    // null: not a system set
GetVocabularyLessonAsync(int setId, int userId, int? lesson, string? topic, bool review) -> VocabularyLessonViewModel?
GetVocabularyReviewAsync(int userId)                                              -> VocabularyLessonViewModel     // all system courses
```

- Remove the old `GetVocabulary*ViewModelAsync` methods and view models only when nothing else references them (grep the solution, including tests). `TryIncrementVocabularySetViewCountAsync` stays.
- **System scope rule (one helper):** a set is in scope only when `OwnerId == systemUserId`; a folder only when `UserId == systemUserId`. Every method uses it.
- **Queries:** `AsNoTracking`, projections, no queries in loops. One query for the sets with card counts; one for the user's progress on system cards.

### 5.3 Home view model

```text
VocabularyHomeViewModel
  Stats { LearnedCards, KnownCards, DueCards, LearnedToday, Streak, TotalCards }
  Next?  { SetId, SetName, LessonNumber, LessonTitle, NewWordCount, Url }       // the recommended lesson
  DueUrl? (when DueCards > 0)  = /Vocabulary/Review
  Collections [{ FolderId, FolderName, Courses [VocabularyCourseCard] }]
  Levels [string]                 // distinct levels present, sorted A1..C2
  FilterFolderId?, FilterFolderName?
VocabularyCourseCard { SetId, SetName, Level?, WordCount, LessonCount, LearnedCount, KnownCount, DueCount, ProgressPercent, Status ("new"|"progress"|"done"), IconKey, Hue }
```

- **`LearnedToday`:** progress rows on system cards with `LastReviewedDate` on today's business date (use the project's `BusinessDateHelper`, as Goals does).
- **`Streak`:** the existing user streak value the current home page reads.
- **`Next`:**
  1. the course studied most recently (existing `GetLastStudiedSetIdAsync`) if it has an unfinished lesson;
  2. else the first course, in collection order, with an unfinished lesson;
  3. else null.
- **`Level`:** the first tag in `Set.Description` matching `#a1 … #c2` (case-insensitive), upper-cased; none → null. Tags are not shown otherwise.
- **`IconKey`:** from the tags, first match wins:

| Tag | `IconKey` |
| --- | --- |
| `food` / `drink` | `food` |
| `travel` / `places` | `travel` |
| `jobs` / `profession` | `jobs` |
| `colors` | `colors` |
| `communication` / `conversation` | `talk` |
| `oxford` / `essential` | `book` |
| `common` / `basic` | `star` |
| (none) | `book` |

- **`Hue`:** `("primary","success","warning","info","danger","accent")[SetId % 6]`.
- **`ProgressPercent`** = learned × 100 / words (0 when empty).
- **`Status`:** `done` when every word is learned and words > 0; `progress` when some; else `new`.

### 5.4 Course view model

```text
VocabularyCourseViewModel
  Course (VocabularyCourseCard) + FolderId, FolderName
  NextLesson? { Number, Url }      CourseComplete (bool)      DueUrl? = /Vocabulary/Study?setId=&mode=review (when due > 0)
  PracticeUrl = /Practice?set={SetId}            // system sets are public, so the practice hub accepts them
  Lessons [{ Number, Title, WordCount, LearnedCount, State, Url, PreviewTerms [≤5], MoreCount }]
  Words [{ CardId, Term, Phonetic?, Definition, Example?, ExampleTranslation?, ImageUrl?, LessonNumber, Topic, State ("new"|"learning"|"known"), IsDue }]
  ActiveTab ("lessons"|"words"), TopicFilter?
```

- **Word state:** `new` = no progress; `known` = `Mastered`; `learning` = any other progress.
- **`PreviewTerms`** are filled only for the lesson recommended as next.
- **Verify `PracticeUrl` at V1:** open `/Practice?set={systemSetId}` as a Standard user. If it does not work for system sets, omit the button and record it.

### 5.5 Lesson view model (the session payload)

```text
VocabularyLessonViewModel
  Kind ("learn"|"relearn"|"review")   SetId?  SetName  LessonNumber?  LessonTitle
  Words [{ id, term, phonetic, definition, example, exampleVi, imageUrl }]          // the session words, in teaching order
  Pool  [{ id, term, definition }]                                                    // up to 60 other cards of the same course(s); top up from other system cards when fewer than 3
  CourseUrl, NextLessonUrl?, NextLessonNumber?, ReviewUrl?, HomeUrl
  EmptyReason? ("no-cards"|"complete"|"nothing-due")
```

- **`ImageUrl`** is passed only when it is an absolute `https` URL or a local path; otherwise null.
- **Never include** other users' data, `PersonalNote`, or cards outside the system scope.
- **`review` for all courses:** `SetName` = "Ôn tập", `CourseUrl` = home.

### 5.6 Tests

**`VocabularyLessonBuilderTests`** (unit)
- empty input;
- one topic of 10, 13, 14, 23 and 3 cards (tail merge);
- several topics kept in first-appearance order;
- blank topic name;
- numbering across topics;
- `Part` / `PartCount`;
- deterministic output.

**`VocabularyIntegrationTests`** (existing factory, helpers, `TestDataIds`; seed extra cards and progress in the test)
- **Legacy routes:** every URL in V-R1 returns 200 for a Standard user; a user-owned set or folder returns 404; anonymous → redirect to login.
- **Home:**
  - contains the system folder and set names (existing assertions);
  - stats reflect seeded progress;
  - `Next` points to the first unfinished lesson of the last-studied course;
  - the due link appears only when something is due;
  - `FolderDetail` shows only that collection.
- **Course:**
  - lesson list numbers and counts follow the builder;
  - `Topics` and `TopicDetail` render the same view with the right `data-active-tab`;
  - `TopicDetail` with a topic marks the filter;
  - real word counts are shown.
- **Lesson payload** (parse the JSON config):
  - `learn` contains only unlearned words of that lesson;
  - a done lesson gives `relearn` with all its words;
  - `mode=review` contains only due words, oldest first, at most 20;
  - the pool excludes session words and stays in system scope;
  - the empty states still render the antiforgery token;
  - `lesson` out of range falls back to the next lesson;
  - a legacy `topic` maps to its first lesson.
- **`/Vocabulary/Review`:** due words across courses; nothing due → the empty state.
- **Recording contract** (unchanged endpoint): posting `good` for a system card from the lesson page token still returns 200 and credits Goals (reuse the existing helper pattern).
- **Page contract:**
  - each page has `class="tct-theme tct-page tv-page`, one `<h1>`, and the kit + `vocabulary.css` links;
  - no inline `on*=` handlers;
  - the lesson page has the config JSON, `vocabulary-core.js`, and `time-tracker.js` with `data-feature="Flashcard"`. V0: confirm the current Vocabulary study page's `data-feature` value, or its absence. If it has none today, add `"Flashcard"`, which the tracker accepts.

All existing suites stay green and unedited, in particular `Sprint1/2/3SmokeTests`, `CriticalFlowSqlite*`, `GoalsPhase3*`, `GoalsUiRender*`, `SharedUiPartialsRender*`, `Ai*`, `HybridRag*`.

### 5.7 Pure client logic: `wwwroot/js/vocabulary-core.js` (V2)

- UMD: `module.exports` under Node; `window.TctVocabulary.core` in the browser.
- No DOM.
- Takes the practice helpers as an injected dependency (`create(practiceCore)`), so Node tests can pass `require('../../TCTEnglish/wwwroot/js/practice-core.js')`.

| Function | Behaviour |
| --- | --- |
| `planRounds(wordIds, size = 5, minTail = 3)` | 4.3 rounds |
| `initSession({ kind, words, rng })` | → state `{ kind, rounds, roundIndex, phase: "intro"\|"choose"\|"type"\|"result", queue, introIndex, words: { [id]: { misses, known, finished, grade, saved } }, answered, firstTryRight }` |
| `current(state)` | the current item `{ phase, wordId }`, or `null` at the result |
| `introNext(state)` / `introKnown(state)` | advance the intro; `introKnown` marks known + finished with grade `easy` |
| `answer(state, wordId, outcome)` | `outcome` ∈ `correct \| close \| wrong`. Applies the 4.3 queue rules and moves pass A → pass B → next round → result. Stray ids are ignored. |
| `questionFor(state, wordId, pool, rng)` | a choice question (prompt by kind) or a typing question; falls back to typing when fewer than 2 distractors |
| `gradeFor(kind, wordState)` | 4.4 table |
| `progress(state)` | `{ finished, total, percent }` |
| `summary(state)` | `{ total, remembered, needReview, known, accuracyPercent, unsaved }`; accuracy = first-try-correct answers / questions asked (rounded; 100 when nothing was asked) |
| `filterCourses(cards, { level, status, q })` | home filters; `q` is accent- and case-insensitive on the name |
| `filterWords(words, { q, state, lesson, topic })` | course word list; `q` matches term or definition, accent-insensitive |

**Node tests** (`scripts/tests/test_vocabulary_core.cjs`, seeded):
- rounds (5, 7, 8, 12 words → tail merge);
- a full `learn` session with no mistakes;
- a miss re-queues inside the same pass;
- "known" skips practice and grades `easy`;
- `review` skips the intro and uses the meaning → term prompt;
- the typing-only fallback with a tiny pool;
- `close` counts as correct;
- the grading table for the three kinds;
- stray answers ignored;
- the summary's accuracy;
- filters with Vietnamese accents.
- The file contains no `document`, `fetch`, or `window.` outside the UMD wrapper.

---

## 6. UI specification

### 6.1 Shared rules

- **Wrapper:** `<div class="tct-theme tct-page tv-page tv-page--home|course|lesson">`.
  - The lesson page **also** carries `tp-page tp-page--session`, so `practice.css` and the `practice-ui` parts apply.
- **Kit scope (V3):** in `study-kit.css`, add `.tv-page` to every `:is(.ts-page, .tp-page[, …])` selector list, and `tv-hue-*` to the hue blocks and the canvas rule. Whatever list exists at that time (it may already contain `.tl-page`), only append; change nothing else in that file.
- **Stylesheets, in order:** `study-kit.css`, (`practice.css` on the lesson page), `vocabulary.css`.
- **`vocabulary.css`:**
  - every selector starts with `.tv-page`;
  - `var(--tct-*)` only: no hex, `rgb()`, `:root`, `!important` or weight above 700;
  - depth uses only `--tct-clay-shadow*` tokens (plus token-coloured rings); radii use tokens;
  - a `@media (forced-colors: active)` block;
  - motion uses `--tct-dur*` / `--tct-ease` and is off under `prefers-reduced-motion: reduce`.
- **Reuse:** `_PageHeader`, `_StateMessage`, `.tct-badge--*`, `.tct-phonetic`, `.tct-btn-icon`, `.tct-clamp-2`, `.tct-break`, Bootstrap buttons/forms/modals, and the kit: `k-card`, `k-panel`, `k-panel--quiet`, `k-soft`, `k-pillow`, `k-art-box`, `k-seg`, `k-progress`, `k-kbd`, `k-btn--soft`, `k-btn--success|warning|danger`, `k-btn--icon-surface`, `k-display`, `k-label`.
- **Icons:** Font Awesome 6.0.0 only. **Course icons** (replace the mockup's emoji, which is sample art):

| `IconKey` | Icon |
| --- | --- |
| `food` | `fa-utensils` |
| `travel` | `fa-plane` |
| `jobs` | `fa-briefcase` |
| `colors` | `fa-palette` |
| `talk` | `fa-comments` |
| `book` | `fa-book-open` |
| `star` | `fa-star` |

  They are shown at `--tct-text-3xl` in `--k-fg` on the cover.
- **Word picture:**
  - `ImageUrl` when present: `<img loading="lazy" decoding="async" alt="">`, with a JS `error` listener that swaps in the fallback;
  - else a **letter tile**: the first letter of the term, upper-case, `k-display` size, on `--k-bg`.
- **Status badges:**
  - course / lesson: "Chưa học" (`tct-badge--primary`), "Đang học" (`tct-badge--warning`, `fa-clock`), "Đã xong" (`tct-badge--success`, `fa-check`);
  - word: "Chưa học", "Đang học", "Đã thuộc"; plus "Cần ôn" (`tct-badge--danger`, `fa-rotate`) when due.
- **Speech:** `TctSets.speak` (`en-US`); buttons hidden when unsupported; nothing auto-plays.
- **Laptop first:** 1440×900 and 1366×768, then 1024 / 768 / 390 / 320 and 200% zoom.

### 6.2 Home: `Index.cshtml` (mockups 01, 06)

```text
_PageHeader: Title "Từ vựng"; Description "Chọn một khóa có sẵn và học mỗi ngày vài phút. Không cần tự tạo thẻ."
            [FolderDetail] Breadcrumbs "Từ vựng" › {FolderName}; Title = FolderName
section.tv-today.k-panel   (same pattern as .ts-overview: gradient linear-gradient(135deg, var(--k-tint), var(--tct-surface) 62%), radius --tct-radius-xl)
  left:  h2  — LearnedToday > 0: "Hôm nay bạn đã học {n} từ"   |  0: "Hôm nay bạn chưa học từ nào"
         p   "Mỗi bài chỉ khoảng 5 phút với 10 từ. Học đều để giữ chuỗi ngày học."
         dl.tv-stats (k-pillow tiles): "từ đã học" LearnedCards (primary) · "từ đã thuộc" KnownCards (success) · "từ cần ôn" DueCards (warning) · "chuỗi học" "{Streak} ngày" (danger; omitted when Streak is 0)
  right: .tv-next.k-card
         [Next] label "BÀI TIẾP THEO" (k-label) · h3 "Bài {n} · {LessonTitle}" · p "{SetName} · {NewWordCount} từ mới"
                a.btn.btn-primary.btn-lg "Học tiếp" (fa-play) → Next.Url
         [no Next, words exist] h3 "Bạn đã học hết các bài hiện có" · p "Ôn lại để nhớ lâu hơn."
         [DueCards > 0] a.btn.btn-outline-primary "Ôn {n} từ đến hạn" (fa-rotate) → /Vocabulary/Review
div.tv-toolbar (role="search")
  search (label visually hidden "Tìm khóa học"; placeholder "Tìm khóa học…"; maxlength 80)
  k-seg levels:  "Tất cả" + each level present      (hidden when fewer than 2 levels)
  k-seg status:  "Tất cả" | "Chưa học" | "Đang học" | "Đã xong"
section per collection: h2 "{FolderName}" + count "{n} khóa" (links to FolderDetail when not already filtered)
  ul.tv-grid (repeat(auto-fill, minmax(280px, 1fr)))  >  li > _CourseCard
```

**`_CourseCard`** (`article.k-card.tv-course` + stretched link, `aria-label "Mở khóa {SetName}"`):
- cover `.k-art-box` (112px) in the course hue, with the icon, the level badge top-left (when `Level` has a value) and the status badge top-right;
- `h3` name (`tct-clamp-2`);
- meta: `fa-book` "{WordCount} từ" · `fa-layer-group` "{LessonCount} bài";
- `k-progress--thin` + "{LearnedCount}/{WordCount} từ" (visually-hidden "đã học");
- `data-level`, `data-status`, `data-name` for the filter.

**Behaviour**
- `vocabulary-home.js` filters client-side (`filterCourses`), hides empty collections, keeps the state in the URL (`level`, `status`, `q`) with `history.replaceState`, and shows `_StateMessage` "Không tìm thấy khóa học phù hợp." + "Xóa bộ lọc".
- **No courses at all:** `_StateMessage` "Chưa có khóa từ vựng nào." / "Nội dung đang được cập nhật.".

### 6.3 Course: `Detail.cshtml` (mockup 02)

```text
_PageHeader: Title = SetName; Breadcrumbs "Từ vựng" › {FolderName} (→ FolderDetail) › SetName      (placed inside the hero, as on /Sets/{id})
section.tv-hero.k-soft (gradient by course hue; radius --tct-radius-xl)
  .tv-hero__icon (120px k-pillow with the course icon)
  meta row: level badge · fa-book "{WordCount} từ" · fa-layer-group "{LessonCount} bài" · [DueCount > 0] "{n} từ cần ôn" (--tct-warning-text, 700)
  k-progress (12px) + "Đã học {LearnedCount}/{WordCount} từ · Đã thuộc {KnownCount}"
  actions:
     [NextLesson]      a.btn.btn-primary.btn-lg "Học tiếp bài {n}" (fa-play)   ("Bắt đầu học" when nothing is learned yet)
     [CourseComplete]  a.btn.btn-primary.btn-lg "Ôn lại từ đầu" (fa-rotate) → lesson 1
     [DueUrl]          a.btn.btn-outline-primary.btn-lg "Ôn {n} từ" (fa-rotate)
     [PracticeUrl]     a.btn.btn-outline-primary.btn-lg "Luyện bằng trò chơi" (fa-gamepad)
div[role=tablist].k-seg: "Bài học" ({LessonCount}) | "Tất cả từ" ({WordCount})      (ARIA tabs: roving tabindex, ←/→/Home/End; state in location.hash #bai-hoc / #tat-ca-tu; data-active-tab from the server)
panel "Bài học": ol.tv-lessons > li > _LessonRow
panel "Tất cả từ": toolbar (search "Tìm từ hoặc nghĩa…"; k-seg "Tất cả | Chưa học | Đang học | Đã thuộc"; [TopicFilter] chip "Chủ đề: {topic}" with a remove button) + table.tct-table in k-panel--quiet > _WordRow
```

**`_LessonRow`** (`article.k-card.tv-lesson[data-state]`):
- **Number disc** (52px):
  - `done` = `--tct-success` + `fa-check`;
  - recommended next = `--tct-primary` + the number;
  - others = tinted track + the number;
  - depth `--tct-clay-shadow-button` / `-quiet`.
- **Text:** `h3` title; "{WordCount} từ · " + "Đã học xong" / "Đã học {n} từ" / "Chưa học".
- **Preview:** the recommended lesson shows `PreviewTerms` as chips (`lang="en"`) + "+{MoreCount}".
- **Progress:** thin progress + "{LearnedCount}/{WordCount}".
- **Action:**
  - done → `btn-outline-primary` "Ôn lại" (`fa-rotate`);
  - recommended → `btn-primary` "Học tiếp" or "Học" (`fa-play`);
  - others → `btn-outline-primary` "Học".
- **Recommended row:** 2px `--tct-primary` border + `--tct-primary-tint` background + `aria-current="step"`.

**`_WordRow`**
- Columns: Từ (`lang="en"`, weight 600) · Phiên âm (`.tct-phonetic`) · Nghĩa · Bài ("Bài {n}") · Trạng thái (badges) · speak button.
- A row with an example has a disclosure button ("Xem ví dụ", `aria-expanded`), which reveals the example (`lang="en"`) and its translation in a field-inset block.
- ≤576px: stacked rows (term + meaning, status on the right).

**Empty states**
- No cards: `_StateMessage` "Khóa này chưa có từ nào.".
- Filter with no match: "Không tìm thấy từ phù hợp.".

### 6.4 Lesson: `Study.cshtml` (mockups 03, 04, 05, 07)

Shell = the practice session shell (same ids, so `practice-ui.js` works: `#tpStage`, `#tpFeedback`, `#tpProgress`, `#tpProgressFill`, `#tpCounter`, `#tpSlot`, `#tpExit`, the exit modal).

```text
div.tct-theme.tct-page.tv-page.tv-page--lesson.tp-page.tp-page--session.tp-hue-primary
  h1.visually-hidden "{LessonTitle} · {SetName}"
  header.tp-bar: exit (k-btn--icon-surface, aria-label "Thoát", href CourseUrl) · progress (role=progressbar) · counter
  ol.tv-phases (aria-label "Các bước của bài học"): "1 · Làm quen" | "2 · Luyện tập" | "3 · Kết quả"
        current = primary pill (aria-current="step"); done = fa-check + success text; review/relearn kinds omit "Làm quen" and renumber
  main#tpStage
  div#tpFeedback (aria-live=polite)
  noscript: _StateMessage "Cần bật JavaScript để học từ vựng."
  @Html.AntiForgeryToken()  ·  <script type="application/json" id="tvLesson">…</script>
```

**Step 1 — Làm quen** (`vocabulary-lesson.js`)
- **Card** `.tv-wordcard` (`k-panel`, radius `--tct-radius-xl`, `--tct-clay-shadow-lift`, centred):
  - "Từ mới" badge (warning);
  - picture or letter tile (132px);
  - term (`k-display`, `lang="en"`);
  - IPA + speak button;
  - meaning (`--tct-text-2xl`, 700);
  - the example block (field inset): sentence with the term in `--tct-primary-strong` 700 (match the term case-insensitively as a whole word; no match → plain sentence), then the translation, muted.
- **Buttons:** `btn-outline-primary btn-lg` "Tôi đã biết từ này" (`fa-check-double`) and `btn-primary btn-lg` "Tiếp tục" (+ `k-kbd` "Enter" at ≥992px).
- **Keys:** `Enter` = "Tiếp tục"; `R` = speak.
- Focus moves to "Tiếp tục" on each card.
- ≤576px: buttons stack, with "Tiếp tục" first.

**Step 2 — Luyện tập**
- **Choose:** label "Nghĩa của từ này là gì?" (term shown, with IPA and speak) or "Từ nào có nghĩa này?" (meaning shown); `ui.options` (keys 1–4, A–D on phones).
- **Type:** label "Gõ từ tiếng Anh"; the meaning shown; `ui.typingInput` (hint, "Không nhớ", diff).
- **Feedback:** `ui.feedbackPanel` ("Đúng rồi!" / "Gần đúng!" / "Chưa đúng") with the answer card; "Tiếp tục" (`Enter`).
- **Round change:** a short interstitial card "Tiếp theo: {n} từ nữa" with "Tiếp tục" (only when another round follows).

**Step 3 — Kết quả**
- **Ring** (`ui.ring`) with `accuracyPercent` and the label "trả lời đúng".
- **Title** `h2`:
  - `learn`: "Xong bài {n} rồi!";
  - `relearn`: "Ôn xong bài {n}!";
  - `review`: "Ôn tập xong!".
- **Line:**
  - `learn`: "Bạn vừa học {total} từ mới." + (needReview > 0: " {k} từ sẽ được nhắc ôn lại sớm.");
  - `relearn` / `review`: "Bạn vừa ôn {total} từ.".
- **KPIs (`k-soft`):** "từ mới" / "từ đã ôn" (primary) · "câu đúng" "{right}/{asked}" (success) · "thời gian" `m:ss` (info) · [xp > 0] "+{xp} XP" (accent).
- **Word list** (`k-panel`): term, IPA, meaning, badge ("Đã nhớ" success / "Đã biết" info / "Cần ôn lại" warning / "Chưa lưu" danger).
- **Buttons:**
  - primary:
    - [NextLessonUrl] "Học bài {n+1}" (`fa-play`);
    - [learn/relearn, no next lesson] "Về khóa học";
    - [review] "Về trang Từ vựng";
  - outline: "Về khóa học" (when it is not already the primary);
  - text link "Học lại bài này" (`relearn` URL).
- Focus moves to the title; the primary button is next in tab order.

**Empty states** (server-rendered `_StateMessage`; the token is still rendered)
- `no-cards`: "Bài này chưa có từ nào." + "Về khóa học".
- `complete`: "Bạn đã học hết khóa này." / "Ôn lại để nhớ lâu hơn." + "Ôn lại từ đầu" (lesson 1) + "Về khóa học".
- `nothing-due`: "Không có từ nào đến hạn ôn." + "Về trang Từ vựng".

**Scripts, in order**
1. `time-tracker.js` (5.6 note);
2. `personal-sets-common.js`;
3. `practice-core.js`;
4. `practice-ui.js`;
5. `vocabulary-core.js`;
6. `vocabulary-lesson.js`.

`practice-ui.js` may be changed **only additively** (e.g. exporting an existing internal helper), with the practice regression in G13. Prefer not to change it.

---

## 7. Content expansion (V10, only if D1 = CÓ)

- **Additive only**, in `wwwroot/data/system-vocabulary.json`.
  - Never rename or remove a folder, set or card.
  - The seeder **deletes** cards of an existing set that are missing from the JSON, and matches sets by name.
- **Do not add cards to the two sets whose names contain "Oxford".** That name refers to a publisher's own word list; do not reproduce it.
- **Targets:**
  - bring each of the other six existing sets to 30–40 cards (keep every existing card unchanged, appended after);
  - add six new sets of 30 cards each in the folder "Từ Vựng Theo Chủ Đề": "Gia đình & Bạn bè", "Nhà cửa & Đồ dùng", "Trường học", "Công việc văn phòng", "Sức khỏe & Cơ thể", "Mua sắm & Tiền bạc";
  - each new set has a `Description` of tags, including a level tag (`#a1` or `#a2`).
- **Each card:**
  - `Term`: everyday English word or short phrase;
  - `Definition`: natural Vietnamese meaning;
  - `Topic`: `"English (Tiếng Việt)"`, 8–12 cards per topic;
  - `Phonetic`: IPA between slashes;
  - `Example`: one simple sentence that contains the term;
  - `ExampleTranslation`;
  - **no** `ImageUrl`.
- **Validator** `SystemVocabularyDataTests` (xUnit, reads the JSON file):
  - valid JSON with the expected shape;
  - required fields non-blank;
  - no duplicate `Term` (case-insensitive) inside a set;
  - `Phonetic` matches `^/.+/$`;
  - `Example` contains the term's first word stem (case-insensitive);
  - every pre-existing term from the V0 snapshot is still present with an identical `Definition`;
  - the "Oxford" sets are unchanged.
- **Run the app once** against the local database. Record the seeder log line and the new card counts. Never reset the database.
- **Check `AGENTS.md` shared-data rules first.** If this JSON is governed by a shared-data workflow that this plan cannot satisfy, skip V10 and record why.
- **Report:** state clearly that the content is AI-written and needs the owner's review before publishing.

---

## 8. Phases and the phase gate

**Run mode: continuous.** Go from V0 to V11 in one run without stopping to ask the owner. A phase ends only when its gate (8.1) is fully green. Then write its gate record (8.3), commit, and start the next phase immediately.

| Phase | Work | Skills |
| --- | --- | --- |
| V0 | **Baseline.** Check branch/HEAD/tree; `git switch master`; verify it contains `d8c5773`; create `feature/vocabulary-rebuild`. Build; full Release tests; all node tests (record counts and warnings). Save this plan. Read D1. Open the 7 mockups. Confirm F1–F9 with file:line. Record the hook inventory: routes, test-asserted strings, the `record` contract. Snapshot the JSON's terms (for V10). Start the app. Take **before** screenshots of all six legacy pages at 1440×900, 1366×768 and 390. | superpowers, engineering, ui-review |
| V1 | **Backend:** lesson builder + tests; service methods; view models; controller actions; minimal placeholder views that render the data and the config JSON (section 5). | vocabulary-feature, feature-scaffold, security-audit |
| V2 | **`vocabulary-core.js`** + node tests (5.7) | engineering |
| V3 | **Kit scope extension** + `vocabulary.css` foundation; regression of `/Sets*` and `/Practice*` | ui-ux-pro-max, ui-component |
| V4 | **Home page** (6.2) + `vocabulary-home.js` | ui-ux-pro-max, ui-component |
| V5 | **Course page** (6.3) + `vocabulary-course.js` | ui-ux-pro-max, ui-component |
| V6 | **Lesson: shell + step 1 "Làm quen"** (6.4) | ui-component, ui-motion |
| V7 | **Lesson: step 2 "Luyện tập"**, grading and recording (4.3, 4.4) | api-endpoint, ui-component, ui-motion |
| V8 | **Lesson: step 3 "Kết quả"**, `relearn` and `review` kinds, `/Vocabulary/Review`, empty states | ui-component, ui-motion |
| V9 | **Cleanup:** delete the old views, partial and stylesheet (after grep); remove unused service methods and view models; docs, tracker, bug-fix log, known issues | engineering |
| V10 | **Content** (section 7) — only if D1 = CÓ | data-seeder |
| V11 | **Full gate** on every page, state and legacy URL; final self-review against AGENTS.md, DESIGN.md and section 10 | ui-review, security-audit, engineering |

### 8.1 Phase gate (every phase, every applicable item, in order)

| # | Check | Command / method | Pass when |
| --- | --- | --- | --- |
| G1 | Build | `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` | 0 errors; warnings ≤ V0 |
| G2 | Area tests | `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~Vocabulary\|FullyQualifiedName~Sprint\|FullyQualifiedName~CriticalFlow\|FullyQualifiedName~GoalsPhase3"` | 0 failed |
| G3 | Full tests | `dotnet test TCTEnglish.Tests -c Release` | 0 failed; passed ≥ V0 (+ new); skipped = V0 |
| G4 | Node tests | `node --test scripts/tests/*.cjs` | 0 failed; count ≥ V0 |
| G5 | Contracts | Re-check the V0 hook inventory: every legacy URL → 200; asserted strings; the `record` request shape | Nothing removed or changed |
| G6 | Tokens-only + diff | `git diff --check` + the grep below | Prints nothing |
| G7 | Scope | `git diff --name-only <previous commit>` | Only section 2 files; no forbidden file |
| G8 | Render | Each touched page at **1440×900, 1366×768, 1024, 768, 390, 320** and 200% zoom; screenshots to `.tmp/vocabulary/<phase>/`; read the console and network | No horizontal scroll, clipping or overlap; no console errors; no failed CSS/JS; Clay canvas covers the page; header/sidebar unchanged |
| G9 | Mockup match | 1440 and 390 screenshots side by side with the phase's mockup(s); also compare with `/Sets` and `/Practice` | Matches, except the recorded differences |
| G10 | Behaviour | The phase checklist (8.4) with mouse **and** keyboard only | All items pass |
| G11 | Accessibility | Tab through every control; visible focus; one `<h1>`; labels on icon buttons; ARIA tabs with arrow keys; live regions announce; states visible in `forced-colors: active`; reduced motion; no keyboard trap | All pass |
| G12 | Contrast | Script the computed colours of new text/control pairs | Text ≥ 4.5:1 (large ≥ 3:1); boundaries/icons ≥ 3:1 |
| G13 | Regression | `/Sets`, `/Sets/{id}`, `/Practice`, one practice mode end to end, `/Goals`, `/Home/Index`, `/Home/Study/{id}`; an AI chat answer that links to Vocabulary | Unchanged from V0 |
| G14 | Clay conformance | The grep below | Every `box-shadow` uses a `--tct-clay-*` / `--tct-shadow-*` token, `none`, or a token ring; no hard-coded radius px except `50%` |
| G15 | Security | The access tests (5.6); grep the vocabulary views and JS for `innerHTML`, `alert(`, `confirm(`, `Html.Raw` (outside the JSON config), inline `on*=` and `console.log` | Tests green; grep prints nothing unexpected |

```powershell
$css = 'TCTEnglish/wwwroot/css/vocabulary.css'
Select-String -Path $css -Pattern '#[0-9a-fA-F]{3,8}\b|rgba?\(|:root|!important|font-weight:\s*(8|9)00'
Select-String -Path $css -Pattern 'box-shadow:' | Where-Object { $_.Line -notmatch 'var\(--tct-(clay-|shadow-)|box-shadow:\s*none' }
Select-String -Path $css -Pattern 'border-radius:\s*\d+px'
Select-String -Path TCTEnglish/wwwroot/js/vocabulary-*.js,TCTEnglish/Views/Vocabulary/*.cshtml -Pattern 'innerHTML|alert\(|confirm\(|console\.log|\son[a-z]+='
```

V0 runs G1, G3, G4 and G8 (before-screenshots). V1 and V2 run G1–G7 and G15. V10 runs G1–G5, G7 and its validator, plus a render check of the home and one new course. V11 runs everything.

### 8.2 When the gate fails

- Fix the cause, then re-run the **whole** gate.
- Up to 3 attempts per failing item. Then:
  - **(a) Caused by this phase:** revert that element to its previous state, mark it "deferred" with the reason, and re-run the gate.
  - **(b) Outside scope:** record the evidence and mark it "blocked". Continue only when every in-scope item is green.
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
### Vk — <name>
- Commit: <hash>
- G1 build: pass (warnings n, V0 n)
- G2 area: passed x / failed 0 / skipped y
- G3 full: passed x / failed 0 / skipped y (V0 …)
- G4 node: passed x / failed 0
- G5 contracts: unchanged
- G6 tokens/diff: clean
- G7 scope: files …
- G8 render: pages × sizes, .tmp/vocabulary/Vk/
- G9 mockup: differences …
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
| V1 | Every legacy URL returns 200 and shows real data in the placeholder views; a user-owned set/folder returns 404; the lesson config JSON matches the seeded progress |
| V3 | The placeholder pages render on the Clay canvas; `/Sets*` and `/Practice*` look identical to V0 |
| V4 | Home numbers match the database for a seeded user; "Học tiếp" opens the recommended lesson; the due button appears only when something is due and opens the review; level, status and search filters alone and combined, with and without Vietnamese accents; URL state survives reload; "Xóa bộ lọc"; `FolderDetail` shows one collection with its breadcrumb; the empty state |
| V5 | Hero numbers and buttons for a new, an in-progress and a complete course; lesson rows match the builder; the recommended row is marked; tabs with mouse, arrow keys and `#hash`; `Topics` and `TopicDetail` URLs open the right tab and filter; word search and state filter; example disclosure; speak; "Luyện bằng trò chơi" opens the practice hub for that course |
| V6 | Step 1: each word's card (with image, with broken image → tile, without image); speak; Enter and R; "Tôi đã biết từ này" records `easy` (check the network call and the database) and skips practice; focus order; exit modal rules |
| V7 | Step 2: correct, wrong and close answers; a wrong word comes back in the same pass; hint, "Không nhớ", diff; keys 1–4/A–D and Enter; rounds of 5 with the interstitial; **one** `record` call per word with the right `masteryLevel` for 0, 1 and 2+ misses; a failed call is retried and reported; the course page then shows the words as learned |
| V8 | Step 3: numbers, badges, XP line only when earned; "Học bài {n+1}" opens the next lesson; last lesson shows "Về khóa học"; `relearn` on a done lesson; course review and `/Vocabulary/Review` with due words, limited to 20; the three empty states; Goals shows today's vocabulary activity increased |
| V9 | Every legacy URL still works after the old files are deleted |
| V10 | The home shows the new courses with real counts; one new lesson can be studied end to end |
| V11 | Everything above, end to end, on a fresh browser profile, at 1366×768 first |

---

## 9. Verification summary (V11)

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/*.cjs
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext   # must report no changes
git diff --check
```

- Plus the G6 / G14 / G15 greps.
- **Browser:** `tct-ui-review` + `team-ui-workflow.md` §7, with synthetic accounts from `.appdata/local/access.json` (never print passwords).
- Screenshots stay in ignored `.tmp/vocabulary/`.

## 10. Definition of done

- [ ] A learner can go from the home page to a finished lesson with one button per step, by mouse or by keyboard alone.
- [ ] Every legacy URL returns 200 with the new views; non-system data returns 404.
- [ ] Progress is recorded once per word through the unchanged endpoint; Goals/XP/streak tests are green and unedited.
- [ ] The three screens follow section 6 and the mockups, use the shared Clay language and match `/Sets` and `/Practice`.
- [ ] `vocabulary-core.js` and `VocabularyLessonBuilder` are pure and covered by tests.
- [ ] The UI JS has no `innerHTML` with data, `alert`, `confirm`, inline handlers or `console.log`.
- [ ] No migration; no `Program.cs`; no forbidden file touched.
- [ ] The old views, partial and stylesheet are gone (or kept with a recorded reason).
- [ ] Tokens-only and Clay greps are clean; forced colours, reduced motion, keyboard and contrast are verified and recorded.
- [ ] All tests are green at or above the V0 counts.
- [ ] Gate records for V0–V11 are complete; the tracker row and docs are updated; F1, F2 and F8 are reported to the owner.
- [ ] The Vietnamese report separates verified from not verified.

## 11. Risks

| Risk | Mitigation |
| --- | --- |
| A lesson's membership shifts when an admin adds or deletes cards | Lessons are computed from current data; progress is per card, so nothing is lost. Record it in `known-issues.md`. |
| Tests expect 200 on `Study` even with odd data | Empty states are server-rendered with the token (5.1). |
| Reusing `practice-ui.js` couples two areas | Same owner; changes are additive only; G13 runs a practice mode end to end. |
| The kit scope edit conflicts with the Listening branch | Append-only edit of selector lists; resolve by keeping both classes. |
| Words marked "known" by one tap | Only the explicit "Tôi đã biết từ này" does that; every other path requires correct answers. |
| AI-written content (D1) has mistakes | Validator test + owner review before publishing; additive only. |

## 12. Execution notes (agent fills in; one gate record per phase)

- Branch / HEAD at V0: started on `feature/listening-redesign` (084830c; tree had only a trailing-newline change in `appsettings.Development.json`, never staged, plus untracked `Claude outputs/`). Local `master` (062851f) is 31 commits behind `origin/master` and does not contain d8c5773, so it was not used. `feature/vocabulary-rebuild` was created from the already-fetched local ref `origin/master` (09d9c8e, contains d8c5773 / PR #135). No fetch or pull was run (tree not clean, so `pull-local.ps1` was not allowed). The Listening redesign is not in this base; the plan does not depend on it.
- D1 as received: blank in the prompt and plan, so KHÔNG (V10 skipped).
- Baseline counts (tests / node / warnings): build 0 errors, 43 warnings. `dotnet test` Release: passed 1468, failed 0, skipped 34 (MariaDB-only suites), total 1502. Node: 271 tests, 270 pass, 1 fail before any change (`test_speaking_catalog_packages.cjs`: sha256 of `data/listening/reach-a1-c2-catalog.json` differs because `core.autocrlf=true` checks it out with CRLF; Windows-only and unrelated to Vocabulary). G4 therefore means no new failures and at least 270 passing.
- Findings confirmed (F1–F9 with file:line):
  - F1: system JSON = 3 folders, 8 sets, 92 cards; "1000 từ…" 20, "3000 từ vựng Oxford A1" 10, A2 10. F2: 62 cards carry an `ImageUrl`.
  - F3: `Views/Vocabulary/Study.cshtml:2069` posts to `/api/LearningApi/important` (no such action).
  - F4: legacy views 172 + 703 + 341 + 1780 + 2475 + 730 + 550 lines; `wwwroot/css/vocabulary-system.css` 4741 lines.
  - F5: inline `recordMastery(...)` handlers and `localStorage` (`Study.cshtml:2355-2360`).
  - F6: four mastery buttons in `_MasteryButtons.cshtml`.
  - F7: `StudyService.Vocabulary.cs:358` `EstimatedMinutes = Math.Max(1, CardCount * 2)`.
  - F8: `LearningApiController.cs:34-50` `record` looks up progress by (cardId, current user) and never checks that the user may view the card.
  - F9: `Learning` / `Reviewing` / `Mastered` strings in `LearningProgress.Status`.
- Hook inventory:
  - Routes pinned by tests: `/Vocabulary/Index`, `FolderDetail?folderId=`, `Detail?setId=`, `Topics?setId=`, `TopicDetail?setId=`, `Study?setId=` (Sprint1/3, CriticalFlowSqlite). The antiforgery token is read from `/Vocabulary/Study?setId=302` (CriticalFlow, GoalsPhase3, Sprint1/2). `GoalsUiRenderTests` asserts the text `/Vocabulary` in the dashboard HTML. `Sprint4SmokeTests` only checks that `ViewModels/VocabularyPageViewModels.cs` exists (keep the file name). `SharedUiPartialsRenderTests` uses `/Vocabulary/Index` only as sample breadcrumb data.
  - **Plan correction:** `/Vocabulary` (without `/Index`) is 404 today (the default route action is `Landing`), so it is not a legacy URL that already works. V1 adds it as an additive alias next to `/Vocabulary/Index` (attribute routes on `Index`).
  - `record` contract: `POST /api/LearningApi/record` with `{ cardId, masteryLevel, timestamp }` and the antiforgery header.
  - The current `Study.cshtml` has no `time-tracker.js`; the new lesson page adds it with `data-feature="Flashcard"` (value already used by `Views/Study/Study.cshtml`).
  - Streak: `StudyService.Vocabulary.cs:26` reads `user.Streak ?? 0`. Last studied set: private `GetLastStudiedSetIdAsync` (`StudyService.Vocabulary.cs:209`).
  - Consumers: `vocabulary-system.css` and `_MasteryButtons` are used only by the six Vocabulary views (grep). `.tl-page` does not exist in `study-kit.css` on this base (lists are `:is(.ts-page, .tp-page)`).
- `PracticeUrl` check result: `/Practice?set=11` (system set) as Standard returns 200 and preselects the set; `/Practice/quiz?set=11&n=5` returns 200. The button stays.
- Local data and environment:
  - The local Docker DB had no system user or system vocabulary (`Database:SeedOnStartup` is false and also drives other seeders, so it was not enabled). The 3 folders, 8 sets and 92 cards were inserted with an idempotent SQL fixture generated from `system-vocabulary.json` (system user id 5, folders 4-6, sets 11-18). It is the content the seeder would create and no existing row was touched. The fixture and the JSON term snapshot are in ignored `.tmp/vocabulary/`.
  - Docker Desktop was stopped and was started for this run. App on port 5100 via `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5100`.
- Old methods / views / CSS removed or kept (with reason):
- Mockup vs rule differences:
- `color-mix` uses:
- Contrast results:
- Browser coverage / not verified:
- For the owner: F1 (set names vs real counts), F2 (hotlinked images), F8 (`record` access check):
- Final counts:

### V0 — Baseline
- Commit: this commit (docs only: the plan)
- G1 build: pass (warnings 43, V0 43)
- G3 full: passed 1468 / failed 0 / skipped 34
- G4 node: passed 270 / failed 1 (pre-existing CRLF hash) / total 271
- G8 render: six legacy pages at 1440×900, 1366×768 and 390, screenshots in `.tmp/vocabulary/before/`. One `<h1>` each (Study has 2), no console errors, no failed requests, no horizontal scroll.
- G2, G5–G7, G9–G15: not applicable at V0 (no code change); G7: only the plan file is added.

### V1 — Backend
- Commit: see `git log` (subject "feat(vocabulary): add the lesson builder, course services and placeholder views")
- G1 build: pass (warnings 43, V0 43)
- G2 area: passed 147 / failed 0 / skipped 0
- G3 full: passed 1522 / failed 0 / skipped 34 (V0: 1468 / 0 / 34; +54 new tests)
- G4 node: passed 270 / failed 1 (same pre-existing CRLF hash failure as V0) / total 271
- G5 contracts: unchanged. Every legacy URL answers 200 (Index, FolderDetail, Detail, Topics, TopicDetail, Study with `setId`, `topic`, `index`, `mode=review`); user-owned and unknown set/folder ids give 404; the `record` request/response is untouched and was re-proved from the new lesson page token (`Record_FromTheLessonPageToken_StillCreditsGoalsForASystemCard`).
- G6 tokens/diff: `vocabulary.css` grep prints nothing; `git diff --check` flags only the pre-existing trailing blank line in the owner's unstaged `appsettings.Development.json` (never staged).
- G7 scope: `VocabularyController.cs`, `IStudyService.cs`, `StudyService.Vocabulary.cs`, `VocabularyPageViewModels.cs`, `Services/Vocabulary/VocabularyLessonBuilder.cs`, `Views/Vocabulary/{Index,Detail,Study}.cshtml` (placeholders), `wwwroot/css/vocabulary.css` (comment-only stub), `VocabularyIntegrationTests.cs`, `VocabularyLessonBuilderTests.cs`. No forbidden file.
- G8–G14: not run (the plan lists only G1–G7 and G15 for V1/V2; the views are data placeholders).
- G15 security: access tests green (user-owned set/folder 404 on every route; anonymous never gets 200; the other user's progress is not counted; pool stays in the system scope; image URLs limited to https or local paths; a term containing `</script>` stays data). Grep: the only `Html.Raw` is the lesson config JSON (`Study.cshtml:29`, built by `ToConfigJson()` whose default encoder escapes `<`, `>`, `&`).
- Decisions and plan corrections:
  - `/Vocabulary` was a 404 (default route action is `Landing`). Added `[HttpGet("/Vocabulary/Index", Order = 0)]` + `[HttpGet("/Vocabulary", Order = 1)]` on `Index`; `Order 0` keeps generated sidebar/dashboard links on `/Vocabulary/Index` (asserted by `GeneratedLinksToTheHome_KeepTheCanonicalIndexUrl`).
  - Old view models and old `GetVocabulary*ViewModelAsync` methods are kept until V9 because the still-present `FolderDetail`, `Topics` and `TopicDetail` views compile against them (now unreachable).
  - The home "Bài {n} · {LessonTitle}" in 6.2 would repeat "Bài"; `LessonTitle` holds the full "Bài 5 · Animals" and the view prints it once (mockup 01 shows one prefix).
  - Icon key uses tag order (first tag with a mapping wins); level = first `#a1…#c2` tag.
  - Course-scoped review uses the existing URL `/Vocabulary/Study?setId=&mode=review`; global review is `/Vocabulary/Review`; `Review?setId=` is also accepted.
- Attempts / deferred / blocked: none.

### V2 — `vocabulary-core.js`
- Commit: see `git log` (subject "feat(vocabulary): add the pure lesson-session logic and its node tests")
- G1 build: pass (warnings 43, V0 43)
- G2/G3: C# unchanged since V1; full run passed 1522 / failed 0 / skipped 34
- G4 node: passed 291 / failed 1 (same pre-existing CRLF hash failure) / total 292; the 21 new tests in `test_vocabulary_core.cjs` pass
- G5 contracts: unchanged (no C# or view change)
- G6 tokens/diff: `git diff --check` clean for the new files; no CSS touched
- G7 scope: `wwwroot/js/vocabulary-core.js`, `scripts/tests/test_vocabulary_core.cjs` only
- G8–G14: not applicable (no UI in this phase)
- G15: grep for `innerHTML|alert(|confirm(|console.log|on*=` in the module prints nothing; a test asserts the module has no `document`, `fetch`, `window`, `localStorage` or `console.` outside the UMD wrapper
- Interpretation recorded (plan 5.7 "accuracy"): `asked` counts each distinct (word, pass) question once, `right` counts those answered correctly on the first try ("close" included), so a retry after a miss is neither a new question nor a first-try answer. Result shows "câu đúng {right}/{asked}".
- API additions beyond the plan table, all pure: `initSession` also takes `pool` (distractor candidates), `answer` returns an event `{ ignored, correct, finished, grade, phaseChanged, roundChanged }`, plus `markSaved`, `roundInfo`, `fold`.
- Attempts / deferred / blocked: none.

### V3 — Kit scope extension and `vocabulary.css` foundation
- Commit: see `git log` (subject "feat(vocabulary): extend the shared kit scope to .tv-page and add the stylesheet foundation")
- G1 build: pass, unchanged (CSS only; warnings 43)
- G2/G3: full run passed 1522 / failed 0 / skipped 34 (includes the tests that read the kit)
- G4 node: passed 291 / failed 1 (pre-existing CRLF hash failure) / total 292
- G5 contracts: unchanged
- G6 tokens/diff: the three greps on `vocabulary.css` print nothing (no hex, `rgb()`, `:root`, `!important`, weight 800+, no non-token shadow, no px radius); `git diff --check` clean
- G7 scope: `wwwroot/css/study-kit.css` (append-only), `wwwroot/css/vocabulary.css`
- G8 render: home, folder, course, lesson, review placeholders at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640; one `<h1>` each, no horizontal scroll, no console error, no failed request; `.tmp/vocabulary/V3/vocab/`. The Clay canvas covers `.main`/`body` (`rgb(240,243,255)` plus glow, identical to `/Sets`); header and sidebar unchanged. 200% zoom is covered by the 640-wide viewport rows and re-checked on the real pages in V4-V8.
- G9–G12: not applicable to data placeholders (no styled components yet)
- G13 regression: `/Sets`, `/Sets/1`, `/Practice`, `/Practice/learn?set=1&n=10` rendered with the old and the new `study-kit.css` at 1440 and 390: PNG files byte-identical. `/Practice/quiz` is not comparable byte for byte because its question order is random (two shots with the same CSS differ too); its scroll height is the same (1327 px) with both kits.
- G14 Clay grep: clean
- Kit change in detail: 142 scope lists `:is(.ts-page, .tp-page)` became `:is(.ts-page, .tp-page, .tv-page)`; the six hue blocks gained `tv-hue-*` (descendant form and `.tv-page.tv-hue-*`); only header comments otherwise. No selector removed or reordered, specificity unchanged.
- Attempts / deferred / blocked: none.

### V4 — Home page
- Commit: see `git log` (subject "feat(vocabulary): build the home page with today's status, next lesson and course filters")
- G1 build: pass (warnings 43, V0 43)
- G2 area: tests whose name contains "Vocabulary" passed 68 / failed 0 (19 builder + 37 integration + others; the 2 new home-markup tests included)
- G3 full: passed 1522 + 2 new / failed 0 / skipped 34 (the 1522 run was taken before the two markup tests were added; the Vocabulary filter was re-run after)
- G4 node: passed 291 / failed 1 (pre-existing CRLF hash failure) / total 292
- G5 contracts: unchanged; `/Vocabulary/Index`, `/Vocabulary` and FolderDetail answer 200; `/Home/Index` still links to `/Vocabulary/Index`
- G6 tokens/diff: the three CSS greps and the JS/Razor greps print nothing; `git diff --check` clean
- G7 scope: `Views/Vocabulary/Index.cshtml`, new `_CourseCard.cshtml`, `wwwroot/css/vocabulary.css`, new `wwwroot/js/vocabulary-home.js`, `VocabularyIntegrationTests.cs`
- G8 render: home at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640, plus the no-match state; no horizontal scroll, no console error, no failed request; `.tmp/vocabulary/V4/`. 200% zoom = 720 CSS px wide and 320 px: page width equals viewport; the level/status segmented control scrolls inside itself at 320.
- G9 mockup (01, 06): same structure and copy. Recorded differences: emoji cover art is replaced by Font Awesome icons (plan 6.1); the mockup's sample numbers are real data here; the status filter stays on phones (the mockup shows only the level filter) because the plan lists both; segmented controls scroll horizontally instead of wrapping.
- G10 behaviour (21 scripted checks, all pass): numbers equal the database fixture (23 learned, 10 known, 2 due, 13 today, streak 5); "Học tiếp" = `/Vocabulary/Study?setId=11&lesson=2` "Bài 2 · Greetings (Chào hỏi)", 5 new words; due button opens `/Vocabulary/Review`; level, status and search alone and combined, with and without accents or capitals; URL state survives reload and invalid values are ignored; empty collections hide and counts update; no-match state and live region; "Xóa bộ lọc" resets and focuses the search; FolderDetail shows one collection with its breadcrumb; keyboard Tab/Enter on filters.
- G11 a11y: one `<h1>`, `lang="vi"`, every control named; 22 tab stops, all with a visible 2px outline; forced-colors: cards get a 1px border and no shadow; reduced motion: no animation in this page; no keyboard trap.
- G12 contrast (scripted over 40+ element/background pairs): lowest ratios 4.95 to 5.9:1 for coloured text (stat "10" 5.48, "Học tiếp" 5.28, "Đã xong" 5.21, "BÀI TIẾP THEO" 5.87); body and heading text 8.3 to 17.1:1; zero failures.
- G13 regression: `/Sets`, `/Sets/1`, `/Practice`, `/Practice/quiz`, `/Goals`, `/Home/Index`, `/Home/Study/1` all 200 (kit unchanged since V3).
- G14 Clay grep: clean. G15: tests green; greps clean (no `innerHTML`, `alert(`, `confirm(`, `console.log`, inline handlers, `Html.Raw`).
- Local data used for G10: synthetic progress rows for `student@tct.local` on system cards, in `.tmp/vocabulary/progress-fixture.sql`; the matching `progress-cleanup.sql` restores user 2 to zero rows and streak 0 (which is what it had before).
- Attempts / deferred / blocked: none. One script expectation (A1 count) was wrong in the checking script itself, not in the page; corrected and re-run, 21/21.

### V5 — Course page
- Commit: see `git log` (subject "feat(vocabulary): build the course page with lessons, word list and ARIA tabs")
- G1 build: pass (warnings 43, V0 43)
- G2 area: tests whose name contains "Vocabulary" passed (40 integration + 19 builder + others), 0 failed
- G3 full: passed 1527 / failed 0 / skipped 34 (V0 1468; 59 new tests so far)
- G4 node: passed 291 / failed 1 (pre-existing CRLF hash failure) / total 292
- G5 contracts: unchanged; Detail, Topics and TopicDetail render the course view with the right tab; `data-active-tab` and `data-topic-filter` stay in the markup
- G6/G14/G15: the CSS greps print nothing; the JS/Razor grep finds no `innerHTML`, `alert(`, `confirm(`, `console.log`, inline handler or `Html.Raw` in the new files. It still lists the legacy views `_MasteryButtons`, `FolderDetail`, `Topics`, `TopicDetail`, which V9 deletes. `git diff --check` clean.
- G7 scope: `Views/Vocabulary/Detail.cshtml`, new `_LessonRow.cshtml` and `_WordRow.cshtml`, `wwwroot/css/vocabulary.css`, new `wwwroot/js/vocabulary-course.js`, `VocabularyIntegrationTests.cs`
- G8 render: in-progress, complete and new course at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640, plus the words tab and a topic filter; no horizontal scroll, console error or failed request; `.tmp/vocabulary/V5/`
- G9 mockup (02): same structure and copy. Recorded differences: the breadcrumb sits inside the hero because the plan says "as on /Sets/{id}" (the mockup draws it above); a Font Awesome star/book icon replaces the emoji; the number disc of a finished lesson uses `--tct-success-text` (5.48:1) instead of the brighter `--tct-success` (2.54:1 with a white check); phones stack lesson rows and word rows, with each action full width.
- G10 behaviour (32 scripted checks, all pass): hero title, breadcrumb, real counts, level, due text and progress; actions for an in-progress, a new ("Bắt đầu học") and a complete course ("Ôn lại từ đầu" to lesson 1, no current step); lesson rows follow the builder, one `aria-current="step"`, chips only on it; tabs by mouse, ArrowLeft/Right with wrap, Home/End, roving tabindex, `#bai-hoc` and `#tat-ca-tu`; `Topics` opens lessons, `TopicDetail` opens words, `topic=` filter with a removable chip that drops `?topic` and returns focus to the search; word search by term or meaning (accents, capitals), state filter and both combined, no-match state and live region; example disclosure; speak button (visible when the browser can speak, calls `speechSynthesis` with `en-US`); "Luyện bằng trò chơi" opens `/Practice?set=11` with the course preselected; keyboard reaches hero actions, tabs and lesson buttons.
- G11 a11y: one `<h1>`, every control named, all tab stops have a visible outline (9 on the lessons tab, 32 on the words tab); `tabpanel`s carry no `tabindex` because they contain focusable controls; forced colours: cards, hero, discs, chips and examples get 1px borders and no shadow, the current lesson gets a 3px border, the selected tab a 2px border; reduced motion: no animation here; 200% zoom (720 px) and 320 px: page width equals the viewport.
- G12 contrast: scripted over about 35 pairs on both tabs. One real failure found and fixed: white check on `--tct-success` was 2.54:1, now 5.48:1. Text pairs are 4.9 to 17:1. Progress-bar fills keep the kit colours (success 2.54:1 and warning 2.15:1 against the track) because the exact `x/y` count is always printed next to the bar, so the colour is not the only carrier of information.
- G13 regression: `/Sets`, `/Sets/1`, `/Practice`, `/Practice/quiz`, `/Goals`, `/Home/Index`, `/Home/Study/1` all 200; `/Home/Index` still links to `/Vocabulary/Index`.
- Attempts / deferred / blocked: none. One check expectation in my script was wrong (a roving-tabindex tablist only has the selected tab in the Tab order); the script was corrected, the page was right.

### V6, V7, V8 — Lesson screen (one commit, three gate records)
- **Process deviation (recorded):** steps 1, 2 and 3 live in one script (`vocabulary-lesson.js`) and one view (`Study.cshtml`), so they were written together and verified with three separate checklists (V6, V7, V8 below), then committed once instead of three times. Every gate item was still run on the finished code.
- Commit: see `git log` (subject "feat(vocabulary): build the three-step lesson with automatic grading and a result screen")
- Shared by the three records:
  - G1 build: pass (warnings 43, V0 43)
  - G3 full: passed 1529 / failed 0 / skipped 34 (V0 1468; 61 new tests so far, 42 of them in `VocabularyIntegrationTests`)
  - G4 node: passed 291 / failed 1 (pre-existing CRLF hash failure) / total 292
  - G5 contracts: unchanged. The `record` request is `{ cardId, masteryLevel, timestamp }` with the antiforgery header through `TctSets.fetchJson`; the endpoint was not touched. Legacy URLs answer 200.
  - G6/G14/G15 greps: CSS greps print nothing; no `innerHTML`, `alert(`, `confirm(`, `console.log` or inline handler in the new JS and views; the only `Html.Raw` is the lesson config JSON (`Study.cshtml:119`, default encoder escapes `<`, `>`, `&`). `git diff --check` clean.
  - G7 scope: `Views/Vocabulary/Study.cshtml`, `wwwroot/css/vocabulary.css`, new `wwwroot/js/vocabulary-lesson.js`, `VocabularyIntegrationTests.cs`. `practice-ui.js`, `practice-core.js`, `practice.css` and `personal-sets-common.js` are untouched and only used.
  - G13 regression: `/Sets`, `/Sets/1`, `/Practice`, `/Practice/quiz`, `/Goals`, `/Home/Index`, `/Home/Study/1` all 200. An AI chat answer that links to Vocabulary was not run (it would call the paid model); no AI service builds a Vocabulary URL, and every legacy URL it could use returns 200 (tests).
  - Local data: sessions were run against the synthetic progress of `student@tct.local`; the fixture is reset before each scenario (`resetFixture`).
- **V6 — shell and step 1 "Làm quen"**
  - G8: intro card at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640; no horizontal scroll; `.tmp/vocabulary/V8/intro-*.png`. The seed picture URL `/images/greetings/*.jpg` answers 404 (data, not code); the card falls back to the letter tile as designed.
  - G9 (mockups 03, 07): same layout and copy; emoji picture is a letter tile or the real image; "Enter" hint and kbd only at 992 px and up.
  - G10 (20 scripted checks, all pass): the lesson loads as `learn` with the 5 unlearned words; step pills; counter 0/5; card content (badge, term, IPA, meaning, term highlighted in the example, translation); a picture that loads is kept (`alt=""`, lazy, async); a failing one becomes a letter tile; focus lands on "Tiếp tục"; R speaks in `en-US`; Enter (no button focused) moves on; Tab order reaches "Tôi đã biết từ này"; that button sends exactly one `record` call with `easy`, the database shows `Mastered`, the counter counts it, and the word is never practised; exit with no answer leaves at once, exit after an answer opens the confirm modal, "Ở lại" closes it and returns focus to the exit button.
- **V7 — step 2 "Luyện tập", grading and recording**
  - G10 (21 scripted checks, all pass): correct, wrong, close and "Không nhớ" answers give the right feedback titles; the last button says "Xem kết quả"; a typed miss or close answer shows the letter diff; the hint button reveals one more letter ("Gợi ý thêm"); keys 1-4 and A-D answer; a wrong word returns later in the same pass; exactly one `record` call per word, with `good` for 0 or 1 miss (close counts as correct) and `hard` for 2 misses, matching the database (`Reviewing`, `Learning` with `WrongCount` 1); the course page then shows 15/20 learned and "Học tiếp bài 3"; a relearn session of 10 words runs two rounds with the interstitial "Tiếp theo: 5 từ nữa" and sends 10 `easy` calls; a failed call is retried at once and again before the result (3 calls) while healthy words send one; only the word that never saved shows "Chưa lưu", one toast explains it, and the database holds the retried word and not the failed one.
  - Interpretation: accuracy counts first-try answers over distinct (word, pass) questions (V2 note).
- **V8 — step 3 "Kết quả", relearn, review, empty states**
  - G10 (21 scripted checks, all pass): title "Xong bài 2 rồi!", line with the number of words to review, focus on the title; ring 80% (8 of 10 first-try); KPIs 5 từ mới, 8/10 câu đúng, m:ss; the XP tile equals the sum of the server `xpEarned` (+50 XP here); word list with 4 "Đã nhớ" and 1 "Cần ôn lại"; "Học bài 3" primary, "Về khóa học", "Học lại bài này" opens a relearn; the Goals daily row grew by 5 reviewed and 5 new words; the last lesson shows only "Về khóa học"; course review (2 due words) and `/Vocabulary/Review` end with "Về trang Từ vựng" (course review also "Về khóa học"); the three empty states render with their buttons and the antiforgery token.
  - G8: result, question (choice and typing), feedback (correct and wrong) and the complete-course empty state at the six sizes, `.tmp/vocabulary/V8/`; no horizontal overflow in any state.
- **G11 a11y (all lesson states):** one `<h1>`; every control named (the only unnamed input is the hidden antiforgery token); a visible outline on every tab stop; `aria-live` regions for feedback; progress bar is a `progressbar`; step pills use `aria-current="step"`; forced colours: KPI tiles, list, pills get a 1px border and no shadow, the current step a 3px border; reduced motion: animation names are `none` and transitions are about 0 s; no keyboard trap (the global handlers ignore keys while typing and while a modal is open).
- **G12 contrast:** about 63 element/background pairs over intro, choice question, feedback after a wrong answer, typing question, diff and result; zero failures.
- Not verified: Safari/Firefox rendering; a real speech voice (the browser call is observed, not heard); an empty `no-cards` course in the browser (covered by an integration test only, the dev database has none).
