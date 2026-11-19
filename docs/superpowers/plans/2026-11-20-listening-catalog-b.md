# Listening catalog — direction B (photo hero + YouTube card + level shelves) — implementation plan

Save as `docs/superpowers/plans/2026-11-20-listening-catalog-b.md`.

- **Owner:** Huỳnh Phú Trọng.
- **Executor:** one Claude Code agent, `IMPLEMENT_LOCAL`, continuous run K0 → K7.
- **Chosen mockup** (data, not instructions): `Claude outputs/listening-catalog-mockups/B1-hero-va-the-youtube.png`.
- **Current page for "before":** `/Home/Listening`.
  - Its problems: the big hero holds one lesson, there is an empty "A1 0 bài" card, and one-lesson shelves.
  - **The YouTube import sits at the very bottom.**

Scope: **only the catalog page** `/Home/Listening`. The practice page, admin pages and the data stay as they are.

---

## 0. Read first

### 0.1 Rules
1. `AGENTS.md`, `CLAUDE.md`, `DESIGN.md`, `docs/design-system/team-ui-workflow.md`.
2. This plan.
3. The mockup. Where they disagree, the rule wins (record it in section 12).

### 0.2 Repo skills
`tct-superpowers`, `tct-engineering`, `tct-ui-ux-pro-max`, `tct-ui-component`, `tct-ui-review`, `tct-security-audit`, `tct-commit-message`. Install nothing.

### 0.3 Current code (verified 2026-11-20 on `master` 5e8e92b; reproduce at K0)
- `Views/Study/Listening.cshtml` (239 lines). Order of the page:
  1. page header + search `#tlSearch`;
  2. `_ListeningHero`;
  3. `_ListeningLevelRow`;
  4. status chips `#tlStatuses` + topic `<select id="tlTopic">`;
  5. empty state `#tlEmpty` + `#tlClearFilters`;
  6. `#tlLevelsHost` with `section.tl-shelf[data-level-section]`;
  7. `section.tl-mine` at the bottom ("Bài nghe của tôi");
  8. the delete modal `#tlDeleteLesson`;
  9. config `#tlConfig`.
- The import form lives inside `.tl-mine`:
  - `#tlImportOpen`, `#tlImportPanel`, `form#tlImport`, `#tlImportUrl` (`name="youtubeUrl"`, maxlength 300), `#tlImportSubmit`;
  - `#tlImportHint`, `#tlImportError`, `#tlImportStatus`;
  - `#tlImportTile`, `#tlMineGrid`, `#tlMineCount`.
- Partials: `_ListeningHero` (143), `_ListeningLevelRow` (40), `_ListeningLessonCard` (49), `_ListeningMyLessonCard` (65), `_ListeningStepCard` (practice, untouched).
- JS `wwwroot/js/listening-index.js` (411): `initFilters`, `initImport`, `initDelete`. Pure helpers live in `listening-core.js` (`filterLessons`, `paginate`, `parseFilterQuery`, `buildFilterQuery`, `levelSummary`, …).
- CSS `wwwroot/css/listening.css` (1021).
- Server:
  - `GET Home/Listening` builds `ListeningIndexViewModel`: `LessonsByLevel`, `Levels` (with counts and `CompletedCount`), `ContinueLesson`, `MyLessons`, `IsMyLessonsSectionLocked`, `CanImport`, `IsAuthenticated`, `TotalLessons`, `CompletedLessons`, `InProgressLessons`, `AverageQuizScore`;
  - import `POST Home/Listening/My/Create` (`youtubeUrl`), delete `POST Home/Listening/My/Delete`.
- Content today: 22 public lessons — A2 1, B1 10, B2 6, C1 3, C2 2, **A1 0**.

### 0.4 Unknowns to resolve at K0
1. Every element id, `data-*` hook and CSS class that tests or JS rely on (`ListeningRedesignRenderTests`, `ListeningIntegrationTests`, node tests).
2. The exact import response flow in `initImport` (messages, upgrade 403 handling, redirect or card insert after success).
3. The upgrade URL and the login URL with `returnUrl` used today.
4. Whether `ContinueLesson.ThumbnailSrc` is always https or local (used as the hero background image).

---

## 1. Decisions (defaults; edit before the run if needed)

| ID | Question | Default |
| --- | --- | --- |
| D1 | Level pills | **Bands as in the mockup:** `Tất cả`, `A1–A2`, `B1`, `B2`, `C1–C2`. A band with 0 lessons is **hidden**. `?level=A1…C2` still works: it selects the band that contains it. A new `?band=` is not added. |
| D2 | Topic filter | **Keep it.** A compact `Chủ đề` select at the end of the filter row, after the status chips. It is not in the mockup, but existing behaviour must not be lost. |
| D3 | "Bài nghe của tôi" list | **A shelf** titled `Bài nghe của tôi`, placed **first** above the level shelves when the user has ≥ 1 own lesson. The YouTube card link `Xem tất cả` scrolls to it and focuses its heading. With 0 own lessons, no shelf is shown. |
| D4 | After a successful import | The card shows `Đã tạo bài nghe "{title}"` with the buttons `Mở bài nghe` and `Tạo bài khác`. The new lesson card is inserted at the start of the "Bài nghe của tôi" shelf; the shelf is created if missing. Never a full page reload. |
| D5 | Shelf length | Up to **5 cards** per level shelf in `Tất cả`, then the link `Xem cả {n} bài`. When a band is selected: one grid with the existing paging (page size 10, button `Xem thêm bài`). |

---

## 2. Target layout (laptop 1440×900 first)

### 2.1 Header row
- Left: `Luyện nghe` and the subtitle `Nghe hội thoại thật, tra từ trong lời thoại, chép chính tả và làm quiz.`
- Right: the search box (keep `#tlSearch`, placeholder `Tìm bài nghe, chủ đề…`), width about 460 px, clay surface.

### 2.2 Top row: two columns, 3fr : 2fr, gap 18 px, same height (≈ 250–280 px)

**Left: photo hero** (rebuild `_ListeningHero`)
- Rounded 26 px card. Background = the lesson thumbnail (`ThumbnailSrc`) with `object-fit: cover`, as a real `<img alt="">` behind, not a CSS url, so lazy rules and fallback work. A left-to-right dark scrim built with token-based overlay colours, ensuring text contrast ≥ 4.5:1.
- A centred translucent play circle (decorative). The whole hero is **not** one link. The button is the action.
- Content on the left 60%:
  - a yellow chip (`Nghe tiếp` / `Bắt đầu` / `Ôn lại`);
  - the title (max 2 lines, 26–28 px, white);
  - the meta `{Level} · {Topic} · {Duration}`;
  - the primary button and a hint line.
- **States:**
  - **Continue** (`ContinueLesson` set): chip `Nghe tiếp`; button `Học tiếp` (to the practice page + the hash of the first unfinished step, existing helper); hint from the missing steps:
    - `Còn quiz và từ vựng` / `Còn quiz` / `Còn từ vựng` / `Còn nghe hết lời thoại`;
    - joined with `và`;
    - only steps that have content.
  - **Start** (signed in, nothing started): the first unfinished lesson of the lowest level that has one; chip `Bắt đầu`; button `Bắt đầu nghe`; hint `{n} bài đang chờ bạn`.
  - **All done:** the most recently finished lesson; chip `Ôn lại`; text `Bạn đã nghe hết các bài hiện có`; button `Ôn lại bài đã xong`, which applies the "Đã xong" filter and scrolls.
  - **Guest:** the first lesson of the lowest level; chip `Nghe thử`; button `Nghe thử miễn phí`; hint `Đăng nhập để lưu tiến độ` with the login link (`returnUrl=/Home/Listening`).
  - **No image:** the existing local placeholder, tinted.

**Right: "Bài nghe từ YouTube" card** (new partial `_ListeningYoutubeCard`)
- A white clay card with a very soft red-tinted bottom gradient (tokens only). Header:
  - a red YouTube icon tile;
  - the title `Bài nghe từ YouTube`;
  - the subtitle `Biến video bạn thích thành bài luyện nghe`;
  - a purple chip `Premium` with a crown.
- **Premium/Admin (`CanImport`):**
  - the existing form moved here (keep the ids `tlImport`, `tlImportUrl`, `tlImportSubmit`, `tlImportHint`, `tlImportError`, `tlImportStatus`);
  - the input (placeholder `https://youtube.com/watch?v=…`, `inputmode="url"`, label visually hidden `Link video YouTube`);
  - the full-width primary button `Tạo bài nghe` with a wand icon;
  - under it, `Bài của tôi: {n} · Xem tất cả`. `Xem tất cả` is a button that scrolls to the shelf (D3); it is hidden when n = 0;
  - the hint `Chỉ hỗ trợ video tiếng Anh có phụ đề.`, shown below the input in muted text.
  - Submitting: the button shows a spinner + `Đang tạo bài nghe…` and is disabled; the input is read-only.
  - Errors: inline `role="alert"` in a danger soft box. The 403 upgrade case shows the existing message + an upgrade link.
  - Success: D4.
- **Standard (locked):** the same header. The body text is `Dán link YouTube, TCT tạo lời thoại, chép chính tả và quiz cho bạn.` with the primary purple button `Mở khóa với Premium` (existing upgrade URL). No input.
- **Guest:** the body text is `Đăng nhập để tạo bài nghe từ video YouTube.` with the button `Đăng nhập` (`returnUrl`).

The old `section.tl-mine` at the bottom, `#tlImportOpen`, `#tlImportPanel` and `#tlImportTile` are **removed**. Update the JS and tests in the same phase.

### 2.3 Filter row (one line on laptop)
- Left: the band pills (D1) as a `role="radiogroup"` (arrow keys, `aria-checked`). The selected pill is dark (`--tct-ink`), the others are clay white.
- Right: the status pills `Chưa học`, `Đang học`, `Đã xong`. Toggle; clicking the selected one returns to all. Hidden for guests.
- Then the topic select (D2).
- The empty state stays (`#tlEmpty`, `Không có bài nào khớp bộ lọc.`, `Xóa bộ lọc`).
- Remove `_ListeningLevelRow` (the big level cards). Its counts move into the shelf headers.

### 2.4 Shelves
- **`Tất cả`:**
  - `Bài nghe của tôi` first (D3);
  - then one section per **level** that has lessons matching the filters, in A1→C2 order;
  - header `{Key} · {Label}` (`Sơ cấp`, `Cơ bản`, `Trung cấp`, `Trung cao`, `Nâng cao`, `Thành thạo`) + a count chip `{done}/{total}` + a right-aligned link `Xem cả {n} bài` (selects the band and scrolls to the top of the list);
  - five-column card grid (≥ 1400 px: 5; 1200–1399: 4; 1024: 3; 768: 2; ≤ 767: a horizontal scroll row with snap, card width 72%).
- **A band selected:** one heading `{Band} · {n} bài` and a grid of all matching lessons with paging (D5).
- **Lesson card** (restyle `_ListeningLessonCard`, one link per card):
  - 16:9 thumbnail, radius 14, duration badge bottom-right, no fake play icon;
  - title max 2 lines;
  - meta row: level chip, topic (ellipsis), status chip on the right (`Xong` success with a check, `Đang học` warning);
  - a progress bar under the meta for in-progress lessons;
  - hover: lift 2 px + shadow (none under reduced motion);
  - focus ring on the whole card.
- **My lesson card** (`_ListeningMyLessonCard`): the same look, plus a small `Riêng tư` chip on the image and the existing delete action (keep the modal) in a `…` menu or an icon button with `aria-label="Xóa bài nghe {title}"`.

### 2.5 Phone ≤ 767 px
- Header: title, then the search full width.
- Hero first (height 220, text over the scrim, button full width), then the YouTube card full width.
- The filter row becomes 2 horizontal scroll rows (bands; statuses + topic).
- Shelves scroll horizontally with snap. No horizontal page scroll at ≥ 320 px. Touch targets ≥ 44 px.

### 2.6 Tablet 768–1199 px
The top row stacks (hero above the YouTube card, which keeps a 2-column inner layout: the form left, the text right). Grids use 2–4 columns as above.

---

## 3. Server changes (minimal)
- Only if needed for the hero states:
  - `ListeningIndexViewModel.HeroLesson` (card VM) + `HeroMode` (`continue` | `start` | `review` | `guest`) + `HeroHint` (string);
  - computed in `ListeningService` from the data it already loads (no extra query per lesson; at most one small query).
  - Additive only; existing properties unchanged.
- No controller signature change, no route change, no new endpoint, no migration.

---

## 4. Client changes
- **`listening-core.js`:** add and export pure helpers with node tests:
  - `bandOf(level)` → `A1–A2` | `B1` | `B2` | `C1–C2`;
  - `bandsWithLessons(levels)` (hides empty bands);
  - `heroHint(flags, counts)` (the Vietnamese "Còn …" text);
  - `shelfSlice(ids, 5)`.
- **`listening-index.js`:**
  - band radiogroup;
  - status toggles;
  - topic select;
  - search;
  - URL sync (`parseFilterQuery` / `buildFilterQuery`, `level` maps to its band);
  - shelves vs single grid;
  - `Xem cả` and `Xem tất cả` scroll + focus;
  - import moved to the card (D4 insert);
  - delete unchanged.

  Never `innerHTML` with server data; use `textContent` and the existing element helpers. Keep the file ≤ ~600 lines; split `listening-import.js` if needed.
- **`listening.css`:** a new hero, the YouTube card, pills, shelf, card and responsive blocks.
  - Tokens only (`--tct-*`, `--tct-clay-*`). The scrim uses token colours with alpha via `color-mix` (already used in the repo? check; otherwise a token overlay variable).
  - No hex.
  - Reduced-motion and forced-colors blocks (the hero scrim becomes a solid `Canvas` panel behind the text in forced colors).
  - Remove the dead CSS of the removed parts.

---

## 5. File scope
- **May edit:**
  - `Views/Study/Listening.cshtml`, `_ListeningHero.cshtml`, `_ListeningLessonCard.cshtml`, `_ListeningMyLessonCard.cshtml`;
  - delete `_ListeningLevelRow.cshtml` (and its references);
  - add `_ListeningYoutubeCard.cshtml`;
  - `wwwroot/js/listening-core.js`, `listening-index.js` (+ optional new `listening-import.js`);
  - `wwwroot/css/listening.css`;
  - `Services/ListeningService.cs` + `ViewModels/ListeningViewModels.cs` (additive, section 3);
  - listening tests (add; update only assertions about removed elements, never weaken behaviour checks);
  - docs.
- **Forbidden:**
  - the practice page and its JS, admin pages;
  - `StudyController` (no change needed);
  - models, migrations, `Program.cs`, appsettings, `.csproj`;
  - shared layouts and CSS (`_Layout*`, `site.css`, `study-kit.css`, tokens);
  - other owners' files;
  - new packages or CDNs.

---

## 6. Phases

### K0 — Baseline
1. Pull `master` per the prompt; branch `feature/listening-catalog-b`.
2. Build, run the full tests and the node tests; record the counts.
3. Resolve 0.4. List every id, hook and test that touches the catalog.
4. Before screenshots for guest, Standard and Premium at 1440×900, 1366×768 and 390×844 → `.tmp/listening-catalog-b/before/`.

### K1 — Logic (tests first)
Core helpers (section 4) + node tests. Hero state + hint in the service (section 3) + C# tests:
- the 4 hero modes;
- the hint texts;
- hidden empty bands;
- `?level=C2` selects `C1–C2`.

### K2 — Top row
The hero rebuild + `_ListeningYoutubeCard` with 3 states. Move the import JS. Render tests:
- the card per role;
- the form ids present only for Premium/Admin;
- the old bottom section is gone.

### K3 — Filters and shelves
Band pills, status pills, topic, search, URL sync, shelves vs grid, `Xem cả`, empty state, the "Bài nghe của tôi" shelf (D3), the card restyle, removal of `_ListeningLevelRow`.

### K4 — Import flow
D4 success insert, errors, 403 upgrade, double-submit guard, delete still works (modal), counts update (`Bài của tôi: n`, the shelf count).

Manual run with Premium:
- a valid captioned link (pick a real short English video with captions; record it) — **only if the network and caption service work locally**. Otherwise verify with a stubbed response in a JS test and record "live import not verified";
- an invalid link;
- a non-YouTube link.

### K5 — Responsive and accessibility
- All widths, 200% zoom, keyboard only (radiogroup, toggles, card links, the form, the menu, the modal).
- Screen-reader names; `aria-live` for import status and counts.
- Contrast script: text on the hero scrim, the yellow and purple chips, status chips.
- Reduced motion, forced colors.

### K6 — Mockup comparison + polish
- Side by side with B1. List the differences with the reasons. Fix spacing and alignment until the top row heights match and shelf headers align.
- After screenshots → `.tmp/listening-catalog-b/after/`.

### K7 — Docs, full gate, report
Tracker row, `known-issues.md` (if any), `bug-fix-log.md` (if bugs were found), plan section 12.

---

## 7. Gates (after every phase; fix and rerun the whole gate; max 3 attempts per item)

| Gate | Check |
| --- | --- |
| G1 | `dotnet build`, no new warnings in touched files |
| G2 | Listening tests |
| G3 | Full suite; counts ≥ K0 |
| G4 | Node tests; counts ≥ K0 |
| G5 | `has-pending-model-changes` → none; route list unchanged |
| G6 | Tokens grep (no hex/rgb in new CSS except forced-colors keywords); no inline style/script/handlers (the existing `--p` progress custom property pattern is allowed); `git diff --check`; encoding guard |
| G7 | File scope = section 5 |
| G8 | Render 1440×900, 1366×768, 1024, 768, 390, 200% zoom × guest/Standard/Premium; console and network clean |
| G9 | Mockup comparison with B1 |
| G10 | Behaviour checklist (section 8) |
| G11 | Accessibility (radiogroup, toggles, focus order, names, live regions, modal) |
| G12 | Scripted contrast ≥ 4.5:1 text, ≥ 3:1 UI |
| G13 | Regression: the practice page opens from cards and the hero; Home, Vocabulary, Sets; admin listening list |
| G14 | Copy exactly as this plan; no English UI labels; no `bi bi-` |
| G15 | Security: no `innerHTML` with server data; antiforgery on import/delete; URL validation unchanged server-side; safe image URLs only |

Never skip, weaken or delete a test. Never touch a forbidden file. Never call a skipped check passed.

## 8. Behaviour checklist
1. The first screen at 1440×900 shows the hero, the YouTube card, the filters and the first shelf header without scrolling.
2. Hero states for continue, start, all done and guest; buttons go to the right place; the hints are correct.
3. YouTube card per role. Import success inserts the card and updates the counts; errors are clear; 403 → upgrade link; no double submit.
4. `Xem tất cả` scrolls to "Bài nghe của tôi"; the shelf only exists when n ≥ 1; delete works and updates the counts.
5. Bands: empty ones hidden; `Tất cả` shows shelves (max 5); a band shows a grid with `Xem thêm bài`; `?level=` deep links work, including C2.
6. Status, topic and search combine; the empty state + `Xóa bộ lọc` resets everything and the URL.
7. Cards: link to practice, status chips, progress bars for in-progress lessons.
8. Phone: stacked top row, scrolling shelves and pills, no horizontal page scroll.

## 9. Risks

| Risk | Mitigation |
| --- | --- |
| Tests depend on removed ids | K0 inventory; update in the same phase |
| Hero photo makes text unreadable | Scrim + contrast script on the real thumbnails |
| Import moved breaks JS | Keep the ids and server endpoints; stubbed JS tests + a manual run |
| Too few lessons in a band | Bands hide when empty; shelves show what exists |

## 10. Definition of done
- [ ] Layout matches B1 for laptop; phone and tablet per 2.5–2.6; differences recorded.
- [ ] The YouTube import sits at the top for every role state; the old bottom section is removed.
- [ ] All existing catalog behaviour still works (search, filters, deep links, continue, import, delete).
- [ ] G1–G15 green per phase; counts ≥ K0; no forbidden file; no migration.
- [ ] Before/after screenshots; docs; section 12 filled.

## 11. Commits
Local commits, one per phase, via `tct-commit-message` (e.g. `feat(listening): move YouTube import into a top card`). No push, PR or merge.

## 12. Execution record
_(Filled by the agent: K0 inventory and verdicts, gate records, mockup differences, final summary.)_

### 12.1 K0 — baseline and inventory (2026-11-20)

**Baseline** (branch `feature/listening-catalog-b` from `master` 5e8e92b; `scripts/pull-local.ps1` ran clean, 456 releases checked, 0 record operations):
- Build: 0 errors. Full .NET suite, two groups with `xUnit.MaxParallelThreads=2` (the host reaps a single full run for low memory): **3761 passed, 0 failed, 58 skipped** (1000 in the Ai group + 2761 in the rest). Node: **589 passed, 0 failed**.
- Screenshots of the current page: `.tmp/listening-catalog-b/before/{guest,standard,premium}-{1440x900,1366x768,390x844}[-full].png`. No horizontal overflow at any size, no console errors.
- Current content: 22 public lessons (A2 1, B1 10, B2 6, C1 3, C2 2, A1 0).

**0.4 resolved**
1. Hooks that tests or scripts rely on and that must survive: `id="tlLevels"` + `role="radiogroup"` (ListeningIntegrationTests), `data-level-section`, `data-id`/`data-status` on public cards, `data-mine-id`, `data-tl-delete`, `id="tlImport"` (Premium only), `id="tlConfig"` with `shelfSize`/`gridSize`/`userKey`/`isAuthenticated`, `tlEmpty`, `tlClearFilters`, `tlTopic`, `tlStatuses`, `tlDeleteLesson`, one `<h1>`, no inline style/`onclick`. Tests that name removed or changed elements: `ListeningRedesignRenderTests` (hero states, level row with 7 radios, my-lessons tile, config sizes 4/8), `ListeningIntegrationTests.IndexPage_GuestSeesTheSignInNotice…` (guest hero copy) and `ListeningCatalogLevelTests` (only `data-level-section`, kept). No node test touches the DOM of the catalogue (`test_listening_catalog_filters.cjs` uses the pure core).
2. `initImport`: on success it shows `Đã thêm bài nghe.` and navigates to `/Home/Listening/Practice/{id}`; errors use `data.message` (default `Không thêm được bài nghe. Hãy kiểm tra video có phụ đề tiếng Anh rồi thử lại.`); 403 with `requiresUpgrade` adds a link `Nâng cấp Premium` to `/Account/Premium`. The create endpoint returns only `{ success, lessonId }` (no title, no card data) and `StudyController` is forbidden, so D4 reads the new card back from the server-rendered catalogue page (same-origin GET of `/Home/Listening`, one `li[data-mine-id]` adopted with DOMParser, no innerHTML); if that read fails the success state still appears without the card.
3. Upgrade URL `/Account/Premium`; login URL `/Account/Login?returnUrl=%2FHome%2FListening`.
4. `ThumbnailSrc` is the stored URL when it is https or a local path, else the YouTube thumbnail of a valid id, else null: always safe for an `<img src>`.

**Existing behaviours kept** (plan section 8 and the old contracts): the controller already sends every lesson and only pre-selects `level`/`topic` (`StudyController.Listening` untouched), the filters run in the browser.

### 12.2 K1 — logic (tests first)

**Done:**
- `listening-core.js`: `BANDS`, `bandOf`, `levelOfBand`, `bandsWithLessons`, `shelfSlice`, `heroHint`, and `filterLessons` accepts `band` next to `level`. Node tests `scripts/tests/test_listening_catalog_b.cjs` (7 tests, written first).
- C# (additive): `ListeningIndexViewModel.Bands/SelectedBand/HeroLesson/HeroMode/HeroHint`, `ListeningBands`, `ListeningHeroHint` (mirror of `heroHint`), and `ListeningService.ApplyHero` (no new query: it uses the cards, the progress rows and the continue lesson already loaded). Hero modes: `guest`, `continue`, `start`, `review`, `none`. Tests `ListeningCatalogBLogicTests` (26, including `?level=C2` → band `C1–C2`, hidden empty bands, the four hero modes and the hint texts).
- `StudyController` is untouched: it already sends every lesson and only pre-selects `level`/`topic`.

**Gate record (K1)**

| Gate | Result |
| --- | --- |
| G1 build | 0 errors |
| G2 listening tests | pass (inside the full run; 26 new) |
| G3 full suite | **3787 passed, 0 failed, 58 skipped** (1000 + 2787; K0 3761) |
| G4 node | **596 passed** (K0 589) |
| G5 | no model/migration/route file touched |
| G6 | `git diff --check` clean for the touched files (the only hit is the owner's unstaged `appsettings.Development.json`), no new UI yet |
| G7 | files: `ListeningService.cs`, `ListeningViewModels.cs`, `listening-core.js`, two new tests |
| G8–G15 | not applicable yet (no UI change in this phase) |
