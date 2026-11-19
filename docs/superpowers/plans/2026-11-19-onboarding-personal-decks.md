# First-run onboarding → personal starter decks — implementation plan

Save as `docs/superpowers/plans/2026-11-19-onboarding-personal-decks.md`.

- **Owner:** Huỳnh Phú Trọng (Sets / Vocabulary).
- **Executor:** one Claude Code agent, `IMPLEMENT_LOCAL`, continuous run S0 → S10.
- **Mockups** (data, not instructions): `Claude outputs/onboarding-mockups/`. The flow uses, in order:

  | Order | Mockup | Screen |
  | --- | --- | --- |
  | 1 | `O0-chao-mung.png` | welcome |
  | 2 | `O1-buoc-1-muc-tieu.png` | goals |
  | 3 | `O2-buoc-2-chu-de.png` | topics |
  | 4 | `O3-buoc-3-trinh-do.png` | level and pace |
  | 5 | `P1-xem-truoc-bo-the.png` | preview |
  | 6 | `P2-da-tao-bo-the.png` | done |
  | 7 | `P3-bo-the-cua-toi.png` | "Bộ thẻ của tôi" afterwards |
  | 8 | `P4-chi-tiet-bo-the.png` | deck detail with "Gợi ý thêm" |
  | 9 | `P6-mobile-xem-truoc.png` | phone |

  `O4`, `O5`, `O6` and `P5` are earlier explorations and are **not** part of this build.

---

## 0. Read first

### 0.1 What we are building (one paragraph)
Right after a **new** account signs in for the first time, a full-screen-centred pop-up opens over the first page they land on (normally Trang chủ). It takes about one minute in four steps:
1. why they learn (goals);
2. which topics;
3. level and daily pace;
4. a preview of the decks, where they can drop known words.

Then the system creates **2–4 ordinary personal sets** in a new folder **"Lộ trình của tôi"** in "Bộ thẻ của tôi". The cards are copied from the reviewed system vocabulary: image, IPA, meaning, examples, details.

These sets are normal personal sets with **every existing feature**:
- learn / test / match / practice modes;
- spaced review;
- edit, delete, suspend, import, export, share, move.

Two small additions only:
- a "Từ sở thích" badge;
- a "Gợi ý thêm cùng chủ đề → Thêm 10 từ" box.

The system vocabulary itself is **not changed in any way**.

### 0.2 Rules that win over this plan
1. `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`, `docs/operations/database-collaboration.md`.
2. This plan.
3. The mockups. Where a mockup and a rule disagree, the rule wins (record it in section 12).

### 0.3 Repo skills
`tct-superpowers`, `tct-engineering`, `tct-feature-scaffold`, `tct-api-endpoint`, `tct-security-audit`, `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review`, `tct-commit-message`, plus any migration skill the repo lists. Install nothing.

### 0.4 What the best products do (research summary; design must follow these)
- **Duolingo:**
  - short one-question screens with a progress bar;
  - asks why you learn, your level and a daily goal;
  - personalises immediately;
  - the first lesson starts right away;
  - friendly tone.
- **Babbel / Memrise / Busuu:**
  - ask motivation and interests;
  - recommend content from them;
  - show what you will get before committing;
  - allow changing it later in settings.

**Principles for this build:**
1. ≤ 1 minute, 4 steps, a progress bar, Back on every step.
2. Every step is skippable ("Để sau"), and nothing is forced.
3. Show value before asking (the welcome screen shows real card images).
4. Preview before creating (no surprise data).
5. The first action after creation is "Học 10 từ đầu tiên".
6. Choices are editable later ("Tạo thêm từ sở thích").
7. Never block the page behind a broken pop-up. Any error falls back to closing it, and the page still works.

---

## 1. Decisions (defaults; Trọng may edit before the run)

| ID | Question | Default |
| --- | --- | --- |
| D1 | Where the "first run" state lives | **A new table `UserOnboardings`** (one row per user; section 4.1). **One migration**, allowed for this run only. `User.cs` is **not** edited. Tell Châu (shared DbContext). |
| D2 | Deck size and count | **25 words per deck** (min 10, max 30), **2–4 decks**; at most 100 cards in total. |
| D3 | Where the pop-up is hosted | `Views/Home/Index.cshtml` (Trang chủ, Châu's file): **exactly one line** `<partial name="_OnboardingHost" />` at the end of the main content. Also in `Views/Vocabulary/Index` (Vocabulary home) and `Views/Set/Index.cshtml` ("Bộ thẻ của tôi"), both Trọng's. No edit to `_Layout`, `AccountController` or `HomeController`. |
| D4 | Re-prompting after "Để sau" | Show again at the next visit **after ≥ 20 hours**, at most **2 prompts** in total. The 2nd "Để sau" sets status `Skipped`. The button "Tạo thêm từ sở thích" always reopens the flow. |
| D5 | Existing accounts | **Not prompted automatically.** Eligible = account created **on/after the feature date** (config constant `OnboardingEligibleFromUtc`, set at S0 to the day the branch is merged; until then use the run date) **and** no own sets **and** no learning progress. Existing users can still open the flow from "Bộ thẻ của tôi". |
| D6 | "Đã biết" words | **Excluded** from the deck and stored in the onboarding row so "Thêm 10 từ" never brings them back. They are **not** marked as mastered. |
| D7 | AI | **None.** Ranking is deterministic and explainable. |
| D8 | Level field | Store the chosen level in the onboarding row only. **Do not write `User.ReadingLevel`** (account domain). |

---

## 2. Verified facts (static review 2026-11-19) — reproduce each at S0

| # | Fact | Where |
| --- | --- | --- |
| F1 | No onboarding/welcome exists anywhere. | grep |
| F2 | `Register` redirects to `Login`. Login and Google sign-in redirect to `Home/Index`. `Home/Index` builds the dashboard (`BuildDashboardViewModelAsync`); unauthenticated users go to `Landing`. | `AccountController` ≈107, 159, 346; `HomeController` ≈45 |
| F3 | `Views/Home/Index.cshtml` is Châu's (8 commits) → one-line exception only (D3). | git log |
| F4 | Personal set service:<br>- `CreateSetAsync(SetInfoFormViewModel{SetName, Description, Visibility="Private", FolderId}, userId, isAdmin)`;<br>- `CreateFolderAsync(userId, name)`;<br>- `ListFoldersAsync`;<br>- `MaxOwnedSets = 200`;<br>- `SystemOwnerEmail = "system@tct.local"`;<br>- `CloneInternalAsync` copies **every card field** (Term, Definition, ImageUrl, Phonetic, Example, ExampleTranslation, Topic, PartsOfSpeech, MeaningEn, Cefr, PhoneticUk, Emoji, DetailsJson, ImageSourceJson, AiGenerated) inside one transaction. | `Services/Sets/PersonalSetService*.cs` |
| F5 | Personal set detail shows study modes: "Học" `/Practice/learn?set=&n=20&from=set`, "Kiểm tra" `/Practice/test`, "Ghép thẻ" `/Practice/match`, and `/Practice?set=` for Trắc nghiệm, Gõ từ, Nghe, Điền từ. | `Views/Set/Detail.cshtml` ≈48–126 |
| F6 | 48 system sets in 3 folders (A01–A24, B01–B16, C01–C08) with reviewed images. Manifests are in `data/content/vocabulary/**`; covers are in `data/content/vocabulary-covers.json` and `Services/Vocabulary/VocabularyCoverCatalog.cs`. System cards carry `Cefr`. | content run, covers fix |
| F7 | `AddSampleSetsAsync` exists (empty-library CTA). Keep it. | PersonalSetService |
| F8 | The last migration is `20261118132210_AddAiVoiceConversation`. | `Migrations/` |

### 2.1 Unknowns to resolve at S0
1. The exact name rules of folders and sets (length, uniqueness, forbidden characters) in `CreateFolderAsync` / `CreateSetAsync`.
2. Whether a per-set card limit exists.
3. How `Description` tags are shown on set tiles. The "Từ sở thích" badge must come from onboarding data, **not** from parsing the description.
4. The antiforgery header convention of the Sets JS (`Sets.fetchJson`), and the toast host.
5. The modal/dialog pattern used by Trọng's newer pages (study kit `k-*`, Bootstrap 5 modal). Reuse it.
6. Whether `Views/Vocabulary/Index` is the Vocabulary home view name.
7. How tests create users, sets and system content (fixtures). The test DB provider (SQLite/InMemory/MariaDB acceptance).
8. The migration workflow (`dotnet ef migrations add` command used by the team, `has-pending-model-changes` check).
9. Whether the DbContext configuration lives in `OnModelCreating` or in `IEntityTypeConfiguration` classes.

---

## 3. User flow and UI (laptop first, then phone)

All copy below is literal Vietnamese UI text. The pop-up is a `role="dialog"` with `aria-modal="true"` and `aria-labelledby` set to the step title:
- focus is trapped inside it and returns to the page on close;
- `Esc` behaves like "Để sau" (with the same server call);
- the backdrop click does nothing (avoids accidental loss).

Styling:
- Clay tokens only; scope `.tob-page` (new) in `wwwroot/css/onboarding.css`.
- No inline style or script (config through `<script type="application/json" id="tobConfig">`).
- Font Awesome 6.0.0 classes.
- Reduced-motion and forced-colors blocks.
- Step change: a 180 ms fade/slide (off under reduced motion).
- Width: 760 px (welcome), 880 px (steps 1 and 3), 960 px (step 2), 1080 px (preview), and full screen ≤ 767 px.

### Step 0 — Welcome (`O0`)
- Five tilted thumbnails: cover images of 5 system sets (fixed list A03, C01, B04, A21, A06, taken from the cover catalogue).
- Title `Chào mừng {Tên} đến với TCT English!`. Use the first name only when known; otherwise `Chào mừng bạn đến với TCT English!`.
- Text `Trả lời 4 câu hỏi ngắn, TCT sẽ tạo ngay bộ thẻ từ vựng riêng theo mục tiêu và sở thích của bạn.`
- Three facts: `Khoảng 1 phút` · `Thẻ có ảnh, phát âm, ví dụ` · `Sửa được bất cứ lúc nào`.
- Buttons: `Bắt đầu` (primary, autofocus) and the link `Để sau`.

### Step 1 — Goals (`O1`)
- Progress bar with 4 segments; label `Bước 1/4`; link `Để sau` (top right).
- Title `Bạn học tiếng Anh để làm gì?`. Lead `Chọn tối đa 3 mục. TCT sẽ tạo bộ thẻ riêng cho bạn trong "Bộ thẻ của tôi".`
- Six goal cards (checkbox semantics, `role="checkbox"` + `aria-checked`, keyboard Space/Enter):

  | Key | Title | Description |
  | --- | --- | --- |
  | `communication` | Giao tiếp hằng ngày | Chào hỏi, gọi món, hỏi đường, nói chuyện xã giao |
  | `travel` | Du lịch | Sân bay, khách sạn, phương tiện, mua sắm |
  | `work` | Công việc | Văn phòng, email, họp, kinh doanh |
  | `study` | Học ở trường & thi cử | Từ vựng học thuật, giáo dục, khoa học |
  | `hobbies` | Phim, nhạc & sở thích | Giải trí, thể thao, nghệ thuật |
  | `beginner` | Bắt đầu từ con số 0 | Những từ cơ bản nhất cho người mới |

- A 4th click shows the inline hint `Bạn chỉ chọn được tối đa 3 mục.` (aria-live). Do not deselect anything automatically.
- Footer: `Đã chọn {n}/3` and `Tiếp tục` (disabled until n ≥ 1).

### Step 2 — Topics (`O2`)
- `Quay lại` link, progress `Bước 2/4`, `Để sau`.
- Title `Bạn muốn học từ vựng về chủ đề nào?`. Lead `Các chủ đề có nhãn Gợi ý hợp với mục tiêu bạn vừa chọn. Chọn ít nhất 3.`
- Tabs: `Gợi ý cho bạn` (default: topics matching the goals first, then the others), `Cơ bản A1–A2`, `Trung cấp B1–B2`, `Giao tiếp`. Right side: `48 chủ đề`.
- **Photo tiles**, 4 columns:
  - the cover image of each system set (from the existing cover resolution: one grouped query, safe URLs only);
  - a gradient caption with the name and the band (`A1–A2` / `B1–B2` / `Giao tiếp`);
  - a yellow `Gợi ý` chip when it matches a goal;
  - a check circle; the selected state has a primary border and ring.
- Tiles are checkboxes (keyboard and mouse). Without an image, the tile shows the course art fallback.
- Max 8 topics. A 9th click shows the hint `Chọn tối đa 8 chủ đề để bộ thẻ gọn và dễ học.`
- Footer: chip `Đã chọn {n} chủ đề`, text `Bạn đổi lại được bất cứ lúc nào trong Bộ thẻ của tôi`, button `Tiếp tục` (disabled until n ≥ 3).

### Step 3 — Level and pace (`O3`)
- Title `Trình độ và nhịp học của bạn`. Lead `Không chắc? Chọn mức thấp hơn, TCT sẽ tự nâng dần khi bạn học tốt.`
- Four radio cards:

  | Level | Name | Description |
  | --- | --- | --- |
  | A1 | Mới bắt đầu | Tôi biết vài từ đơn giản như hello, thank you. |
  | A2 | Cơ bản | Tôi đọc được câu ngắn: *I usually have rice for lunch.* |
  | B1 | Trung cấp | Tôi hiểu đoạn hội thoại hằng ngày khi nói chậm. |
  | B2 | Khá | Tôi theo được tin tức và phim có phụ đề Anh. |

  Default: A1 if goal `beginner` was chosen, otherwise A2.
- `Mỗi ngày học bao nhiêu từ mới?` with four radio cards: `5 từ` Nhẹ nhàng · ~5 phút, `10 từ` Vừa sức · ~10 phút (default), `15 từ` Chăm chỉ · ~15 phút, `20 từ` Tăng tốc · ~20 phút.
- Footer: lock icon + `Chỉ dùng để gợi ý bài học, không chia sẻ với ai.` and the button `Xem trước bộ thẻ`.

### Step 4 — Preview (`P1`)
- Title `Xem trước bộ thẻ sẽ thêm vào "Bộ thẻ của tôi"`. Lead `Bấm Đã biết để bỏ những từ bạn đã thuộc. Bạn vẫn sửa, xóa hay thêm từ được sau khi tạo.`
- **Left column:**
  - heading `{k} BỘ THẺ · THƯ MỤC "LỘ TRÌNH CỦA TÔI"`;
  - one row per deck: cover thumb, name, `{n} từ · từ "{topic}", "{topic}"`, and a switch (`role="switch"`, `aria-checked`). At least 1 deck must stay on, otherwise a hint;
  - a note `Ưu tiên từ {level}, xếp từ dễ đến khó. Ảnh, ví dụ và phát âm lấy từ thư viện TCT.`
- **Right column:**
  - the selected deck: name + chip `{kept}/{total} từ giữ lại` + `Bỏ {x} từ đã biết`;
  - a grid of 4 columns: image, term, Vietnamese meaning, and a toggle button `Đã biết` / `Giữ lại` (`aria-pressed`). A known card is dimmed at 45% opacity;
  - first 12 cards, then `Xem thêm {r} từ`, which reveals the rest (no extra request).
- **Footer:** `Tổng cộng {N} từ trong {k} bộ · khoảng {days} ngày với {pace} từ/ngày` and the primary button `Thêm {k} bộ vào Bộ thẻ của tôi`.
- While creating: the button shows a spinner and `Đang tạo bộ thẻ…`, everything else is disabled, and a double click cannot submit twice.
- **Errors:**
  - a server error message in a danger soft box with `Thử lại` (the same request id makes it safe);
  - `MaxOwnedSets` reached: `Bạn đã có quá nhiều bộ thẻ (tối đa 200). Hãy xóa bớt bộ cũ rồi thử lại.`

### Step 5 — Done (`P2`)
- Success icon, `Đã thêm {k} bộ thẻ cho bạn!`, `{N} từ đã nằm trong thư mục Lộ trình của tôi ở Bộ thẻ của tôi.`
- A list of the created decks (thumb, name, count, green check).
- Buttons:
  - `Mở Bộ thẻ của tôi` → the folder page;
  - `Học {pace} từ đầu tiên` (primary) → `/Practice/learn?set={firstDeckId}&n={pace}&from=set`.

### "Bộ thẻ của tôi" afterwards (`P3`)
- A dismissible banner (once, per user, server flag in the onboarding row: `BannerDismissedAt`): `{k} bộ thẻ mới được tạo theo sở thích của bạn`, the summary line, and the link `Đổi sở thích`, which opens the flow at step 1 in "again" mode.
- Header button `Tạo thêm từ sở thích` (wand icon), next to `Tạo bộ thẻ`, always visible for signed-in users.
- The folder "Lộ trình của tôi" gets a route icon and a `Mới` chip for 7 days after creation.
- Set tiles of onboarding decks:
  - a 2×2 **mosaic cover** of the first 4 card images. This applies to **all** personal sets with ≥ 4 images. A set with 1–3 images shows one photo; a set with none keeps the current art;
  - a yellow `Từ sở thích` chip.

### Deck detail (`P4`)
- Badge `Từ sở thích` next to the visibility chip.
- Subtitle adds `Nguồn: {topic names} (thư viện TCT)`.
- Side card **"Gợi ý thêm cùng chủ đề"**:
  - `Thêm 10 từ tiếp theo về {chủ đề}?`, 4 preview thumbs, button `Thêm 10 từ`;
  - shown when the deck has ≤ 5 unlearned cards **or** always as a collapsed link (decide at S6 by the look; record);
  - after adding: toast `Đã thêm {n} từ vào "{deck}".`;
  - when the pool is empty: `Bạn đã có tất cả từ của chủ đề này 🎉` and a link to the system course.
- Everything else on the page is unchanged (all modes and actions).

### Phone ≤ 767 px (`P6`)
- The pop-up becomes a full-screen sheet: top bar (back, step label, `Để sau`), scrolling content, and a sticky bottom bar with the main button.
- Topic tiles and preview cards in 2 columns, tabs as a horizontal scroll row.
- Touch targets ≥ 44 px. No horizontal page scroll at ≥ 320 px.

---

## 4. Server design

### 4.1 Data (one migration `AddUserOnboarding`)

```
UserOnboarding
  UserId            int  PK, FK → Users.UserId (cascade delete)
  Status            varchar(16)  Pending | Completed | Skipped | NotEligible
  PromptCount       int  default 0
  LastPromptedAt    datetime(6) null
  GoalsJson         varchar(400) null      // ["travel","communication"]
  TopicKeysJson     varchar(1000) null     // ["A06","C03",...]
  Level             varchar(2) null        // A1|A2|B1|B2
  DailyNewWords     int null               // 5|10|15|20
  KnownTermsJson    mediumtext null        // lower-case terms excluded (≤ 2000 items)
  DecksJson         mediumtext null        // [{setId, goal, topicKeys:[...]}]
  FolderId          int null               // "Lộ trình của tôi"
  LastRequestId     char(36) null          // idempotency of Complete/Refill
  BannerDismissedAt datetime(6) null
  CreatedAt, UpdatedAt, CompletedAt datetime(6)
```

- `Models/UserOnboarding.cs` (new file).
- DbContext: one `DbSet` plus its configuration. Additive lines only; follow F9/unknown 9.
- Check the migration SQL against MariaDB (Pomelo): no default `GETDATE()`-style SQL Server syntax.
- Run `has-pending-model-changes` (none after adding).
- No data back-fill. A missing row = "not decided yet" (4.2).

### 4.2 Services (new folder `Services/Onboarding/`)
- `OnboardingTopicCatalog` (static):
  - the 48 topic keys (`A01`…`C08`), each with: system set name, band (`A1–A2`/`B1–B2`/`Giao tiếp`), min/max CEFR, and goal tags;
  - mapping rule, reviewed by the agent against the set names, e.g.:
    - `travel` → A16, A15, B04, B05, C04, C05, C07;
    - `communication` → C01–C08, A09, A23;
    - `work` → A14, B01, B02, B16, C06;
    - `study` → A13, B08, B11, B09, B03;
    - `hobbies` → A19, A20, A21, B12, B13, A03, A06, A05;
    - `beginner` → A01–A12, A17, A18, A22, A24;
  - a test asserts that every key maps to an existing system set by exact name and that every goal has ≥ 4 topics.
- `IOnboardingService` / `OnboardingService`:
  - `GetStateAsync(userId)` → `{ eligible, status, shouldPrompt, step data }`.
    - **Eligibility:** row Pending, **or** no row and (user `CreatedAt ≥ OnboardingEligibleFromUtc` and no own sets and no `LearningProgress`). In that case create a Pending row. Otherwise create a NotEligible row.
    - `shouldPrompt` = Pending and PromptCount < 2 and (LastPromptedAt null or ≥ 20 h ago).
  - `MarkPromptedAsync`: increments only once per 20 h window.
  - `SkipAsync`: the 2nd skip → Skipped.
  - `BuildPreviewAsync(userId, goals, topics, level, pace)` → decks with cards (section 4.3). Read-only.
  - `CompleteAsync(userId, request)`, all in **one transaction**:
    1. validate the input again (never trust the client);
    2. rebuild the preview server-side;
    3. apply the decks switched off and the known terms;
    4. create or reuse the folder;
    5. create the sets;
    6. copy the cards with the **exact field copy of `CloneInternalAsync`**, through a new shared private helper in `PersonalSetService` (`CopyCardsInto(Set target, IEnumerable<Card> source, bool copyNotes:false, DateTime now)`), refactored so clone and onboarding use the same code;
    7. save the row as Completed (decks, known terms, request id).

    Idempotent by `requestId`: the same id returns the same result without creating anything. `MaxOwnedSets` is checked before writing.
  - `RefillAsync(userId, setId, count=10, requestId)`:
    - only for sets listed in `DecksJson` **and** owned by the user;
    - picks the next cards from that deck's topics (same ranking) that are not in **any** of the user's own sets (case-insensitive lemma) and not in KnownTerms;
    - copies them in.
  - `DismissBannerAsync`.
- `IPersonalSetService` gains **only** what is needed: the copy helper (internal/private) and, if required, a method to create a set inside the same transaction. Do not change existing public signatures or behaviour. Clone tests must still pass unchanged.

### 4.3 Ranking and deck building (deterministic, unit-tested)
1. **Allowed CEFR window** by level: A1 → {A1, A2}; A2 → {A1, A2, B1}; B1 → {A2, B1, B2}; B2 → {B1, B2}. For C-sets (phrases), the band is ignored and phrases are always allowed.
2. **Candidates:** all cards of the chosen topics' system sets, CEFR in the window, with an image (cards without an image go last).
3. **Score:**
   - `levelDistance` (0 = same as the user's level, 1 = one step away), lower is better;
   - then topic round-robin (variety);
   - then the original card order (CardId).
4. **Deduplication:** the case-insensitive trimmed lemma across all decks, against the user's existing own sets, and against KnownTerms.
5. **Decks:**
   - one per chosen goal (in goal order), with the topics tagged for that goal;
   - topics matching no chosen goal → one deck `Chủ đề bạn chọn`;
   - if any C-topic is chosen and the goals include `communication` or `travel`, the phrases go to a separate deck `Câu giao tiếp {khi đi du lịch | hằng ngày}`;
   - decks with < 10 cards merge into the nearest deck;
   - max 4 decks (the smallest merge);
   - size = D2 (25, cap 30).
6. **Names** (unique per user; add ` (2)` when taken), mapped from the deck's goal:

   | Goal | Deck name |
   | --- | --- |
   | `travel` | Du lịch – bộ khởi đầu |
   | `communication` | Giao tiếp hằng ngày – bộ khởi đầu |
   | `work` | Công việc – bộ khởi đầu |
   | `study` | Học tập – bộ khởi đầu |
   | `hobbies` | Sở thích & giải trí – bộ khởi đầu |
   | `beginner` | Từ vựng nền tảng – bộ khởi đầu |
   | (phrase deck) | Câu giao tiếp khi đi du lịch / Câu giao tiếp hằng ngày |
   | (no goal) | Chủ đề bạn chọn |

   Description: `Tạo từ sở thích · Nguồn: {topic names}`.
7. **Folder:** `Lộ trình của tôi`. Reused if the user already has a folder with exactly that name; created otherwise. If the name is invalid for the service, stop (8.2).
8. **"Again" mode** (from "Tạo thêm từ sở thích"): the same flow; new decks go into the same folder; names get ` (2)` etc.; DecksJson is appended; KnownTerms are merged.

### 4.4 HTTP (`Controllers/OnboardingApiController.cs`, `[Authorize]`, `[Route("api/onboarding")]`, `[AutoValidateAntiforgeryToken]`)

| Verb | Route | Body | Result |
| --- | --- | --- | --- |
| GET | `state` | – | `{ shouldPrompt, status, mode, hasDecks }` |
| POST | `prompted` | – | 204 |
| POST | `skip` | – | `{ status }` |
| POST | `preview` | `{ goals[], topics[], level, pace }` | `{ decks:[{key,name,goal,topics,cards:[{cardId,term,meaning,imageUrl,cefr}]}], total, days }` |
| POST | `complete` | `{ requestId, goals, topics, level, pace, decksOff[], knownCardIds[] }` | `{ folderId, folderUrl, decks:[{setId,name,count,url}], firstLearnUrl }` |
| POST | `refill/{setId}` | `{ requestId, count }` | `{ added, setUrl, exhausted }` |
| POST | `banner/dismiss` | – | 204 |

- **Validation (all 400 with a Vietnamese `error`):**
  - goals: 1–3 known keys, unique;
  - topics: 3–8 known keys;
  - level in A1/A2/B1/B2;
  - pace in 5/10/15/20;
  - requestId a GUID;
  - knownCardIds ⊆ the preview's card ids (re-checked server-side);
  - count 1–20.
- Errors: 401 guest; 404 for a set not owned or not an onboarding deck; 409 for an idempotency mismatch (same id, different payload).
- Rate limit: complete/refill 10 per 10 min per user (reuse the existing limiter pattern).
- Never return system internals, user emails or other users' data. Image URLs pass the safe-URL rule.

### 4.5 Hosting the pop-up
- `Views/Shared/_OnboardingHost.cshtml` (new, Trọng's):
  - renders nothing for guests;
  - otherwise the dialog skeleton, the JSON config (state, topic catalogue with cover URLs, goal list, copy), `onboarding.css` and `onboarding.js` (`defer`);
  - the server decides `shouldPrompt`. The JS opens the dialog automatically only when it is true and calls `prompted`.
- Includes:
  - `Views/Home/Index.cshtml`: one line (D3);
  - `Views/Vocabulary/Index` and `Views/Set/Index.cshtml`: one line each;
  - the `Set/Index` header button `Tạo thêm từ sở thích` (`data-tob-open="again"`).
- **Safety:** if the config or JS fails, nothing opens and the page is untouched. Wrap init in try/catch and log to the console only in Development.

### 4.6 Front-end files
- `wwwroot/js/onboarding-core.js`: pure UMD logic (selection limits, step validation, the days estimate, the summary text), with node tests.
- `wwwroot/js/onboarding.js`: the dialog, step rendering, API calls (antiforgery header like `Sets.fetchJson`), focus management, keyboard, `aria-live` announcements, error states, idempotent request ids (`crypto.randomUUID`, with a fallback).
- `wwwroot/css/onboarding.css` (`.tob-page` scope; tokens only).
- Mosaic cover for set tiles: `Views/Set/_SetTile.cshtml` + set CSS (Trọng's). The service supplies up to 4 safe image URLs per set in **one grouped query** (no N+1).

---

## 5. File scope

**May add:**
- `Models/UserOnboarding.cs`;
- `Migrations/<timestamp>_AddUserOnboarding.cs` (+ Designer) and the snapshot update;
- `Services/Onboarding/**`;
- `Controllers/OnboardingApiController.cs`;
- `Views/Shared/_OnboardingHost.cshtml` (+ `_Onboarding*` partials);
- `wwwroot/js/onboarding*.js`, `wwwroot/css/onboarding.css`;
- tests `TCTEnglish.Tests/Onboarding*Tests.cs` and node tests next to the existing ones;
- docs.

**May edit:**
- the DbContext: `DbSet` + configuration only (additive);
- `Program.cs`: DI lines for the new services, nothing else;
- `Services/Sets/PersonalSetService*.cs`: extract the card-copy helper, add a grouped "first 4 images" query; no behaviour change;
- `ViewModels/Sets/*`: additive fields (`MosaicImages`, `IsStarterDeck`, `StarterTopics`);
- `Views/Set/Index.cshtml`, `_SetTile.cshtml`, `Detail.cshtml` (badge, source line, refill card), set CSS/JS;
- `Views/Vocabulary/Index*` (one line);
- `Views/Home/Index.cshtml` (**one line**, D3).

**Forbidden:**
- `User.cs`, `AccountController`, `HomeController`, `_Layout*`, `site.css`, `study-kit.css`, tokens;
- Goals, AI, Class, Reading;
- system content data and packages, seeders, appsettings, `.csproj`, new packages or CDNs;
- any other migration.

---

## 6. Tests (write before code in each phase)

**C# unit/integration (`OnboardingServiceTests`, `OnboardingApiTests`, `OnboardingRankingTests`):**
1. **Eligibility:** a new user → Pending + shouldPrompt; an old user / a user with sets / a user with progress → NotEligible; Skipped after 2 skips; prompted at most once per 20 h.
2. **Catalogue:** 48 keys map to existing system sets by exact name; each goal has ≥ 4 topics.
3. **Ranking:**
   - the CEFR window per level;
   - round-robin variety;
   - deterministic output (same input → same order);
   - dedupe against existing own sets and known terms;
   - merge rules (< 10 → merge; > 4 decks → merge);
   - the C-phrase deck rule;
   - names unique with ` (2)`.
4. **Complete:**
   - creates the folder once, the sets as `Private` and owned by the user, the cards with **all fields equal** to the source (compare field by field, incl. DetailsJson and ImageSourceJson; PersonalNote null);
   - the system cards are unchanged (row hash before/after);
   - transaction rollback on failure (simulate) leaves no partial set;
   - idempotent with the same requestId;
   - 409 for a different payload with the same id;
   - `MaxOwnedSets` respected;
   - knownCardIds outside the preview → 400.
5. **Refill:**
   - adds the next N, never duplicates, never returns known terms;
   - other users' sets → 404; a non-onboarding set → 404;
   - an empty pool → `exhausted:true`.
6. **Security:** antiforgery required on POST; guest 401; no IDOR (every query scoped by userId); rate limit; input bounds.
7. **Personal set regression:** all existing clone, create, folder and set tests pass unchanged. A new mosaic query test: constant query count for 1 vs 50 sets.
8. **Render tests:**
   - the host renders nothing for guests and the config for users;
   - `shouldPrompt` false → no auto-open attribute;
   - Set/Index shows the button and the banner states;
   - the deck detail badge and the refill card.

**Node (`onboarding-core`):** selection limits (goals ≤ 3, topics 3–8), the days estimate (`ceil(N / pace)`), the summary text, the step guard, request id fallback.

**Migration:** the `Up`/`Down` round-trip on a throwaway local DB **only through the team's documented local command** (never reset the shared dev DB). `has-pending-model-changes` → none.

---

## 7. Phases

### S0 — Baseline and discovery
- Pull, branch `feature/onboarding-personal-decks`.
- Build and run the full tests and the node tests; record the counts.
- Reproduce F1–F8 and resolve 2.1.
- Take before screenshots of Trang chủ, Vocabulary home, Bộ thẻ của tôi and a set detail (1440×900, 390×844) into `.tmp/onboarding/before/`.
- Set `OnboardingEligibleFromUtc` to the run date and record it.

### S1 — Data
Model, DbContext lines, migration, `has-pending-model-changes`, a migration test (apply on the test DB).

### S2 — Topic catalogue and ranking
Tests first, then the catalogue and the deck builder.

### S3 — Service
State, prompt, skip, preview, complete (with the copy helper refactor) and refill, tests first.

### S4 — API
Controller, validation, rate limit, security tests.

### S5 — Pop-up UI
Host, core JS + node tests, dialog JS, CSS, steps 0–5 exactly as section 3, error and loading states. Include in the three pages (D3).

### S6 — Bộ thẻ của tôi and set detail
Mosaic covers (grouped query), the "Từ sở thích" chip, the banner, the header button, the deck detail badge, the source line and the refill card.

### S7 — Phone, accessibility, polish
All widths, 200% zoom, keyboard-only run, screen-reader names, contrast script (chips on photos, the yellow `Gợi ý` chip, disabled buttons), reduced motion, forced colors. Visual comparison with the mockups. List the differences.

### S8 — End-to-end
1. Register a **new synthetic test account** through the normal UI (allowed: a local account with an `@tct.local`-style test email; never a real address), or use a fresh synthetic account from `.appdata/local/access.json` if one is unused.
2. Sign in: the pop-up opens automatically.
3. Go through the steps with travel + communication + hobbies, 5 topics, A2, 10 words. In the preview, switch one deck off and mark 3 words known. Create.
4. Check:
   - the folder and decks in Bộ thẻ của tôi;
   - card fields complete (image, IPA, examples) on 3 random cards;
   - every study mode opens and works for one deck (Học, Kiểm tra, Ghép thẻ, Trắc nghiệm, Gõ từ, Nghe, Điền từ);
   - edit and delete a card;
   - Refill adds 10 with no duplicates.
5. Sign out and in: no pop-up.
6. Test "Để sau" twice on a second new account (with the 20 h window simulated in a test, not by waiting).
7. An old account: no automatic pop-up; the button works.
8. Clean up: delete the test decks through the UI. Leave the test accounts only if the team keeps synthetic accounts; otherwise record them.

### S9 — Docs
- Tracker row.
- `docs/known-issues.md`:
  - the one-line Home include for Châu;
  - the DbContext change;
  - the eligibility date;
  - mosaic covers now on all personal sets.
- `project-structure.md`; `bug-fix-log.md` if bugs were found.
- A short `docs/features/onboarding.md`: how it works, how to change goals and topics, how to reset a test user's onboarding (local SQL is **not** allowed; provide a dev-only admin-free test helper or tell how to create a new test account).

### S10 — Full gate, self-review against section 10, report.

---

## 8. Gates and run rules

### 8.1 Phase gate G1–G16

| Gate | Check |
| --- | --- |
| G1 | `dotnet build`, no new warnings in touched files |
| G2 | Onboarding + Sets tests |
| G3 | Full suite; counts ≥ S0 |
| G4 | Node tests; counts ≥ S0 |
| G5 | `has-pending-model-changes` → none; exactly one new migration; the route list has only the new `api/onboarding/*` routes added |
| G6 | Tokens grep (no hex/rgb outside forced-colors in new CSS); no inline style/script/handlers in new views; `git diff --check`; encoding guard |
| G7 | File scope = section 5; `Home/Index.cshtml` diff = 1 line; `Program.cs` diff = DI lines; DbContext diff additive |
| G8 | Render matrix 1440×900, 1366×768, 1024, 768, 390, 200% zoom; console and network clean |
| G9 | Mockup comparison (O0–O3, P1–P4, P6); differences listed with reasons |
| G10 | Behaviour checklist 8.3 (mouse and keyboard) |
| G11 | Accessibility: dialog focus trap and return, checkbox/radio/switch roles, `aria-live` hints, names, tab order |
| G12 | Scripted contrast ≥ 4.5:1 text, ≥ 3:1 UI parts, incl. text over photos |
| G13 | Regression: Trang chủ, Vocabulary home and course pages, all set pages and study modes, Goals page, login/register |
| G14 | Consistency: Vietnamese copy exactly as section 3; no `bi bi-`; no English UI labels |
| G15 | Security: tests of section 6.6 pass; scoped queries; no `innerHTML` with server data; idempotency and rate limit |
| G16 | Performance: preview ≤ 300 ms and complete ≤ 1.5 s locally for 4×25 cards; query count constant (log it) |

Gate rules:
- A failed item → fix and rerun the **whole** gate.
- At most 3 attempts per item, then deferred or blocked with evidence.
- Never skip, weaken or delete a test. Never touch a forbidden file. Never call a skipped check passed.

### 8.2 Stop conditions
1. A change needs a forbidden file (beyond the allowed one-line includes).
2. The migration cannot be generated cleanly, or it touches other tables.
3. The personal set service cannot create the folder or sets with the planned names, or cannot be used inside one transaction, without changing its public behaviour.
4. A gate item fails 3 times and blocks every later phase.
5. Any risk to real user data.

### 8.3 Behaviour checklist (G10)
1. A new account → the pop-up opens on the first page after login. An old account → it does not.
2. `Để sau` closes; the next visit ≥ 20 h later reopens it; the 2nd skip ends it; `Esc` = `Để sau`.
3. `Quay lại` keeps the earlier choices. Refreshing the page mid-flow restarts at the welcome screen without errors (choices are not persisted until Complete).
4. Limits: goals ≤ 3, topics 3–8 with hints; level and pace required (defaults preselected).
5. The preview matches the server output. Switches and the known toggle update the counts and the footer live.
6. Create → done screen. The links work. The first learn link opens 10 cards from deck 1.
7. A double click creates nothing twice. A network failure → error + retry → same result.
8. Bộ thẻ của tôi: banner, folder `Mới`, mosaics, chips. The banner dismiss is remembered.
9. Deck detail: all modes work. Edit and delete work. Refill adds 10 with no duplicates. Exhausted message.
10. "Tạo thêm từ sở thích" (again mode) adds decks to the same folder with unique names.
11. Phone: full-screen sheet, sticky bar, 2-column grids, no horizontal scroll.

---

## 9. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Pop-up breaks the dashboard | Server-side `shouldPrompt`, try/catch init, no layout change, render tests |
| Duplicate decks on double submit or retry | requestId idempotency + disabled button + transaction |
| Copy misses card fields | Shared copy helper with `CloneInternalAsync` + a field-by-field test |
| Existing users nagged | Eligibility date + no sets + no progress (D5) |
| Shared files (Home/Index, DbContext, Program) | One-line / additive diffs, gate G7, known-issues note for Châu |
| Slow preview | One grouped query per request, in-memory ranking, ≤ 8 topics × 25 cards |
| Topic mapping wrong | Catalogue test + agent review against set names |

---

## 10. Definition of done
- [ ] Section 3 implemented for laptop and phone; matches the mockups (differences recorded).
- [ ] Decks are ordinary personal sets with all modes and actions; cards are complete copies; system content unchanged.
- [ ] Eligibility, prompt and skip rules (D4, D5) proven by tests.
- [ ] One clean migration; `has-pending-model-changes` none; no forbidden file; the one-line Home include only.
- [ ] G1–G16 green per phase (or deferred with evidence); counts ≥ S0.
- [ ] End-to-end S8 passes; test data cleaned or recorded.
- [ ] Docs written; section 12 complete.

## 11. Commit plan
One local commit per phase via `tct-commit-message`, for example:
- `feat(onboarding): add the onboarding state table`
- `feat(onboarding): build starter decks from the topic catalogue`
- `feat(onboarding): add the onboarding API`
- `feat(onboarding): add the first-run pop-up`
- `feat(sets): show starter decks, mosaic covers and refill`
- `docs(onboarding): record the feature`

No push, no PR, no merge.

## 12. Execution record
_(Filled by the agent: S0 verdicts; per-phase gate records; mockup differences; the eligibility date; final summary.)_

### 12.1 S0 record (2026-11-19, local)

**Branch.** `feature/onboarding-personal-decks` from `master` at `e90c8f8` (the system content fill and the vocabulary covers were already merged into `master`; `pull-local.ps1` exit 0). Nothing pushed.

**Baseline (Release, `xUnit.MaxParallelThreads=2`, the same tree `e90c8f8`):** .NET 3645 passed, 0 failed, 58 skipped; node 570 passed, 0 failed. Later gates must keep these counts or higher. `has-pending-model-changes`: none. Before screenshots: `.tmp/onboarding/before/{home,vocabulary,sets,setdetail}-{1440,390}.png`.

**Verdicts F1–F8**
| # | Verdict | Evidence |
| --- | --- | --- |
| F1 | Confirmed. No onboarding code, view or script exists. | grep over `*.cs`, `*.cshtml`, `*.js` finds nothing outside migrations |
| F2 | Confirmed. Login and Google sign-in redirect to `Home/Index`; Register redirects to Login. | `AccountController` lines 81, 92, 159, 346 |
| F3 | Confirmed. `Views/Home/Index.cshtml` (350 lines) has several authors in `git log` (reading, dashboard, goals). One-line include only. | git log |
| F4 | Confirmed, with the rules below. | `PersonalSetService.Write.cs` |
| F5 | Confirmed. Set detail offers Học, Kiểm tra, Ghép thẻ, Lật thẻ and a link to `/Practice?set=` for Trắc nghiệm, Gõ từ, Nghe, Điền từ. | `Views/Set/Detail.cshtml` 48–126 |
| F6 | Confirmed. 48 system sets; covers in `data/content/vocabulary-covers.json` (moved out of `vocabulary/` on 2026-11-19) and `VocabularyCoverCatalog`. | content run |
| F7 | Confirmed (`AddSampleSetsAsync`, line 801). Kept unchanged. | |
| F8 | Confirmed. Last migration `20261118132210_AddAiVoiceConversation`. | `Migrations/` |

**Unknowns 2.1**
1. Names: a folder name is trimmed, 1–100 characters and unique per owner ignoring case (`ValidateFolderName`, `OwnerHasFolderNamedAsync`). A set name is trimmed, 1–255 characters and is **not** unique (only clone adds " (bản sao)"); description is trimmed to 500. "Lộ trình của tôi" is valid. The planned deck names are valid; the " (2)" suffix is applied by the onboarding builder, not the service.
2. No per-set card limit exists (only `MaxOwnedSets = 200` sets per owner).
3. Set tiles do not show description tags (they show the sample term, folder chip, counts, progress); the "Từ sở thích" chip will come from `UserOnboarding.DecksJson`.
4. Sets JS uses `TctSets.fetchJson` (`personal-sets-common.js`): header `RequestVerificationToken`, errors carry `status` and the server `error`. Toasts: `TctSets.toast(message, options)` in `#tsToastHost`.
5. Dialogs: the Set pages use Bootstrap 5 modals (`_ShareSetModal`, `Detail.cshtml`). The pop-up will be a Bootstrap modal with `data-bs-backdrop="static"` (backdrop click does nothing) and the plan's own Esc handling.
6. Yes: `Views/Vocabulary/Index.cshtml` is the Vocabulary home.
7. Tests: `TestWebApplicationFactory` (SQLite in memory), `TestDataIds`, `IntegrationTestClientHelper.CreateAuthenticatedClient`, `CountingDbCommandInterceptor` through `factory.SaveInterceptors`. Seed users have `CreatedAt = UtcNow`; user 101 owns set 301, so a test needs a fresh user for the eligible case.
8. Migration: `dotnet ef migrations add AddUserOnboarding --project TCTEnglish --startup-project TCTEnglish --context DbflashcardContext`; check with `dotnet tool run dotnet-ef migrations has-pending-model-changes --project TCTEnglish/TCTEnglish.csproj --no-build --configuration Release`.
9. Newer entities live in partial files `Models/DbflashcardContext.<Feature>.cs` with a static `Configure<Feature>(ModelBuilder)` called from `OnModelCreating` (lines 118–119). Plan: `Models/DbflashcardContext.Onboarding.cs` plus one added line `ConfigureOnboarding(modelBuilder);` in the main file.

**Decisions recorded at S0**
- **Eligibility date** `OnboardingEligibleFromUtc = 2026-11-19T00:00:00Z` (the run date). All 10 users of the local database were created before it, so none is auto-prompted locally; the end-to-end run uses a new account.
- **Rate limit (deviation from "reuse the pattern").** `Program.cs` already has `AddRateLimiter` with an `account-security` policy, but a new policy there would be more than "DI lines only". The 10 per 10 minutes limit for complete/refill is therefore a small singleton limiter (`OnboardingRequestLimiter`, same idea as `AiRequestRateLimiter`), registered with one DI line.
- The pop-up uses a Bootstrap modal (existing pattern) instead of a hand-written dialog.

### 12.2 S1 gate record (data)

New: `Models/UserOnboarding.cs`, `Models/DbflashcardContext.Onboarding.cs` (partial file, `DbSet` + `ConfigureOnboarding`), migration `20261119151230_AddUserOnboarding` (+ Designer, snapshot +78 lines), `OnboardingSchemaTests`. Edited: one line `ConfigureOnboarding(modelBuilder);` in `Models/DbflashcardContext.cs`.

**Deviation from plan 4.1 (additive, same table).** Three extra columns for idempotency: `LastRequestHash varchar(64)` (the 409 rule needs a payload fingerprint) and `LastResultJson varchar(4000)` (a retried request returns the same result without recomputing). No other change; no `FolderId` foreign key by design.

| Gate | Result |
| --- | --- |
| G1 | Debug and test project build, 0 errors. |
| G2 | `OnboardingSchemaTests` 3/3 (migration has only `CreateTable`/`DropTable` of `UserOnboardings`, row round trip, one row per user, cascade on user delete, column mapping). |
| G3, G4 | Full .NET and node suites are **not** run at S1 (no behaviour or script changed); they run at S3, S4, S6, S8 and S10. Baseline to beat: .NET 3645 / node 570. |
| G5 | `has-pending-model-changes`: none. Exactly one new migration; it creates one table and touches no other. Applied to the local Docker MariaDB 10.5-compatible database (`Applying migration '20261119151230_AddUserOnboarding'`, `SHOW CREATE TABLE` checked, FK cascade present). |
| G6 | `git diff --check` clean on new files; encoding guard passed. No CSS/views touched. |
| G7 | Files = section 5. The DbContext diff is one added line. |
| G8–G12, G14 | Not applicable (no UI). |
| G13 | Not applicable yet (no behaviour change). |
| G15 | Cascade delete only; no query exists yet. |
| G16 | Not applicable. |

### 12.3 S2 gate record (catalogue and ranking)

New: `Services/Onboarding/OnboardingCatalog.cs` (6 goals, 48 topics, goal → topic map, levels, paces, `EstimateDays`, `DefaultLevel`), `Services/Onboarding/OnboardingDeckBuilder.cs` (pure ranking and deck building), `OnboardingRankingTests` (written first; 42 tests incl. the catalogue checks against the committed manifests).

**Design decisions taken while building (all inside plan 4.3):**
- The CEFR window is `[max(0, l-1), min(3, l+1)]` over A1..B2 (A1 → A1–A2, A2 → A1–B1, B1 → A2–B2, B2 → B1–B2). **A topic with no card inside the window is not empty:** the sets of folder A only hold A1–A2 cards and folder B only B1–B2, so an A1 learner who picks a B topic would otherwise get nothing. Pass 1 takes window cards; pass 2 (only when the deck is still short) takes the nearest levels first. Phrase sets ignore the window.
- Within a pass the topics are mixed round-robin; per topic the order is images first, then level distance, then card id. The final deck is listed from easy to hard (stable).
- A topic fitting several chosen goals goes to the first chosen goal. Phrase topics go to their own deck when `travel` or `communication` is chosen (name by `travel` first). Groups with fewer than 10 cards merge into the previous deck (the next for the first); above 4 decks the smallest merges into its neighbour; at most 30 cards per deck and 100 in total.
- Deck names get ` (2)`, ` (3)` … against the names the user already has.

| Gate | Result |
| --- | --- |
| G1 | Debug build, 0 errors. |
| G2 | `OnboardingCatalogTests` + `OnboardingRankingTests` + earlier `OnboardingSchemaTests`: all pass (42 + 3). Three first-draft tests used deck sizes below the 10-card minimum; the tests were rewritten to valid sizes, not the code. |
| G3, G4 | Full suites run at S3 (no existing code changed here). |
| G5 | No model change. No route added. |
| G6 | New C# only; no CSS, views or inline code. |
| G7 | Files = section 5. |
| G8–G14, G16 | Not applicable (no UI). |
| G15 | Pure functions; no data access. |

### 12.4 S3 gate record (service)

New: `Services/Onboarding/{OnboardingModels,OnboardingService,OnboardingRequestLimiter}.cs`, `PickNext` in the deck builder, `OnboardingServiceTests` (33 tests incl. the limiter). Edited: `Services/Sets/PersonalSetService.Write.cs` (the card copy of `CloneInternalAsync` moved into `internal static CopyCard`, same fields, same values; no signature or behaviour change) and two DI lines in `Program.cs`.

**Design notes**
- The service does not use `IPersonalSetService`: it works on the `DbflashcardContext` in one transaction and shares only `CopyCard`, so the public interface is unchanged.
- Idempotency: inside the transaction the request id is claimed with one `UPDATE … WHERE LastRequestId <> @id`. Under InnoDB the row lock makes a concurrent second request wait and then see zero rows updated, and it replays the stored result (`LastResultJson`). The same id with another payload hash is a conflict. SQLite has no row locks, so the tests prove the sequential retry, the conflict and the rollback; the concurrent case relies on this MariaDB behaviour and is **not** covered by an automated test.
- A system topic is the system set with that exact name; when an older set shares the name ("Nghề nghiệp" exists twice) the one with more cards is used.
- "Đã biết" only removes cards from the decks created now and stores the lower-case terms; no progress row is written.
- The "Again" mode reuses the folder (name compared ignoring case and edge spaces), numbers the names and never repeats a term the user already owns.

| Gate | Result |
| --- | --- |
| G1 | Debug and Release build, 0 errors. |
| G2 | `OnboardingServiceTests` 33/33 (state, prompt spacing, skip rules, preview validation and determinism, owned terms excluded, query count constant, complete with field-by-field copy and unchanged system rows, switched-off decks and known words, idempotency and conflict, rollback at the second save, set limit, again mode, existing folder, refill, banner). Sets and clone tests pass unchanged. |
| G3 | Full .NET suite (Release, 2 threads): **3723 passed**, 0 failed, 58 skipped (baseline 3645 + the 78 onboarding tests). |
| G4 | Node: 570 passed (baseline 570). |
| G5 | No model change; no route added yet. |
| G6 | New C# only. |
| G7 | `Program.cs` diff = 2 DI lines; the DbContext is untouched in this phase; `PersonalSetService.Write.cs` change is the helper extraction. |
| G8–G14, G16 | Not applicable (no UI); query count of the preview is asserted in the tests (≤ 8 SELECTs, constant for 3 and 8 topics). |
| G15 | Every query is scoped by the user id; the system vocabulary is only read (fingerprint of the system rows identical before and after). |

### 12.5 S4 gate record (API)

New: `Controllers/OnboardingApiController.cs` (`[Authorize]`, `[AutoValidateAntiforgeryToken]`, `api/onboarding`), `Services/Onboarding/OnboardingDtos.cs`, `OnboardingApiTests` (21 tests).

Routes added (7, nothing else): `GET state`, `POST prompted`, `POST skip`, `POST preview`, `POST complete`, `POST refill/{setId:int}`, `POST banner/dismiss`. Errors: 400 `{error}` in Vietnamese, 401 guests (302 to the login for the cookie scheme), 404 for a set that is not the user's or not an onboarding deck (never 403), 409 for the same request id with another payload, 429 `{error}` from the limiter. The request id is read as a string so a bad one is a 400 with a message. The links in the answers are built from integer ids only (`/Sets/Folders/{id}`, `/Sets/{id}`, `/Practice/learn?set={id}&n={pace}&from=set`). The limiter (10 per 10 minutes per user) counts the writing calls after the request id is checked.

| Gate | Result |
| --- | --- |
| G1 | Build, 0 errors. |
| G2 | `OnboardingApiTests` 21/21: guests are not served; every POST needs the antiforgery token; state for a new and an existing account; preview shape and bad choices (incl. malformed JSON); complete creates two sets and one folder, a retry returns the same ids, another payload is a 409; the created set opens on the normal page; refill adds words to the owner's deck and is a 404 for another user, a missing set and a plain personal set; count and id bounds; the limiter blocks the 11th call and does not affect another user. |
| G3, G4 | Full suites run at S6 (the only code outside new files is unchanged since the S3 run: .NET 3723 passed, node 570). |
| G5 | `has-pending-model-changes` unchanged (no model edit). Only the seven `api/onboarding/*` routes are new. |
| G6 | New C# only. |
| G7 | Files = section 5. |
| G8–G14, G16 | Not applicable (no UI). |
| G15 | Antiforgery on every POST, `[Authorize]`, queries scoped by user id, no user emails or other users' data in any answer (asserted), idempotency and rate limit tested. |
