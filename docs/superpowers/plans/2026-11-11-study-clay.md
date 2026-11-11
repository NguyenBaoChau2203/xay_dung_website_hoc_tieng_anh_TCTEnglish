# Study Clay adoption: Personal Sets + Practice (2026-11-11)

> **Tóm tắt cho Trọng (tiếng Việt).**
> - Chuyển toàn bộ giao diện Bộ thẻ và Luyện tập sang **Claymorphism** theo quyết định chung của nhóm (DESIGN.md, 08/11/2026). Dùng **đúng bộ token Clay của Châu** trong `tct-learning-ui.css`: nền lavender, khối bo 24px, nút/ô nhập bo 16px, bóng mềm ngoài + viền sáng trong, nút lún khi bấm.
> - Bỏ kiểu "cạnh dày nổi khối" của đợt trước.
> - Thêm khối tổng quan "Hôm nay bạn có N từ cần ôn" ở trang Bộ thẻ của tôi.
> - Không đổi chức năng, route, dữ liệu; không đụng file chung, header, sidebar.
> - Agent chạy liên tục C0 → C10, không hỏi lại. Mỗi phase phải qua đủ bộ kiểm tra G1–G14 (mục 7) mới commit và sang phase sau.
>
> Phần chỉ dẫn cho agent viết bằng tiếng Anh theo `AGENTS.md`.

- **Run mode:** continuous C0 → C10 with no questions to the owner. Every phase must pass the full phase gate (section 7.1) before its commit and before the next phase starts.
- Owner / page owner: Huỳnh Phú Trọng (Set/Card, Folder, study modes per `README.md`).
- Save as `docs/superpowers/plans/2026-11-11-study-clay.md`. Prompt: `.ai/prompts/claude-code-study-clay.md`.
- **Approved mockups (visual contract), approved by the owner on 2026-11-11:** 12 PNGs in the untracked folder `Claude outputs/clay-mockups/` at the repo root. Never commit them.

| Mockup | Page |
| --- | --- |
| `01-bo-the-cua-toi.png` | `/Sets` |
| `02-chi-tiet-bo-the.png` | `/Sets/{id}` |
| `03-chi-tiet-tu.png` | `/Sets/Cards/{id}` |
| `04-luyen-tap.png` | `/Practice` |
| `05-trac-nghiem-sai.png` | quiz, wrong answer |
| `06-hoc-chon-chang.png` | learn map |
| `07-ghep-the.png` | match |
| `08-lat-the.png` | flashcard |
| `09-kiem-tra.png` | test |
| `10-ket-qua.png` | summary |
| `11-mobile-chi-tiet-bo.png` | set detail at 390px |
| `12-mobile-trac-nghiem-dung.png` | quiz at 390px, correct |

- **The mockups are static HTML with copied values.** Implement them with tokens. The **Clay tokens of `tct-learning-ui.css` win over any pixel** in a mockup. Record every difference in section 11.
- **Rules to (re)read first:**
  - `AGENTS.md`;
  - `DESIGN.md`: "Visual direction", "Demo priority", "Claymorphism implementation";
  - `docs/design-system/team-ui-workflow.md`;
  - `.ai/prompts/adopt-claymorphism-for-current-ui-task.md` (the team's Clay adoption prompt; this plan is its concrete application to Trọng's screens);
  - the live gallery `docs/design-system/preview.html` (Clay section).
- **Reference implementation by Châu:** `TCTEnglish/wwwroot/css/goals.css` (canvas handling, Clay surfaces). Read it; do not copy its `!important`.
- **Prior plans (context and hooks):**
  - `docs/superpowers/plans/2026-11-11-study-ui-refresh.md`: its section 5.1 hook inventory and section 11 notes are still valid;
  - `2026-11-10-practice-modes.md`;
  - `2026-11-09-personal-sets-redesign.md`.
- **Expected baseline** (verify at C0):
  - local `master` contains PR #134 (`062851f`, "Merge pull request #134 … feature/study-ui-refresh") and PR #132 (Clay foundation, `e6521ad`);
  - the checked-out branch is `feature/study-ui-refresh` (`5e499b7`, merged);
  - untracked `Claude outputs/`; modified `TCTEnglish/appsettings.Development.json` (unrelated, never stage).

---

## 0. What changes and what does not

**Changes**
- `wwwroot/css/study-kit.css` (main work);
- `wwwroot/css/personal-sets.css`, `wwwroot/css/practice.css`;
- small markup changes:
  - the new overview block in `Views/Set/Index.cshtml`;
  - class additions where a section below says so;
- the tracker rows; the docs listed in section 9.

**Does not change**
- Controllers, services, view models, routes, APIs, migrations;
- all JS logic. JS edits are allowed **only** where section 5 lists them (class names only);
- `Program.cs`, appsettings, `.csproj`;
- `_Layout.cshtml`, `layout.css`, `site.css`, `tct-learning-ui.css`, `_PageHeader.cshtml`, `_StateMessage.cshtml`;
- header, sidebar, footer, the AI launcher (the shared shell needs its own review per DESIGN.md);
- other owners' pages, legacy `Views/Study/**`.

---

## 1. Decisions (fixed)

| ID | Decision |
| --- | --- |
| C-R1 | **Use only the shared Clay tokens for depth:** `--tct-clay-shadow`, `-quiet`, `-lift`, `-overlay`, `-button`, `-pressed`, `-field` (or their aliases `--tct-shadow-soft/-lift/-overlay` inside `.tct-theme`). Remove every local depth value: the pressed-edge `box-shadow: 0 Npx 0 …`, the `translateY` press and any other hand-made shadow. The only extra `box-shadow` parts allowed are **focus/selection rings** written as `0 0 0 Npx var(--tct-*-soft)`, appended to a Clay token. |
| C-R2 | **Radii come from tokens.** Inside `.tct-theme`, `--tct-radius-md` = 16px, `--tct-radius-lg` = 24px, `--tct-radius-xl` = 28px. Remove hard-coded radii (14px, 20px, 28px, 18px…) in owner CSS; map them to `md` (controls, options, tiles, small panels), `lg` (cards, panels, art boxes) or `xl` (hero, feedback sheet, flashcard, overview). `--tct-radius-sm` and `pill` stay. |
| C-R3 | **Canvas:** the Set and Practice pages paint the Clay canvas (`--tct-clay-canvas`) behind `.main`, the same way `goals.css` does, but **without `!important`** (section 3.1). If the selector cannot win without `!important`, use Châu's pattern exactly and record why. |
| C-R4 | **Controls keep explicit boundaries** (DESIGN.md "Forms"). Fields keep a `--tct-border-control` border and the shared focus outline. Selected, correct and wrong states keep a coloured **border plus icon and text**. Never use a shadow as the only signal. |
| C-R5 | **Buttons use the shared Clay buttons.** Delete the owner overrides that re-style `.btn`, `.btn-primary`, `.btn-outline-primary`, `.btn-outline-danger` inside `:is(.ts-page,.tp-page)`, so the `.tct-theme` rules from `tct-learning-ui.css` apply: raised `--tct-clay-shadow-button`, pressed `--tct-clay-shadow-pressed`, blue outline style. Keep only these owner rules: `.btn-lg` size, and the helper colour variants (`.k-btn--success`, `--danger`, `--soft`, `--mark-learning`, `--mark-known`), which take the Clay button/pressed shadows. |
| C-R6 | **Forced colours and reduced motion:** add an owner `@media (forced-colors: active)` block that removes `box-shadow` and sets `border-color: CanvasText` on every owner class that has Clay depth (`.k-card`, `.k-panel`, `.k-soft`, `.k-seg`, `.tp-option`, `.tp-tile`, `.tp-flashcard`, `.tp-step__node`, `.ts-overview`, …). Mirror the shared block. Keep all `prefers-reduced-motion` rules. |
| C-R7 | **Hues stay the six token pairs.** The tinted gradients use `color-mix()` between two tokens, with a plain-token fallback declared first. Record each use in section 11. |
| C-R8 | **Demo priority:** review the laptop/desktop first, at 1440×900 **and** 1366×768 (limited height), then tablet and phone. |

---

## 2. Scope

The same screens as the 2026-11-11 refresh:
- **Personal sets:** `/Sets`, `/Sets/{id}`, `/Sets/Cards/{id}`, `/Sets/{id}/Add`, `/Sets/{id}/Import`, `/Sets/Create`, `/Sets/{id}/Settings`, EditCard, `/s/{slug}`, the share and delete modals.
- **Practice:** `/Practice`, every `/Practice/{mode}` (learn, test, match, flashcard, quiz, typing, listening, cloze), the summary, `/Practice/HardWords`.
- **Every page:** loading, empty, error and success states.

---

## 3. Clay system in `study-kit.css`

### 3.1 Page canvas

Add the rule below. The radial glows are decorative backgrounds, not shadows.

```css
/* C-R3: Clay canvas behind Trọng's pages (pattern from goals.css, without !important) */
body:has(.tct-theme:is(.ts-page, .tp-page)),
.main:has(> .tct-theme:is(.ts-page, .tp-page)),
.main:has(.tct-theme:is(.ts-page, .tp-page)) {
    background-color: var(--tct-clay-canvas);
}
.main:has(.tct-theme:is(.ts-page, .tp-page)) {
    background-image:
        radial-gradient(circle at 92% 6%, color-mix(in srgb, var(--tct-accent) 13%, transparent), transparent 32%),
        radial-gradient(circle at 4% 96%, color-mix(in srgb, var(--tct-primary) 8%, transparent), transparent 40%);
    background-attachment: local;
}
```

- Check in DevTools that this wins over `layout.css`. If it does not, raise specificity, e.g. `html body:has(...) .main`. Use `!important` only as Châu did, and record why.
- Verify the sidebar and header stay white.
- Verify the canvas covers the full height on short pages (practice sessions) and long pages (set detail).

### 3.2 Hue variables

Keep the existing `--k-bg / --k-fg / --k-fill / --k-sq-bg / --k-sq-fg` blocks. **Delete `--k-edge`** and every use of it, replacing each use as in 3.3–3.6. Add one variable per hue:

```css
--k-tint: var(--k-bg);
--k-tint: color-mix(in srgb, var(--k-fill) 18%, var(--tct-surface));   /* hero/overview gradient start */
```

### 3.3 Surfaces (replace the old `.k-card`, `.k-panel`, `.k-soft`)

| Class | New rule |
| --- | --- |
| `.k-card` (interactive tiles: set tiles, mode cards, study tiles, skill cards) | `background: var(--tct-clay-surface)`; `border: 1px solid var(--tct-border)`; `border-radius: var(--tct-radius-lg)`; `box-shadow: var(--tct-clay-shadow)`; `transition: box-shadow var(--tct-dur-fast) var(--tct-ease)`. Hover / `:focus-within`: `box-shadow: var(--tct-clay-shadow-lift)`. **No transform.** |
| `.k-panel` (setup panel, stage card, word sections, question cards, missed list) | same, without hover |
| `.k-panel--quiet` (**new**; tables, long word lists, the hard-words table, test result list) | `box-shadow: var(--tct-clay-shadow-quiet)`. DESIGN.md: dense content uses quiet containers; do not emboss each row. |
| `.k-soft` (KPI tiles, stat tiles) | `background: var(--k-bg)`; `border-radius: var(--tct-radius-md)`; `box-shadow: var(--tct-clay-shadow-quiet)` |
| `.k-art-box` (mode art, set cover inside a tile, hero art inset) | `border-radius: var(--tct-radius-lg)`; `background: var(--k-bg)`; `box-shadow: var(--tct-clay-shadow-field)` (inset "pressed into clay" look) |
| `.k-pillow` (**new**; raised white block inside a coloured area: hero art, overview stats, chips) | `background: var(--tct-clay-surface)`; `border-radius: var(--tct-radius-md)`; `box-shadow: var(--tct-clay-shadow-quiet)` |

### 3.4 Buttons (C-R5)

- **Delete** from `study-kit.css`:
  - the `:is(.ts-page,.tp-page) .btn…` rules that set `box-shadow`, `transform`, `background`, `border` or `color` for `.btn`, `.btn-primary`, `.btn-outline-primary` and `.btn-outline-danger`;
  - their `:hover`, `:active`, `.btn-sm` and disabled variants.
- **Keep:** `.btn.btn-lg` (min-height 56px, `font-size: var(--tct-text-lg)`, `border-radius: var(--tct-radius-md)`) and `.tct-btn-icon` sizing.
- **Helper variants** (`.k-btn--success`, `.k-btn--danger`, `.k-btn--warning`):
  - keep their background and colour;
  - set `box-shadow: var(--tct-clay-shadow-button)`;
  - `:active` → `var(--tct-clay-shadow-pressed)`.
- **`.k-btn--soft`:** `background: var(--k-bg)`, `color: var(--k-fg)`, `border: 0`, `box-shadow: var(--tct-clay-shadow-quiet)`; `:active` → pressed.
- **`.k-btn--mark-learning` / `.k-btn--mark-known`:**
  - surface background with a 2px border in `--tct-warning` / `--tct-success`;
  - text in `--tct-warning-text` / `--tct-success-text`;
  - `box-shadow: var(--tct-clay-shadow-quiet)`; `:active` → pressed.
- **Contrast check** of the shared outline button on the Clay canvas and on tints. It is the gallery button, already measured by Châu; re-measure it on our tinted hero.

### 3.5 Segmented control, fields, pills

- **`.k-seg` track:** `background: color-mix(in srgb, var(--tct-primary) 8%, var(--tct-surface))`, fallback `var(--tct-primary-tint)`; `box-shadow: var(--tct-clay-shadow-field)`; padding 5px; radius pill (wrapped: `--tct-radius-lg`).
- **Selected item:** `background: var(--tct-surface)`; `box-shadow: var(--tct-clay-shadow-quiet)`; text `--tct-text`, weight 600. The selected state also keeps `aria-current` / `:checked` **and** a visible difference that does not rely on the shadow: the surface colour vs the tinted track, plus weight 600 vs 500. Always give the selected item a 1px `--tct-border` border, so its edge stays visible without the shadow.
- **Search field (set toolbar):** `background: var(--tct-surface)`; `border: 1.5px solid var(--tct-border-control)`; `border-radius: var(--tct-radius-md)`; `box-shadow: var(--tct-clay-shadow-field)`. Shared focus outline. This **replaces** the grey borderless search box of the refresh.
- **Filter pills (selects):** keep the pill shape and the `--tct-border-control` border; add `box-shadow: var(--tct-clay-shadow-field)`. Active filter (`.ts-filter--active`): `--tct-primary` border, `--tct-primary-tint` background, `box-shadow: var(--tct-clay-shadow-quiet)`.
- **Other form controls on the Set pages** (`form-control`, `form-select`): remove any owner override of `box-shadow` / `border-radius`, so the shared Clay field style applies.
- **Switches** (`.form-switch .form-check-input`, the card-detail suspend switch): track `box-shadow: var(--tct-clay-shadow-field)`. Keep the existing border/colours.
- **`.k-kbd`:** `box-shadow: var(--tct-clay-shadow-field)`; background `--tct-primary-tint`.

### 3.6 Progress

- **Track** (`.k-progress`, `.tp-progress`, `.k-progress--split`, `.ts-progress`): `background: color-mix(in srgb, var(--tct-primary) 10%, var(--tct-surface))`, fallback `var(--tct-disabled-bg)`; `box-shadow: var(--tct-clay-shadow-field)`.
- **Session bar:** height 16px.
- **Fill:** colour as now, `border-radius: pill`, **no shadow** (C-R1). Remove the old inset fill shadow.

### 3.7 Forced colours (C-R6)

Write one `@media (forced-colors: active)` block that lists every owner class with Clay depth. It sets `box-shadow: none; border: 1px solid CanvasText`, and for selected/correct/wrong states `border-width: 2px` so state stays visible.

---

## 4. Page specifications

Keep every id, `data-*` attribute, JS-queried class and test-asserted string (hook inventory: the refresh plan 5.1; re-run it at C0).

### 4.1 `/Sets` (mockup 01): `Index.cshtml`, `_SetTile.cshtml`, `personal-sets.css`

**Page header**
- `_PageHeader` actions become **only** "Tạo bộ thẻ" (primary). "Luyện tập" moves into the overview block.
- `PracticeIntegrationTests` asserts `href="/Practice"` on the list page. The overview "Mở Luyện tập" link keeps it.
- When the overview is not rendered (empty state), keep the header action "Luyện tập", so the link always exists.
- Run the test to confirm.

**Overview block `.ts-overview` (new markup, after the header, before the chips)**
- **Render it** only when `!Model.IsEmpty` **and** `Model.Filter` is null/empty (the "Tất cả" view).
- **Values, computed in the view from existing data** (no service change):

```csharp
var allTiles = Model.Own.Concat(Model.Subscribed).Concat(Model.Shared).DistinctBy(t => t.SetId).ToList();
var totalSets = allTiles.Count;
var totalWords = allTiles.Sum(t => t.WordCount);
var dueWords = allTiles.Sum(t => t.DueCount);
var dueSets = allTiles.Count(t => t.DueCount > 0);
```

- **Markup:**

```html
<section class="ts-overview k-panel h-primary ts-hue-primary" aria-labelledby="ts-overview-title">
  <div class="ts-overview__main">
    <h2 id="ts-overview-title" class="ts-overview__title">…</h2>
    <p class="ts-overview__text">…</p>
    <dl class="ts-overview__stats">
      <div class="ts-overview__stat k-pillow ts-hue-primary"><dt>bộ thẻ</dt><dd>@totalSets</dd></div>
      <div class="ts-overview__stat k-pillow ts-hue-success"><dt>từ đã lưu</dt><dd>@totalWords</dd></div>
      <div class="ts-overview__stat k-pillow ts-hue-warning"><dt>từ đến hạn</dt><dd>@dueWords</dd></div>
    </dl>
    <div class="ts-overview__actions">…</div>
  </div>
  <div class="ts-overview__art" aria-hidden="true"><partial name="~/Views/Practice/_ModeArt.cshtml" model="@("flashcard")" /></div>
</section>
```

- **Text when `dueWords > 0`:**
  - title "Hôm nay bạn có @dueWords từ cần ôn";
  - text "Nằm trong @dueSets bộ thẻ. Ôn đều mỗi ngày để giữ chuỗi học.";
  - actions:
    - primary `<a class="btn btn-primary" href="/Practice/flashcard?scope=mine&source=due&n=0&srs=1&from=hub"><i class="fa-solid fa-bolt" aria-hidden="true"></i>Luyện từ đến hạn</a>`;
    - outline `<a class="btn btn-outline-primary" href="/Practice"><i class="fa-solid fa-dumbbell" aria-hidden="true"></i>Mở Luyện tập</a>`.
  - **Before using that URL**, check `PracticeQuery` / `PracticeModes` accept `scope=mine`, `source=due`, `n=0` for flashcard and `srs=1`. If any value is not accepted, use the nearest accepted one, and record it.
- **Text when `dueWords == 0`:** title "Bạn đã ôn xong các từ đến hạn"; text "Có thể luyện thêm để nhớ lâu hơn."; only the outline "Mở Luyện tập" (as primary).
- **Stats layout:** each stat shows the `dd` number first (`display:flex; flex-direction:column-reverse`). Number `--tct-text-2xl` weight 700 in `--k-fg`; label `--tct-text-sm` weight 600 `--tct-text-secondary`; min-width 110px; padding 10px 16px.
- **Styles:**
  - `.ts-overview`: radius `--tct-radius-xl`; padding `--tct-space-lg` `--tct-space-xl`; background `linear-gradient(135deg, var(--k-tint), var(--tct-surface) 60%)`; flex row, gap `--tct-space-xl`.
  - Art: 220×130 at ≥992px, hidden below 768px.
  - Stats wrap.
  - At ≤576px, the actions stack full width.

**Relation chips (`.k-seg`):** Clay track (3.5). Spacing 18px above the groups.

**Tiles (`_SetTile`, `.ts-tile`):**
- `.k-card`;
- the cover `.ts-tile-art` becomes an inset art box:
  - margin 10px 10px 0, radius `--tct-radius-lg`, `box-shadow: var(--tct-clay-shadow-field)`;
  - the two decorative circles stay;
  - the visibility icon in a 30px `.k-pillow` circle;
- body as now;
- `.ts-progress` per 3.6;
- the quick "Ôn" button keeps the shared `.btn-primary`.

**Add tile `.ts-tile-add`:**
- 2px dashed `--tct-border-control` border at 60% opacity (use `color-mix(... 60%, transparent)`);
- background `color-mix(in srgb, var(--tct-surface) 45%, transparent)`;
- radius `--tct-radius-lg`;
- icon square `.k-pillow`.

**Group titles** unchanged.

### 4.2 `/Sets/{id}` (mockups 02, 11): `Detail.cshtml`, `personal-sets.css`

- **Hero `.ts-hero`:**
  - `background: linear-gradient(135deg, var(--k-tint), var(--tct-surface) 78%)`; radius `--tct-radius-xl`; `box-shadow: var(--tct-clay-shadow)`;
  - decorative circle kept (surface, opacity .4).
  - **Art `.ts-hero-art`:** `.k-pillow` look with radius `--tct-radius-lg` and `box-shadow: var(--tct-clay-shadow)` (raised white block); inner tint circle kept; term colour `--k-fg`.
  - **Buttons:** shared Clay (`.btn-primary.btn-lg`, `.btn-outline-primary.btn-lg`, the menu toggle `.btn-outline-primary.tct-btn-icon` 56px).
- **Study tiles `.ts-study-tile`:**
  - `.k-card`;
  - art in `.k-art-box` (field inset);
  - suggestion badge, name and note unchanged.
- **More link:** unchanged.
- **Toolbar:**
  - the search field per 3.5 (white with border-control and field inset, **not** grey borderless);
  - filters per 3.5;
  - "Chọn" = `.btn-outline-primary` shared;
  - the list/grid toggle = `.k-seg`.
- **Word table wrapper `.ts-list-view`:**
  - `.k-panel.k-panel--quiet`;
  - header row `background: color-mix(in srgb, var(--tct-primary) 3%, var(--tct-surface))`;
  - row hover `--tct-primary-tint`;
  - **no per-row shadows**;
  - speak buttons = `.k-btn--soft` 36px;
  - CEFR tag unchanged.
- **Grid view cards `.ts-word-card`:** `.k-card`; art area `.k-art-box`.
- **Bulk bar:** `.k-panel` with `box-shadow: var(--tct-clay-shadow-overlay)` (it floats above content).
- **Mobile (mockup 11):** the same rules; hero stacks (art full width 100px tall); actions grid `1fr auto auto`.

### 4.3 `/Sets/Cards/{id}` (mockup 03): `CardDetail.cshtml`, `personal-sets.css`

- **Hero card `.ts-word-hero`:** `.k-panel`, radius `--tct-radius-xl`; the 6px primary top bar is **removed**. Clay depth replaces it; record it.
  - Image/emoji box `.k-art-box` (150px).
  - Pronunciation chips: `.k-pillow`-like with `box-shadow: var(--tct-clay-shadow-field)` and a white background.
  - Speak buttons `.k-btn--soft` 36px.
- **Actions:** shared Clay buttons. The overflow menu toggle is `.tct-btn-icon`.
- **Word sections** (`.ts-word-section`): `.k-panel`.
  - **Examples `.ts-example`:**
    - remove the 4px left border;
    - each example becomes a field-inset block: background `color-mix(in srgb, var(--tct-primary) 3%, var(--tct-surface))`, radius `--tct-radius-lg`, padding 14px 16px, `box-shadow: var(--tct-clay-shadow-field)`;
    - highlight unchanged;
    - Vietnamese line muted.
  - **Chips `.ts-chip`:**
    - remove the dashed border; `background: var(--tct-surface)`; `box-shadow: var(--tct-clay-shadow-quiet)`;
    - **plus** a 1px `--tct-border` border, so chip edges stay visible in forced colours and low-contrast screens;
    - `.ts-chip--link` text `--tct-primary-strong`;
    - `.ts-chip--add` keeps "+ ".
- **Progress card:**
  - `.k-panel`; the 2×2 stats as `.k-soft` tiles;
  - the suspend switch row: track per 3.5;
  - keep `role="switch"` / `aria-checked` / `aria-pressed` sync from the refresh.

### 4.4 Other Set pages (no mockup)

AddCards, Import, CreateSet, Settings, EditCard, Shared, `_ShareSetModal`, delete modals:
- Panels → `.k-panel`. Dense lists (composer rows, import preview table) → `.k-panel--quiet`.
- Composer row number circle → `.k-pillow` circle.
- Remove owner `box-shadow` and radius overrides on `form-control` / `form-select` (shared Clay fields apply).
- Segmented choices → `.k-seg`.
- Modals: shared (`.modal-content` already uses Clay overlay in `.tct-theme`); remove any owner override.
- The `/s/{slug}` hero uses the 4.2 hero rules.

### 4.5 `/Practice` (mockup 04): `Index.cshtml`, `_ModeCard.cshtml`, `practice.css`

- **Big mode cards `.tp-mode-card--big`:**
  - `.k-card`, 2px transparent border;
  - art box `.k-art-box`.
  - **Checked** (`:has(input:checked)`):
    - `border-color: var(--k-fill)`, `background: var(--k-bg)`;
    - `box-shadow: var(--tct-clay-shadow-lift)`;
    - the art box becomes `background: var(--tct-surface)` with `box-shadow: var(--tct-clay-shadow-quiet)`;
    - check badge: `--k-fill` circle, `box-shadow: var(--tct-clay-shadow-button)`.
- **Small skill cards:** `.k-card`; icon square radius `--tct-radius-md`, `box-shadow: var(--tct-clay-shadow-button)`.
- **Setup panel `.tp-setup`:** `.k-panel`; select = shared Clay field; count/source/ask/types `.k-seg`; switch per 3.5; "Bắt đầu luyện" shared `.btn-primary.btn-lg`.
- **Hard-words link:** `.k-card`; icon square `--tct-danger-soft` with `box-shadow: var(--tct-clay-shadow-quiet)`.

### 4.6 Session shell, drills, feedback (mockups 05, 12): `practice.css`, `practice-ui.js`

- **Bar:**
  - exit = `.btn-outline-primary.tct-btn-icon` → change to a white Clay icon button: add class `k-btn--icon-surface` = surface background, `--tct-text-secondary` icon, no border, `box-shadow: var(--tct-clay-shadow-quiet)`, `:active` pressed. Keep `#tpExit` and its `aria-label`.
  - progress per 3.6 (16px);
  - counter unchanged;
  - slot buttons (speak) = same surface icon button.
- **Term area:** unchanged typography. Speak button `.k-btn--soft`.
- **Options `.tp-option`:**
  - surface; 2px **transparent** border; radius `--tct-radius-md`; `box-shadow: var(--tct-clay-shadow)`; min-height 68px;
  - key badge = `.k-kbd` style (field inset).
  - Hover: `box-shadow: var(--tct-clay-shadow-lift)`, border `color-mix(in srgb, var(--k-fill) 40%, transparent)`.
  - **Correct:** border `--tct-success`, background `--tct-success-soft`, text `--tct-success-text`, `box-shadow: var(--tct-clay-shadow-pressed), 0 0 0 4px var(--tct-success-soft)`, icon `fa-circle-check`.
  - **Wrong:** the same with danger.
  - **Selected (test mode, before submit):** border `--tct-primary`, background `--tct-primary-soft`, `box-shadow: var(--tct-clay-shadow-pressed)`.
  - **Others after answering:** opacity .55 (`.tp-options--answered`).
- **Feedback `.tp-feedback`:**
  - **becomes a floating Clay card** instead of a full-bleed bottom sheet;
  - `position: sticky; bottom: var(--tct-space-md)`; margin-top `--tct-space-xl`;
  - **no negative inline margins**; radius `--tct-radius-xl`; `box-shadow: var(--tct-clay-shadow-lift)`;
  - background by outcome (`--tct-success-soft` / `--tct-warning-soft` / `--tct-danger-soft`), content unchanged;
  - the continue button uses `.k-btn--success|warning|danger` (Clay button shadow).
  - Keep the right padding that clears the AI launcher at ≤768px, and re-measure.
- **Typing:**
  - input = shared Clay field, 56px, radius `--tct-radius-md`;
  - hint button `.k-btn--soft` (accent);
  - diff rows inside a `.k-panel--quiet`.
- **Listening:** play button 96px circle, `box-shadow: var(--tct-clay-shadow-button)`; "Nghe chậm" `.k-btn--soft`.
- **Cloze:** sentence box `.k-art-box` style (field inset, neutral background `color-mix(in srgb, var(--tct-primary) 4%, var(--tct-surface))`).
- **Key hints:** `.k-kbd`.

### 4.7 Summary (mockup 10)

- **Ring `.tp-ring`:** outer `box-shadow: var(--tct-clay-shadow-lift)`; inner circle `background: var(--tct-surface)` with `box-shadow: var(--tct-clay-shadow-field)`.
- **Stars:** unchanged (keep the text-stroke).
- **KPIs `.k-soft`.** Missed list `.k-panel`. Buttons shared.

### 4.8 Flashcard (mockup 08)

- **Card `.tp-flashcard`:**
  - radius `--tct-radius-xl`, 1px `--tct-border`, `box-shadow: var(--tct-clay-shadow-lift)`;
  - **remove the "stack" pseudo-element**; back face `--k-bg`.
- **Direction toggle `.k-seg`;** shuffle shared outline button.
- **Mark buttons:** `.k-btn--mark-learning` / `-known` (3.4); prev/next = `k-btn--icon-surface` 56px.
- **Count chips:** badges unchanged.

### 4.9 Match (mockup 07)

- **Tiles `.tp-tile`:** surface, 2px transparent border, radius `--tct-radius-md`, `box-shadow: var(--tct-clay-shadow)`; hover lift.
- **Selected:** `--tct-primary` border, `--tct-primary-soft` background, `box-shadow: var(--tct-clay-shadow-pressed)`, **no transform**.
- **Matched slot:**
  - 2px dashed `color-mix(in srgb, var(--tct-border-control) 60%, transparent)` border;
  - background `color-mix(in srgb, var(--tct-surface) 35%, transparent)`;
  - `box-shadow: var(--tct-clay-shadow-field)`.
- **Wrong:** danger border/background, `box-shadow: var(--tct-clay-shadow-pressed)` + wiggle; the "+1 giây" pill gets `box-shadow: var(--tct-clay-shadow-button)`.
- **Timer and badges:** unchanged.

### 4.10 Test (mockup 09)

- **Jump buttons:** no border, `.k-pillow`-like (`box-shadow: var(--tct-clay-shadow-quiet)`).
  - Answered: `background: var(--tct-primary-soft)`, `color: var(--tct-primary-strong)`, `box-shadow: var(--tct-clay-shadow-pressed)`. Keep whatever text/ARIA answered signal `practice-test.js` already sets; the colour change, not the shadow, is the visual signal.
  - Current: 2px `--tct-primary` border.
- **Question cards `.k-panel`;** number circle `box-shadow: var(--tct-clay-shadow-button)`.
- **Sticky submit bar:** background `color-mix(in srgb, var(--tct-clay-canvas) 96%, transparent)`, top border `--tct-border`; progress per 3.6.
- **Result list:** `.k-panel--quiet` items.

### 4.11 Learn (mockup 06)

- **Stage nodes `.tp-step__node`** (Clay balls):
  - done: `background: var(--tct-success)`, `box-shadow: var(--tct-clay-shadow-button)`;
  - current: `background: var(--tct-primary)`, `box-shadow: var(--tct-clay-shadow-button), 0 0 0 10px var(--tct-primary-soft)`;
  - locked: `background: color-mix(in srgb, var(--tct-primary) 10%, var(--tct-surface))`, icon `--tct-disabled-text`, `box-shadow: var(--tct-clay-shadow)`;
  - trophy: `background: var(--tct-accent)`, `box-shadow: var(--tct-clay-shadow-button)`;
  - remove every old `0 6px 0` edge.
- **Current-stage label pill:** `.k-pillow` (no border).
- **Stage card `.k-panel`.** Word chips: `.k-pillow` (new words keep `--tct-warning-soft`). Info line in a `--tct-primary-tint` block with field inset.
- **Split progress** per 3.6.

### 4.12 Hard words

- Table in `.k-panel--quiet`.
- Filter select: shared field.
- Actions: shared buttons.

---

## 5. JavaScript (class names only)

Allowed edits:

| File | Edit |
| --- | --- |
| `practice-ui.js` | Add `k-btn--icon-surface` to the exit button if the JS builds it (otherwise do it in `Session.cshtml`). Change the feedback "continue" button classes, if they still use removed classes. |
| `practice-flashcard.js` | Prev/next buttons → `k-btn--icon-surface`. |
| `practice-match.js`, `practice-learn.js`, `practice-test.js`, `practice-drill.js` | Only if they set inline classes that this plan removes (e.g. `k-edge-*`). Replace them with the new ones. |

- No logic changes; `practice-core.js` untouched.
- Grep for every class removed in section 3 (`--k-edge`, any `k-*` class you delete) across `wwwroot/js`, `Views` and `scripts/tests`, and fix all references.

---

## 6. Files

| File | Change |
| --- | --- |
| `wwwroot/css/study-kit.css` | Section 3 (main rewrite of depth/radius/buttons; canvas; forced colours) |
| `wwwroot/css/personal-sets.css` | Sections 4.1–4.4; remove local shadows and radii |
| `wwwroot/css/practice.css` | Sections 4.5–4.12; remove local shadows and radii |
| `Views/Set/Index.cshtml` | Overview block + header action change (4.1) |
| `Views/Set/*.cshtml`, `Views/Practice/*.cshtml` | Class additions only (`k-panel--quiet`, `k-pillow`, `k-btn--icon-surface`) where the spec says |
| JS files in section 5 | Class names only |
| `docs/design-system/team-ui-workflow.md` §6 | Update the "Bộ thẻ cá nhân" and "Luyện tập" rows: "Đã chuyển Clay 2026-11-11 (token Clay dùng chung)" |
| `.ai/context/known-issues.md` | Only if a limitation is found |

---

## 7. Phases and the phase gate

**Run mode: continuous.** Go from C0 to C10 in one run without stopping to ask the owner. A phase ends only when its gate (7.1) is fully green; then write its gate record (7.3), commit, and start the next phase immediately.

| Phase | Work | Mockups |
| --- | --- | --- |
| C0 | Baseline. Check branch/HEAD/tree. Run `git switch master`. Verify it contains `062851f`. Create `feature/study-clay`. Build. Run the full Release tests and all node tests, and record the counts and the warning count. Save this plan. Open all 12 mockups and the gallery Clay section. Re-run and save the hook inventory. Start the app (`pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`). Take **before** screenshots of every in-scope page at 1440×900, 1366×768 and 390. | all |
| C1 | `study-kit.css`: section 3 (canvas, hues, surfaces, buttons, seg/fields, progress, forced colours). Fix every reference to the removed classes. | gallery |
| C2 | `/Sets` (4.1) incl. the overview block | 01 |
| C3 | `/Sets/{id}` (4.2) | 02, 11 |
| C4 | `/Sets/Cards/{id}` (4.3) | 03 |
| C5 | Other Set pages and modals (4.4) | — |
| C6 | Practice hub (4.5) | 04 |
| C7 | Session shell, drills, feedback, summary (4.6, 4.7) | 05, 10, 12 |
| C8 | Flashcard, match (4.8, 4.9) | 07, 08 |
| C9 | Test, learn, hard words (4.10–4.12) | 06, 09 |
| C10 | Full gate on **every** page and mode; docs; section 11; final self-review against AGENTS.md, DESIGN.md Clay rules and section 10 | all |

### 7.1 Phase gate (every phase, every item, in order)

| # | Check | Command / method | Pass when |
| --- | --- | --- | --- |
| G1 | Build | `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` | 0 errors; warnings ≤ C0 |
| G2 | Area tests | `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~PersonalSet\|FullyQualifiedName~Practice\|FullyQualifiedName~CriticalFlow"` | 0 failed; count ≥ C0 |
| G3 | Full tests | `dotnet test TCTEnglish.Tests -c Release` | 0 failed; passed ≥ C0; skipped = C0 (explain any change) |
| G4 | Node tests | `node --test scripts/tests/*.cjs` | 0 failed; count ≥ C0 |
| G5 | Hooks | re-run the C0 hook grep and diff it | nothing removed or renamed |
| G6 | Tokens-only + diff | `git diff --check`, plus the check below (must print nothing) | clean |
| G7 | Scope | `git diff --name-only <previous commit>` | only section 6 files |
| G8 | Render | each touched page at **1440×900, 1366×768, 1024, 768, 390, 320** and 200% zoom; screenshots to `.tmp/study-clay/<phase>/`; read the console and network | no horizontal scroll, clipping or overlap; no console errors; no failed CSS/JS; the canvas covers the full page; header and sidebar unchanged |
| G9 | Mockup match | 1440 and 390 screenshots side by side with the phase mockup(s) | matches, except the recorded differences |
| G10 | Behaviour | the phase checklist (7.4) with mouse **and** keyboard only | all items work as at C0 |
| G11 | Accessibility | Tab through every control: visible focus; one `<h1>`; icon buttons labelled; live regions announce; selected/correct/wrong visible **without shadows** (emulate `forced-colors: active` in DevTools); reduced motion removes motion | all pass |
| G12 | Contrast | Script the computed colours of the text and control pairs on the touched pages. It must include: text on the canvas; text on the hero/overview gradient (both ends); `--k-fg` on `--k-bg` for six hues; the selected seg item vs track; the outline button on the tinted hero; option states; muted text on the field-inset blocks. | text ≥ 4.5:1 (large ≥ 3:1); boundaries/icons ≥ 3:1 |
| G13 | Regression | open `/Home/Study/{id}`, `/Home/QuizMode/{id}`, `/Home/Index` or the dashboard, `/Vocabulary`, `/Goals` | unchanged from C0 screenshots; the canvas rule does not leak to them |
| G14 | Clay conformance | the grep below | every `box-shadow` in the owner CSS uses a `var(--tct-clay-*)` / `var(--tct-shadow-*)` token, or `none`, or a token ring appended to one; no `translateY` on `:active`; no hard-coded radius px except `50%` and the documented pill |

**G6 (tokens-only):**

```powershell
Select-String -Path TCTEnglish/wwwroot/css/study-kit.css,TCTEnglish/wwwroot/css/personal-sets.css,TCTEnglish/wwwroot/css/practice.css -Pattern '#[0-9a-fA-F]{3,8}\b|rgba?\(|:root|!important|font-weight:\s*(8|9)00'
```

**G14 (Clay conformance):**

```powershell
$css = 'TCTEnglish/wwwroot/css/study-kit.css','TCTEnglish/wwwroot/css/personal-sets.css','TCTEnglish/wwwroot/css/practice.css'
Select-String -Path $css -Pattern 'box-shadow:' | Where-Object { $_.Line -notmatch 'var\(--tct-(clay-|shadow-)|box-shadow:\s*none' }
Select-String -Path $css -Pattern 'box-shadow:\s*0 \d+px 0|:active[^{]*\{[^}]*translateY'
Select-String -Path $css -Pattern 'border-radius:\s*\d+px'
```

Each line printed must be fixed or justified in section 11. A token-coloured ring such as `0 0 0 10px var(--tct-primary-soft)` after a Clay token is allowed.

C0 runs G1, G3, G4 and G8 (before-screenshots), and records the baselines. C10 runs the full gate on every page.

### 7.2 When the gate fails

- Fix the cause, then re-run the **whole** gate.
- Up to 3 attempts per failing item. Then:
  - **(a) Caused by this phase:** revert that element to its C0 look, mark it "deferred" with the reason, and re-run the gate.
  - **(b) Outside scope** (an existing bug, or the app/browser unavailable): record the evidence and mark it "blocked". Continue only when every in-scope item is green.
- Never skip, weaken or delete a test.
- Never edit a forbidden file.
- Never record a skipped or blocked check as passed.
- **Stop the run** only if:
  - a green build or tests needs a forbidden file; or
  - the base state differs from this plan in a way that makes it unsafe.
  Finish the current gate record and the report, then stop.
- If the session is interrupted, resume at the first phase without a gate record in section 11.

### 7.3 Gate record (write into section 11 before each commit)

```text
### Ck — <name>
- Commit: <hash>
- G1 build: pass (warnings n, C0 n)
- G2 area: passed x / failed 0 / skipped y
- G3 full: passed x / failed 0 / skipped y (C0 …)
- G4 node: passed x / failed 0
- G5 hooks: unchanged (n)
- G6 tokens/diff: clean
- G7 scope: files …
- G8 render: pages × sizes, .tmp/study-clay/Ck/
- G9 mockup: differences …
- G10 behaviour: items 1..n pass
- G11 a11y (incl. forced colours): …
- G12 contrast: pairs + ratios
- G13 regression: pass
- G14 clay grep: clean / justified lines …
- Attempts / deferred / blocked: …
```

### 7.4 Behaviour checklists (G10)

| Phase | Items |
| --- | --- |
| C1 | Every in-scope page loads; Bootstrap dropdowns, modals and toasts open and close; buttons work with mouse, Enter and Space; hover/pressed states visible; legacy pages are unaffected (canvas rule scoped) |
| C2 | Overview numbers equal the sums of the tiles. The due branch and the zero-due branch both render (use a seeded user with due words, then mark everything as learned or use a user with none). "Luyện từ đến hạn" opens the practice flashcard with the due words. "Mở Luyện tập" opens `/Practice`. The overview is hidden on the "Của tôi", "Đang theo học" and "Được chia sẻ" tabs and in the empty state; the empty state still shows a `/Practice` link. Tabs switch. Tile link, quick "Ôn", create link. |
| C3 | Search (type, clear); each filter and the active styling; sort; list/grid; selection and every bulk action (cancel and confirm); load more; speak; suggestion badge; study tile URLs equal C0; actions menu (share on/off/copy/regenerate, settings, delete); "Theo học" as a non-owner; "Thêm từ"; 390 layout |
| C4 | Speak US/UK; "Học từ này" / "Gõ từ này" URLs; "Sửa"; overflow menu → delete (cancel/confirm); suspend switch on → reload shows on → off; synonym links and "+" add links |
| C5 | AddCards add/save/validation and AI suggestion (stub); Import paste/CSV preview + import; Create and Settings save/validation; EditCard save; Shared page as anonymous (follow/clone prompts); share modal |
| C6 | Mode cards select (mouse and arrow keys); count reset per mode; test types (the last one cannot be unchecked) and ask; source with due count; switch; summary line; "Bắt đầu luyện" URL equals C0; remembered setup after reload; hard-words link |
| C7 | Quiz/typing/listening/cloze: right, wrong, close; keys 1–4/A–D, R, Enter; hint, diff, "Không nhớ"; listening auto-speaks once and "Nghe chậm"; cloze empty state; feedback card focus moves to "Tiếp tục"; the card stays clear of the AI launcher at 390 and 320 and the sticky card does not cover the options at 1366×768; exit modal; summary buttons; "Tính vào ôn tập" on changes a card's status, off does not |
| C8 | Flashcard: flip (click, Space, Enter), ←/→, 1/2, R, swipe; direction; shuffle; counts; end screen. Match: start, timer, matched slot, "+1 giây", board 2, result, "Kỷ lục mới!" |
| C9 | Test: every type, jump grid, unanswered-confirm, submit, "Chỉ câu sai", "Làm lại câu sai", draft restore. Learn: map states, start stage, miss re-queue, round end, resume, completion links. Hard words: filter, table, both action URLs, empty state. |
| C10 | All of the above end to end on a fresh browser profile, at 1366×768 first |

---

## 8. Verification summary (C10)

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/*.cjs
git diff --check
# + G6 and G14 greps
```

- **Browser:** `tct-ui-review` + `team-ui-workflow.md` §7, laptop first (1440×900, 1366×768), then 1024 / 768 / 390 / 320 and 200% zoom.
- **Compare** every page with its mockup and with the gallery Clay section.
- **Record** the contrast and forced-colour results.
- Screenshots stay in ignored `.tmp/study-clay/`.

## 9. Docs

- `team-ui-workflow.md` §6: update the two rows.
- This plan's section 11.
- `.ai/context/known-issues.md`: only if needed.

## 10. Definition of done

- [ ] Every in-scope page uses the shared Clay language (canvas, Clay surfaces, Clay buttons, field insets), matches its mockup within the rules, and keeps its behaviour.
- [ ] No pressed-edge shadows, `translateY` presses or local shadow/radius values remain in owner CSS (G14 clean or justified).
- [ ] No route, API, service, view model, migration, `practice-core.js` or shared-file change; JS edits are class names only.
- [ ] All tests green at or above C0; hooks intact.
- [ ] Accessibility floor verified, including forced colours and reduced motion; contrast recorded.
- [ ] Legacy and other owners' pages are unchanged.
- [ ] The gate records for C0–C10 are complete; the tracker rows are updated; the Vietnamese report separates verified from not verified.

## 11. Execution notes (agent fills in; one gate record per phase)

- Branch / HEAD at C0: `feature/study-clay` created from local `master` = `062851f` (contains PR #134 and PR #132 `e6521ad`). Working tree: `appsettings.Development.json` modified (unrelated, never staged), `Claude outputs/` untracked.
- Baseline counts (tests / node / warnings):
  - G1 `dotnet build TCTEnglish/TCTEnglish.csproj -c Release`: 0 errors, **43 warnings**.
  - G2 area filter (`PersonalSet|Practice|CriticalFlow`): **108 passed**, 0 failed, 0 skipped.
  - G3 full `dotnet test TCTEnglish.Tests -c Release`: **1328 passed**, 0 failed, **22 skipped** (all MariaDB-only tests), 1350 total.
  - G4 `node --test scripts/tests/*.cjs` (files passed explicitly on PowerShell): **205 passed**, 0 failed.
- Hook inventory: 457 entries (ids 143, `data-*` 44, aria/role 114, JS selectors 93, JS classes 57, `dataset` 6) from `Views/Set/**`, `Views/Practice/**`, `wwwroot/js/{practice-*,set-*,personal-sets-common}.js`. Generated by a scratch script kept outside the repo; G5 re-runs it and reports any removed entry.
- Canvas selector outcome (did it win without `!important`?): **Yes.** `.main:has(.tct-theme:is(.ts-page, .tp-page))` (specificity 0,3,0) beats `layout.css` `.main` (0,1,0), and `body:has(...)` beats `body`. Computed `.main` background is `rgb(240, 243, 255)` = `--tct-clay-canvas` on all Set/Practice pages. Only `.main` and `body` were needed; the plan's third selector `.main:has(> ...)` is redundant and was dropped. Legacy pages keep `rgb(248, 250, 252)` (verified on dashboard, Study, QuizMode, Vocabulary).
- `color-mix` uses (each has a plain-token fallback declared first, except the two glows and the transparent mixes):
  - `study-kit.css`: `--k-tint` in six hues (`--k-fill` 18% into `--tct-surface`, fallback `--k-bg`); `.k-seg` track (`--tct-primary` 8% into surface, fallback `--tct-primary-tint`); `.k-progress` track (primary 10%, fallback `--tct-disabled-bg`); `--k-bg` of the accent hue (accent 30%, unchanged); `.btn .k-kbd` translucent chip (surface 22% into transparent, unchanged); the two canvas glows (accent 13% / primary 8% into transparent).
- Mockup vs rule differences: _filled as found_
- Overview URL check (accepted query values): `PracticeModes.Normalize` accepts `source=due`, `n=0` (flashcard only, `CountsFor(Flashcard)` = 20/50/0), `srs=1` (not match) and `from=hub`; `scope` is not read from the query, it is derived (`mine` when no `set`). So `/Practice/flashcard?scope=mine&source=due&n=0&srs=1&from=hub` is valid as written.
- Contrast results (C1, computed from the live page, scripted):
  - text / text-secondary / text-muted on the Clay canvas: 15.45 / 8.41 / 5.30; primary-strong (links, outline button text) on canvas 6.91; text on surface 17.10.
  - six hues `--k-fg` on `--k-bg`: 6.84 / 5.21 / 6.84 / 5.91 / 5.15 / 15.02 (primary, success, warning, danger, info, accent); on `--k-tint`: 5.91 / **4.63** / 6.18 / **4.73** / **4.54** / 15.81 (success, danger and info pass with little margin); on the gradient end (surface) all >= 5.36; icon squares >= 5.36.
  - outline button border (`--tct-primary`) on canvas 4.77; control border on surface 3.66; unselected seg text on its track 8.32.
  - **Known limit:** the selected `.k-seg__item` boundary (surface vs track 1.12:1, `--tct-border` vs track 1.11:1) is below 3:1. The state is also carried by text colour (17.10:1 vs 8.32:1), weight 600 vs 500, the `aria-current` / `aria-pressed` state and a 2px `CanvasText` border in forced colours. This is the plan's own 3.5 design; it is recorded here, not hidden.
- Browser coverage / not verified:
  - C0 baseline screenshots: 27 in-scope and regression pages x 1440x900, 1366x768 and 390x844 in ignored `.tmp/study-clay/c0/` (headless Chrome via DevTools protocol, Standard synthetic account, app on port 5100).
  - Existing (pre-C0) console issues, not caused by this work: `/Sets/Cards/{id}/Edit` throws `TypeError: Cannot set properties of null (setting 'textContent')` in `Object.setData`; `/Goals` logs `jQuery is not defined` from `jquery.validate.js`. Both are recorded as baseline and must not get worse.
  - Local data has one personal set (id 1, six words, nothing due), so the overview "due" branch needs extra data in C2.
  - Mockups are opened in the phase that implements them rather than all at C0.
- Final counts: _filled at C10_

### C0 — Baseline
- Commit: see `git log` (message "docs(study-clay): save the plan and prompt, record the C0 baseline")
- G1 build: pass (warnings 43, C0 43)
- G2 area: passed 108 / failed 0 / skipped 0
- G3 full: passed 1328 / failed 0 / skipped 22 (C0 baseline)
- G4 node: passed 205 / failed 0
- G5 hooks: baseline captured (457)
- G6 / G7 / G9–G14: not applicable at C0 (no code change)
- G8 render: baseline screenshots only, `.tmp/study-clay/c0/`
- Attempts / deferred / blocked: none

### C1 — Clay system in `study-kit.css`
- Commit: see `git log` ("feat(study-clay): Clay canvas, surfaces, buttons and forced colours in the study kit")
- G1 build: pass (warnings 43, C0 43; forced rebuild)
- G2 area: passed 108 / failed 0 / skipped 0
- G3 full: passed 1328 / failed 0 / skipped 22 (C0: same)
- G4 node: passed 205 / failed 0
- G5 hooks: unchanged (457, removed 0)
- G6 tokens/diff: clean (`git diff --check` clean for the CSS; the only finding is the unrelated, unstaged `appsettings.Development.json` blank line, never staged). Tokens-only grep prints nothing for `study-kit.css`.
- G7 scope: `wwwroot/css/study-kit.css` and `wwwroot/css/practice.css` (three `--k-edge` / `--k-listen-edge` uses replaced: listening play button and checked mode card, both pulled forward from C6/C7 so no reference to the deleted variable remains).
- G8 render: 19 in-scope pages x 1440x900, 1366x768, 1024x768, 768x1024, 390x844, 320x640 and 720x450 (= 200% of 1440), `.tmp/study-clay/c1/`. Canvas `rgb(240, 243, 255)` on every page; one `<h1>` each; no horizontal overflow (the only reported element is the closed off-screen drawer on `/Sets/1/Add`, by design). Console/network: only the baseline `card-edit` `textContent` TypeError; no new errors, no failed CSS/JS.
- G9 mockup: no mockup belongs to C1. The system was checked against the shared tokens by computed style (primary button, `.k-card`, seg, switch, forced colours) rather than by eye against the gallery; pages mid-migration still show their old page-level rules until C2-C9.
- G10 behaviour: 17/17 scripted checks pass: primary button rest = Clay button shadow and `:active` = Clay pressed shadow with no transform; `.k-card` rest = Clay shadow, 24px radius, hover = Clay lift with no transform (probe card, so page rules cannot interfere); Bootstrap dropdown and the share modal open and close with Escape; seg toggle works with Space (`aria-pressed` false to true) and the selected item has weight 600, 1px border and the quiet Clay shadow; keyboard focus shows a 2px outline; canvas rule is scoped (dashboard, Study, QuizMode, Vocabulary keep their own background).
- G11 a11y: forced-colors emulation: `box-shadow: none` on card, panel, seg and primary button, selected seg border 2px. Reduced motion: `.k-card` transition is 0.01ms (the shared base rule). Not re-run in C1: full Tab order of every page (done per phase on the pages being restyled).
- G12 contrast: see "Contrast results" above (all text pairs pass; seg boundary limit recorded).
- G13 regression: Study, QuizMode, dashboard, Vocabulary, Goals pages do not load `study-kit.css`; computed backgrounds unchanged; screenshots byte-identical to C0 for Study, Goals (both sizes) and Vocabulary at 390; QuizMode, dashboard and Vocabulary at 1440 differ by a few hundred bytes, which is dynamic content (shuffled quiz, dates), not layout.
- G14 clay grep: clean for `study-kit.css` (no `box-shadow` without a Clay/shadow token or `none`, no `0 Npx 0` edge, no `translateY` on `:active`, no hard-coded radius). `personal-sets.css` and `practice.css` still hold old depth values; they are cleaned in their own phases (C2-C9) and re-checked there.
- Differences found: the shared `.tct-card--interactive:hover` adds `translateY(-2px)`; an owner rule (`.k-card.tct-card--interactive:is(:hover, :focus-within)`) sets `transform: none` so Clay cards only deepen their shadow.
- Attempts / deferred / blocked: 2 attempts (first run: hover transform, probe fixed); nothing deferred or blocked.
