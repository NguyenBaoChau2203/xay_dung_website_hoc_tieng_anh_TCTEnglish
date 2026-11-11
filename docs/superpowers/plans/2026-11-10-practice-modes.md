# Practice & Study Games — scoped plan (2026-11-10)

> **Tóm tắt cho Trọng (tiếng Việt).** Plan này làm khu **Luyện tập** với các chế độ học
> và trò chơi theo mẫu English_China, dựa trên phần Bộ thẻ vừa làm xong:
> - Bốn chế độ chính: Học, Kiểm tra, Ghép thẻ, Lật thẻ.
> - Bốn kỹ năng riêng: Trắc nghiệm, Gõ từ, Nghe, Điền từ.
> - Trang "Từ hay quên".
>
> Bốn trang học cũ (`/Home/Study|QuizMode|WriteMode|MatchingMode`) **giữ nguyên**, vì test
> và dữ liệu chatbot của Châu đang dựa vào chúng. Plan không cần migration và không sửa
> `Program.cs`. Chỉ có **một** ô cần quyết (D1, mục 1.2). Phần chỉ dẫn cho agent viết bằng
> tiếng Anh theo `AGENTS.md`.

- Owner / page owner: Huỳnh Phú Trọng (Vocabulary, Set/Card, Folder, study modes per `README.md`).
- Save as `docs/superpowers/plans/2026-11-10-practice-modes.md`.
- Prompt: `.ai/prompts/claude-code-practice-modes.md`.
- **Baseline, verified 2026-11-10 on the owner's machine:**
  - Branch `feature/personal-sets-redesign` at `a9964a6`, 5 local commits on top of `origin/master` `eed4e2c`, **not pushed**.
  - One unrelated uncommitted change: `TCTEnglish/appsettings.Development.json` (leave it).
  - Full Release test run recorded by the previous agent: 1272 passed / 22 skipped / 0 failed. Node tests: 25/25.
  - Re-verify all of this at Q0.
- **Reference product:** `tronghp160/English_China` (Next.js). The relevant parts are its `docs/PLAN-study-games.md`, `lib/practice/*` and `components/practice/*`. Use them for **behavior only**; every rule needed is restated below. Do not copy TS/React code or add packages.

---

## 0. Current state this plan builds on

### 0.1 Personal sets (finished on the branch above)

- **Service:** `IPersonalSetService` / `PersonalSetService` (+ `.Write.cs`) in `Services/Sets/`, registered in `Program.cs`.
  - Access helpers: `CanViewSetAsync`, `CanEditSetAsync`, `GetViewableSetIdsAsync`.
  - `CardStatus` (new / learning / learned / hard / suspended).
  - `SetConstants` (POS, topics, CEFR, cover hues).
- **Controllers:**
  - `SetController` (+ `.Personal.cs`): `/Sets`, `/Sets/{id}`, `/Sets/{id}/Add`, `/Sets/{id}/Import`, `/Sets/Cards/{id}`, `/s/{slug}`.
  - `SetApiController` (`api/sets`) and `VocabApiController` (`api/vocab`).
- **Card data available for questions:**
  - `Term`, `Definition`, `Phonetic`, `PhoneticUk`, `MeaningEn`, `PartsOfSpeech`, `Cefr`, `Topic`, `Emoji`, `ImageUrl`.
  - `Example`/`ExampleTranslation` and `DetailsJson` (examples ≤6, synonyms, antonyms, collocations, word family, `verbForms`, `plural`).
  - `LearningProgress.IsSuspended` (per user).
- **UI:**
  - `wwwroot/css/personal-sets.css` (scoped `.ts-page`, tokens only).
  - `wwwroot/js/personal-sets-common.js`: `window.TctSets` with `fetchJson`, `toast`, `debounce`, `speak`, `safeStorage`, `el`, `pending`, …
  - Set detail page `Views/Set/Detail.cshtml` with a "Học bộ này" strip. It links to the **legacy** pages: Thẻ ghi nhớ → `/Home/Study/{id}`, Trắc nghiệm → `/Home/QuizMode/{id}`, Ghép thẻ → `/Home/MatchingMode/{id}`, Viết → `/Home/WriteMode/{id}`.
  - `wwwroot/js/set-detail.js` reads `localStorage['tct:set-stats:{setId}']` (`quizPercent`, `matchMs`) and marks one tile "Gợi ý cho bạn".
  - `_SetActionsMenu` has "Luyện trắc nghiệm" → `/Home/QuizMode/{id}`.

### 0.2 Legacy study pages (stay as they are)

- `Views/Study/Study.cshtml` (1813 lines), `QuizMode.cshtml` (914), `WriteMode.cshtml` (970), `MatchingMode.cshtml` (733): inline `<style>` and inline scripts.
  - `QuizMode` and `MatchingMode` already write `quizPercent` / `matchMs` into `tct:set-stats:{setId}`.
  - `Study` (+ `?mode=due`) writes progress via `POST /Home/UpdateCardProgress` (`{ cardId, isKnown }`, antiforgery header). That endpoint now checks `CanViewSetAsync` (B7).
- **Dependencies on those routes:**
  - `CriticalFlowSqliteIntegrationTests` (`/Home/WriteMode|QuizMode|MatchingMode/301`).
  - `PersonalSetsIntegrationTests` (Study, `?mode=due`).
  - AI chat citations `/Home/Study/{SetId}` (`GroundingCitationService`, `AiRagPhase5/6` tests).
  - The Hybrid RAG evaluation fact "Quiz mode opens at /Home/QuizMode/{setId}." (`HybridRagEvaluationBaselineTests`).
- **Progress rules** (`StudyService.UpdateCardProgressAsync`): known → `Mastered`, +7 days, `RepetitionCount+1`; unknown → `Learning`, +1 minute, `WrongCount+1`, `RepetitionCount=0`.

### 0.3 Time tracking and goals

- Pages include `<script src="~/js/time-tracker.js" data-feature="X" defer>`, which posts to `/api/LearningApi/track-time`.
- That endpoint accepts only: `Flashcard`, `Quiz`, `Speaking`, `Reading`, `Listening`, `Writing`, `Grammar`. Dashboard minutes read `Flashcard`/`Quiz`/`Writing`.
- Goals, streak and XP live in Châu's code (`GoalsService`, `CompletionCreditService`, `LearningApiController`). They are **not** touched.

---

## 1. Decisions

### 1.1 Fixed decisions

| ID | Decision | Reason |
| --- | --- | --- |
| P1 | The four legacy pages and their routes stay **unchanged** (markup, scripts, behavior). The only exception is that nothing new links to them, except the set hero's "Ôn bộ này" → `/Home/Study/{id}?mode=due` (kept as is). | Existing tests, chat citations and the RAG evaluation corpus depend on them. Retiring them is a later task that needs Châu. |
| P2 | New area under `/Practice`, served by a new `PracticeController`. Service methods are **added to `IPersonalSetService`** (new partial `PersonalSetService.Practice.cs`). **No `Program.cs` change.** | Controllers need no registration; the service is already registered. |
| P3 | **No migration.** Learn progress, Match records, the test draft and per-set stats are stored on the device (`localStorage` / `sessionStorage`, every access in try/catch). | Same choice as the reference product. Losing it means "start again", never data loss. |
| P4 | Learning progress is written **only** through the existing `POST /Home/UpdateCardProgress`, and **only** when the session switch "Tính vào ôn tập" is on (default **off**). Match never writes progress. The new Flashcard mode writes only when the switch is on. | Practice must not silently move review schedules; reuses the existing, access-checked rule. |
| P5 | Time tracking: Flashcard mode → `data-feature="Flashcard"`; every other new mode → `data-feature="Quiz"`. No change to `LearningApiController`. | Only these values are accepted; "Writing" minutes belong to Châu's Writing module. |
| P6 | English only. Drop from the reference product: Chinese modes (tones, handwriting pad, pinyin/Hanzi typing), the mascot (Bin/Pup, mood, talk lines, gifts), combo/XP HUD, the session "gifts", confetti libraries, push/offline outbox, AI tutor prompt links, dark-mode toggles. | Owner request; English-only product; no new infrastructure. |
| P7 | All question building, grading, Learn state and Match boards are **pure JavaScript** in `wwwroot/js/practice-core.js` (UMD: `module.exports` under Node, `window.TctPractice.core` in the browser). It takes an injectable `rng` and is unit-tested with `node --test`. The server only selects words. | Testable and deterministic; mirrors the reference split. |
| P8 | Design system rules from the personal-sets plan F9 apply unchanged (see 6.1). New CSS file `wwwroot/css/practice.css` scoped under `.tp-page`, `var(--tct-*)` only. | AGENTS.md "Shared UI foundation", DESIGN.md. |
| P9 | No AI calls anywhere in practice. | Cost; no value for drills. |
| P10 | Access: every practice read uses `CanViewSetAsync` / `GetViewableSetIdsAsync`. A set the user cannot view → 404. Suspended cards (per user) are excluded from every session. | Same rules as the set pages. |

### 1.2 Owner decision

| ID | Question (who approves) | If CÓ | If KHÔNG / blank |
| --- | --- | --- | --- |
| D1 | Có được thêm **1 mục** "Luyện tập" (`/Practice`) vào sidebar `_Layout.cshtml`, ngay dưới "Bộ thẻ của tôi" không? (Tiến / Châu) | Add exactly one `nav-sub-item` matching its neighbours (same pattern used for "Bộ thẻ của tôi"). | Entry points are only on Trọng's pages: set detail, set list, card detail. |

D1 =

---

## 2. Scope

### 2.1 In scope

1. **Practice hub** `/Practice` (`?set={id}` preselects a set): pick a mode, a scope, a count, a word source, test options and the "Tính vào ôn tập" switch; then start.
2. **Học (Learn):** rounds of 7 words, multiple choice → typing; misses are re-asked in the same round; per-round stars; resume.
3. **Kiểm tra (Test):** a mixed test on one page (choice, true/false, typing, cloze), submit, score, review, retry the missed words.
4. **Ghép thẻ (Match):** boards of 6 pairs, a timer, +1 s per miss, a device-local best time.
5. **Lật thẻ (Flashcard):** flip, swipe, arrow keys, mark known/learning, term-first or meaning-first, shuffle, restart with the unlearned cards.
6. **Skill drills:** Trắc nghiệm (choice), Gõ từ (typing with hint and letter diff), Nghe (listen and choose), Điền từ (cloze from example sentences).
7. **Shared session summary** for the drills, and a **"Từ hay quên"** page `/Practice/HardWords`.
8. **Integration:**
   - The set detail strip and suggestion logic switch to the new modes; the actions menu gains "Luyện tập".
   - Card detail links to "Học từ này".
   - Set list header links to "Luyện tập".
   - Optional sidebar item (D1).
9. Tests (C# integration + Node), browser review, docs.

### 2.2 Out of scope

- Everything in P6.
- Changing or retiring the legacy study pages.
- FSRS or any change to `UpdateCardProgressAsync`.
- Goals, XP and streak.
- Server-side storage of Learn/Match/Test state.
- Admin screens.
- Vocabulary (system) study page `Views/Vocabulary/Study.cshtml`.

---

## 3. English_China → TCT mapping

| English_China | TCT |
| --- | --- |
| `/practice` setup, scope "Mọi bộ <ngôn ngữ>" | `/Practice`, scope "Tất cả bộ của tôi" (own + followed sets) or one set |
| `/practice/[mode]?deck&n&source&srs&types&ask` | `/Practice/{mode}?set&scope&n&source&srs&types&ask&words` |
| `practice_words` RPC (targets + pool of 200) | `IPersonalSetService.GetPracticeAsync` (targets + pool ≤200) |
| `enqueueReview` outbox (rating 3/1, `updateCard` = switch) | `POST /Home/UpdateCardProgress { cardId, isKnown }`, called only when the switch is on |
| `idb-keyval` stores | `TctSets.safeStorage` (localStorage) / `sessionStorage` |
| `MODE_INFO`, `countsFor`, `suggestMode`, `buildQuestions`, `buildTest`, `gradeTest`, `learn.ts`, `match.ts`, `typing-diff.ts`, `checkTyping`, `makeCloze`, `pickDistractors` | Same behavior in `practice-core.js` (section 5) |
| `StudyWord.meaning_vi` / `parts_of_speech` / `topic` / `lang_extra.verb_forms` / `plural` / `examples` | `Definition` / `PartsOfSpeech` / `Topic` / `DetailsJson.verbForms` / `DetailsJson.plural` / `DetailsJson.examples` (legacy `Example` fallback) |
| `hard-words` page | `/Practice/HardWords` |
| `SessionSummary` (stars, KPIs, missed list, tutor link) | Summary partial without the tutor link (P6, P9) |

---

## 4. Server side

### 4.1 Routes (`Controllers/PracticeController.cs`)

- Namespace `TCTEnglish.Controllers`; inherits `BaseController`.
- Attributes: `[Authorize]`, `[AutoValidateAntiforgeryToken]`, `[Route("Practice")]`.
- User id from `TryGetCurrentUserId`; admin flag from the same helper used by `SetController`.

| Method | Route | Action | Result |
| --- | --- | --- | --- |
| GET | `/Practice` | `Index(int? set)` | View `Practice/Index` (`PracticeHubViewModel`). `set` not viewable → ignored (no preselect, no error) |
| GET | `/Practice/{mode}` | `Session(string mode, PracticeQuery q)` | View `Practice/Session` (`PracticeSessionViewModel`). Unknown mode → 404. A `set` the user cannot view → 404. Fewer words than the mode needs → same view in an "empty" state (section 6.11) |
| GET | `/Practice/HardWords` | `HardWords(int? set)` | View `Practice/HardWords` |

**`mode`** ∈ `learn | test | match | flashcard | quiz | typing | listening | cloze` (case-insensitive; the canonical lowercase value is used in URLs).

**`PracticeQuery`** (binds from the query string; every value is validated and unknown values fall back to defaults, never an error):

| Field | Allowed values | Default |
| --- | --- | --- |
| `set` | int | — |
| `scope` | `mine` (ignored when `set` is present) | `mine` when no `set` |
| `n` | must be in `countsFor(mode)` (section 5.2); `0` = "Cả bộ" for flashcard only | first allowed value |
| `source` | `all`, `due`, `hard`, `recent` | `all` |
| `srs` | `1` / absent | off |
| `types` | test only: CSV subset of `choice,tf,typing,cloze` | `choice,tf,typing` |
| `ask` | test only: `both`, `term`, `meaning` | `both` |
| `words` | CSV of card ids, ≤50; used by "retry missed" and "learn these words"; overrides `source`; ids outside viewable sets are dropped silently | — |
| `from` | `set` / `hub` / `hard` (only selects the exit link) | — |

### 4.2 Service additions (`Services/Sets/PersonalSetService.Practice.cs` + `IPersonalSetService`)

```text
GetPracticeHubAsync(int? setId, int userId, bool isAdmin, ct)           -> PracticeHubViewModel
GetPracticeAsync(string mode, PracticeQuery q, int userId, bool isAdmin, ct) -> PracticeSessionViewModel?   // null = set not viewable
GetHardWordsAsync(int? setId, int userId, bool isAdmin, ct)              -> HardWordsViewModel
```

**Scope**
- `set`: one set, which must pass `CanViewSetAsync`.
- `mine`: sets the user owns or follows (`SetSubscriptions`). Shared-via-folder sets are excluded here (they are reachable through `set=`).

**Card filter (every source):**
- card belongs to the scope;
- the user's progress is not suspended;
- `Term` and `Definition` are not blank.

**Sources (targets):**

| Source | Rule |
| --- | --- |
| `all` | Set order: `CreatedAt` asc (nulls first) then `CardId` asc. Scope `mine`: grouped by set, newest set first. |
| `due` | Progress exists, not suspended, `NextReviewDate <= UtcNow`, ordered by `NextReviewDate`. |
| `hard` | Progress with `WrongCount >= 1`, ordered by `WrongCount` desc, then `LastReviewedDate` desc. |
| `recent` | `CreatedAt` desc (nulls last), then `CardId` desc. |
| `words=` | Exactly those ids (deduplicated, viewable, not suspended), in the given order. |

**Limits**
- Targets: `Take(n)`. Flashcard `n=0` → whole scope, capped at 500.
- For `test` with `cloze` selected, fetch up to `2n` targets, so the client can still fill `n` questions after dropping words without a usable example.
- **Pool** (distractors): up to 200 other cards from the same scope (query ordered by `CardId`, capped at 1000 ids, then sampled in memory with `Random.Shared`; no `ORDER BY RAND()`), excluding the targets.
  - Pool fewer than 3 and scope is a single set → top up from the user's other viewable sets.
  - Still fewer → the client degrades (section 5.3).

**`PracticeWordDto`** (camelCase JSON, read-only projection, `AsNoTracking`):

```text
id, setId, term, phonetic, phoneticUk, definition, meaningEn, partsOfSpeech[], topic, cefr, emoji, imageUrl,
examples[{ text, vi }] (DetailsJson.examples, else legacy Example/ExampleTranslation, ≤6),
verbForms{ past, pastParticiple, ing, thirdPerson }?, plural?, status (CardStatus key), wrongCount
```

- Never include `PersonalNote`, `ImageSourceJson`, `DetailsJson` raw, share slugs or other users' data.
- Parse `DetailsJson` with the existing tolerant parser used by `GetCardDetailAsync`; bad JSON → empty.

**`PracticeSessionViewModel`**

```text
Mode, ModeLabel, Query (normalized), ScopeKey ("set:{id}" | "mine"), ScopeName, SetId?, Targets[], Pool[],
CountsToSrs, TimeFeature ("Flashcard" | "Quiz"), UserId, ExitUrl, AgainUrl, HubUrl, SetUrl?, TestUrl, LearnUrl, Empty (bool), EmptyReason
```

- `ExitUrl`: `from=set` → `/Sets/{id}`; `from=hard` → `/Practice/HardWords`; else `/Practice?set={id}`.
- `AgainUrl`: the same query.

**`PracticeHubViewModel`**
- Sets: own + followed + (when `set` is a shared/public viewable set) that set. Each entry: `{ id, name, wordCount (non-suspended), dueCount }`. Sets with 0 words are excluded.
- `PreselectedSetId`, `UserId`, `HardWordCount`.

**`HardWordsViewModel`**
- Words with `WrongCount >= 1` in viewable sets (or one `set`), not suspended.
- Ordered by `WrongCount` desc, then `LastReviewedDate` desc, top 100.
- Each row: `cardId, setId, setName, term, phonetic, definition, wrongCount, lastReviewed`.

**Rules**
- No queries in loops. Use one query per list with projections.
- All async with `CancellationToken`.

### 4.3 Progress writes

The client calls the existing endpoint:

```js
fetchJson('/Home/UpdateCardProgress', { method: 'POST', body: { cardId, isKnown } })
```

- Only when `CountsToSrs` is true (`TctSets.fetchJson` adds the antiforgery header; the page must render `@Html.AntiForgeryToken()`).
- Fire-and-forget with one retry on network failure. Failures never block the session; show one toast per session: "Không lưu được tiến độ ôn tập. Kết quả luyện tập vẫn được giữ."
- **Which answers write:**
  - drills, Test and Learn: one call per answered question (`isKnown` = correct, "close" counts as correct);
  - Flashcard: one call per mark;
  - Match: never.

### 4.4 Tests (`TCTEnglish.Tests/PracticeIntegrationTests.cs`, existing factory, helpers and `TestDataIds`)

- **Access:**
  - Owner gets 200 on `/Practice`, `/Practice/learn?set=301`, `/Practice/HardWords`.
  - Outsider on a Private set → 404 for every mode.
  - Public system set (302) → 200 for a stranger.
  - Unknown mode → 404.
  - Anonymous → redirect to login.
- **Payload** (parse the JSON blob in the page):
  - suspended cards excluded;
  - `source=due|hard|recent` order;
  - `words=` keeps only viewable ids and their order;
  - `n` clamped to the allowed list;
  - pool excludes targets;
  - no `personalNote` key anywhere;
  - `test` with `cloze` returns up to `2n` targets.
- **Scope `mine`:** includes followed sets and excludes others' sets.
- **Hard words:** order, the ≥1 threshold, suspended excluded.
- **Antiforgery:** the page contains an antiforgery token, and `UpdateCardProgress` without the header still → 400 (existing contract).
- **Hub preselect:** `set` of a non-viewable set is ignored, not leaked (the response does not contain its name).

---

## 5. Pure logic: `wwwroot/js/practice-core.js`

- UMD, no DOM access, no globals except `window.TctPractice.core` in the browser.
- Every random step takes `rng` (default `Math.random`). Export `mulberry32(seed)` for tests.
- Strings shown to users are Vietnamese literals as given here.

### 5.1 Helpers

- `shuffle(list, rng)`: Fisher–Yates on a copy.
- `textKey(s)`: NFC, trim, lowercase, collapse whitespace.
- `levenshtein(a, b)`.
- **`checkTyping(input, word)`** → `"correct" | "close" | "wrong"`.
  - Clean both sides: NFC, lowercase, `’` and `` ` `` → `'`, trim, strip trailing `.!?,;:`, collapse spaces.
  - Equal → `correct`.
  - The term without spaces has ≥6 letters **and** `levenshtein ≤ 1` → `close`.
  - Blank input → `wrong`.
- **`pickDistractors(target, pool, rng, count = 3)`**
  - Shuffle the pool, drop the target, then stable-sort by score: +2 when they share a part of speech, +1 for the same topic.
  - Skip a candidate whose `textKey(term)` or lowercased definition was already used (the target counts as used).
- **`choiceOptions(target, pool, rng)`** = `shuffle([target, ...pickDistractors(...)])`.
- **`englishForms(word)`**
  - The term, plus `verbForms.*` and `plural` when present.
  - For single-word terms: `+s`, `+es`, `+d`, `+ed`, `base+ing` (base = term without a final `e`), `term+ing`, `y→ies`.
  - Sorted longest first.
- **`makeCloze(word)`**
  - For each example `{ text, vi }` and each form, match `(^|[^\p{L}'])(form)(?![\p{L}])` with flags `iu`.
  - First hit → `{ before, answer, after, vi }`. No hit → `null`.
- `fitsMode(word, mode)`: `cloze` → `makeCloze(word) !== null`; otherwise `true`.

### 5.2 Modes registry

```text
MODE_INFO = {
  learn:     { label: "Học",         hint: "Học theo vòng tới khi thuộc hết bộ từ." },
  test:      { label: "Kiểm tra",    hint: "Làm bài trộn nhiều dạng câu, chấm điểm cuối bài." },
  match:     { label: "Ghép thẻ",    hint: "Ghép từ với nghĩa nhanh nhất có thể." },
  flashcard: { label: "Lật thẻ",     hint: "Lật qua lật lại, tự đánh dấu thuộc hay chưa." },
  quiz:      { label: "Trắc nghiệm", hint: "Chọn nghĩa đúng của từ, hoặc từ đúng với nghĩa." },
  typing:    { label: "Gõ từ",       hint: "Nhìn nghĩa, gõ lại từ." },
  listening: { label: "Nghe",        hint: "Nghe đọc rồi chọn từ đúng." },
  cloze:     { label: "Điền từ",     hint: "Chọn từ còn thiếu trong câu ví dụ." }
}
STUDY_GAMES = ["learn", "test", "match", "flashcard"];  SKILLS = ["quiz", "typing", "listening", "cloze"]
countsFor(mode): flashcard [20, 50, 0(=Cả bộ)]; match [6, 12, 18] (pairs); learn [10, 20, 50]; others [10, 20, 30]
COUNT_UNIT: flashcard { legend "Số thẻ", unit "thẻ" }, match { "Số cặp", "cặp" }, learn { "Số từ", "từ" }, default { "Số câu", "câu" }
```

The server mirrors `countsFor` in C# (`PracticeModes.CountsFor`). A test asserts both lists match: parse the JS constant in a Node test and the C# constant in an xUnit test against the same literal table in this plan.

### 5.3 Question model and drills

```text
Question = { id, kind: "choice", prompt: "term"|"meaning"|"audio", word, options[{id,label}], answerId }
         | { id, kind: "typing", word }
         | { id, kind: "truefalse", prompt: "term"|"meaning", word, shown, isTrue }
         | { id, kind: "cloze", word, cloze, options, answerId }
         | { id, kind: "flashcard", word }
```

`buildQuestions(mode, targets, pool, rng)` for the drills. `all` = targets ∪ pool, deduplicated by id; words that do not fit the mode are filtered out.
- `quiz`: `prompt` is `term` or `meaning` with 50/50 chance. Options come from `choiceOptions`; labels are definitions when `prompt=term`, terms otherwise.
- `listening`: `prompt=audio`; option labels are terms.
- `typing`: `{ kind: "typing" }`.
- `cloze`: options = `[answer form] + distractor terms`, shuffled.
- Fewer than 2 distinct candidates for a choice question → fall back to `typing`.

### 5.4 Test

```text
TEST_TYPES = ["choice","tf","typing","cloze"]; DEFAULT_TEST_TYPES = ["choice","tf","typing"]
TEST_TYPE_LABEL = { choice: "Trắc nghiệm", tf: "Đúng / Sai", typing: "Gõ từ", cloze: "Điền từ" }
ASK labels: both "Trộn", term "Từ", meaning "Nghĩa"
parseTestTypes(raw) / parseTestAsk(raw): unknown -> defaults
buildTest(targets, pool, { types, ask, count }, rng)
```

**`buildTest` rules:**
- **Words:** shuffle the deduplicated targets; `total = min(count, words)`.
- **Type set:** `chosen` = the requested types. When `all < 2`, drop `choice` and `tf`; if nothing is left, use `["typing"]`.
- **Quotas:** each type gets `floor(total / chosen.length)`; the remainder goes to randomly chosen types.
- **Cloze first:** take words that fit cloze, up to the cloze quota. The unfilled cloze quota is redistributed across the other types (or `choice`/`typing` when cloze was the only type).
- Remaining words are assigned to the remaining quotas in order. Every word appears once; the result is shuffled.
- **`choice`:** prompt from `ask` (`both` = 50/50).
- **`tf`:**
  - Pick one distractor; `isTrue` 50/50.
  - `prompt=term` → `shown` = the definition of the true word or of the distractor.
  - `prompt=meaning` → `shown` = a term.
  - No distractor → `typing`.
- **`typing`:** always definition → term.

**`gradeTest(questions, answers)`**
- Answers are option ids, `"true"/"false"` or typed text.
- Blank → `wrong`. Typing uses `checkTyping`; `close` counts as correct.
- Returns `{ correct, total, percent (rounded), items[{ question, answer, result }] }`.

**`answerText(q, answer)` / `expectedText(q)`:**
- true/false → "Đúng" / "Sai"; the expected text for a false item is `Sai (<correct pair>)`.
- Typing expected text = term (+ ` · phonetic` when present).

### 5.5 Learn

```text
LearnWordState { wordId, level: 0|1|2, misses }
LearnState { version: 1, words[], round, queue[], roundWords[], roundStars[] }
LEARN_ROUND_SIZE = 7
initLearn(ids) · startRound(state, size) · levelOf · questionFor(state, word, pool, rng) · answer(state, wordId, correct)
roundDone · learnComplete · progress -> { notStarted, learning, mastered } · starsFor(right, total) · recordRound(state, stars)
roundsDone · stageCount · previewRound -> { ids, fresh, review } · masteryDots(state, id) -> 0..3 · restoreLearn(saved, availableIds, targetIds, limit)
```

- **Round selection:** `startRound` takes up to 7 words with level < 2: words already seen (level > 0 or misses > 0) first, then new words, in study order. It increments `round` and sets both `queue` and `roundWords`.
- **Question choice:** `questionFor` gives level 0 a `choice` question (50/50 prompt; fewer than 2 options → `typing`) and level 1 a `typing` question.
- **Answering** (`answer`; ids not in the queue are ignored):
  - Remove the id from the queue.
  - Correct → level +1 (max 2).
  - Wrong → level 0, misses +1, and the id is pushed to the end of the queue.
- **Stars:** `starsFor` = 3 at ≥90%, 2 at ≥70%, else 1.
- **Mastery dots:** `masteryDots` = mastered 3 · level 1 → 2 · missed → 1 · new → 0.
- **Restoring a saved state** (`restoreLearn`):
  - Drop ids that no longer exist.
  - Append new targets at level 0 up to `limit`.
  - Another version or an empty result → `null`.

### 5.6 Match

```text
MATCH_PAIRS_PER_BOARD = 6; MATCH_PENALTY_MS = 1000
buildMatchBoards(words, pairs, rng): boards never hold two words sharing textKey(term) or textKey(definition); a board with <2 pairs is dropped; tiles { id: "<id>:t"|"<id>:m", pairId, side: "term"|"meaning", label } shuffled per board
isPair(a, b): same pairId, different side
formatMs(ms): "12,3 giây" / "1 phút 05,2 giây" (tenths, comma decimal)
```

### 5.7 Typing aids

- `diffChars(typed, answer)`: case-insensitive LCS → `{ typed[{ch, ok}], answer[{ch, ok}] }`.
- `hintMask(answer, shown)`: reveals the first `shown` non-space characters and replaces the rest with `_`, keeping spaces.
- `maxHints(answer)` = letters − 1.

### 5.8 Suggestion

```text
suggestMode({ learnPercent, testPercent, matchMs }):
  learnPercent null or <100 -> "learn"; testPercent null -> "test"; testPercent <80 -> "learn"; else "match"
```

### 5.9 Device storage (`practice-store` part of `practice-core.js`, storage injected so Node tests can pass a fake)

| Key | Store | Value |
| --- | --- | --- |
| `tct:learn:{userId}:{scopeKey}` | local | `LearnState` |
| `tct:match-best:{userId}:{scopeKey}:{pairs}` | local | `{ ms, at }` |
| `tct:test-draft:{userId}:{testId}` | session | `{ answers }`; `testId` = hash of the question ids |
| `tct:set-stats:{setId}` | local | **existing key, merge only:** `learnPercent`, `learnAt`, `testPercent`, `testAt`, and `matchMs`/`matchAt` (lower value wins, only for full 6-pair-or-more games). Never remove `quizPercent` (the legacy Quiz page writes it). |
| `tct:practice-setup:{userId}` | local | last hub choices `{ mode, scope, n, source, srs, types, ask }` |

### 5.10 Node tests (`scripts/tests/test_practice_core.cjs`, `node:test`, seeded `mulberry32`)

- **Typing:** `checkTyping` correct / close / wrong / blank, and the ≥6-letter rule.
- **Distractors:** `pickDistractors` uniqueness and POS/topic preference.
- **Cloze:**
  - `makeCloze` hits for "running", "studies", "went" (from `verbForms`) and multi-word terms;
  - `null` without examples;
  - no match inside other words ("art" in "start").
- **Drills:** `buildQuestions` choice fallback with a tiny pool.
- **Test:**
  - `buildTest` quota spread, cloze redistribution, no repeated words, both true and false items, typing fallback with fewer than 2 words;
  - `gradeTest` correct / close / wrong / blank.
- **Learn:**
  - round size 7;
  - a miss is re-queued and resets to level 0;
  - mastered words are never asked;
  - `progress`, `learnComplete`, `starsFor`, `recordRound`;
  - `restoreLearn` drops removed ids and rejects another version;
  - stray `answer` calls are ignored.
- **Match:** boards contain each pair exactly once, no duplicate term/definition on a board, a final 1-pair board is dropped, deterministic with a seed; `formatMs`.
- **Typing aids:** `diffChars`, `hintMask` with a multi-word term, `maxHints`.
- **Suggestion:** `suggestMode` table.
- **Counts:** `countsFor` matches the C# table.
- **Storage:** helpers survive a storage that throws.

---

## 6. UI

### 6.1 Rules (same as personal sets)

- **Wrapper:** `<div class="tct-theme tct-page tp-page tp-page--{hub|session|hard}">`.
  - The hub uses `_PageHeader`.
  - The session screen uses its own compact top bar (no page header). It still has exactly one `<h1>`: visually hidden, "{Mode} · {ScopeName}".
- **Shared building blocks:** `_StateMessage`, `.tct-card`, `.tct-badge--*`, `.tct-toolbar`, `.tct-skeleton*`, `.tct-phonetic`, `.tct-btn-icon`, `.tct-clamp-2`, Bootstrap buttons, forms, modals and toasts.
- **CSS:** only `var(--tct-*)` in `practice.css`. No hex, `:root`, element selectors outside `.tp-page`, `!important`, or font weight above 700. No inline styles except CSS custom properties carrying data (`--p`, `--i`).
- **Icons:** Font Awesome 6.0.0 only, checked to exist.

| Use | Icon |
| --- | --- |
| Học | `fa-graduation-cap` |
| Kiểm tra | `fa-clipboard-check` |
| Ghép thẻ | `fa-shuffle` |
| Lật thẻ | `fa-clone` |
| Trắc nghiệm | `fa-list-check` |
| Gõ từ | `fa-keyboard` |
| Nghe | `fa-headphones` |
| Điền từ | `fa-pen-to-square` |
| Speak | `fa-volume-high` |
| Hint | `fa-lightbulb` |
| Correct | `fa-circle-check` |
| Wrong | `fa-circle-xmark` |
| Exit | `fa-xmark` |
| Timer | `fa-stopwatch` |
| Record | `fa-trophy` |
| Stars | `fa-star` (solid/regular) |
| Lock | `fa-lock` |
| Play | `fa-play` |

- **Mode hues** (existing token pairs, the same six as the set covers):

| Mode | Hue |
| --- | --- |
| learn | success |
| test | primary |
| match | warning |
| flashcard | info |
| quiz | primary |
| typing | accent |
| listening | info |
| cloze | danger |

  Every mode is still identified by text, never by color alone.
- **Motion:** `--tct-dur*` / `--tct-ease` only. Any keyframe (flip, pop, wiggle, fade-out) is disabled under `prefers-reduced-motion: reduce`. In that case the card flip becomes an instant face swap.
- **Speech:** `TctSets.speak(text)` (`en-US`, cancel before speaking; the button is hidden when unsupported). Nothing auto-plays except a `listening` question when it first appears.
- **Every session page:**
  - renders `@Html.AntiForgeryToken()` and `time-tracker.js` with `TimeFeature`;
  - embeds `<script type="application/json" id="tpConfig">` (serialized `PracticeSessionViewModel` minus server-only fields);
  - loads `personal-sets-common.js`, `practice-core.js`, `practice-ui.js` and one mode file.
  - JS renders with `createElement`/`textContent` only.
- **Responsive:** check 1440 / 768 / 390 / 320 + 200% zoom. No horizontal scroll. Fixed and sticky bars respect `env(safe-area-inset-bottom)` and keep clear of the AI chat launcher (read its real offsets in `ai-chat-launcher.css`).
- **Keyboard:**
  - `1–4` pick an option; `R` replays audio; `Enter` checks or continues; `Escape` closes dialogs.
  - Flashcard adds `Space` (flip), `←/→` (move) and `1/2` (mark).
  - Keys are ignored while typing in an input or textarea, and when Ctrl, Meta or Alt is held.
  - Key hints are shown on ≥992px only.

### 6.2 Files

| File | Role |
| --- | --- |
| `Controllers/PracticeController.cs` | Routes (4.1) |
| `ViewModels/Sets/PracticeViewModels.cs` | `PracticeQuery`, `PracticeWordDto`, `PracticeSessionViewModel`, `PracticeHubViewModel`, `HardWordsViewModel` |
| `Services/Sets/PracticeModes.cs` | C# mode list, labels, `CountsFor`, normalization of the query |
| `Views/Practice/Index.cshtml` | Hub |
| `Views/Practice/Session.cshtml` | Session shell (all modes) |
| `Views/Practice/HardWords.cshtml` | Hard words |
| `Views/Practice/_ModeCard.cshtml` | One radio card on the hub |
| `wwwroot/css/practice.css` | Styles (`.tp-page`) |
| `wwwroot/js/practice-core.js` | Pure logic (section 5) |
| `wwwroot/js/practice-ui.js` | Shared DOM parts: top bar, progress, term/meaning lines, options, feedback panel, key hints, typing hint/diff, summary, empty state, progress writer, toast-once |
| `wwwroot/js/practice-hub.js` | Hub form |
| `wwwroot/js/practice-drill.js` | quiz / typing / listening / cloze |
| `wwwroot/js/practice-flashcard.js` | Flashcard |
| `wwwroot/js/practice-match.js` | Match |
| `wwwroot/js/practice-test.js` | Test |
| `wwwroot/js/practice-learn.js` | Learn |

### 6.3 Hub `/Practice`

```text
_PageHeader: Title "Luyện tập"; Description "Luyện thêm ngoài giờ ôn. Mặc định không làm thay đổi lịch ôn."; Breadcrumbs Bộ thẻ › Luyện tập
form#tpSetup (GET to /Practice/{mode}; JS sets the action)
  fieldset legend visually-hidden "Chế độ"
    p.tp-group-title "Học bộ từ"
    div.tp-mode-grid--big (2 cols; 4 cols >=992): 4 radio cards (learn, test, match, flashcard)
       card = label.tct-card.tp-mode-card.tp-hue-{hue} > input[type=radio name=mode].visually-hidden + icon block (soft hue bg, 64px) + name (600) + hint (sm muted) + stat line badge (hue soft) + optional "Gợi ý cho bạn" badge (primary) ; checked: 2px hue border + soft bg + check mark; focus-visible ring on the card (:has(:focus-visible))
    p.tp-group-title "Luyện riêng từng kỹ năng"
    div.tp-mode-grid--small (1 col; 2 cols >=480; 4 cols >=992): 4 smaller cards (icon 44px + name + hint)
  div.tct-card.tp-setup-panel
    label "Bộ từ" + .form-select: "Tất cả bộ của tôi" (value "mine") + each set "{name} ({wordCount})"; disabled + hint when the user has no words: _StateMessage "Bạn chưa có từ nào để luyện." + link "Tạo bộ thẻ"
    fieldset (radio pills, .btn-check + .btn-outline-primary) legend = COUNT_UNIT.legend; labels "{n} {unit}" or "Cả bộ"
    [test] fieldset "Dạng câu": 4 checkbox pills (≥1 required; the last checked one cannot be unchecked; helper text "Chọn ít nhất một dạng câu.")
    [test] fieldset "Hỏi bằng": Trộn | Từ | Nghĩa
    fieldset "Nguồn từ": Tất cả | Đến hạn | Hay quên nhất | Mới thêm  (show the due count next to "Đến hạn" for a single set: "Đến hạn (12)")
    [mode != match] .form-check.form-switch "Tính vào ôn tập" + help "Kết quả luyện tập sẽ thay đổi lịch ôn của từ."
  div.tp-start-bar (sticky bottom; mobile above safe area)
    p aria-live=polite: "{Mode} · {Scope} · {n} {unit}"
    button.btn.btn-primary.btn-lg.w-100 "Bắt đầu luyện"
a.tct-card.tct-card--interactive.tp-hard-link -> /Practice/HardWords : "Từ hay quên" + "Xem và luyện riêng những từ bạn hay quên." + count badge (danger) + chevron
```

**Behavior** (`practice-hub.js`):
- **Defaults:** last choices from `tct:practice-setup:{userId}`, then `?set` preselect, then mode `learn`.
- Switching mode resets `n` to that mode's first allowed value when the current value is not allowed.
- **Stat lines** read `tct:set-stats:{setId}` and the match-best key when the scope is one set:
  - Học "Đã thuộc x%"
  - Kiểm tra "Lần trước x%" (`testPercent`, else legacy `quizPercent`)
  - Ghép thẻ "Kỷ lục {formatMs}" (best for the pair count that would be played, else legacy `matchMs`)
  - Lật thẻ none
- **"Gợi ý cho bạn"** = `suggestMode(stats)` among learn/test/match.
- **Submit** builds `/Practice/{mode}?set=…|scope=mine&n&source&[srs=1]&[types&ask]&from=hub` and saves the choices.

### 6.4 Session shell `Views/Practice/Session.cshtml`

```text
div.tct-theme.tp-page.tp-page--session.tp-hue-{hue} (min-height: calc(100dvh - header); flex column; max-width 720px centered; test mode 960px)
  h1.visually-hidden "{ModeLabel} · {ScopeName}"
  header.tp-bar (sticky top below site header): a.tct-btn-icon (fa-xmark, aria-label "Thoát", href ExitUrl) · progress (div role=progressbar aria-valuemin 0 aria-valuemax total aria-valuenow done aria-label "Tiến độ") · counter "3/20" (tabular-nums) · mode-specific slot (timer, round label, marks)
  main#tpStage (mode renders here)
  div#tpFeedback (sticky bottom; aria-live polite)
  div#tsToastHost.toast-container
  noscript: _StateMessage "Cần bật JavaScript để luyện tập."
```

**Exit confirmation:** when a session has answers and is unfinished, "Thoát" opens a Bootstrap modal "Thoát phiên luyện tập?" / "Kết quả các câu đã làm vẫn được giữ." / "Ở lại" | "Thoát". Learn shows no confirmation (its progress is saved).

### 6.5 Shared question parts (`practice-ui.js`)

- **`termLine(word)`**
  - Term (`lang="en"`, `--tct-text-3xl`, 700, `.tct-break`).
  - IPA (`.tct-phonetic`) + a speak button (`aria-label` "Phát âm {term}").
  - Emoji or image (96px, `alt=""`) when present and the question is not about the image.
- **`meaningLine(word)`**: definition (`--tct-text-2xl`, 700) + `meaningEn` (muted, when present).
- **`options(question, { onPick, outcome })`**
  - `div role="group" aria-label="Các đáp án"`; 1 column, 2 columns at ≥768px.
  - Each option is a `<button>` with an A–D badge (mobile) or a 1–4 key badge (desktop), min-height 56px, label `.tct-clamp-2`.
  - After answering: the correct option gets success styling + `fa-circle-check` + visually-hidden "Đáp án đúng"; a wrong pick gets danger styling + `fa-circle-xmark` + "Bạn chọn". Then all options are disabled.
- **`feedbackPanel(outcome, word, next)`** — sticky bottom panel:

| Outcome | Title | Background / text |
| --- | --- | --- |
| correct | "Đúng rồi!" | `--tct-success-soft` / `--tct-success-text` |
| close | "Gần đúng!" | `--tct-warning-soft` / `--tct-warning-text` |
| wrong | "Chưa đúng" | `--tct-danger-soft` / `--tct-danger-text` |

  - Shows the full answer card (term, IPA, definition, speak).
  - Button "Tiếp tục" (or "Xem kết quả" on the last item) receives focus. `Enter` continues.
- **`typingInput`**
  - `input` with `autocomplete=off autocapitalize=none autocorrect=off spellcheck=false lang=en`, height `--tct-control-h` × 1.25, label "Gõ từ tiếng Anh".
  - Buttons "Kiểm tra" (primary) and "Không nhớ" (outline; counts as wrong).
  - **Hint button** "Gợi ý" / "Gợi ý thêm" (`fa-lightbulb`): each press reveals one more letter via `hintMask`, up to `maxHints`. The line is `aria-live="polite"` with a visually-hidden prefix "Gợi ý: ".
  - After a wrong or close answer: `diffChars` list. "Bạn gõ" letters not in the answer are danger-underlined; "Đáp án" letters missing from the typed text are warning-highlighted. Each list carries a text label; color is never the only signal.
- **`keyHints(items)`**: visible at ≥992px only.
- **`summary({ results, durationMs, againUrl, exitUrl, extra })`**
  - Stars (1–3 by percent: ≥90 → 3, ≥70 → 2, else 1; `role="img"` `aria-label "{n} sao"`).
  - Heading "Xong phiên rồi!" (or "Chưa có câu nào"). `h2`; the page `h1` stays the hidden one.
  - KPIs (`.tct-card`): "Đúng {x}/{n}", "{p}%", "Thời gian {mm:ss}".
  - Section "Từ làm sai" with each term linked to `/Sets/Cards/{id}`, a definition and a speak button.
  - Buttons: "Luyện tiếp" (`AgainUrl`), "Học các từ sai" (`/Practice/learn?words=…&from=…`, only when there are misses), "Từ hay quên", "Xong" (`ExitUrl`).
- **`progressWriter`**: implements 4.3.

### 6.6 Drills (`practice-drill.js`)

- One question at a time, with a slide-in transition (disabled under reduced motion).
- **Question screens:**

| Kind | Content | Prompt line |
| --- | --- | --- |
| choice / term | `termLine` | "Nghĩa của từ này là gì?" |
| choice / meaning | visual + `meaningLine` | "Từ nào?" |
| choice / audio (listening) | big replay button (96px circle, `aria-label` "Nghe lại") + a "Nghe chậm" pill (rate 0.6) | "Bạn nghe thấy từ nào?" (auto-speaks once on show) |
| cloze | example sentence with a blank (`before` + 4em underline `span` + `after`, `lang=en`) and its Vietnamese line, then options | — |
| typing | visual + `meaningLine` + `typingInput` | — |

  On a cloze answer, the blank fills with the answer (success color).
- After answering → `feedbackPanel`. Results collected → `summary`.
- **Empty states:**
  - cloze with no usable examples → "Các từ này chưa có câu ví dụ để điền. Thêm câu ví dụ trong trang từ, hoặc chọn chế độ khác." + link back.

### 6.7 Flashcard (`practice-flashcard.js`)

- **Top bar slot:**
  - Chips "Chưa thuộc: {n}" (warning) and "Đã thuộc: {n}" (success). The label text is visually hidden below 576px; the number stays.
  - Toggle button "Từ trước" / "Nghĩa trước" (`aria-pressed`).
  - `.tct-btn-icon` "Xáo trộn thẻ" (`fa-shuffle`).
- **Card:**
  - A `<button class="tp-flashcard" aria-label="Lật thẻ">` with two faces (`aria-hidden` on the hidden face; `aria-live` announces the shown face text).
  - Front: term (+ IPA, image/emoji). Back: definition + meaningEn + first example.
  - 3D flip via a `transform` transition (an instant swap under reduced motion).
- **Swipe** (pointer events):
  - The card follows the finger horizontally with a slight tilt.
  - "Chưa thuộc" fades in on the left, "Đã thuộc" on the right.
  - Past 100px it marks; otherwise it snaps back. A tap still flips.
- **Controls under the card:**
  - "Chưa thuộc" (outline-warning) and "Đã thuộc" (outline-success);
  - previous / next `.tct-btn-icon`;
  - a speak button.
- **Keys:** `Space`/`Enter`/`↑`/`↓` flip (Enter does not flip while a button is focused), `←/→` move, `1` chưa thuộc, `2` đã thuộc, `R` speak.
- **Marking** advances to the next card and writes progress per 4.3.
- **End screen:**
  - "Hết thẻ rồi!" and "Bạn đã thuộc hết cả bộ thẻ này." / "Còn {n} thẻ cần học thêm."
  - Counts, plus the list of not-yet-known cards.
  - Buttons "Học tiếp {n} thẻ chưa thuộc" (restart with those only) and "Học lại cả bộ"; links "Học bằng chế độ Học" (`/Practice/learn?words=…`) and "Xong".

### 6.8 Match (`practice-match.js`)

- **Start screen** (the clock does not run yet):
  - Title "Ghép thẻ"; "Ghép từ với nghĩa nhanh nhất có thể. Ghép sai bị cộng 1 giây."
  - The current best (`fa-trophy` "Kỷ lục: {formatMs}") or "Chưa có kỷ lục".
  - Big button "Bắt đầu".
- **Top bar:** timer (`fa-stopwatch`, updated every 100 ms via `requestAnimationFrame`, `aria-hidden`; a visually-hidden live region announces only matches and misses), "Bảng 1/2".
- **Board:**
  - Grid of tile `<button>`s, 3 columns (<768px) or 4 columns, min-height 80px, `.tct-card` look, label `.tct-clamp-2` (~60 characters visible), term tiles `lang="en"`.
  - Select: primary border + `aria-pressed="true"`. Tapping the selected tile again unselects it.
- **Second pick:**
  - **Pair:** both tiles pop then fade, but keep their grid space (`visibility:hidden`, `disabled`); live region "Đúng".
  - **Miss:** both tiles get a danger border + wiggle for 400 ms, +1000 ms penalty, a floating "+1 giây" badge; live region "Sai, cộng 1 giây".
- **Flow:** board cleared → next board, the clock keeps running. All boards cleared → result.
- **Result:**
  - Time, number of misses.
  - "Kỷ lục mới!" (`.tct-badge--success` + a star pop, no confetti library) when the time beats the stored best for `{scopeKey}:{pairs}`; otherwise "Kỷ lục: {best}".
  - Buttons "Chơi lại" (`AgainUrl`, reshuffled), "Thử chế độ Học", "Xong".
- **Storage:** match-best per 5.9. Also merge `matchMs` into `tct:set-stats:{setId}` for a single-set scope.
- Fewer than 2 usable pairs → empty state "Cần ít nhất 2 từ để ghép thẻ."
- No speech, no progress writes.

### 6.9 Test (`practice-test.js`)

**Layout:** one scrolling page (max-width 960px). On ≥1200px, a right-hand jump list sits beside it.
- **Each question** is a `.tct-card` `section` with `aria-labelledby` and the header "Câu {i}/{n}" + a type badge (`TEST_TYPE_LABEL`):
  - **choice:** prompt (term or meaning) + 4 option buttons. Selectable and changeable before submit (`aria-pressed`, no right/wrong colors yet).
  - **truefalse:** "{term}" — "{shown}" with "Nghĩa này đúng không?" (or the meaning first + "Từ này đúng không?"), and two buttons "Đúng" / "Sai".
  - **typing:** meaning + an input (same attributes as 6.5; no hint button in tests).
  - **cloze:** sentence with a blank + options.
- **Jump list:** a grid of numbered buttons (answered = primary-soft); clicking scrolls to and focuses that question.
- **Sticky bottom bar:** "Đã làm {a}/{n}" + "Nộp bài" (btn-primary). With unanswered items, a modal asks "Còn {k} câu chưa làm. Vẫn nộp?" with "Làm tiếp" | "Nộp bài".
- **Draft:** answers saved to `tct:test-draft:{userId}:{testId}` in sessionStorage on every change and restored on reload. Store `{ seed, answers }` and build the questions with `mulberry32(seed)`, so the same targets from the server rebuild the same test.

**On submit:**
- `gradeTest`; write progress per 4.3.
- Merge `testPercent` (single-set scope); clear the draft.
- **Result view:**
  - A progress ring (CSS `conic-gradient` with `--p`; `role="img"` `aria-label "{p}%"`), "Đúng {c}/{n}".
  - Remark: ≥90 "Xuất sắc!", ≥70 "Khá lắm!", else "Cần ôn thêm một chút."
  - Filter toggle "Chỉ câu sai" (`aria-pressed`).
  - The question list: wrong items expanded with "Bạn trả lời: …" (danger, or "Bỏ trống") and "Đáp án: …" (success) + a speak button; correct items collapsed (`<details>`); "close" items marked "Gần đúng" (warning).
- **Buttons:**
  - "Làm lại câu sai": a client-side `buildTest` over the missed words with the same types/ask, pool = all words in this test, new seed. Hidden when everything was correct.
  - "Học các từ sai" (`/Practice/learn?words=…`).
  - "Làm bài mới" (`AgainUrl`).
  - "Xong".

### 6.10 Learn (`practice-learn.js`)

- **Start:** load `restoreLearn(saved, targetIds, targetIds, n)`.
  - A saved state with progress → resume screen: "Tiếp tục học?", "Đã thuộc {m}/{total}", buttons "Học tiếp" | "Bắt đầu lại".
  - Otherwise `initLearn(targetIds)`.
- **Stage list** (replaces the reference product's map; no mascot):
  - A vertical list of stages (`stageCount`). Done stages show a check + 1–3 stars; the current stage shows `fa-play` + "Đang học"; future stages are locked (`fa-lock`, disabled).
  - Under it, a card for the stage to play: "Chặng {k}", "{fresh} từ mới · {review} từ ôn lại" (`previewRound`) and its word chips (new words badge "Mới"; others show `masteryDots` as 3 small dots with a visually-hidden "{d}/3").
  - Button "Bắt đầu chặng {k}".
- **Round:**
  - Bar slot: "Vòng {round}" + a 3-segment progress (mastered success / learning warning / not started muted), `aria-label "Đã thuộc {m}/{total}, đang học {l}"`.
  - The level label above each question: "Từ mới" (warning badge) / "Học lại" (danger badge, missed before) / "Ôn lại — gõ từ" (primary badge).
  - Questions from `questionFor`.
  - Before the first question of a never-seen word, show a short intro card "Từ mới" (term, IPA, definition, first example, speak) with "Đã hiểu" (Enter), at most once per word per visit.
  - Answer → `feedbackPanel`. Wrong answers show the full answer card. Write progress per 4.3. Save the state after each answer.
- **Round end:**
  - `recordRound(starsFor(right, total))`.
  - Screen "Hết vòng {k}": stars, three bars (Chưa học / Đang học / Đã thuộc with counts), the round's words (`fa-circle-check` "Đã thuộc" or "Đang học").
  - Button "Tiếp tục" → stage list.
- **Complete** (`learnComplete`):
  - "Bạn đã thuộc hết {n} từ!" + a star pop.
  - Merge `learnPercent: 100` (single-set scope; otherwise only the learn key).
  - Buttons "Học lại từ đầu" (clear state), "Làm bài kiểm tra" (`TestUrl` with the same scope/words), "Xong".
  - While incomplete, after each round merge `learnPercent = round(mastered/total*100)`.

### 6.11 Empty and error states (session)

- No words in scope → `_StateMessage(Empty)` "Chưa có từ nào để luyện." + "Thêm từ vào bộ thẻ trước nhé." + action link to the set (or `/Sets`).
- **Source-specific:**
  - `due` with nothing due → "Không có từ nào đến hạn ôn." + link "Luyện tất cả" (same query with `source=all`).
  - `hard` with none → "Bạn chưa có từ nào hay quên."
- Fewer than needed for match → 6.8. Cloze without examples → 6.6.
- Corrupt device storage → start fresh silently (log `console.warn`).

### 6.12 Hard words `/Practice/HardWords`

```text
_PageHeader: "Từ hay quên"; Description "Những từ bạn trả lời sai nhiều nhất khi ôn và luyện tập."; Breadcrumbs Luyện tập › Từ hay quên
optional set filter (.form-select "Tất cả bộ" + viewable sets; GET)
empty -> _StateMessage "Bạn chưa có từ nào hay quên." + "Cứ ôn tập đều đặn, từ nào sai nhiều sẽ hiện ở đây."
.tct-table-wrap > table.tct-table: Từ (link /Sets/Cards/{id}) · Phiên âm · Nghĩa · Bộ · "Sai {n} lần" (danger badge); mobile stacked rows
actions (sticky on mobile): btn-primary "Học các từ này" (/Practice/learn?words=<first 50 ids>&from=hard) · btn-outline-primary "Kiểm tra các từ này" (/Practice/test?words=…&from=hard)
```

---

## 7. Integration with existing pages (Trọng's files only)

1. **`Views/Set/Detail.cshtml` "Học bộ này" strip**: replace the four tiles.

| Key | Title | Icon | Hue | Href | Note (server default) |
| --- | --- | --- | --- | --- | --- |
| learn | Học | `fa-graduation-cap` | success | `/Practice/learn?set={id}&n=20&from=set` | "Đã thuộc {MasteredPercent}%" |
| test | Kiểm tra | `fa-clipboard-check` | primary | `/Practice/test?set={id}&n={min(WordCount,20) snapped to an allowed value}&from=set` | "{n} câu" |
| match | Ghép thẻ | `fa-shuffle` | warning | `/Practice/match?set={id}&n=6&from=set` | "Chưa có kỷ lục" |
| flashcard | Lật thẻ | `fa-clone` | info | `/Practice/flashcard?set={id}&n=0&from=set` | "{WordCount} thẻ" |

   - Add a link under the strip: "Luyện riêng từng kỹ năng" → `/Practice?set={id}`.
   - Keep the hero "Ôn bộ này" → `/Home/Study/{id}?mode=due` (P1).
2. **`wwwroot/js/set-detail.js`**: read the new stats.
   - learn: `learnPercent` → "Đã thuộc x%".
   - test: `testPercent` ?? `quizPercent` → "Lần trước x%".
   - match: the best of `tct:match-best:{userId}:set:{id}:6` and `matchMs` → "Kỷ lục …".
   - Suggestion: due > 0 keeps pointing at the hero (no badge change); otherwise `suggestMode`. Expose `userId` in the existing config JSON for the key.
3. **`Views/Set/_SetActionsMenu.cshtml`**: replace "Luyện trắc nghiệm" (`/Home/QuizMode`) with "Luyện tập" (`fa-dumbbell`) → `/Practice?set={id}`.
4. **`Views/Set/Index.cshtml`**: add a secondary header action "Luyện tập" → `/Practice` (`PageHeaderAction`, Secondary).
5. **`Views/Set/CardDetail.cshtml`**: add "Học từ này" → `/Practice/learn?words={cardId}&from=set` and "Gõ từ này" → `/Practice/typing?words={cardId}`. Visible to viewers.
6. **If D1 = CÓ:** in `_Layout.cshtml`, add one sidebar item "Luyện tập" (`fa-dumbbell`, `aria-current="@AriaCurrent("Practice")"`) right after "Bộ thẻ của tôi", copying its markup pattern. Nothing else in the layout changes.
7. **`docs/design-system/team-ui-workflow.md` section 6 tracker:** add a row "Luyện tập" (`/Practice*`, `practice.css`, `--tct-*`, "Đã dùng token", Huỳnh Phú Trọng). This was granted for the owner's screens under the previous D3 approval.

---

## 8. Phases

Each phase:
1. Load only the needed skills (`tct-superpowers`, `tct-engineering`, `tct-vocabulary-feature`, `tct-api-endpoint` for Q1, `tct-ui-component` for Q3+, `tct-ui-review` at Q10, `tct-commit-message` when committing).
2. Implement.
3. `dotnet build TCTEnglish/TCTEnglish.csproj`.
4. Run that phase's tests.
5. Review the diff against AGENTS.md.
6. Tick the checklist and commit locally (authority in the prompt).

| Phase | Work | Tests / acceptance |
| --- | --- | --- |
| Q0 | Confirm branch, HEAD and tree; create `feature/practice-modes` from `feature/personal-sets-redesign`. Build. Baseline: full `dotnet test -c Release` + all `scripts/tests/*.cjs`. Read the legacy study pages, `set-detail.js`, `personal-sets-common.js`, `time-tracker.js`, `LearningApiController.track-time`. Save this plan. | Baseline counts recorded in section 11 |
| Q1 | `PracticeModes.cs`, view models, service partial + interface, `PracticeController`, empty views rendering config JSON | `PracticeIntegrationTests` (4.4) green |
| Q2 | `practice-core.js` (section 5) | `test_practice_core.cjs` green; no DOM references in the file |
| Q3 | `practice.css`, `practice-ui.js`, `Session.cshtml` shell, empty states, exit modal, progress writer | Shell renders for every mode; empty states reachable; antiforgery and time-tracker present |
| Q4 | Drills + summary (6.6, 6.5) | Browser: each drill completes; keyboard 1–4/R/Enter; listening auto-speaks once |
| Q5 | Flashcard (6.7) | Swipe/keys/marks/end screen; reduced-motion swap |
| Q6 | Match (6.8) | Penalty, boards, record persistence, live region |
| Q7 | Test (6.9) | Grading, draft restore after reload, retry missed, result filter |
| Q8 | Learn (6.10) | Rounds, re-queue on miss, resume, stars, completion links |
| Q9 | Hub (6.3), Hard words (6.12), integration (section 7), D1 | Hub defaults/suggestion; set detail strip numbers; menu/card links |
| Q10 | Full verification (section 9), docs, final report | Definition of done |

---

## 9. Verification

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/*.cjs
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext   # must report no changes (no migration in this plan)
git diff --check
```

- Test counts must not drop below the Q0 baseline. Report skipped tests as skipped.
- `scripts/**` changes may trigger tooling checks; run the ones AGENTS.md lists (e.g. `uv run --no-project python -m unittest discover -s scripts/tests -v` when `uv` exists).

**Browser review** (`tct-ui-review`, `team-ui-workflow.md` section 7):
- Start: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` on a free port. Synthetic accounts from `.appdata/local/access.json` (never print passwords). Screenshots only in ignored `.tmp/practice/`.
- **Every mode at 1440 / 768 / 390 / 320 + 200% zoom:** keyboard-only run-through, visible focus, contrast (scripted 4.5:1 check as in the previous pass), long Vietnamese definitions, multi-word terms ("look forward to"), no horizontal scroll, reduced-motion emulation.
- **Server writes:** with "Tính vào ôn tập" on, answering changes the card's status on `/Sets/{id}`. With it off, nothing changes.
- **Regression:** `/Home/Study/{id}`, `/Home/Study/{id}?mode=due`, `QuizMode`, `WriteMode`, `MatchingMode` still work, and an AI chat citation link still opens.
- Anything not observed is reported as not observed.

**Docs:**
- `docs/project-structure.md` (`PracticeController`, `/Practice` routes).
- `.ai/context/known-issues.md`: device-local state limitation.
- The tracker row.
- Section 11 below.

---

## 10. Definition of done

- [x] All routes in 4.1 work with P10 access (404 for non-viewable sets); payloads never contain personal notes or other users' data.
- [x] No migration; no `Program.cs`, appsettings, `.csproj`, shared CSS/partials, `LearningApiController`, Goals or AI changes. `_Layout.cshtml` only per D1.
- [x] The legacy study pages and their tests are unchanged and green; chat citation tests are green.
- [x] `practice-core.js` is pure and fully covered by `test_practice_core.cjs`; the C# and JS count tables match.
- [x] Progress writes happen only with the switch on, through `UpdateCardProgress`; Match never writes.
- [x] Every mode has loading / empty / error / success states, keyboard support, labelled controls, a live region for feedback, and reduced-motion handling. (No client "loading" state: every session embeds its words, the only request is the optional progress write.)
- [x] CSS is tokens-only under `.tp-page`; Font Awesome 6.0.0 only; no new packages or CDNs.
- [x] No mascot, Chinese-language, XP, confetti-library or AI remnants.
- [x] Set detail strip, actions menu, set list and card detail link to the new modes; the hero "Ôn bộ này" is unchanged.
- [x] The Vietnamese report separates local / CI / staging / production.

## 11. Execution notes (agent filled in)

- Branch / HEAD at Q0: `feature/practice-modes` created from `feature/personal-sets-redesign` at `a9964a6` (5 local commits over origin/master `eed4e2c`, not pushed). The working tree had the unrelated `TCTEnglish/appsettings.Development.json` change and an untracked `Claude outputs/` folder; neither was staged or edited. No pull, rebase or reset.
- D1 as received: blank in both the plan and the prompt, so **KHÔNG**. `_Layout.cshtml` is untouched; entry points are the set detail strip and menu, the set list header and card detail. A test asserts that the dashboard page has no `/Practice` link.
- Baseline counts (dotnet / node): `dotnet test -c Release` 1272 passed / 22 skipped / 0 failed; `node --test scripts/tests/*.cjs` 93 passed.
- Legacy pages read: not re-read line by line in this session. Behaviour follows plan 0.2 and the previous session's notes; `StudyController.UpdateCardProgress` (antiforgery header, `CanViewSetAsync`) was re-read. They were verified instead by the unchanged legacy test suites and by opening `/Home/Study/5`, `?mode=due`, `QuizMode`, `WriteMode` and `MatchingMode` in the browser (no console errors). `time-tracker.js` and `track-time` were not re-read; the `Flashcard` / `Quiz` feature values come from the plan.
- Pool top-up behaviour observed: a 14-word set asked for cloze n=10 returns all 14 targets and an empty own-set pool, so the pool is topped up from other viewable sets (synthetic words from another set appeared as distractors). Covered by `TinyPool_IsToppedUpFromOtherViewableSets`.
- Deviations and why:
  - `PracticeQuery.Words` is the normalized CSV string (model binding) with a `WordIds` list property; `PracticeSessionViewModel` also carries `AllUrl` (the "Luyện tất cả" link of an empty source) and `Hue`.
  - `ScopeKey` for `words=` sessions is `words:{setId|mine}:{hash}` so a partial retry never overwrites the Learn state of the whole set. Set stats (`learnPercent`, `testPercent`, `matchMs`) are merged only for single-set sessions without `words`.
  - The test draft is keyed by a hash of the targets and options (not of the question ids), because the key must be known before the seeded rebuild. Choice and true/false question ids include the prompt side, so a changed "Hỏi bằng" never reuses answers.
  - Drill and flashcard orders are shuffled in the browser (targets arrive in source order).
  - In Match a second tile of the same side counts as a miss (plan 6.8 lists only pair or miss).
  - The three fixed bottom bars (feedback panel, test submit bar, hub start bar) sit above the AI chat launcher at every width (offset 5.5rem), not only on mobile, because the launcher overlapped them at 992-1300px.
  - The hard-words set filter uses a "Lọc" button instead of an inline `onchange`.
  - `set-detail.js` repeats the small suggestion rule instead of loading `practice-core.js` on the detail page.
  - A `tp-stage` class clash between the shell and the Learn stage list was renamed to `tp-step`.
  - "Làm lại câu sai" in Test is not persisted as a draft.
- Browser coverage (headless Chrome over CDP, local web on a free port, synthetic Premium account, screenshots only in ignored `.tmp/practice/`):
  - All 8 modes at 1440 / 768 / 390 / 320 and 720 px at 2x (200% zoom equivalent): no horizontal overflow, 0 console errors, 0 failures from the scripted contrast check (4.5:1, 3:1 for large text) in a prepared state per mode (answered, flipped, started, first question).
  - Keyboard: quiz, typing, listening, cloze and flashcard were played to the end with keys. Learn (start the stage, intro, answer with 1, Enter to continue), Match (Tab to a tile, Enter to select and pair) and Test (Tab reaches "Nộp bài" in 71 stops, Space picks an option, Enter opens the confirmation, Escape closes it) were walked with real key events. The first 16 Tab stops of every mode show a focus ring.
  - Reduced motion: slide-in, tile wiggle and pop animations resolve to none; the flip is an instant swap.
  - Server writes: flashcard with the switch off left `learned` at 0 and with it on it became 1; Learn with it on moved the set to 11 learned; Test with it on moved 8 wrong answers to learning; Match with `srs=1` still reports `countsToSrs=false` and writes nothing.
  - Also checked: hub defaults restored from the saved setup, suggestion badge, test types keep at least one, Match hides the switch, hard-words stacked rows at 390 px, set detail strip / menu / list / card links, legacy pages opening.
- Not verified: screen readers; real touch swipe (mouse-drag emulation only); audible speech and the listening auto-play; Firefox and Safari; a Standard or Admin visual pass of the practice pages (integration tests only); class or folder-member scope in a browser; an actual AI chat citation click (no key; only the `/Home/Study/{id}` target was opened); the empty-words hub state visually (covered by a render test). `SharedDataMariaDbTests`, CI, staging and production were not run or touched; no schema changed.
- Final counts: `dotnet test -c Release` 1287 passed / 22 skipped / 0 failed (baseline 1272 / 22); `node --test scripts/tests/*.cjs` 115 passed (baseline 93); `has-pending-model-changes`: no changes; `scripts/agent_tooling.py`: verified; tooling unit tests (pinned uv): 139 run, OK, 1 skipped; `git diff --check` flags only the pre-existing blank line at the end of `appsettings.Development.json`.
