# Plan: System Vocabulary — visual polish and spaced repetition (SRS)

Save as `docs/superpowers/plans/2026-11-14-vocabulary-srs-polish.md`.
Execution prompt: `.ai/prompts/claude-code-vocabulary-srs-polish.md`.

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Mục tiêu:** trang Từ vựng hệ thống đẹp hơn hẳn (hero xanh, vòng mục tiêu ngày, bìa khóa học có minh họa, thẻ từ có hình emoji, bố cục 2 cột) và có **học lặp lại ngắt quãng thật sự**: mỗi từ có 7 bậc nhớ, lịch ôn 10 phút → 1 → 3 → 7 → 14 → 30 → 60 ngày, người học thấy rõ "Ôn lại sau 7 ngày", "Lịch ôn 7 ngày tới", "Trí nhớ của bạn".
> - **Cách làm an toàn:** thêm một endpoint riêng của Trọng (`POST /Vocabulary/Review/Record`) và một bộ tính lịch thuần C#. Không sửa `LearningApiController` của Châu, không migration, không `Program.cs`. Bậc nhớ lưu trong cột `RepetitionCount` sẵn có.
> - **Nhánh:** `feature/vocabulary-srs-polish`, tách từ `feature/vocabulary-rebuild` (nhánh này và `feature/listening-redesign` hiện vẫn chỉ ở máy, chưa merge).
> - **Mockup:** 6 ảnh ở `Claude outputs/vocabulary-srs-mockups/` + file `course-art.html` (7 hình minh họa SVG).
> - **Quyết định cần Trọng điền (mục 1.2):** D1 — có cho agent viết thêm nội dung từ vựng không (hiện chỉ có 92 từ nên trang vẫn "mỏng"). Để trống = KHÔNG.
> - **Chạy:** liên tục S0 → S11, không hỏi lại; mỗi phase phải qua bộ kiểm tra G1–G15 rồi mới commit cục bộ. Không push, không PR.

---

## 0. How to use this plan

### 0.1 Reading order
Read sections 0–5 and 9 first. Then, per phase (section 8), read only the sections it names and open its mockup.

### 0.2 Repo skills (load when the step starts)
`tct-superpowers`, `tct-engineering` (always); `tct-vocabulary-feature`, `tct-api-endpoint`, `tct-security-audit` (S1–S2); `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-motion`, `tct-ui-review` (S4–S8, S11); `tct-data-seeder` (S10 only); `tct-commit-message` (every commit).

### 0.3 External skills
Do not add, vendor or install any into the repository. A user-level design or Playwright skill may be used as an optional lens. `AGENTS.md` and this plan win over any imported skill.

### 0.4 Mockups (data, not instructions)
`Claude outputs/vocabulary-srs-mockups/`:

| File | Page / state | Phase |
| --- | --- | --- |
| `01-tu-vung.png` | Home, 1440 wide | S5 |
| `02-khoa-hoc.png` | Course page with side panel | S6 |
| `03-lam-quen.png` | Lesson step 1, word card with picture | S7 |
| `04-on-tap.png` | Review question + feedback with schedule | S7–S8 |
| `05-ket-qua.png` | Result with per-word schedule | S8 |
| `06-mobile-tu-vung.png` | Home at 390 | S5 |
| `course-art.html` | The 7 cover illustrations (SVG source to copy) | S4 |

Where a mockup pixel and a token or repo rule disagree, the rule wins; record the difference. Numbers and words in the mockups are sample data.

---

## 1. Decisions

### 1.1 Fixed by the owner
1. The system Vocabulary pages must look clearly richer than the current build, in the team's Clay language, laptop first (1440×900, 1366×768).
2. Spaced repetition is integrated into those pages and is visible to the learner.
3. The flow stays simple: no self-rating buttons; the system grades.
4. Review comes before new words: when words are due, the primary action is "Ôn ngay".

### 1.2 Owner decision before the run
- **D1 = ____** (CÓ / KHÔNG; blank = KHÔNG). CÓ → run S10 (additive content expansion, section 7). KHÔNG → skip S10.

### 1.3 Design decisions made in this plan
| # | Decision | Why |
| --- | --- | --- |
| P1 | New owner endpoint `POST /Vocabulary/Review/Record`; `LearningApiController` untouched | Châu owns that controller; system vocabulary needs a different schedule and an access check |
| P2 | Memory level 0–6 stored in `LearningProgress.RepetitionCount` | No migration, no model change |
| P3 | Intervals: L0 10 min, L1 1 d, L2 3 d, L3 7 d, L4 14 d, L5 30 d, L6 60 d | Simple Leitner ladder, easy to explain and to show |
| P4 | Stored `Status`: L0 Learning, L1–L3 Reviewing, L4–L6 Mastered | Keeps Goals, dashboard and existing counters meaningful |
| P5 | Four learner-facing stages: "Mới học" (L0–1), "Đang nhớ" (L2–3), "Nhớ tốt" (L4–5), "Thuộc lâu" (L6) | Seven levels are too many to read |
| P6 | Client sends an outcome (`again`, `hard`, `good`, `known`); the server computes level and date | The schedule cannot be forged beyond the four outcomes; one source of truth |
| P7 | Early review (card not yet due) never promotes and never moves the due date | Replaying a lesson must not inflate memory |
| P8 | Review session = due cards, oldest first, at most 20 | Short sessions; the result page offers the next batch |
| P9 | Daily target is the constant 10 words | No settings page, no schema |
| P10 | Word pictures are emoji from a static map; no hotlinked images | Finding F2 of the previous plan (62 hotlinked images) |
| P11 | Course covers are 7 inline SVG illustrations coloured through token-mapped classes | No new image files, tokens-only CSS |

---

## 2. Scope

### 2.1 Files that may be added
- `Services/Vocabulary/SpacedRepetitionScheduler.cs`
- `Services/Vocabulary/VocabularyEmojiMap.cs`
- `Views/Vocabulary/_CourseArt.cshtml`, `_MemoryPanel.cshtml`, `_ReviewForecast.cshtml`, `_LevelDots.cshtml`
- `TCTEnglish.Tests/SpacedRepetitionSchedulerTests.cs`, `TCTEnglish.Tests/VocabularyReviewRecordTests.cs`
- `scripts/tests/test_vocabulary_srs.cjs` (or new cases inside `test_vocabulary_core.cjs`)
- S10 only: a content validator test; additions to `wwwroot/data/system-vocabulary.json`

### 2.2 Files that may be edited
- `Controllers/VocabularyController.cs`
- `Services/StudyService.Vocabulary.cs`, `Services/IStudyService.cs` (vocabulary members only, additive)
- `Services/Vocabulary/VocabularyLessonBuilder.cs`
- `ViewModels/VocabularyPageViewModels.cs`
- `Views/Vocabulary/Index.cshtml`, `Detail.cshtml`, `Study.cshtml`, `_CourseCard.cshtml`, `_LessonRow.cshtml`, `_WordRow.cshtml`
- `wwwroot/css/vocabulary.css`
- `wwwroot/js/vocabulary-core.js`, `vocabulary-home.js`, `vocabulary-course.js`, `vocabulary-lesson.js`
- Trọng's own tests from the previous phase — `VocabularyIntegrationTests.cs`, `VocabularyLessonBuilderTests.cs`, `scripts/tests/test_vocabulary_core.cjs` — only where behaviour changes on purpose; record each change and why in section 12
- Docs: the tracker row "Từ vựng", `bug-fix-log.md`, `known-issues.md`, `project-structure.md` when applicable; this plan's section 12

### 2.3 Forbidden
`Program.cs`, `appsettings*.json`, `.csproj`, migrations, `Models/**`; `LearningApiController.cs`, `GoalsService*`, `IGoalsService`, `Services/AI/**`; `time-tracker.js`, `personal-sets-common.js`, `practice-core.js`; `practice-ui.js` and `study-kit.css` except additive, backward-compatible changes; `_Layout.cshtml`, `layout.css`, `site.css`, `tct-learning-ui.css`, shared partials; `Areas/Admin/**`; personal-sets, practice and listening views; any other existing test; new packages, fonts, CDNs, image files.

### 2.4 Must stay unchanged
- All `/Vocabulary/*` URLs (200) and the 404 rule for user-owned sets and folders.
- `POST /api/LearningApi/record` request, response and behaviour; its tests stay green.
- Antiforgery on every POST; existing data and progress rows; strings asserted by tests that are not being changed on purpose.
- `/Sets*`, `/Practice*`, Goals, dashboard.

---

## 3. Findings (verified 2026-11-14 from source; re-verify at S0)

| # | Finding |
| --- | --- |
| F1 | Branch `feature/vocabulary-rebuild` at `e1da1d1` holds the rebuilt pages (V0–V11). It is local and unmerged; so is `feature/listening-redesign`. Both extend the `:is(...)` scope in `study-kit.css`; when they meet, keep both classes. |
| F2 | The lesson records through `POST /api/LearningApi/record`: hard → Learning +1 min; good → Reviewing 1 h/12 h/24 h; easy/perfect → Mastered 1/3/7 d. A word answered perfectly is "Mastered" after one lesson and returns after one day; there is no long ladder and no access check on `cardId`. |
| F3 | `LearningProgress` has `Status`, `RepetitionCount`, `WrongCount`, `NextReviewDate`, `LastReviewedDate`, `IsSuspended`. Nothing else is needed. |
| F4 | `StudyService` already receives `IGoalsService`; `BuildVocabularyActivityUpdate(isNewProgress, previousStatus, newStatus)` and `RecordLearningActivityAsync(userId, update)` are public. |
| F5 | Current pages: one flat icon per course cover, a letter tile per word, a single column with empty space on laptop, review schedule not shown anywhere. |
| F6 | Content: 3 folders, 8 sets, 92 cards. D1 was left blank last time, so the catalogue is still thin. |
| F7 | Existing rows written by the legacy endpoint carry `RepetitionCount` as a repetition counter. Section 4.2 defines how they are read as levels. |

---

## 4. Spaced repetition specification

### 4.1 Levels
| Level | Interval after reaching it | Stored `Status` | Stage key | Stage label | Hue |
| --- | --- | --- | --- | --- | --- |
| 0 | 10 minutes | Learning | `new` | Mới học | warning |
| 1 | 1 day | Reviewing | `new` | Mới học | warning |
| 2 | 3 days | Reviewing | `growing` | Đang nhớ | info |
| 3 | 7 days | Reviewing | `growing` | Đang nhớ | info |
| 4 | 14 days | Mastered | `strong` | Nhớ tốt | primary |
| 5 | 30 days | Mastered | `strong` | Nhớ tốt | primary |
| 6 | 60 days | Mastered | `long` | Thuộc lâu | success |

Level meter: 5 dots; filled dots = `min(level, 5)`; level 6 shows 5 filled dots in the success hue. Dots are decorative (`aria-hidden`); the text label carries the meaning.

### 4.2 Reading an existing row as a level
`level = clamp(RepetitionCount, 0, 6)`; if `Status == Mastered` and that value is below 4, use 4; if `Status == Learning`, use 0. No row → the word is "Chưa học" (not a level). Do not rewrite legacy rows in bulk; a row is normalised the next time it is recorded.

### 4.3 Outcomes
| Outcome | Sent when | New level | Due date | Other |
| --- | --- | --- | --- | --- |
| `again` | 2 or more wrong answers for the word in the session | 0 | now + 10 min | `WrongCount + 1` |
| `hard` | exactly 1 wrong answer | `max(1, level)` | now + interval(new level) | — |
| `good` | no wrong answer | `min(6, level + 1)` | now + interval(new level) | — |
| `known` | "Tôi đã biết từ này" in step 1 | `max(4, level)` | now + interval(new level) | — |

For a word with no row, `level` starts at 0 (so `good` and `hard` → 1, `again` → 0, `known` → 4).

**Early review (P7).** If a row exists, `NextReviewDate` is in the future and the outcome is `hard`, `good` or `known`: keep level, status and `NextReviewDate`; update `LastReviewedDate` only; respond with `promoted = false`. `again` always applies.

All times are UTC in storage. "Today", "Hôm nay" and the 7-day forecast use the business date helper the Goals/dashboard code already uses (find it at S0; do not invent a second time zone rule).

### 4.4 Scheduler (pure, unit-tested)
`Services/Vocabulary/SpacedRepetitionScheduler.cs`, static, no EF, no `DateTime.UtcNow` inside:

```csharp
public enum ReviewOutcome { Again, Hard, Good, Known }
public sealed record SrsState(int Level, DateTime? NextReviewUtc, bool Exists);
public sealed record SrsResult(int Level, string Status, DateTime NextReviewUtc,
                               bool Promoted, bool Demoted, bool EarlyReview, bool CountsWrong);

public static class SpacedRepetitionScheduler
{
    public const int MaxLevel = 6;
    public static TimeSpan IntervalFor(int level);
    public static int LevelFrom(int repetitionCount, string? status);   // 4.2
    public static string StatusFor(int level);                           // 4.1
    public static string StageFor(int level);                            // new|growing|strong|long
    public static SrsResult Apply(SrsState current, ReviewOutcome outcome, DateTime nowUtc);
    public static bool TryParseOutcome(string? raw, out ReviewOutcome outcome); // case-insensitive, exact names only
}
```

Use the repo's existing status constants or enum for `Status` (discover; do not introduce new strings).

Unit tests (`SpacedRepetitionSchedulerTests`), at least: every row of 4.3 from every level 0–6; caps at 0 and 6; all seven intervals; new word for each outcome; early review for each outcome; due exactly now counts as due; legacy mapping (Mastered with count 1 → 4; Learning with count 3 → 0; count 99 → 6; negative → 0); `TryParseOutcome` rejects unknown, empty, numeric and padded values.

### 4.5 Service
Add to `IStudyService` / `StudyService.Vocabulary.cs`:

```csharp
Task<VocabularyReviewRecordResult?> RecordVocabularyReviewAsync(int userId, int cardId, ReviewOutcome outcome);
```

Steps: (1) load the card with its set and owner; return `null` unless the set belongs to the system user and is visible under the same rule the pages use; (2) load or create the user's `LearningProgress`; skip scheduling when `IsSuspended` (return current state, `promoted = false`); (3) `Apply`; (4) write `RepetitionCount = level`, `Status`, `NextReviewDate`, `LastReviewedDate = now`, `WrongCount` when `CountsWrong`; (5) call `BuildVocabularyActivityUpdate(isNewProgress, previousStatus, newStatus)` and `RecordLearningActivityAsync` exactly as the legacy endpoint does, but not for an early review that changed nothing; (6) one `SaveChanges`; (7) return level, stage, label strings, `nextReviewDate`, `promoted`, `demoted`, `xpEarned`, `streak` (same sources as the legacy response).

Inject the clock the way the service already gets time (discover: `TimeProvider`, helper or `DateTime.UtcNow`); tests must be able to control it or assert with tolerance.

### 4.6 Endpoint
`VocabularyController`: `[HttpPost("/Vocabulary/Review/Record")]`, `[ValidateAntiForgeryToken]`, authenticated like the other actions, JSON body `{ "cardId": 123, "outcome": "good" }`.

| Case | Response |
| --- | --- |
| OK | 200 `{ success, cardId, level, stage, stageLabel, nextReviewDate, nextReviewLabel, promoted, demoted, xpEarned, streak }` |
| Unknown outcome, missing body, `cardId <= 0` | 400 |
| Card missing, not a system card, not visible | 404 (same body as a missing card) |
| No or bad antiforgery token | 400 as the framework does |
| Anonymous | the existing auth behaviour for this controller |

`nextReviewLabel` values (server-built, Vietnamese): "Ôn lại sau 10 phút", "Ôn lại sau 1 ngày", "Ôn lại sau 3 ngày", "Ôn lại sau 7 ngày", "Ôn lại sau 14 ngày", "Ôn lại sau 30 ngày", "Ôn lại sau 60 ngày"; for an early review: "Lịch ôn giữ nguyên".

Integration tests (`VocabularyReviewRecordTests`): each outcome on a new word; `good` twice in a row (second is an early review, level unchanged); demotion with `again`; a due word promoted; user-owned card → 404; another user's private set card → 404; missing token → 400; bad outcome → 400; Goals activity recorded once for a new word; `LearningApi/record` tests still green and untouched.

### 4.7 Read models (additive, in `VocabularyPageViewModels.cs`)
- **Home:** `DueCount`, `DueMinutes` (`ceil(due × 15 s / 60)`, min 1), `TodayCount` (distinct system cards with `LastReviewedDate` on today's business date), `DailyTarget = 10`, `Streak`, `LearnedCount`, `Memory` (four stage counts + total), `Forecast` (7 items: label, count, isToday; day 0 includes everything overdue), `NextLesson` (existing recommendation), and per course `DueCount`, `ArtKey`.
- **Course:** `Memory` for this set, `Upcoming` (up to 6 words with the nearest `NextReviewDate`: term, level, label "Hôm nay" / "Ngày mai" / "Sau n ngày"), per lesson `DueCount`, per word `Level?`, `Stage`, `NextReviewLabel`, `IsDue`, `Emoji`.
- **Lesson / review payload:** per word `level`, `stage`, `reviewCount` (level, for "lần ôn thứ n"), `emoji`.
- **Review:** due cards ordered by `NextReviewDate`, take 20; `RemainingDue` = total due − taken.

Keep the query count flat: one grouped query per panel, no per-card queries (assert with the existing query-count pattern if the test project has one; otherwise review the SQL log once and record it).

---

## 5. UI specification

Common rules: everything lives under `.tv-page`; classes are `tv-*` plus the owner kit `k-*`; tokens only (no hex, `rgb()`, `:root`, `!important`, weight above 700); depth only through `--tct-clay-*` shadows and token rings; `color-mix` between tokens with a plain-token fallback; one `forced-colors` block and one `prefers-reduced-motion` block per new component; touch targets ≥ 44 px; focus visible; Vietnamese strings below are literal.

### 5.1 Home `/Vocabulary` (mockups 01, 06)
Order top to bottom:

1. **Header.** `h1` "Từ vựng"; sub "Học từ có sẵn theo bài ngắn, rồi ôn lại đúng lúc sắp quên."
2. **Hero panel** `.tv-hero` (primary surface, text on primary, radius xl, `--tct-clay-lift`). Three columns on laptop (ring | review card | next-lesson card); stacked below 992 px.
   - Ring: SVG progress ring, centre "x/10" and "từ hôm nay". `role="img"`, `aria-label="Hôm nay đã học x trên 10 từ"`.
   - Headline beside the ring: x < 10 → "Còn n từ nữa là đạt mục tiêu hôm nay"; x ≥ 10 → "Đã đạt mục tiêu hôm nay!"; no progress at all → "Bắt đầu với 5 từ đầu tiên nhé".
   - Chips: "Chuỗi n ngày" (fire icon), "n từ đã học".
   - Card **ÔN TẬP HÔM NAY** (surface card inside the hero): due > 0 → big number + "từ đến hạn", note "Ôn trước khi học bài mới để không quên. Khoảng n phút.", primary button "Ôn ngay" → `/Vocabulary/Review`. due = 0 → check icon, "Hôm nay không có từ nào cần ôn", note "Từ tiếp theo đến hạn: {label}" when one exists, no button.
   - Card **BÀI TIẾP THEO**: lesson title "Bài n · {tên}", course name and "n từ mới"; button "Học bài mới" — soft style when due > 0, primary when due = 0. All lessons done → "Bạn đã học hết các bài hiện có" and no button.
3. **Two panels in a row** (stack below 992 px):
   - `_MemoryPanel` **Trí nhớ của bạn**: right-aligned "n từ đã học"; a 4-segment distribution bar (`role="img"` with an `aria-label` that reads the four numbers); four tiles — "Mới học · ôn sau 10 phút – 1 ngày", "Đang nhớ · ôn sau 3 – 7 ngày", "Nhớ tốt · ôn sau 14 – 30 ngày", "Thuộc lâu · ôn sau 60 ngày"; explainer line "Trả lời đúng, từ lên một bậc và lâu hơn mới phải ôn: 1 → 3 → 7 → 14 → 30 → 60 ngày. Quên thì quay về đầu." Empty (0 learned): bar hidden, text "Học bài đầu tiên để bắt đầu theo dõi trí nhớ."
   - `_ReviewForecast` **Lịch ôn 7 ngày tới**: right-aligned "n lượt ôn"; 7 bars, labels "Hôm nay", then weekday short names "T2…T7, CN"; today's bar in the warning hue, others soft primary; number above each bar; a visually hidden `<table>` (or `<ul>`) carries the same data for screen readers; bars are CSS heights from a `--tv-h` custom property set inline as a percentage (inline custom properties are allowed; no inline colours). All zero → "Chưa có lịch ôn. Học một bài để hệ thống xếp lịch cho bạn."
4. **Search** (existing behaviour) and the **course groups** (existing grouping).
   - `_CourseCard`: cover area 16:9.6 with a soft tinted background per art key and the `_CourseArt` SVG; level badge top-left; status chip top-right ("Chưa học", "Đang học", "Đã xong"); title; meta "n từ", "n bài" and, when due > 0, a danger-tinted chip "n từ cần ôn"; progress bar with "x/n từ". The whole card is one link; hover lifts (`--tct-clay-lift`), pressed uses `--tct-clay-pressed`.

### 5.2 Course art (`_CourseArt.cshtml`)
- Model: an art key string. Copy the seven SVGs from `course-art.html` verbatim (viewBox `0 0 200 120`, `aria-hidden="true"`, `focusable="false"`), one `case` per key: `star`, `talk`, `book`, `food`, `travel`, `jobs`, `colors`; unknown → `star`.
- The SVGs carry classes only (`fP`, `fPk`, `fS`, `fSk`, `fW`, `fWk`, `fD`, `fI`, `fIk`, `fA`, `fSf`, `fT`, `fM`, `sSf`, `sP`, `sM`, `sT`, `sWk`, `sIk`, `tx`, `o5`, `o3`). In `vocabulary.css` map each to a token under `.tv-art`: `f*` → `fill`, `s*` → `stroke` with `fill: none`; P primary, Pk primary-strong, S success, Sk success-strong, W warning, Wk warning-strong, D danger, I info, Ik info-strong, A accent (the yellow the Premium button uses), Sf surface, T text, M border/muted. Use the token names that exist in `tct-learning-ui.css`; if a hue has no "strong" token, use `color-mix` of the hue with the text token and a plain fallback. `o5` / `o3` are opacity .5 / .3. `tx` is 700 26px in the inherited family.
- Art key per course, decided in `VocabularyLessonBuilder` (or where the current icon is chosen) by case-insensitive name keywords, first match wins: "giao tiếp" → `talk`; "oxford" → `book`; "ăn", "uống", "thực phẩm" → `food`; "du lịch", "địa điểm" → `travel`; "nghề", "công việc" → `jobs`; "màu" → `colors`; "thông dụng", "1000" → `star`; otherwise by `setId % 7` over the list above so covers stay stable. Unit-test the mapping.
- Forced colours: fills fall back to `CanvasText` / `Canvas` so the drawing stays visible.

### 5.3 Word pictures (`VocabularyEmojiMap`)
- Order: `Card.Emoji` when the model has such a field and it is set; else a static, case-insensitive term → emoji dictionary; else a monogram tile (first letter, existing style).
- The dictionary covers every term in `system-vocabulary.json` that has an obvious, neutral emoji (for example "Please" 🙏, "Thank you" 😊, "Sorry" 🙇, "horse" 🐴, "rice" 🍚, "airport" ✈️, "doctor" 🧑‍⚕️, "red" 🔴). Skip abstract words rather than force a picture. No flags for languages or nationalities, no skin-tone variants.
- Emoji are decorative: `aria-hidden="true"`; the term is always present as text.
- A unit test asserts the map has no empty values and that lookups ignore case and surrounding spaces.

### 5.4 Course page `/Vocabulary/Detail` (mockup 02)
- Header: back link "Từ vựng"; cover tile with the art (small); title; level and counts; primary CTA — due in this course > 0 → "Ôn n từ đến hạn" (→ `/Vocabulary/Review?setId=`) plus secondary "Học bài tiếp theo"; else primary "Học bài tiếp theo"; everything learned and nothing due → "Học lại từ đầu" (soft).
- Two columns from 1200 px (main 1fr, aside 340 px, sticky under the top bar); one column below, with the aside placed above the tabs.
- Main: tabs "Bài học" / "Danh sách từ" (existing `k-seg`).
  - `_LessonRow`: number tile, title, "n từ", state chip ("Chưa học", "Đang học x/n", "Đã xong"), and "n từ cần ôn" chip when due > 0; a mini row of dots, one per word, coloured by stage (decorative).
  - `_WordRow`: emoji or monogram tile, term, IPA, meaning, `_LevelDots`, stage label, next review label ("Hôm nay", "Ngày mai", "Sau n ngày", or "Chưa học"); speak button. Filters (`k-seg`): "Tất cả", "Cần ôn", "Chưa học", "Đã học"; empty filter result → "Không có từ nào ở mục này."
- Aside: panel **Trí nhớ khóa này** (`_MemoryPanel`, compact variant) and panel **Sắp ôn lại** (up to 6 rows: term, dots, label). Empty → "Chưa có từ nào trong lịch ôn."

### 5.5 Lesson `/Vocabulary/Study` (mockups 03, 04)
Keeps the existing shell ids (`#tpStage`, `#tpFeedback`, `#tpProgress`), `time-tracker.js` and the three steps "1 · Làm quen", "2 · Luyện tập", "3 · Kết quả".

- **Step 1 card:** chip "Từ mới" (or the stage label for a word being relearned); round dots top-right (one per word, current filled); picture tile 120 px with the emoji or monogram; term (display size); IPA + speak; meaning; example box with the term highlighted and its translation; buttons "Tôi đã biết từ này" (soft) and "Tiếp tục" (primary, hint `Enter`); key hints "R nghe lại", "Enter từ tiếp theo". "Tôi đã biết từ này" records `known` once and removes the word from step 2.
- **Step 2 question:** under the prompt, for words that already have a level, a memory chip: dots + "{stage} · lần ôn thứ n". New words show "Từ mới".
- **Feedback panel** (correct and wrong variants keep their current structure) gains one schedule row once the word's outcome is final (its last question in the session):
  - promoted: calendar icon, "Nhớ tốt lên một bậc", dots, right side "Ôn lại sau 7 ngày" (label from the server);
  - `hard`: "Giữ nguyên bậc nhớ" + label;
  - `again`: "Từ này sẽ quay lại sớm" + "Ôn lại sau 10 phút";
  - early review: "Lịch ôn giữ nguyên";
  - request failed: "Chưa lưu được tiến độ" with a "Thử lại" button; the session continues and the result page retries unsaved words once.
  The row is announced through the existing live region; no extra focus move.
- Recording: `vocabulary-lesson.js` posts to `/Vocabulary/Review/Record` (through `TctSets.fetchJson`, antiforgery header as the other owner pages do) once per word, when the word leaves the queue. It no longer calls `/api/LearningApi/record`.

### 5.6 Review `/Vocabulary/Review` (mockup 04)
- Same shell; step pills "Ôn tập hôm nay" / "Kết quả"; no step 1.
- Queue: the server's due list (max 20). Each word gets one question; a wrong answer re-queues it at the end (max 2 extra tries), which makes the outcome `hard` (1 miss) or `again` (2+).
- Question type by level: L0–1 choose (term ↔ meaning), L2–3 alternate choose and type, L4+ type. Words whose term contains spaces or more than 24 characters stay on choose.
- Nothing due: state card "Hôm nay không có từ nào cần ôn", note "Từ tiếp theo đến hạn: {label}" when available, buttons "Học bài mới" and "Về trang Từ vựng".

### 5.7 Result (mockup 05)
- Ring with the percentage of correct answers and "trả lời đúng".
- Title: lesson → "Xong bài n rồi!"; review → "Ôn xong n từ!". Sub: lesson → "Bạn vừa học n từ mới. Hệ thống đã xếp lịch ôn cho từng từ."; review → "Lịch ôn mới đã được xếp cho từng từ."
- Four tiles: "từ mới" (lesson) or "từ đã ôn" (review); "câu đúng" x/y; "thời gian" m:ss; "XP" +n (hide the tile when the server returned no XP).
- Word list: tile, term, meaning, `_LevelDots` (client-rendered equivalent), and the server's `nextReviewLabel`; "Ôn lại sau 10 phút" rows use the danger text token. The count in the sub-title must equal the number of rows.
- Buttons: remaining due > 0 → primary "Ôn n từ đến hạn", secondary "Về khóa học" (lesson) / "Về trang Từ vựng" (review); else primary "Học bài tiếp theo" when one exists, secondary as above.

### 5.8 `vocabulary-core.js` (pure, node-tested)
Add, without breaking existing exports:
- `outcomeFor(misses, known)` → `known` | `good` | `hard` | `again`;
- `stageFor(level)`, `stageLabel(level)`, `dotsFor(level)` → `{ filled, hue }`;
- `reviewQuestionType(level, term, index)`;
- `planReview(words, rng)` → queue with the re-queue rule of 5.6;
- `markRecorded(state, cardId, serverResult)`, `unsaved(state)`;
- `summary(state)` extended with per-word `level`, `label`, `promoted` and `xp` total.
Node tests cover every branch, the 20-word cap being respected when given more, deterministic order with `mulberry32`, and that a word is never reported for recording twice.

---

## 6. Accessibility, motion, performance
- Keyboard: every action reachable; `1–4` choose, `Enter` continue, `R` replay, `Esc` asks before leaving a session (existing behaviour kept). No keyboard trap in the aside or the tabs.
- Ring, bar chart and distribution bar have text alternatives; colour is never the only signal (labels and numbers always shown).
- Contrast: text ≥ 4.5:1, large text and UI parts ≥ 3:1, checked by script on the hero (text on primary), chips, tiles and the chart labels.
- Motion: ring and bars may animate once on load (≤ 600 ms, transform/opacity/stroke only); none under `prefers-reduced-motion`.
- Forced colours: borders on every card, chip and bar; SVG art visible.
- No layout shift from the hero or charts: fixed heights; server renders final numbers (no client fetch for the home panels).
- Page weight: no new requests besides the existing CSS/JS files; SVG art is inline.

---

## 7. Content expansion (S10, only with D1 = CÓ)
Follow section 7 of `docs/superpowers/plans/2026-11-13-vocabulary-rebuild.md` unchanged: additive only; never rename or remove folders, sets or cards; nothing added to "Oxford" sets; each new card has term, IPA, Vietnamese meaning, one example with translation; no hotlinked images; a validator test for the JSON (unique terms per set, required fields, no empty strings); never reset or reseed the dev database — add local data with an idempotent fixture as before. Extend `VocabularyEmojiMap` for new terms. Mark in the handoff that the content is AI-written and needs the owner's review.

---

## 8. Phases

Run mode: continuous, autonomous, one agent. After each green gate: write the gate record (8.3) into section 12, commit locally, start the next phase.

| Phase | Work | Read | Mockup |
| --- | --- | --- | --- |
| **S0 Baseline** | `git status` clean check; `git switch feature/vocabulary-rebuild`; verify HEAD `e1da1d1` or later; create `feature/vocabulary-srs-polish`; build; full tests and node tests (record counts, warnings, the known pre-existing node failure); save this plan; read D1; confirm F1–F7; find the business-date helper, status constants, clock source, antiforgery header convention, token names; start the app with `scripts/start-local.ps1`; before-screenshots of home, course, lesson, review, result at 1440×900 and 390 into `.tmp/vocabulary-srs/before/` | 0–4 | all |
| **S1 Scheduler** | Tests first, then `SpacedRepetitionScheduler` | 4.1–4.4 | — |
| **S2 Service, endpoint, read models** | Tests first; `RecordVocabularyReviewAsync`; endpoint; view-model additions; review query (20, ordered); art key and emoji map with tests | 4.5–4.7, 5.2, 5.3 | — |
| **S3 Core JS** | `vocabulary-core.js` additions and node tests | 5.8 | — |
| **S4 CSS foundation and partials** | `vocabulary.css`: hero, ring, memory panel, forecast, art classes, level dots, chips, two-column layout, forced-colours and reduced-motion blocks; `_CourseArt`, `_LevelDots`, `_MemoryPanel`, `_ReviewForecast` | 5 intro, 5.2 | `course-art.html` |
| **S5 Home** | `Index.cshtml`, `_CourseCard`, `vocabulary-home.js`; all states (new learner, due > 0, due = 0, target reached, all lessons done) | 5.1 | 01, 06 |
| **S6 Course page** | `Detail.cshtml`, `_LessonRow`, `_WordRow`, `vocabulary-course.js`; filters; aside | 5.4 | 02 |
| **S7 Lesson** | Step 1 card; memory chip; feedback schedule row; switch recording to the new endpoint; failure and retry path | 5.5 | 03, 04 |
| **S8 Review and result** | Review queue rules; empty state; result list, tiles, CTAs | 5.6, 5.7 | 04, 05 |
| **S9 Cleanup and docs** | Remove dead CSS/JS left by the change; update owned tests that changed on purpose (list them); tracker row, `bug-fix-log.md`, `known-issues.md`, `project-structure.md` | 2 | — |
| **S10 Content** | Only with D1 = CÓ | 7 | — |
| **S11 Full gate** | Whole gate on every page and state; end-to-end learner run (8.5); self-review against `AGENTS.md`, `DESIGN.md`, section 10; after-screenshots into `.tmp/vocabulary-srs/after/` | all | all |

### 8.1 Phase gate (run every applicable item, in order)
| Gate | Check |
| --- | --- |
| G1 | `dotnet build` succeeds; warnings ≤ S0 |
| G2 | Area tests: `Vocabulary*`, `SpacedRepetition*`, `LearningApi*`, `Goals*` |
| G3 | Full test suite; counts not below S0 (plus the new tests) |
| G4 | Node tests; only the failure already known at S0 may remain |
| G5 | Contracts: legacy URLs 200; 404 rules; `LearningApi/record` untouched (`git diff` empty for that file and its tests); asserted strings |
| G6 | Tokens-only grep on changed CSS (no hex, `rgb(`, `:root`, `!important`, weight > 700); no inline colours in views; `git diff --check` |
| G7 | File scope: every changed path is in section 2.1 or 2.2 |
| G8 | Render at 1440×900, 1366×768, 1024, 768, 390, 320 and 200% zoom; no horizontal scroll; console and network clean |
| G9 | Side-by-side with the phase mockup; consistent with `/Sets` and `/Practice` |
| G10 | The phase behaviour checklist (8.4) by mouse and by keyboard only |
| G11 | Accessibility: names and roles, focus order, live region, forced colours, reduced motion, no trap |
| G12 | Scripted contrast on new colour pairs |
| G13 | Regression: `/Sets`, `/Practice`, Goals, dashboard, a personal-set study session still recording through the legacy endpoint |
| G14 | Clay conformance grep: depth only via `--tct-clay-*`; no `box-shadow` literals; radius tokens |
| G15 | Security: antiforgery on the POST; 404 for non-system cards; no credentials, mockups, screenshots or `.tmp/` in the diff |

### 8.2 Failure and stop rules
- A failed item: fix it and rerun the **whole** gate. At most 3 attempts per item.
- Still failing and caused by this phase → revert that element, mark it "deferred" with evidence, continue.
- Failing because of something outside the scope → record evidence, mark "blocked", continue with what is independent.
- **Stop the run** only if green requires a forbidden file, the base state is unsafe (dirty tree with foreign changes, wrong branch history), or data would have to be reset. Then write the gate record and the report.
- Never skip, weaken or delete a test; never call a skipped check passed.

### 8.3 Gate record (append to section 12 before each commit)
Phase, date, commit subject; G1–G15 each with pass / n-a / deferred / blocked and one line of evidence (counts, file, screenshot path); decisions taken; differences from the mockup; owned tests changed and why.

### 8.4 Behaviour checklists
**S2:** new word × 4 outcomes store the expected level, status and date; second `good` in a row is an early review; due word promotes; `again` demotes and counts wrong; suspended row untouched; user-owned and foreign cards 404; bad outcome 400; Goals credited once.
**S5:** new learner sees "Bắt đầu với 5 từ đầu tiên nhé", empty memory and forecast texts, primary "Học bài mới"; with due words the primary is "Ôn ngay" and the lesson button is soft; ring number equals today's distinct words; target reached text at ≥ 10; chips show streak and learned; course cards show art, status chip and "n từ cần ôn"; search still filters; all links work; layout holds at 1366×768 without the hero overflowing.
**S6:** CTA matches due state; lesson rows show state and due chips; four word filters and their empty text; aside sticky on laptop and above the tabs on narrow screens; "Sắp ôn lại" ordered by date; speak buttons work.
**S7:** emoji or monogram shown; "Tôi đã biết từ này" records `known` once and skips practice for that word; each word is recorded exactly once (verify in the network panel and the DB); feedback row shows the server label; early-review text on a replayed lesson; simulated failure shows "Chưa lưu được tiến độ" and "Thử lại" works; `Esc` confirmation; time tracker still reports.
**S8:** review takes at most 20, oldest first; wrong answer re-queues; outcome mapping 0/1/2+ misses; question type by level; empty state; result rows equal the number of words; labels match the DB dates; "Ôn n từ đến hạn" appears only when words remain; XP tile hidden without XP.

### 8.5 End-to-end run (S11)
With a synthetic learner from `.appdata/local/access.json` (never print the password): learn one lesson → check levels and dates in the DB → home shows the ring, memory and forecast updated → make two words due by setting their `NextReviewDate` in the past with a single targeted SQL update on that learner's rows (record the statement; no reset) → home shows "Ôn ngay" with 2 → review them, one right and one wrong twice → verify L+1 and L0 → Goals shows today's activity.

---

## 9. Constraints
- One agent, local only. Granted: local commits on `feature/vocabulary-srs-polish`, one per phase, via `tct-commit-message`. Not granted: push, PR, merge, rebase, reset, deploy, remote CI.
- Never stage `appsettings*.json`, `Claude outputs/`, `.tmp/`, screenshots.
- Never reset or reseed the dev database; no paid AI calls; never print or store credentials.
- Plan and code comments in English; UI copy in Vietnamese exactly as written here.

## 10. Definition of Done
- [ ] Scheduler, service and endpoint implemented as in section 4; unit and integration tests green.
- [ ] `LearningApiController` and its tests have an empty diff; personal sets still record through it.
- [ ] The lesson and review record each word exactly once through `/Vocabulary/Review/Record`.
- [ ] Home shows hero, ring, review card, next lesson, memory panel, 7-day forecast and illustrated course cards, in every state of 5.1.
- [ ] Course page has the two-column layout, due chips, level dots, filters and the aside.
- [ ] Lesson shows pictures, memory chip and the schedule row; result lists each word with its next review.
- [ ] Review is capped at 20, ordered, with the empty state.
- [ ] Laptop sizes first, then 1024/768/390/320 and 200% zoom, verified with screenshots.
- [ ] Keyboard, forced colours, reduced motion and contrast recorded.
- [ ] Tokens-only and Clay greps clean; no forbidden file touched; no migration pending (`has-pending-model-changes` reports none).
- [ ] Section 12 has a gate record per phase; docs updated.
- [ ] With D1 = CÓ: content added per section 7 with its validator.

## 11. Risks
| Risk | Mitigation |
| --- | --- |
| Legacy rows read as odd levels | Rule 4.2 + tests; rows normalise on next record |
| Personal sets and system cards share `LearningProgress` | The new endpoint accepts system cards only; legacy endpoint unchanged |
| Double recording (retry, double click) | Client records once per word; early-review rule makes a repeat harmless |
| Goals credited twice | Credit only when state changes; test asserts one activity |
| Hero text contrast on the primary surface | G12 script; fall back to the surface card style for any failing pair |
| Emoji render differently per OS | Decorative only; monogram fallback; fixed tile size |
| `study-kit.css` scope conflict with the listening branch | Do not touch the scope list here; note for the merge: keep both classes |
| Time zone at midnight | Single business-date helper; tests around the day boundary |
| Thin catalogue still visible | D1; otherwise report it to the owner |

## 12. Execution notes
(The agent appends one gate record per phase here, plus discovery notes from S0.)

### S0 — Baseline and discovery (2026-11-14)

**Base.** Started on `feature/vocabulary-rebuild` at `e1da1d1`; created `feature/vocabulary-srs-polish` from it. The working tree carried two foreign items that stay unstaged: `TCTEnglish/appsettings.Development.json` (one added blank line) and the untracked `Claude outputs/`. D1 was blank, so **D1 = KHÔNG** and S10 is skipped.

**Baseline.** `dotnet build` of the test project (also builds the app): 0 errors, **58 warnings**. `dotnet test` (Debug): passed 1529, failed 0, skipped 34, total 1563. Node: 292 tests, 291 pass, 1 fail (`test_speaking_catalog_packages.cjs`, the known CRLF hash failure with `core.autocrlf=true`; unrelated). `test_vocabulary_core.cjs`: 21/21.

**Findings F1–F7 re-verified.**
- F1 holds (both branches local, unmerged).
- F2 holds: `LearningApiController.Record` maps hard → Learning +1 min, good → Reviewing 1 h/12 h/24 h, easy/perfect → Mastered 1/3/7 d; no check that the card exists or is visible.
- F3 holds: `LearningProgress` has `Status`, `RepetitionCount`, `WrongCount`, `NextReviewDate`, `LastReviewedDate`, `IsSuspended`.
- F4 holds: `StudyService` already has `_goalsService` (`IGoalsService`).
- F5, F6 hold; F7 holds (legacy rows keep `RepetitionCount` as a counter).

**Unknowns resolved.**
- Business date: `TCTVocabulary.Services.BusinessDateHelper` (`Today`, `ToBusinessDateFromUtcStorage`), already used by `StudyService.Vocabulary.cs` and Goals. UTC+7 ("SE Asia Standard Time").
- Status constants: none; the code uses the strings `"Learning"`, `"Reviewing"`, `"Mastered"` (nullable column). The scheduler exposes them as constants with the same values.
- Clock: `StudyService` uses `DateTime.UtcNow` directly (no `TimeProvider`). The scheduler takes `nowUtc` as a parameter; the service reads `DateTime.UtcNow` once per call; tests assert with tolerance or with a due date far in the past/future.
- Antiforgery convention: `TctSets.fetchJson` (personal-sets-common.js) sends the header `RequestVerificationToken` from the page's `@Html.AntiForgeryToken()` field; `Study.cshtml` already renders that token.
- Tokens: `--tct-primary`, `--tct-primary-strong`, `--tct-primary-soft`, `--tct-primary-tint`, `--tct-on-primary`, `--tct-accent`, `--tct-on-accent`, `--tct-surface`, `--tct-surface-muted`, `--tct-text`, `--tct-text-secondary`, `--tct-text-muted`, `--tct-border`, `--tct-border-control`; `--tct-success|warning|danger|info` with `-text` and `-soft` variants (no "strong" variants: use `-text`); `--tct-radius-md|lg|xl`; `--tct-clay-shadow`, `-quiet`, `-lift`, `-button`, `-pressed`, `-field`.
- `Card.Emoji` **exists** (`string?`), so `VocabularyEmojiMap` reads it first (plan 5.3).
- Current course icon: `StudyService.GetVocabularyIconKey(description)` (tag based) and `Hue` from `setId % 6`; the art key (plan 5.2) will replace the icon use.
- Namespaces in the vocabulary code are `TCTVocabulary.*` (not `TCTEnglish.*`); new files follow that, as the rest of the module does.

**Environment.** No `.codegraph/` index in the repo, so CodeGraph was skipped and bounded reads were used. App started with `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5100`; `/health/ready` = 200. `student@tct.local` and `premium@tct.local` have no system-vocabulary progress in this database (home shows the new-learner state).

**Before screenshots** (`.tmp/vocabulary-srs/before/`, ignored): home, course, review at 1440 and 390; lesson step 1 at 1440 and 390; lesson question at 1440. The result screen is not captured before the change (it needs a completed session; it is captured in S8/S11).

### Gate record S0

| Gate | Result |
| --- | --- |
| G1 | n-a (baseline: 58 warnings, 0 errors) |
| G2–G4 | baseline recorded above |
| G5–G15 | n-a (no code change in S0; plan and prompt copied only) |

Decisions: D1 = KHÔNG; the prompt file is also saved at `.ai/prompts/claude-code-vocabulary-srs-polish.md` as its header instructs. Mockup differences: none yet.

### Gate record S1 — Scheduler (2026-11-14)
Commit: `feat(vocabulary): add the pure spaced-repetition scheduler with its unit tests`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0) |
| G2 | pass: `SpacedRepetition*` 100 tests (every row of 4.3 from levels 0–6, caps, seven intervals, new word, early review per outcome, due exactly now, legacy mapping, `TryParseOutcome` rejects) |
| G3 | pass: 1629 passed / 0 failed / 34 skipped (S0 1529 + 100 new) |
| G4 | pass: node untouched (S0 counts hold) |
| G5 | pass: no controller, endpoint or view touched; `LearningApiController.cs` diff empty |
| G6 | n-a: no CSS or view; `git diff --check` clean |
| G7 | pass: only `Services/Vocabulary/SpacedRepetitionScheduler.cs` and `TCTEnglish.Tests/SpacedRepetitionSchedulerTests.cs` (section 2.1) |
| G8–G13 | n-a: no UI in this phase |
| G14 | n-a |
| G15 | pass: pure static code, no I/O, no secrets |

Decisions: `StageLabelFor`, `NextReviewLabelFor` and the `Status*` constants live in the scheduler (same file, additive to the 4.4 signature) so the service, endpoint and views share one source for the Vietnamese labels. `Promoted` is `newLevel > startLevel`, so a first `good` or `hard` on a new word reports `promoted = true` (0 → 1); the client picks its feedback text from the outcome first. Early review applies only when a row exists, `NextReviewUtc > now` and the outcome is not `again`; a row without a date counts as due.

### Gate record S2 — Service, endpoint, read models, art key, emoji map (2026-11-14)
Commit: `feat(vocabulary): record outcomes on the memory ladder and expose the SRS read models`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0) on a `--no-incremental` build |
| G2 | pass: `Vocabulary*`, `SpacedRepetition*`, `LearningApi*`, `Goals*` = 300 passed, 0 failed |
| G3 | pass: 1699 passed / 0 failed / 34 skipped (S1 1629 + 70 new) |
| G4 | pass: node untouched |
| G5 | pass: all legacy `/Vocabulary/*` routes still 200, user-owned ids still 404 (existing `VocabularyIntegrationTests` unchanged and green); `LearningApiController.cs` diff empty; the legacy endpoint is re-proved for a personal card (`TheLegacyEndpoint_StillWorksForAPersonalCard`) |
| G6 | n-a: no CSS or view changed; `git diff --check` clean |
| G7 | pass: `Controllers/VocabularyController.cs`, `Services/StudyService.Vocabulary.cs`, `Services/IStudyService.cs`, `Services/Vocabulary/VocabularyLessonBuilder.cs`, `Services/Vocabulary/VocabularyEmojiMap.cs`, `ViewModels/VocabularyPageViewModels.cs`, the two new test files |
| G8–G11 | n-a: no rendered change in this phase |
| G12–G14 | n-a |
| G15 | pass: `[ValidateAntiForgeryToken]` on the POST (missing token → 400 test); user-owned, another user's private and missing cards → 404 and nothing written; bad outcome (easy, perfect, "1", " good", empty, missing), `cardId <= 0`, a non-numeric id, malformed or empty JSON → 400; anonymous → denied; no secrets |

S2 behaviour checklist (8.4): new word × 4 outcomes store the expected level, status and date; a second `good` is an early review (level, status and date unchanged, label "Lịch ôn giữ nguyên"); a due word promotes (2 → 3, +7 d); `again` demotes (3 → 0, `WrongCount` 1, +10 min) even before the due date; a legacy Mastered row with count 1 reads as level 4 and becomes 5; a suspended row is untouched and credits nothing; Goals is credited once (`CardsReviewed` 1, `NewCardsLearned` 1) across good + good + hard; bad outcome 400; user-owned and foreign cards 404.

Decisions:
- Endpoint DTO `VocabularyReviewRecordRequest` lives in `VocabularyController.cs`; the response type `VocabularyReviewRecordResult` in the view-model file. The response is camelCase JSON with `success, cardId, level, stage, stageLabel, nextReviewDate, nextReviewLabel, promoted, demoted, xpEarned, streak`.
- Early review updates `LastReviewedDate` only and does **not** credit Goals (nothing changed); an `again` always applies and is credited.
- A concurrent first record for the same word (unique `UserId, CardId`) is retried once on top of the row the other request created.
- If Goals refuses the activity after the progress is saved, the response is still 200 with `xpEarned = 0` and a warning is logged; a retry would be an early review.
- Suspended rows no longer count as due on the home, course and review pages (they are "hidden from review"); previously the due check ignored `IsSuspended`.
- Word rows with a progress row but no date show "Chưa có lịch".
- `Card.Emoji` is used first, capped at 16 characters; `IconKey` is kept for now (the existing tests assert it) and is removed or left in S9.
- Query count: home = system user, folders, cards, progress, streak (5 queries, unchanged shape); the new panels are computed from the already loaded progress rows, so no per-card queries were added. The record call is user, card check, progress, save, goals.

### Gate record S3 — `vocabulary-core.js` (2026-11-14)
Commit: `feat(vocabulary): add the spaced-repetition helpers to the pure lesson core`

| Gate | Result |
| --- | --- |
| G1 | pass: no C# change; build unchanged (0 errors, 58 warnings at S2) |
| G2 | pass: `Vocabulary*`, `SpacedRepetition*`, `LearningApi*` re-run = green |
| G3 | pass: C# untouched since the S2 full run (1699 passed / 34 skipped) |
| G4 | pass: node 315 tests, 314 pass, 1 fail (the known CRLF hash failure); `test_vocabulary_core.cjs` 21/21 untouched and green; `test_vocabulary_srs.cjs` 23 new tests green |
| G5 | pass: no URL, endpoint or contract touched |
| G6 | n-a: no CSS or view; `git diff --check` clean |
| G7 | pass: `wwwroot/js/vocabulary-core.js` and `scripts/tests/test_vocabulary_srs.cjs` (both in section 2) |
| G8–G14 | n-a: not wired into a page yet (S7/S8) |
| G15 | pass: pure module, the purity test (no DOM, network or window access) still passes |

Decisions:
- The new review flow is opt-in: `initSession({ kind: 'review', srs: true, ... })`. Without `srs`, `review` keeps the two-pass behaviour so the 21 existing tests did not change. S8 passes `srs: true` for review sessions; S9 decides whether the old review path stays (it is still used by tests only).
- Every finished word now carries `outcome` (`again | hard | good | known`) beside the old `grade`; `gradeFor` is unchanged. `answer()` events gain `outcome`.
- `planReview(words)` keeps the server's oldest-first order and caps at 20; it takes no rng because the order must not be shuffled (the plan's `rng` argument is therefore not used; determinism is tested by identical input giving identical output). A word is asked once; a wrong answer goes to the end up to two extra times, so a word is asked at most three times and ends as `hard` (1 miss), `again` (2 or more).
- Choice prompt in an srs review alternates by position (even: meaning → term, odd: term → meaning); level 2–3 words alternate choose/type by position; terms with spaces or over 24 characters stay on choose.
- Recording bookkeeping: `claimRecord` returns a finished word's outcome exactly once, `releaseRecord` re-opens it after a failed call, `markRecorded` stores the server answer, `unsaved` lists words not yet saved; `summary()` adds `words[]` (level, stage, label, promoted, outcome, saved) and `xp`.

### Gate record S4 — CSS foundation and partials (2026-11-14)
Commit: `feat(vocabulary): add the SRS styling foundation and the art, dots, memory and forecast partials`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0); the four new partials compile |
| G2 | pass: Vocabulary / SpacedRepetition / LearningApi / Goals area green (part of the full run) |
| G3 | pass: 1699 passed / 0 failed / 34 skipped (= S2) |
| G4 | pass: node untouched (315 tests, the one known failure) |
| G5 | pass: no URL, endpoint or existing view touched |
| G6 | pass: grep on `vocabulary.css` finds no hex, `rgb(`, `:root`, `!important`, weight above 700 or non-token `box-shadow`; braces balance; `git diff --check` clean |
| G7 | pass: `wwwroot/css/vocabulary.css` and the four partials `_CourseArt`, `_LevelDots`, `_MemoryPanel`, `_ReviewForecast` (section 2.1/2.2) |
| G8–G13 | n-a for S4: no page consumes the new CSS yet, so nothing renders; the components are rendered and compared with the mockups in S5 (home) and S6 (course), and fixes land in those commits |
| G14 | pass: depth only through `--tct-clay-shadow*`; radii through `--tct-radius-*`; the review card uses a token border, not a literal shadow |
| G15 | pass: no secrets, no inline colours (only the `--tv-n` / `--tv-h` custom properties are inline) |

Decisions:
- The home banner is `.tv-banner` (not `.tv-hero`): `.tv-hero` is still the course-page header until S6.
- Token names: the plan's `--tct-clay-lift` is `--tct-clay-shadow-lift`; there are no "strong" hue tokens, so `fSk`/`fWk`/`fIk` and strokes use the `-text` tokens; the muted art fill (`fM`) is a `color-mix` of `--tct-text-muted` with the surface, with `--tct-border` as the plain fallback.
- `<text>` is a Razor transition tag inside a code block, so the one SVG `<text>` element ("Aa") is emitted through `Html.Raw` in `_CourseArt.cshtml`.
- The ring is an SVG (decorative) with its figures as real text over it; `pathLength=100` lets `--tv-p` be the percentage. Bars and the segmented memory bar get their sizes from inline custom properties.
- Banner chips use `--tct-primary-strong` with `--tct-on-primary` text for contrast on the primary surface (checked in S5 by script).

### Gate record S5 — Home (2026-11-14)
Commit: `feat(vocabulary): rebuild the home with the goal ring, review card, memory panel, forecast and illustrated covers`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0) on a `--no-incremental` build |
| G2 | pass: `Vocabulary*`, `SpacedRepetition*`, `LearningApi*`, `Goals*` green (part of the full run) |
| G3 | pass: 1705 passed / 0 failed / 34 skipped (S4 1699 + 6 new home-state render tests) |
| G4 | pass: node 315 tests, 314 pass, the one known CRLF failure |
| G5 | pass: every legacy `/Vocabulary/*` route still 200 and user-owned ids still 404 (existing route tests green); `LearningApiController.cs` diff empty |
| G6 | pass: no hex, `rgb(`, `:root`, `!important` or weight above 700 in `vocabulary.css`; no inline colours in the views (test `CourseCards_CarryTheIllustrationAndNoInlineColours`); `git diff --check` clean for the changed paths |
| G7 | pass: `Views/Vocabulary/{Index,_CourseCard,_MemoryPanel}.cshtml`, `wwwroot/css/vocabulary.css`, `TCTEnglish.Tests/{VocabularyIntegrationTests,VocabularyReviewRecordTests}.cs` |
| G8 | pass: home rendered at 1440×900, 1366×768, 1024×768, 768×1024, 390×844, 320×640 and at 200 % zoom (720 css px) for a new learner and a learner with data; no horizontal overflow (only the scrollable segmented filter at 320 px, by design); console and network clean (`.tmp/vocabulary-srs/s5/`) |
| G9 | pass with differences: laid out like `01-tu-vung.png` / `06-mobile-tu-vung.png`; consistent with `/Sets` and `/Practice` (same kit, cards and tokens). See "differences" below |
| G10 | pass: 27 scripted checks by mouse and keyboard — new learner (start headline, empty memory/forecast texts, no review button, primary lesson button, ring 0/10), learner with data (due 8, "Ôn ngay" primary, lesson button soft, ring label equals ring text, chips, memory tiles sum to learned words, 7 bars + hidden table, art on every card, due chip, search filters to 2 courses, Tab reaches "Ôn ngay" with a 2 px focus outline then "Học bài mới"); the remaining states (due = 0 with next-due text, target reached, all lessons done, folder view) are covered by render tests |
| G11 | pass: names and roles (ring `role="img"` with label, bar chart hidden from AT with a table, memory bar `role="img"` reading the four numbers); focus order; forced colours (`home-forced-colors-1440.png`: cards bordered, art and bars visible); `prefers-reduced-motion`: ring and bars have no animation; no keyboard trap |
| G12 | pass: contrast script — all text pairs ≥ 4.5:1 (lowest: info-text on info-soft 5.15, white on primary 5.28) and UI parts ≥ 3:1 (ring arc on primary 3.52, bar and dot outlines 3.66). The four filled memory-bar segments and the non-today bars are decorative redundancy (their numbers and labels are text beside them); their fills alone are 2.15–2.54:1, so each bar/segment container has a 3.66:1 outline |
| G13 | pass: `/Sets`, `/Practice`, `/Home/Index` render with a clean console; the legacy endpoint still records a personal card (`TheLegacyEndpoint_StillWorksForAPersonalCard`). `/Goals` logs "jQuery is not defined" from the validation scripts: pre-existing, on a page this phase does not touch |
| G14 | pass: depth only through `--tct-clay-shadow*`; radii through tokens |
| G15 | pass: no secrets; the new view has no raw HTML; no screenshot, mockup or `.tmp` path staged |

Differences from the mockups (and why): the "Lặp lại ngắt quãng" chip row in the memory panel is the plan's sentence ("Trả lời đúng, từ lên một bậc ... 1 → 3 → 7 → 14 → 30 → 60 ngày. Quên thì quay về đầu."); at 1366 px the ring sits above the headline (three banner columns need about 1400 px for ring and text side by side); course group titles and counts are the real catalogue; the floating AI launcher overlaps the lower right of the page as on every page.

Decisions:
- `.tv-banner` replaces `.tv-today`; its CSS (`.tv-today*`, `.tv-stat*`, `.tv-next*`, `.tv-course__icon`) is now unused and is removed in S9.
- The banner and the two panels show on the unfiltered home only; the folder view (`FolderDetail`) keeps header, toolbar and its collection.
- `ViewData["Title"]` is the page title and is shared with partials, so `_MemoryPanel` reads `PanelTitle`/`PanelTitleId` instead (found by the first render test).
- `vocabulary-home.js` is unchanged: its filters work on `[data-tv-course]`, which the new card keeps.

Owned tests changed on purpose (`VocabularyIntegrationTests.cs`): `Home_RendersTheNextLessonButtonDueButtonAndCourseCards` (the old "Học tiếp", "Ôn 1 từ đến hạn" and "Hôm nay bạn đã học 2 từ" strings became "Học bài mới", the due card, the ring label and the memory/forecast panels) and `Home_HidesTheLevelFilterWhenThereAreFewerThanTwoLevels_AndOmitsAZeroStreak` ("chuỗi học" and "Hôm nay bạn chưa học từ nào" became "Chuỗi " and "Bắt đầu với 5 từ đầu tiên nhé"). Intent unchanged.

Data note: no data was reset or seeded. The existing synthetic learner `premium@tct.local` already had 10 system words from the previous run (legacy rows: 8 due, 2 mastered) and was used as the "learner with data"; `student@tct.local` has none and was the new learner.

### Gate record S6 — Course page (2026-11-14)
Commit: `feat(vocabulary): rebuild the course page with the review CTA, memory aside, level dots and word filters`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0; one xUnit2013 in a new test was fixed) |
| G2/G3 | pass: Vocabulary area 152 green; full suite 1708 passed / 0 failed / 34 skipped before the one-line warning fix |
| G4 | pass: node 316 tests, 315 pass, the one known CRLF failure |
| G5 | pass: legacy routes and 404 rules unchanged; `course.DueUrl` kept as `/Vocabulary/Study?setId=..&mode=review` (an existing test asserts it; it renders the same review view as `/Vocabulary/Review?setId=`) |
| G6 | pass: token-only greps clean on `vocabulary.css` |
| G7 | pass: Detail, `_LessonRow`, `_WordRow`, `_LevelDots`, `vocabulary-course.js`, `vocabulary-core.js`, view model, service, tests |
| G8 | pass: course and words tab rendered at 1440, 1366, 1024, 768, 390, 320 and 200 % zoom; no overflow; console clean (`.tmp/vocabulary-srs/s6/`) |
| G9 | pass with differences: hero art tile, chips, CTAs, two columns, aside with memory and "Sắp ôn lại" as in `02-khoa-hoc.png`; the memory panel uses the compact 2×2 tiles instead of the mockup's list rows |
| G10 | pass: 20 scripted checks (CTA order, due chip, lesson chips and dots, sticky aside under the top bar, ArrowRight tabs, 20 word rows with tile/dots/stage/next label, the four filters 8/10/10/20, empty filter text, example disclosure) |
| G11/G12 | pass: forced colours verified; aside above the tabs on narrow screens; no new colour pairs beyond S5's checked tokens |
| G13–G15 | pass: regression pages clean; antiforgery/404 unchanged |

Decisions: word rows are a two-line grid at every width (the 7-column table collapsed the term to one letter per line next to the aside); `_LevelDots` takes an int (-1 = not learned) because a null partial model falls back to the page model; filters gained `due` and `learned` in `vocabulary-core.js` (node-tested). Owned tests changed on purpose: "Học tiếp bài 2" → "Học bài tiếp theo", "Ôn lại từ đầu" → "Học lại từ đầu".

**Run status at the usage limit:** S0–S6 done and committed; S7 (lesson), S8 (review and result), S9 (cleanup and docs), S11 (full gate and end-to-end run) are NOT done. S10 skipped (D1 blank).

### Gate record S7 + S8 — Lesson, review and result (2026-11-14)
Commit: `feat(vocabulary): record lessons and reviews on the memory ladder with a schedule row and a per-word result`

S7 and S8 share `vocabulary-lesson.js`, `Study.cshtml` and the same CSS block, so they were built and gated together and committed once; every item below covers both.

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0) |
| G2/G3 | pass: Vocabulary area 155; full suite 1711 passed / 0 failed / 34 skipped |
| G4 | pass: node 317 tests, 316 pass, the one known CRLF failure |
| G5 | pass: legacy routes and 404 rules unchanged; `LearningApiController.cs` diff empty; the lesson script no longer calls `/api/LearningApi/record` (asserted by a node test and by the browser run: no call to it) |
| G6 | pass: token-only greps clean; no inline colours; `git diff --check` clean |
| G7 | pass: `vocabulary-lesson.js`, `Study.cshtml`, `vocabulary.css`, `StudyService.Vocabulary.cs`, the owned and new tests |
| G8 | pass: lesson intro and review page rendered at 1366×768, 1024×768, 768×1024, 390×844, 320×640 with no overflow and a clean console; 1440 screens (intro, feedback, result) in `.tmp/vocabulary-srs/play/` and `s7/` |
| G9 | partial: compared by eye with `03-lam-quen.png`, `04-on-tap.png`, `05-ket-qua.png`: picture tile, round dots, soft "Tôi đã biết từ này", memory chip, schedule row, four tiles, word list with dots and labels all present; pixel-level comparison not done |
| G10 | pass (browser, mouse): see "Behaviour run" below. Keyboard-only play of the whole session was not scripted; the existing Enter/1–4/R handlers are unchanged |
| G11 | partial: the schedule row is written into the existing live region without moving focus; forced colours and reduced-motion rules added for the new components but not captured in a screenshot for these screens |
| G12 | not re-run: the new colour pairs reuse tokens already checked in S5 (`primary-strong` on `primary-soft`, `danger-text` on surface, `text-secondary` on surface) |
| G13 | partial: the legacy endpoint test for a personal card is green; `/Sets`, `/Practice`, `/Home/Index` were checked after S5 and are untouched since |
| G14/G15 | pass: tokens only; the POST keeps antiforgery (header from the page token) and answers 404 for non-system cards |

**Behaviour run** (real Chrome, `premium@tct.local`, real endpoint, no SQL):
- Lesson 2 (5 new words): "Tôi đã biết từ này" on the first word → `known` (level 4, +14 days, "Ôn lại sau 14 ngày"); three clean words → `good` (level 1, +1 day); one word with one wrong answer → `hard` (level 1, `WrongCount` unchanged, "Giữ nguyên bậc nhớ"). Exactly 5 POSTs, one per word, no legacy call; the database rows match the labels on screen; Goals daily activity rose (+55 XP shown).
- Replaying that lesson (not due): five "Lịch ôn giữ nguyên" rows, levels and dates unchanged (early review).
- Review of 8 due words: 8 POSTs; Cat wrong twice → `again` (level 0, +10 min, `WrongCount` +1, "Từ này sẽ quay lại sớm" / "Ôn lại sau 10 phút", red label on the result row); Bird wrong once → `hard` (level 1); the other six `good` (+1 or +3 days); 6/8 correct, +40 XP, result rows equal the 8 words.
- Simulated failure (requests answered 500): after the immediate retry the row shows "Chưa lưu được tiến độ" and "Thử lại", the session continues, and "Thử lại" then saves and shows the schedule.
- Review cap of 20, oldest first, `RemainingDue` and the nothing-due state are covered by the integration tests (`Review_IsCappedAtTwentyOldestFirst...`, `ReviewPage_NothingDue...`).

Decisions: the lesson's "Ôn n từ đến hạn" button uses `RemainingDue`, which for a lesson is the number of due words in that course at page load and for a review is the number that did not fit in the 20; the empty review state now reads "Hôm nay không có từ nào cần ôn" with "Từ tiếp theo đến hạn: …", "Học bài mới" and "Về trang Từ vựng" (owned test updated: the old "Không có từ nào đến hạn ôn."). A replayed lesson still says "Lịch ôn mới đã được xếp cho từng từ" in its sub-title even though every row says "Lịch ôn giữ nguyên"; left as is.

Not verified in a browser: the rebuilt nothing-due screen (the running app was not restarted after that last view edit; covered by render tests), 200 % zoom and forced colours on the lesson screens, full keyboard-only session.

### Gate record S9 — Cleanup and docs (2026-11-14)
Commit: `chore(vocabulary): remove the dead home and lesson styles and the icon key, update the docs`

| Gate | Result |
| --- | --- |
| G1 | pass: 0 errors, 58 warnings (= S0) |
| G2/G3 | pass on the second and third full runs: 1711 passed / 0 failed / 34 skipped. **The first full run of this phase reported 1 failed test whose name I did not capture; two reruns of the whole suite were green, so I could not reproduce or identify it** (the Vocabulary area is green: 155) |
| G4 | pass: node 317 tests, 316 pass, the one known CRLF failure |
| G5 | pass: `LearningApiController.cs` diff empty; routes unchanged |
| G6 | pass: token-only greps clean on the pruned CSS; braces balance |
| G7 | pass: `vocabulary.css`, `StudyService.Vocabulary.cs`, `VocabularyPageViewModels.cs`, `VocabularyIntegrationTests.cs`, tracker row, `project-structure.md`, `known-issues.md`, `bug-fix-log.md`, this plan |
| G8–G15 | n-a / unchanged: no page markup changed; the removed rules matched no class in any view or script (grep over `Views` and `wwwroot/js`), but the pages were not re-rendered after the prune |

Removed: CSS rules for `.tv-today*`, `.tv-stats`, `.tv-stat*`, `.tv-next*`, `.tv-course__icon`, `.tv-hero__icon`, `.tv-due`, `.tv-resultrow__ipa`, `.tv-resultrow__badge`; `CourseCard.IconKey` and `GetVocabularyIconKey` (the art key replaced them). Kept on purpose: `gradeFor` and the two-pass `review` flow in `vocabulary-core.js` (21 existing tests and the relearn flow use them).
Owned tests changed on purpose: `Home_CourseCardsCarryRealCountsLevelIconAndStatus` was renamed to `...LevelArtAndStatus` and asserts `ArtKey` (`jobs` for set 390, `talk` for set 302) instead of `IconKey`.
Docs: tracker row "Từ vựng", `docs/project-structure.md`, `.ai/context/known-issues.md` (TD-013 update, new TD-016), `.ai/context/bug-fix-log.md`.
