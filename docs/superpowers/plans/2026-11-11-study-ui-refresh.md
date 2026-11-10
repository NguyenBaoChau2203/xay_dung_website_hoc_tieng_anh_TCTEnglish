# Study UI refresh: Personal Sets + Practice (2026-11-11)

> **Tóm tắt cho Trọng (tiếng Việt).**
> - **Phạm vi:** làm lại **giao diện** toàn bộ phần Bộ thẻ và Luyện tập đã xong ở 2 plan trước, theo 12 ảnh mockup bạn đã duyệt ngày 10/11/2026.
> - **Phong cách:** học English_China (nút nổi khối, hình minh họa cho chế độ, màn luyện tập thoáng, bảng phản hồi trượt lên, bản đồ chặng, màn kết quả có vòng %), nhưng giữ nét TCT (header/sidebar, xanh #4255ff, vàng nhấn, Inter ≤700, chỉ token màu).
> - **Không đổi chức năng:**
>   - không đổi route, API, service, database hay logic `practice-core.js`;
>   - JS chỉ đổi phần dựng DOM ở vài chỗ (bản đồ chặng, bảng phản hồi, đồng hồ Ghép thẻ).
> - **Không đụng file chung.** Không cần quyết định mới.
> - **Chạy liên tục U0 → U10, không hỏi lại.** Sau mỗi phase phải qua đủ 13 bước kiểm tra (mục 7.1: build, test, hook, token, phạm vi, trình duyệt 5 kích thước, so mockup, chức năng, accessibility, tương phản, hồi quy). Còn lỗi thì sửa rồi kiểm tra lại từ đầu. Xanh hết mới commit và sang phase sau.
>
> Phần chỉ dẫn cho agent viết bằng tiếng Anh theo `AGENTS.md`.

- **Run mode:** continuous U0 → U10 with no questions to the owner. Every phase must pass the full phase gate (section 7.1) before its commit and before the next phase starts.
- Owner / page owner: Huỳnh Phú Trọng (Set/Card, Folder, study modes per `README.md`).
- Save as `docs/superpowers/plans/2026-11-11-study-ui-refresh.md`. Prompt: `.ai/prompts/claude-code-study-ui-refresh.md`.
- **Approved mockups (the visual contract):** 12 PNGs, approved by the owner on 2026-11-10, in the untracked folder `Claude outputs/ui-refresh-mockups/` at the repo root.
  - Open each one before building its page. Do **not** commit them; they stay untracked.
  - Mockup files:

| File | Page |
| --- | --- |
| `01-bo-the-cua-toi.png` | `/Sets` |
| `02-chi-tiet-bo-the.png` | `/Sets/{id}` |
| `03-chi-tiet-tu.png` | `/Sets/Cards/{id}` |
| `04-luyen-tap.png` | `/Practice` |
| `05-trac-nghiem-sai.png` | quiz, wrong answer |
| `06-hoc-chon-chang.png` | learn stage map |
| `07-ghep-the.png` | match |
| `08-lat-the.png` | flashcard |
| `09-kiem-tra.png` | test |
| `10-ket-qua.png` | summary |
| `11-mobile-chi-tiet-bo.png` | set detail at 390px |
| `12-mobile-trac-nghiem-dung.png` | quiz at 390px, correct |

  - The mockups were drawn as static HTML with hard-coded colours. **Implement them with tokens** (section 3); where a mockup pixel and a rule disagree, the rule wins. Record each such case in section 11.
- **Baseline expected** (verify at U0):
  - branch `feature/practice-modes` at `f690ff5`;
  - 10 practice commits on top of `feature/personal-sets-redesign` (`a9964a6`), none pushed;
  - untracked `Claude outputs/`; modified `TCTEnglish/appsettings.Development.json` (unrelated: never stage it);
  - previous run: full Release tests green; node tests green (`test_practice_core.cjs` included).
- **Prior plans** (read their section 11 execution notes):
  - `docs/superpowers/plans/2026-11-09-personal-sets-redesign.md`
  - `docs/superpowers/plans/2026-11-10-practice-modes.md`

---

## 0. What this plan changes and what it does not

**Changes**
- `personal-sets.css`, `practice.css`, plus one new owner CSS file `wwwroot/css/study-kit.css`.
- Markup of the views in `Views/Set/**` and `Views/Practice/**`.
- One new partial `Views/Practice/_ModeArt.cshtml`.
- DOM-building code in `practice-ui.js`, `practice-learn.js`, `practice-match.js`, `practice-flashcard.js`, `practice-test.js`, `practice-drill.js`, `practice-hub.js`, `set-detail.js`, `set-card-detail.js`.
  - Change only what each section below names.
  - Keep every id, `data-*` hook and class the JS or tests already query (section 5.1).

**Does not change**
- Controllers, services, view models, routes, APIs, migrations.
- `practice-core.js` (no edits at all; its node test must stay green unchanged).
- `personal-sets-common.js` (except adding nothing).
- `Program.cs`, appsettings, `.csproj`, `_Layout.cshtml`, `layout.css`, `site.css`, `tct-learning-ui.css`, `_PageHeader.cshtml`, `_StateMessage.cshtml`.
- AI code, legacy study pages (`Views/Study/**`), Vocabulary, Folder views, Admin.

If a mockup element would need a data value the view model does not have, **leave it out** and note it in section 11. Do not add server data.

**Known items with no model data** (expected to be left out, or kept as the existing control):
- card list "level" tag (B2/B1…): use `Cefr` when present, else omit;
- the list-page "+ Tạo bộ thẻ mới" dashed tile: a plain link to the existing create URL, so it is fine.

---

## 1. Decisions (fixed, no owner input needed)

| ID | Decision |
| --- | --- |
| R1 | **Shell untouched.** Header, sidebar, AI launcher and `.main` padding stay as they are. Practice sessions also stay inside the shell (DESIGN.md "Shell" is mandatory). The "focused" look comes from removing card chrome inside `.main`, not from hiding the shell. |
| R2 | **Shared kit file.** New file `wwwroot/css/study-kit.css` holds the components shared by Trọng's two areas. Every selector starts with `:is(.ts-page, .tp-page)`. `personal-sets.css` and `practice.css` keep page-specific rules. Every Set and Practice view loads `study-kit.css` **before** its page CSS. |
| R3 | **Tokens only.** `var(--tct-*)` everywhere, with no hex, `rgb()`, `:root`, `!important` or weight above 700. Two exceptions, each also written with a plain-token fallback declared first: (a) `color-mix()` **between two tokens** for the pressed edge of danger and success buttons and for the soft accent tint; (b) `transparent`. List every `color-mix` use in section 11. |
| R4 | **Hues stay the six token pairs** (primary, success, warning, danger, info, accent) used by `.ts-hue-*` / `.tp-hue-*`. No purple, pink, coral or orange palette from English_China. |
| R5 | **Pressed-edge ("3D") style** for primary actions, option buttons, mode cards, match tiles and set tiles. It is done with `box-shadow: 0 Npx 0 <edge>` plus `transform: translateY()` on `:active`. Under `prefers-reduced-motion: reduce` the transform is dropped (the edge stays). |
| R6 | **Illustrations are inline SVG** from one partial, coloured only through CSS classes that map to tokens. They are decorative (`aria-hidden="true"`, `focusable="false"`). No image files, no external art, no mascot. |
| R7 | **Accessibility floor unchanged:** one `<h1>` per page, 44px targets, visible focus ring (`--tct-focus-ring`), text + icon for every status, live regions kept, contrast ≥ 4.5:1 for text and ≥ 3:1 for control boundaries. Re-measure the new pairs in U10. |
| R8 | **Copy:** keep the existing Vietnamese strings. New strings are only those quoted in this plan. |

---

## 2. Scope (pages and states)

**Personal sets**
1. `/Sets` (Index + `_SetTile`)
2. `/Sets/{id}` (Detail + `_SetActionsMenu`)
3. `/Sets/Cards/{id}` (CardDetail)
4. Restyle-only, no mockup, apply the kit: `/Sets/{id}/Add` (AddCards), `/Sets/{id}/Import`, `/Sets/Create`, `/Sets/{id}/Settings`, `EditCard`, `/s/{slug}` (Shared), `_ShareSetModal`, `_SetAlerts`.

**Practice**

5. `/Practice` (Index + `_ModeCard`)
6. Session shell (`Session.cshtml` + `practice-ui.js`) for all 8 modes.
7. Each mode's look:
   - quiz, listening, cloze, typing (drill);
   - flashcard, match, test, learn;
   - the shared summary.
8. `/Practice/HardWords`.

**Every page**
- States: loading skeleton, empty, error, success.
- Widths 1440 / 1024 / 768 / 390 / 320, plus 200% zoom.

---

## 3. Visual language (implemented in `study-kit.css`)

All values are tokens from `tct-learning-ui.css`. Names below are the **new** classes.

### 3.1 Hue variables

`study-kit.css` defines one hue helper used by both areas, without removing the existing `.ts-hue-*` / `.tp-hue-*`. Make those set the same four variables, so old and new markup both work.

The selector must match both a hue class on a child and one on the page wrapper itself, because `Session.cshtml` puts `tp-hue-*` on `.tp-page`:

```css
:is(.ts-page,.tp-page) :is(.ts-hue-primary,.tp-hue-primary),
.tp-page.tp-hue-primary { --k-bg: var(--tct-primary-soft); --k-fg: var(--tct-primary-strong); --k-fill: var(--tct-primary); --k-edge: var(--tct-primary-strong); }
/* success: --tct-success-soft / --tct-success-text / --tct-success / --tct-success-text
   warning: --tct-warning-soft / --tct-warning-text / --tct-warning / --tct-warning-text
   danger : --tct-danger-soft  / --tct-danger-text  / --tct-danger  / edge = --tct-danger-text, then
            color-mix(in srgb, var(--tct-danger) 70%, var(--tct-text))
   info   : --tct-info-soft    / --tct-info-text    / --tct-info    / --tct-info-text
   accent : bg = --tct-accent, then color-mix(in srgb, var(--tct-accent) 30%, var(--tct-surface));
            fg = --tct-on-accent; fill = --tct-accent; edge = --tct-warning-text */
```

Keep the existing `--ts-hue-*` / `--tp-hue-*` variables, pointing at the same values, so current rules keep working until they are replaced.

### 3.2 Buttons (scoped override of Bootstrap buttons inside the two areas only)

| Class (existing Bootstrap) | New look |
| --- | --- |
| `.btn` (in scope) | `border-radius: var(--tct-radius-md)`; weight 600; `min-height: var(--tct-target-min)`; transition `transform`, `box-shadow` at `--tct-dur-fast` |
| `.btn-primary` | weight 700; `box-shadow: 0 4px 0 var(--tct-primary-strong)`. `:hover` keeps the token hover colour. `:active` uses `translateY(2px)` with a 2px shadow. |
| `.btn-outline-primary` | `background: var(--tct-surface)`; `color: var(--tct-text)`; `border: 1.5px solid var(--tct-border)`; `box-shadow: 0 3px 0 var(--tct-border)`. `:hover` uses border `--tct-primary` and colour `--tct-primary-strong`. **Contrast note:** the control boundary is now `--tct-border` (decorative). The button is still identified by its text label and the edge shadow. That is acceptable for buttons (WCAG 1.4.11 does not require a boundary when the text identifies the control). Record this in section 11. |
| `.btn-outline-danger` | same as outline-primary, but text `--tct-danger-text` and hover border `--tct-danger` |
| `.btn-lg` (in scope) | `min-height: 56px`; `font-size: var(--tct-text-lg)`; `border-radius: var(--tct-radius-lg)`. The mockup's 14px is not a token, so use 18px. |
| `.tct-btn-icon` | 44×44, same outline look |

New helper classes:
- **`.k-btn--success`**: background `--tct-success-text`, white text, edge `color-mix(--tct-success-text 65%, --tct-text)`.
- **`.k-btn--danger`**: background `--tct-danger`, white text, edge as danger.
- **`.k-btn--soft`**: `--k-bg` background, `--k-fg` text, edge `--k-fill` at 0 3px.

**`.k-btn--mark-learning` / `.k-btn--mark-known`** (flashcard): white background with a 2px border.
- Warning variant: `--tct-warning` border, `--tct-warning-text` text, edge `--tct-warning`.
- Success variant: `--tct-success` border, `--tct-success-text` text, edge `--tct-success`.

**Reduced motion:** remove `transform` on `:active`.

### 3.3 Surfaces

- **`.k-card`**: the existing `.tct-card` look, but radius `--tct-radius-xl` (20px), border `1px solid var(--tct-border)`, `box-shadow: 0 3px 0 var(--tct-border)`. Use it on set tiles, mode tiles and word sections.
- **`.k-panel`**: a plain `.tct-card` with radius `--tct-radius-xl` and no edge (setup panel, stage card, missed list).
- **`.k-soft`**: `--k-bg` background, radius `--tct-radius-xl`, no border (hero, KPI tiles, stat tiles).

### 3.4 Segmented control (`.k-seg`)

Used for set list tabs, hub counts and sources, flashcard direction and the view toggle.
- **Track:** `inline-flex`, background `--tct-disabled-bg` (#eef0f4, token), radius pill, padding 4px, gap 2px, wraps when needed (`flex-wrap: wrap`, radius `--tct-radius-lg` when wrapped).
- **Items:**
  - `<a>` (tabs), or `.btn-check` + `<label>` (radios);
  - padding 8px 16px, radius pill, weight 600, colour `--tct-text-secondary`, min-height 40px;
  - selected: background `--tct-surface`, colour `--tct-text`, `box-shadow: var(--tct-shadow-soft)`, plus `aria-current="page"` / `:checked`;
  - focus ring visible.
- **Count suffix:** `<span class="k-seg__count">7</span>` (muted, weight 600, `--tct-text-sm`).

### 3.5 Pills and badges

- **Filter `select`s** in the set toolbar become pill-shaped:
  - `border-radius: var(--tct-radius-pill)`; border `1.5px solid var(--tct-border-control)`; height 40px.
  - Active filter (`.ts-filter--active`, already set by JS): border `--tct-primary`, background `--tct-primary-tint`, colour `--tct-primary-strong`, weight 600.
- **Status badges:** keep `.tct-badge--*`. Add a leading dot through `.k-dot-badge::before` (8px circle in `currentColor`) on the word list status column only.
- **CEFR tag:** `.k-cefr` with a 1.5px `--tct-border-control` border, radius `--tct-radius-sm`, `--tct-text-xs`, weight 700, padding 1px 6px.

### 3.6 Progress

- **`.k-progress`:**
  - height 14px (session bars) or 8px (`.k-progress--thin` on tiles);
  - track `--tct-disabled-bg`, radius pill;
  - fill `--k-fill` (session: `--tct-success`), radius pill, inset bottom shadow `inset 0 -3px 0` with `color-mix(… var(--tct-text) 12%, transparent)` (record it);
  - width from `--p`.
- **`.k-progress--split`:** a three-part bar (mastered `--tct-success`, learning `--tct-warning`, new = track).

### 3.7 Typography helpers

| Class | Style |
| --- | --- |
| `.k-display` | `font-size: clamp(2.5rem, 2rem + 2.5vw, 3.5rem)`, weight 700, letter-spacing -0.02em, `line-height: 1.05`. Used for the session term, card detail term and match timer. |
| `.k-title-lg` | `--tct-text-3xl`, weight 700 |
| `.k-label` | `--tct-text-sm`, weight 600, `--tct-text-muted` |

- Inter weights ≤700 only.
- Numbers in timers and counters use `font-variant-numeric: tabular-nums`.

### 3.8 Key hints

`.k-kbd` style:
- inline-grid, min-width 24px, height 22px, padding 0 6px;
- radius `--tct-radius-sm`, background `--tct-disabled-bg`;
- `--tct-text-xs`, weight 700, colour `--tct-text-secondary`.

The key-hint row is a centred flex row with gap 18px, shown at ≥992px (existing rule).

### 3.9 Motion

Only `--tct-dur-fast` / `--tct-dur` and `--tct-ease`. Keyframes:
- `k-pop`: scale 0.96 → 1;
- `k-wiggle`: translateX ±4px, 400ms;
- `k-rise`: translateY 16px → 0 with opacity, for the feedback sheet.

All are disabled under `prefers-reduced-motion: reduce`.

### 3.10 Illustrations: `Views/Practice/_ModeArt.cshtml`

- **Model:** `string` key (`learn | test | match | flashcard`).
- **Markup:** `<svg class="k-art" viewBox="0 0 160 80" aria-hidden="true" focusable="false">`, plus the shapes below.
- **Colour classes** (defined in `study-kit.css`, tokens only):
  - `.k-art .f-primary{fill:var(--tct-primary)}`, same for `success`, `accent`, `surface`;
  - `.s-primary{stroke:var(--tct-primary)}`, same for `primary-strong`, `success`, `success-text`, `warning`, `info`, `info-text`;
  - `.s-muted{stroke:var(--tct-border-control);opacity:.5}`;
  - `.s-white{stroke:var(--tct-surface)}`, `.f-warning-text{fill:var(--tct-warning-text)}`;
  - `fill="none"` stays as an attribute.
- **Shapes (copy exactly):**

```html
@* learn *@
<path class="s-muted" d="M30 58 C55 58 60 30 85 30 S120 22 132 18" fill="none" stroke-width="3" stroke-dasharray="1 8" stroke-linecap="round"/>
<circle class="f-success" cx="30" cy="58" r="10"/><path class="s-white" d="M25 58l4 4 7-8" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
<circle class="f-primary" cx="85" cy="30" r="13"/><path class="f-surface" d="M81 23v14l11-7z"/>
<circle class="f-accent" cx="132" cy="18" r="9"/><path class="s-warning-text" d="M132 13v10M127 18h10" stroke-width="2.6" stroke-linecap="round"/>

@* test *@
<rect class="f-surface s-primary" x="54" y="8" width="52" height="64" rx="7" stroke-width="3"/>
<rect class="f-primary" x="66" y="4" width="28" height="10" rx="4"/>
<rect class="s-primary" x="63" y="26" width="8" height="8" rx="2" fill="none" stroke-width="2.4"/><path class="s-primary k-art-half" d="M75 30h22" stroke-width="2.6" stroke-linecap="round"/>
<rect class="f-success" x="63" y="40" width="8" height="8" rx="2"/><path class="s-white" d="M64.5 44l2 2 3.5-4" stroke-width="1.8" fill="none"/><path class="s-primary k-art-half" d="M75 44h18" stroke-width="2.6" stroke-linecap="round"/>
<rect class="s-primary" x="63" y="54" width="8" height="8" rx="2" fill="none" stroke-width="2.4"/><path class="s-primary k-art-half" d="M75 58h20" stroke-width="2.6" stroke-linecap="round"/>
<circle class="f-primary" cx="108" cy="12" r="9"/><path class="s-white" d="M104 12l3 3 5-6" stroke-width="2.4" fill="none" stroke-linecap="round"/>

@* match *@
<rect class="f-surface s-warning" x="34" y="14" width="40" height="18" rx="6" stroke-width="3"/><path class="s-warning" d="M44 23h20" stroke-width="3" stroke-linecap="round"/>
<rect class="s-muted" x="34" y="46" width="40" height="18" rx="6" fill="none" stroke-width="2.4" stroke-dasharray="4 4"/>
<rect class="f-success-soft s-success" x="92" y="14" width="40" height="18" rx="6" stroke-width="3"/><path class="s-success-text" d="M102 23h20" stroke-width="3" stroke-linecap="round"/>
<rect class="f-surface s-warning" x="92" y="46" width="40" height="18" rx="6" stroke-width="3"/>
<path class="s-warning" d="M76 23h14" stroke-width="2.4" stroke-dasharray="2 4" stroke-linecap="round"/><circle class="f-warning" cx="83" cy="52" r="6"/><path class="s-white" d="M80 52l2 2 4-4" stroke-width="2" fill="none"/>

@* flashcard *@
<rect class="f-surface s-info" x="46" y="18" width="62" height="44" rx="8" stroke-width="3" transform="rotate(-8 77 40)"/>
<rect class="f-surface s-info-text" x="54" y="14" width="62" height="44" rx="8" stroke-width="3"/><path class="s-info-text" d="M66 30h38M66 40h26" stroke-width="3" stroke-linecap="round"/>
<path class="s-info-text" d="M118 50c10-2 14-10 12-20" fill="none" stroke-width="3" stroke-linecap="round"/><path class="s-info-text" d="M124 32l6-4 4 7" fill="none" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
```

  - Add `.f-success-soft{fill:var(--tct-success-soft)}`, `.f-warning{fill:var(--tct-warning)}` and `.k-art-half{opacity:.55}`.
  - Unknown key → render nothing.
- **Size:** the art sits in `.k-art-box`: height 84px (set detail), 96px (hub) or 64px (≤576px); background `--k-bg`; radius `--tct-radius-lg`; the SVG fills it.
- **Skill icons:** the four skill drills use a Font Awesome icon in a 48px square, not the SVG.
  - quiz `fa-list-check` primary, typing `fa-keyboard` accent, listening `fa-headphones` info, cloze `fa-pen-to-square` danger.
  - Square background `--k-edge` with icon `--tct-on-primary`. For danger use `--tct-danger`. For accent use background `--tct-accent` with icon `--tct-on-accent`.
  - Check icon contrast: white on `--tct-info-text`, `--tct-success-text`, `--tct-danger` and `--tct-primary`; all are ≥4.5 per DESIGN.md table/compute.

---

## 4. Page specifications

Each bullet is a requirement. "Mockup NN" refers to the PNG. Keep every existing `id`, `data-*` attribute, form, antiforgery token and JS hook.

### 4.1 `/Sets`: `Views/Set/Index.cshtml`, `_SetTile.cshtml` (mockup 01)

- **Header:**
  - keep `_PageHeader` (title "Bộ thẻ của tôi");
  - actions: secondary "Luyện tập" (`fa-dumbbell`) → `/Practice`, then primary "Tạo bộ thẻ" (`fa-plus`);
  - order exactly as in the mockup (secondary first, primary last).
- **Relation tabs:**
  - existing filter links "Tất cả / Của tôi / Đang theo học / Được chia sẻ" become a `.k-seg` with counts (`.k-seg__count`), only when the view model already provides those counts;
  - otherwise no counts;
  - keep the URLs and the `aria-current`.
- **Section headings:** "Của tôi 5" as `h2` (`--tct-text-2xl`, weight 700) + count (`--tct-text-base`, weight 600, muted).
- **Grid:** `repeat(auto-fill, minmax(280px, 1fr))`, gap `--tct-space-lg`.
- **Tile (`_SetTile`):** keep the structure, classes and the stretched link; apply `.k-card` + hover lift.
  - **Cover `.ts-tile-art`:**
    - height 112px, `--k-bg` background;
    - two decorative circles via `::before` / `::after` (`--tct-surface`, opacity .45 / .35);
    - term `--tct-text-3xl` weight 700 in `--k-fg`, `.tct-break`, one line with ellipsis on overflow;
    - top-right visibility icon in a 30px white circle (text label stays visually hidden ≤ all widths, already `ts-tile-vis-text`);
    - top-left CEFR tag only if the tile model has one (it does not today, so **omit**).
  - **Body:**
    - name `--tct-text-lg` weight 700 (`.tct-clamp-2`);
    - meta row "{n} từ" (+ owner) on the left, status badge on the right (same row, `justify-content: space-between`, wraps under 360px);
    - `.k-progress--thin` in hue fill.
  - **Quick "Ôn" button:** keep it, positioned bottom-right over the body on hover/focus-within, and always visible on touch (`@media (hover: none)`).
- **Add tile:** append `<a class="ts-tile-add">` (dashed 2px `--tct-border-control` border, radius 20px, min-height 230px) only in the "Của tôi" section.
  - Content: `fa-plus` in a 48px `--tct-primary-soft` square, "Tạo bộ thẻ mới", small muted "hoặc nhập từ file CSV".
  - It links to the existing create URL (same as the header action). Mark it `aria-hidden="true" tabindex="-1"`, since it duplicates the header action for keyboard users.
- **Empty/loading states:** unchanged partials, restyled by the kit.

### 4.2 `/Sets/{id}`: `Views/Set/Detail.cshtml`, `_SetActionsMenu.cshtml`, `set-detail.js` (mockups 02, 11)

- **Hero `.ts-hero` (`.k-soft` with the cover hue):**
  - padding `--tct-space-xl`, radius 24px → use `--tct-radius-xl`;
  - one decorative circle top-right (`::after`, `--tct-surface`, opacity .35, `pointer-events:none`).
  - **Art `.ts-hero-art`:**
    - 220×150px, `color-mix(surface 75%, transparent)` or `--tct-surface` with opacity;
    - radius `--tct-radius-lg`, one inner circle in `--k-bg` top-right;
    - term `clamp(2rem, 1.5rem + 1.5vw, 2.5rem)` weight 700 `--k-fg`;
    - `z-index: 1` above the hero circle.
  - **`_PageHeader` stays inside `.ts-hero-main`** (it owns the `<h1>` and breadcrumbs):
    - restyle within `.ts-hero` only: breadcrumb `--tct-text-sm` muted, title `clamp(1.75rem, 1.5rem + 1vw, 2.125rem)`, description `--tct-text-secondary`;
    - the mockup's "‹ Bộ thẻ" back link is replaced by the breadcrumb (rule wins; note it).
  - **Meta line:** keep. "12 cần ôn" uses `--tct-warning-text` weight 700; visibility shows `fa-lock` / `fa-globe` / `fa-link` + text.
  - **Actions:**
    - "Ôn bộ này (n)" = `.btn-primary.btn-lg`;
    - "Thêm từ" = `.btn-outline-primary.btn-lg`;
    - `_SetActionsMenu` toggle = `.btn-outline-primary.tct-btn-icon` at 56px height;
    - "Theo học" (non-owner) = `.btn-primary.btn-lg`.
  - **≤576px:**
    - hero stacks, art full width 100px high;
    - actions become a grid `1fr auto auto` (mockup 11).
- **Study strip `.ts-study-strip`:** four tiles, `repeat(4, 1fr)`; 2 columns ≤992px; gap `--tct-space-md`.
  - **Tile `.ts-study-tile`:** `.k-card`, padding 10px 10px 16px.
    - Inside: `.k-art-box` with `<partial name="~/Views/Practice/_ModeArt.cshtml" model="key" />` (map `flashcard` → `flashcard`);
    - optional suggestion badge;
    - name (`--tct-text-lg` 700);
    - note (`--tct-text-sm` 600 in `--k-fg`).
  - Remove the old `.ts-study-icon` square (the art replaces it). Keep `data-ts-mode`, `data-ts-note` and the `href`s exactly.
  - **Suggestion badge:**
    - `set-detail.js` currently inserts `tct-badge tct-badge--primary ts-suggest` "Gợi ý cho bạn";
    - keep the text and classes; change only the insertion point to **after the art box, before the name**;
    - add icon `fa-wand-magic-sparkles` (`aria-hidden`).
  - **More link:**
    - "Luyện riêng từng kỹ năng" becomes "Luyện riêng từng kỹ năng: Trắc nghiệm, Gõ từ, Nghe, Điền từ" + `fa-arrow-right`;
    - weight 600 `--tct-primary-strong`; same `href`.
- **Toolbar `.ts-toolbar`:**
  - search full width on its own row: height 48px, radius `--tct-radius-lg`, background `--tct-disabled-bg`, no border, focus ring `--tct-focus-ring` (the input keeps `form-control`, overridden in scope);
  - filter selects become pills (3.5);
  - the count "48 từ" moves right (`margin-left:auto`) next to "Chọn" and the list/grid toggle, which becomes `.k-seg` with icon buttons (keep their ids and `aria-pressed`);
  - ≤576px: only Sort and Status selects stay visible inline; the rest go behind the existing "more filters" control if there is one, otherwise wrap (do not hide controls without an alternative).
- **Word table (`.ts-list-view`):** `.k-panel` with overflow hidden.
  - **Header row:** `--tct-surface-muted`, `--tct-text-sm` 600 muted.
  - **Term cell:** `--tct-text-lg` weight 600 `lang="en"`. Keep it a link if it is a link today.
  - **IPA cell:** `.tct-phonetic` muted.
  - **CEFR cell:** `.k-cefr`.
  - **Status cell:** existing badge + `.k-dot-badge`.
  - **Speak button:** 36px `.k-btn--soft` (primary hue) icon button.
  - Row hover `--tct-primary-tint`.
  - **≤576px:**
    - rows become 2-column grids: term + meaning on the left, status on the right;
    - hide IPA, CEFR and speak; keep select checkboxes when selection mode is on;
    - header row hidden (mockup 11);
    - use the existing `ts-col-*` classes for targeting.
- **Grid view (`.ts-grid` / `.ts-word-card`):** cards get `.k-card`, art area `--k-bg` by status hue, term weight 700.
- **Bulk bar:** keep the behaviour. Restyle as a sticky bottom `.k-panel` with the edge shadow, above the AI launcher offsets already handled.

### 4.3 `/Sets/Cards/{id}`: `Views/Set/CardDetail.cshtml`, `set-card-detail.js` (mockup 03)

- **Header (fixes today's duplicate visible term):**
  1. Keep `_PageHeader` *above* the hero with the term as `Title` and the existing breadcrumbs. In scope, hide only its title visually: `.ts-page--word .tct-page-header .tct-page-title` gets the visually-hidden pattern. The `<h1>` stays in the DOM.
  2. Inside the hero, the existing `<p class="ts-word-term-lang" lang="en">` becomes the big visible term: add `k-display`, size `clamp(2.75rem, 2rem + 3vw, 4rem)`, and `aria-hidden="true"`, so screen readers hear the term once, from the h1.
- **Hero card `.ts-word-hero`:**
  - `.tct-card` radius `--tct-radius-xl`, padding `--tct-space-xl`, 6px top bar `::before` in `--tct-primary`;
  - flex row: main on the left, art on the right (150px square, `--k-bg` primary, radius 20px, emoji 72px or image `object-fit:cover`).
  - **Pronunciations:** each US/UK becomes a chip (`--tct-bg` background, radius `--tct-radius-md`, padding 6px 6px 6px 14px) with the `abbr` label (xs 700 muted), IPA, and the existing speak button as a 36px soft icon button. Keep `data-ts-speak` and the `hidden` toggling done by JS.
  - The image credit line stays under the card, right-aligned, xs muted.
- **Below the hero:**
  - badges row: POS (`tct-badge--primary`), CEFR (`.k-cefr`), topic (`tct-badge--info`, text "Chủ đề: X" stays);
  - definition `--tct-text-2xl` → mockup is 28px, so use `--tct-text-3xl` at ≥768px and `--tct-text-2xl` below, weight 700;
  - `MeaningEn` muted `--tct-text-lg`.
- **Actions `.ts-word-actions`** (order changes, hooks unchanged):
  1. "Học từ này" = `.btn-primary`;
  2. "Gõ từ này" = outline;
  3. "Sửa" = outline (was primary);
  4. an overflow `dropdown` (`.tct-btn-icon`, `fa-ellipsis`, `aria-label="Thêm thao tác"`) containing "Xóa từ" (danger text; still opens `#tsDeleteCard`).
  - Only owners/editors see Sửa and Xóa (keep the existing conditions).
- **Two-column body** at ≥992px: `grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr)`.
  - **Left:** examples, collocations, synonyms/antonyms, word family, personal note.
  - **Right:** progress card, `position: sticky; top: calc(var(--tct-header-h) + var(--tct-space-md))`.
  - Below 992px, one column with progress **first**.
- **Examples:**
  - each `li.ts-example` gets a 4px `--tct-primary` left border, padding-left 16px;
  - `mark.ts-hl` becomes `background: transparent; color: var(--tct-primary-strong); font-weight: 700`;
  - Vietnamese line muted;
  - speak button 36px soft, on the right.
- **Chips:** `.ts-chip` gets a 1.5px dashed `--tct-border-control` border, pill radius, padding 5px 12px; `.ts-chip--link` gets a solid border + primary text; `.ts-chip--add` keeps "+ ".
  - Each relation group is a row: label (110px, sm 700 muted), then chips.
- **Progress card:**
  - **Title:** "Tiến độ của bạn" + status badge inline.
  - **Stats:**
    - the existing `<dl class="ts-dl">` values are shown as a 2×2 grid of `.k-soft` tiles: number `--tct-text-2xl` 700, label sm 600;
    - hues: lần ôn = primary, lần quên = danger, nhớ liên tiếp = success, lần ôn tới = info;
    - keep it a `<dl>` (each tile = `div` > `dt` + `dd`, with `dd` the big number shown first via `display:flex; flex-direction:column-reverse`) so semantics stay;
    - use exactly the fields the view already has, and their labels.
  - **Last reviewed:** a muted line.
  - **Suspend:** moves here as a row: text "Tạm ẩn khỏi ôn tập" + help "Từ vẫn nằm trong bộ nhưng không xuất hiện khi ôn.", with the **existing `#ts-suspend` button** on the right.
    - Restyle it as a switch: keep `type="button"` and `aria-pressed`; add `role="switch"` and `aria-checked` mirroring `aria-pressed`.
    - Update `set-card-detail.js` so it sets both attributes and stops changing the label text. The label lives in the row; keep `#ts-suspend-label` as a visually-hidden span with the current text, so the JS that updates it still works.
    - Visual: a 46×26 track (`--tct-border-control` off, `--tct-primary` on) with a 20px knob in `--tct-surface`, and focus ring.

### 4.4 Other Set pages (no mockup; apply the kit)

AddCards, Import, CreateSet, Settings, EditCard, Shared, `_ShareSetModal`:
- Load `study-kit.css`.
- Buttons get the pressed edge automatically (3.2).
- Panels become `.k-panel` (radius 20px).
- Segmented choices (e.g. visibility, import format, if they are radio groups) become `.k-seg`.
- **Composer rows** (AddCards): each word row is a `.k-card` with a numbered circle (`--tct-primary-soft` background, `--tct-primary-strong` text, 28px), term input `--tct-text-lg` weight 600; suggestion chips use `.ts-chip` styling.
  - Keep every id and `data-*` used by `set-composer.js` and `test_set_composer_behavior.cjs`.
- **Shared page `/s/{slug}`:** the hero uses the same `.ts-hero` styles as Detail.
- Do not change layout order or fields on these pages.

### 4.5 `/Practice`: `Views/Practice/Index.cshtml`, `_ModeCard.cshtml`, `practice-hub.js` (mockup 04)

- **Layout:**
  - `_PageHeader` (title "Luyện tập", existing description);
  - then `.tp-hub` grid at ≥1200px with `grid-template-columns: minmax(0,1fr) 360px`, gap `--tct-space-xl`;
  - left column: mode groups + hard-words link;
  - right column: the existing setup fieldsets + start bar inside one `.k-panel.tp-setup` titled `h2` "Thiết lập", `position: sticky; top: calc(var(--tct-header-h) + var(--tct-space-lg))`;
  - below 1200px, one column with the setup panel after the modes, and the start bar becomes the existing sticky bottom bar.
- **Group titles:** "Học bộ từ", "Luyện riêng từng kỹ năng" (sm 700 `--tct-text-secondary`).
- **`_ModeCard` big variant (4 study modes):**
  - `.k-card` with a 2px border, padding 10px 10px 16px;
  - `.k-art-box` (96px) with `_ModeArt`;
  - optional suggestion badge (`data-tp-suggest`, keep `hidden` logic) placed **between art and name**;
  - name `--tct-text-lg` 700; hint sm `--tct-text-secondary`; stat (`data-tp-stat`) as sm 700 `--k-fg` text, not a pill (remove the `tct-badge` look, keep the element and attribute);
  - 4 columns at ≥992px, 2 columns below.
- **`_ModeCard` small variant (4 skills):**
  - horizontal: 48px icon square (3.10) + name (`--tct-text-lg` 700) + hint;
  - 2 columns at ≥576px.
- **Checked state** (`:has(input:checked)`):
  - border `--k-fill`, background `--k-bg`, edge `0 3px 0 var(--k-edge)`;
  - the art box turns `--tct-surface`;
  - a 26px check badge top-right (`--k-fill` background, `fa-check`, `--tct-on-primary`).
  - Keep `.tp-mode-card__check`, restyled as that badge.
  - Focus: ring on `:has(:focus-visible)`.
- **Setup fields:**
  - "Bộ từ" select: height 46px, radius `--tct-radius-md`;
  - count / source / ask / types radio groups become `.k-seg` (keep `.btn-check` inputs and names; the `tp-pill` class generated by `practice-hub.js` gets the `.k-seg` item style);
  - the switch row "Tính vào ôn tập" keeps the Bootstrap `form-switch` markup, restyled 46×26.
- **Start bar:** summary line (centred, sm 600) above "Bắt đầu luyện" (`.btn-primary.btn-lg w-100`). Keep the `aria-live`.
- **Hard-words link:** `.k-panel` row with a 48px `--tct-danger-soft` square (`fa-brain`, `--tct-danger-text`), title, sub, count badge `tct-badge--danger`, chevron.

### 4.6 Session shell: `Session.cshtml`, `practice-ui.js` (mockups 05, 12)

- **Page:**
  - `.tp-page--session` is a flex column with `min-height: calc(100dvh - var(--tct-header-h) - 2 * var(--tct-main-pad))` (and `--tct-main-pad-sm` ≤768px);
  - max-width 760px centred (test 820px, learn 1080px: add `tp-page--wide` for learn/test);
  - **no card around the stage.** Remove any `.tct-card` wrapper `practice-ui.js` puts around the question; the stage is transparent.
- **Top bar `.tp-bar`:**
  - no background; exit button = 44px **ghost** icon (`--tct-text-secondary`, no border, hover `--tct-disabled-bg`);
  - `.tp-progress` 14px tall (`.k-progress`, fill `--tct-success`) with `role="progressbar"` and ids kept;
  - counter 700 tabular `--tct-text-secondary`;
  - `#tpSlot` for mode extras.
- **Question area** (`termLine`, `meaningLine`):
  - centred; prompt label (`.k-label`) above;
  - term `.k-display` `lang="en"`;
  - IPA + speak (44px `.k-btn--soft`) on one centred row;
  - meaning prompt `clamp(1.75rem, 1.4rem + 1.5vw, 2.25rem)` weight 700;
  - visual (emoji/image) 96px in a `--k-bg` square above the term only when present;
  - top margin `--tct-space-2xl` at ≥768px.
- **Options `.tp-options` / `.tp-option`:**
  - 2 columns at ≥768px, 1 below, gap 14px;
  - **option:** min-height 68px (56px ≤576px), `--tct-surface`, 2px `--tct-border` border, radius `--tct-radius-lg`, edge `0 4px 0 var(--tct-border)`, `--tct-text-lg` weight 600;
  - **key badge `.tp-option__key`:** 30px, radius `--tct-radius-sm`, `--tct-disabled-bg`, 700. It shows 1–4 at ≥992px and A–D below (existing logic).
  - **States** (classes already set by JS):
    - `--correct`: border `--tct-success`, background `--tct-success-soft`, text `--tct-success-text`, edge `--tct-success`, mark `fa-circle-check` right;
    - `--wrong`: the same with danger;
    - other options after answering get `opacity: .55` via a new parent class `.tp-options--answered`, added in `practice-ui.js` where options are disabled;
    - hover (enabled): border `--k-fill`, background `--k-bg`;
    - `:active` press.
- **Feedback sheet `.tp-feedback`** (rendered into `#tpFeedback`):
  - becomes a **bottom sheet**: `position: sticky; bottom: 0`;
  - `margin-top: auto` (pushes to the bottom of the flex column);
  - negative inline margins equal to the main padding, so it spans the column: `margin-inline: calc(-1 * var(--tct-space-xl))` inside the 760px column at ≥768px, and `calc(-1 * var(--tct-main-pad-sm))` below;
  - radius `--tct-radius-xl --tct-radius-xl 0 0`;
  - padding `--tct-space-lg` `--tct-space-xl` plus `env(safe-area-inset-bottom)`;
  - animation `k-rise`.
  - **Variant classes** (JS already knows the outcome; add `tp-feedback--correct|close|wrong`):
    - correct: background `--tct-success-soft`, title colour `--tct-success-text`;
    - close: warning;
    - wrong: danger.
  - **Content:**
    - title row (icon + "Đúng rồi!" / "Gần đúng!" / "Chưa đúng", `--tct-text-2xl` 700) with the speak button on the right (soft, white background);
    - answer line: term `--tct-text-3xl` 700 `lang="en"`, IPA, definition (`--tct-text-lg` secondary);
    - full-width `.btn-lg` "Tiếp tục" / "Xem kết quả" with `.k-kbd` "Enter" (≥992px), class `.k-btn--success`, warning-coloured, or `.k-btn--danger` by outcome. Warning: background `--tct-warning-text`, white text, edge mix.
  - Keep focus moving to the button (existing).
- **AI launcher:** the launcher (bottom-right, `--tct-z-launcher`) overlaps the sheet's right edge at ≤768px.
  - Add bottom padding to the sheet so the button never sits under the launcher: right padding `calc(var(--tct-target-min) + 2 * var(--tct-space-md))` ≤768px.
  - Measure the real launcher size in U4; do not move or hide the launcher.
- **Key hints `.tp-keyhint`:** a `.k-kbd` row (3.8).
- **Typing (`typingInput`):**
  - input 56px, radius `--tct-radius-lg`, 2px `--tct-border-control` border, `--tct-text-xl` 600, focus ring;
  - buttons: "Kiểm tra" `.btn-primary.btn-lg`, "Không nhớ" outline;
  - hint button = `.k-btn--soft` (accent hue) with `fa-lightbulb`;
  - the hint line shows the mask in `--tct-font-mono`, letter-spacing .15em;
  - diff rows: two labelled rows; wrong letters get a danger underline (2px) + `--tct-danger-soft` background; missing letters get `--tct-warning-soft` + bold.
- **Listening:**
  - play button 96px circle `--k-fill` with `--tct-on-primary` icon, edge `0 5px 0 var(--k-edge)`;
  - "Nghe chậm" as `.k-btn--soft` pill.
- **Cloze:**
  - sentence box `--tct-disabled-bg` background, radius `--tct-radius-xl`, padding `--tct-space-lg`, `--tct-text-xl`;
  - blank = 4em bottom border 3px `--tct-border-control`, filled answer in `--tct-success-text` 700 with a success border;
  - Vietnamese line muted below.

### 4.7 Summary (shared, `practice-ui.js summary()`) (mockup 10)

- **Ring `.tp-ring`:**
  - 220px (160px ≤576px), `conic-gradient(var(--k-fill) calc(var(--p)*1%), var(--tct-disabled-bg) 0)`;
  - inner 184px circle `--tct-bg`;
  - percent `clamp(2.5rem, 2rem + 2vw, 3.5rem)` 700 `--tct-success-text` (danger text below 50%, warning text 50–69%);
  - label "trả lời đúng" — **new string**, only if the ring currently has no label; otherwise keep the existing label.
  - Keep `role="img"` + `aria-label`.
- **Stars:**
  - 40px `fa-star` solid, `--tct-accent` for earned and `--tct-border` for unearned;
  - earned stars get `-webkit-text-stroke: 1px var(--tct-warning-text)`, so yellow stays visible on the light canvas (non-text contrast);
  - keep `role="img"` + `aria-label`.
- **Title** `h2` "Xong phiên rồi!" `--tct-text-3xl`.
- **KPIs `.tp-kpis`:** 3 columns (1 column ≤400px), each `.k-soft`: primary = câu hỏi / số thẻ, success = đúng, info = thời gian. Value `--tct-text-3xl` 700 `--k-fg`; label sm 600.
- **Missed list `.tp-missed`:** `.k-panel` with title `fa-circle-xmark` (danger) "Từ làm sai", rows term 700 + IPA + definition + 36px soft speak.
- **Actions:**
  - "Luyện tiếp" `.btn-primary.btn-lg`;
  - "Học các từ sai" outline `.btn-lg` (2-column grid at ≥576px);
  - then "Từ hay quên" and "Xong" as text links, centred.

### 4.8 Flashcard (`practice-flashcard.js`, mockup 08)

- **Bar slot:** chips "Chưa thuộc: n" (`tct-badge--warning`) and "Đã thuộc: n" (`tct-badge--success`), padding 5px 12px, `--tct-text-sm`.
- **Toolbar** (under the bar, centred):
  - the direction toggle as `.k-seg` with two buttons "Từ trước" / "Nghĩa trước". Keep the single toggle button semantics if the JS uses one button with `aria-pressed`: render it as a `.k-seg`-looking button with two spans, highlighting the active one;
  - shuffle as outline button with text "Xáo trộn" (keep `aria-label` "Xáo trộn thẻ").
- **Card `.tp-flashcard`:**
  - height 360px (280px ≤576px), radius 28px → use `--tct-radius-xl`;
  - 2px `--tct-border` border, edge `0 6px 0 var(--tct-border)` + `--tct-shadow-lift`;
  - a "stack" `::after` behind the card (inset 28px, bottom -14px, `--tct-disabled-bg`, radius bottom 24px);
  - **front:** emoji/image, term `.k-display`, IPA;
  - **back:** `--k-bg` background, definition `--tct-text-3xl` 700, `meaningEn`, example;
  - top-left tag (CEFR · POS) when data exists, top-right speak button (soft);
  - bottom hint line xs muted with `fa-rotate`: "Chạm để lật · vuốt trái “chưa thuộc”, vuốt phải “đã thuộc”" (new string, ≥576px only).
- **Swipe hints:** keep.
- **Controls:**
  - prev/next 56px outline icons;
  - "Chưa thuộc" `.k-btn--mark-learning` (`fa-rotate-left`) and "Đã thuộc" `.k-btn--mark-known` (`fa-check`), 56px, min-width 200px (flex 1 ≤576px), with a `.k-kbd` "1"/"2" at ≥992px.
- **End screen:** uses the summary styles (4.7) with the existing texts.

### 4.9 Match (`practice-match.js`, mockup 07)

- **Bar:** exit, title "Ghép thẻ" (`--tct-text-xl` 700), "Bảng 1/2" right. Hide the progress bar in match (it has none meaningful).
- **Timer:**
  - centred `.k-display` (`clamp(3rem, 2.5rem + 2.5vw, 4rem)`) number + "giây" (`--tct-text-xl` 700 muted);
  - under it a badge row: "Ghép đúng n" (success, `fa-check`), "n lần ghép sai" (danger, `fa-xmark`, hidden while 0), "Kỷ lục …" (accent, `fa-trophy`, only when a record exists).
  - These counters are new DOM; the counts exist in the JS state.
  - The timer stays `aria-hidden`; the live region is unchanged.
- **Grid:** 4 columns at ≥768px, 3 below; gap 14px.
- **Tiles `.tp-tile`:**
  - height 104px (88px ≤576px), radius `--tct-radius-lg`;
  - 2px `--tct-border` border, edge `0 4px 0 var(--tct-border)`;
  - term tiles weight 600 `--tct-text-lg`; meaning tiles weight 500.
  - **States:**
    - selected (`aria-pressed=true`): `--tct-primary` border, `--tct-primary-soft` background, `--tct-primary-strong` text, edge `--tct-primary-strong`, `translateY(-2px)`;
    - matched (`visibility:hidden` today): **change to** a dashed 2px `--tct-border-control` slot with no edge and no text (`disabled`, `aria-hidden="true"`, content removed), so the grid shape stays visible like the mockup;
    - wrong: danger border/background/edge + `k-wiggle`, and a floating "+1 giây" pill (danger, white, xs 700) at the tile's top-right for 800ms.
- **Start screen and result:**
  - start: centred `_ModeArt match` at 160px, title, rule text, record line, `.btn-primary.btn-lg` "Bắt đầu";
  - result uses the summary styles; "Kỷ lục mới!" is a `tct-badge--success` with `k-pop`.

### 4.10 Test (`practice-test.js`, mockup 09)

- **Layout:** `tp-page--wide` (820px).
- **Bar:** exit, title "Kiểm tra" 700, scope + count on the right (muted).
- **Jump grid:**
  - **moves to the top** (under the bar) as a wrapping row of 36px buttons, radius `--tct-radius-sm`;
  - answered `--tct-primary-soft` + `--tct-primary-strong`; current = primary border + 2px soft ring;
  - this replaces the right-hand column. Keep its markup and handlers; only its placement/CSS changes.
- **Question card:** `.k-panel`, padding `--tct-space-lg`.
  - Header: a 34px number circle in the question type's hue (choice primary, tf info, typing warning, cloze danger), "Câu i/n" 700, type badge.
  - Prompt centred `--tct-text-3xl` 700 for terms and `--tct-text-2xl` for meanings.
  - Options = `.tp-option` (4.6). Before submit, the selected one uses the "selected" look (primary border/soft/edge) via `aria-pressed="true"`.
  - True/false: two equal `.tp-option` buttons with `fa-check` (success text) / `fa-xmark` (danger).
  - Typing: label "Gõ từ tiếng Anh" + 56px input (4.6).
- **Sticky submit bar:**
  - `--tct-bg` background, top border;
  - "Đã làm a/n" 700, a thin `.k-progress` (primary fill), "Nộp bài" `.btn-primary`.
- **Result view:**
  - ring + "Đúng c/n" + remark (4.7 styles);
  - the filter "Chỉ câu sai" as a `.k-seg` toggle;
  - items as `.k-panel`; wrong answers show "Bạn trả lời" (danger soft line) and "Đáp án" (success soft line).

### 4.11 Learn (`practice-learn.js`, mockup 06)

- **Layout:** `tp-page--wide` (1080px).
- **Bar:**
  - exit, then `.k-progress--split`, sized from `core.progress(state)` (mastered %, learning %);
  - `aria-label` "Đã thuộc m/total, đang học l" kept;
  - badge `tct-badge--success` "Đã thuộc m/total".
- **Heading block:**
  - muted "Học · {ScopeName}", `h2` "Chọn chặng để học" (`--tct-text-3xl`; the page h1 stays hidden);
  - legend row: three 10px squares + "Đã thuộc n", "Đang học n", "Chưa học n" (counts from `core.progress`).
- **Stage map (replaces the vertical `ol.tp-step-list` look; keep the `ol` / `li` structure and classes):**
  - `ol.tp-step-list.tp-path`: a column of `li.tp-step`, each horizontally offset in a zig-zag: `margin-inline-start: calc(var(--x) * 1%)`, where `--x` cycles 20, 45, 25, 50, 30… (set by JS as an inline custom property, allowed);
  - between nodes a dotted connector: `li + li::before`, a 4px-wide vertical dotted line in `--tct-border-control` at 50% opacity, height 28px;
  - **node `.tp-step__node`** (new span, the icon holder):
    - done: 76px circle `--tct-success`, edge `0 6px 0 var(--tct-success-text)`, `fa-check` white;
    - current: 96px `--tct-primary`, edge `--tct-primary-strong` + `0 0 0 10px var(--tct-primary-soft)` ring, `fa-play`;
    - locked: 76px `--tct-disabled-bg`, edge `--tct-border`, `fa-lock` `--tct-disabled-text`;
    - after the last stage, a trophy node (76px `--tct-accent`, edge `--tct-warning`, `fa-trophy` `--tct-on-accent`), decorative, `aria-hidden`.
  - **Text:** under done nodes, stars (`fa-star` ×3, earned `--tct-accent`, unearned `--tct-border`, `role="img"` `aria-label "n sao"`); under the current node, a pill label "Chặng k · Đang học". `.tp-step__name` stays as visually-hidden text for each node ("Chặng k" + state), so screen readers still get the list.
  - **Interaction:** done and current stage nodes stay focusable as they are today (or not, if they are not interactive today; do not add new behaviour).
- **Stage card `.tp-stage-card`:**
  - at ≥992px a right column (380px, sticky); below, after the map;
  - `.k-panel`, `h3` "Chặng k" + meta "{fresh} từ mới · {review} ôn lại";
  - word chips: new words `--tct-warning-soft` with "Mới" (xs 700 `--tct-warning-text`); others `--tct-bg` + mastery dots (3×7px, filled `--tct-success`, empty `--tct-border-control`) + visually-hidden "d/3";
  - info line (`fa-circle-info` `--tct-info-text`): "Mỗi từ qua 2 bước: chọn nghĩa, rồi gõ lại từ. Sai thì hỏi lại cuối vòng." (new string);
  - `.btn-primary.btn-lg w-100` "Bắt đầu chặng k".
- **In-round:**
  - uses the drill styles (4.6);
  - the level badge ("Từ mới" warning / "Học lại" danger / "Ôn lại — gõ từ" primary) is centred above the prompt.
- **Intro card "Từ mới":** a `.k-panel`, centred, `k-pop`.
- **Round end:**
  - stars (40px) + three horizontal bars (`.tp-bar-row`, 12px tall, hue fills) + the word list with `fa-circle-check` / `fa-clock` and text;
  - `.btn-primary.btn-lg` "Tiếp tục".
- **Complete:** summary styles + an accent `fa-trophy` 64px, `k-pop`.

### 4.12 `/Practice/HardWords`

- `_PageHeader` as now.
- **Filter select:** pill style.
- **Table:** same as the set word table (4.2), with a "Sai n lần" `tct-badge--danger`.
- **Actions:** "Học các từ này" `.btn-primary.btn-lg` and "Kiểm tra các từ này" outline `.btn-lg`; sticky on mobile as today.
- **Empty state:** unchanged.

---

## 5. JavaScript rules

### 5.1 Hooks that must not change

Grep before editing; this list is the minimum.

| Area | Hooks |
| --- | --- |
| Tests | `class="tct-theme tct-page ts-page ts-page--list"`, `class="tct-theme tct-page tp-page tp-page--`, `id="tpExitModal"`, `id="tpProgress" role="progressbar"`, `id="tpHubConfig"`, `data-user-id=`, `data-label="Số lần sai"`, `tct-state--empty`, the script tags, `"userId":`, `href="/Practice"`, the label strings asserted in `PracticeIntegrationTests` lines ~366, 427, 453, 456 |
| set-detail.js | every class in its `ts-*` list (`ts-row`, `ts-rows`, `ts-col-*`, `ts-word-*`, `ts-bulk-*`, `ts-share-*`, `ts-filter--active`, `ts-select-*`, `ts-sentinel`, `ts-skeleton`, …), `data-ts-*`, `#ts-search`, `#ts-sort` |
| practice JS | `#tpStage`, `#tpFeedback`, `#tpSlot`, `#tpCounter`, `#tpProgress`, `#tpProgressFill`, `#tpExit`, `data-tp-*`, every `tp-*` class listed by `grep -oE "['\"]tp-[a-z0-9_-]+" wwwroot/js/practice-*.js` |
| set-card-detail.js | `#ts-suspend`, `#ts-suspend-label`, `data-ts-speak` |
| set-composer.js | all ids/`data-*` in `AddCards.cshtml` and those used by `test_set_composer_behavior.cjs` |

Run `node --test scripts/tests/*.cjs` and the integration filters after each phase.

### 5.2 Allowed JS edits (DOM structure and classes only)

| File | Edit |
| --- | --- |
| `practice-ui.js` | remove the card wrapper from the stage; add `tp-options--answered`; add `tp-feedback--{outcome}` and the restructured sheet content (4.6); the `.k-kbd` key badges; summary markup (ring label, KPI hues, 2-column actions); typing hint/diff classes |
| `practice-learn.js` | stage map (4.11): nodes, `--x` offsets, stars, trophy, legend, split progress, stage card |
| `practice-match.js` | timer block with counters, matched-slot rendering, "+1 giây" float, start screen art (the SVG markup is copied into JS as a string constant, using the same classes, built with `createElementNS`, **not** `innerHTML`) |
| `practice-flashcard.js` | toolbar, tag, hint line, button classes |
| `practice-test.js` | jump grid placement, number circle hue, sticky bar progress |
| `practice-drill.js` | listening/cloze classes |
| `practice-hub.js` | `tp-pill` → also `k-seg__item`; stat element as text; no logic change |
| `set-detail.js` | suggestion badge insertion point + icon; nothing else |
| `set-card-detail.js` | `role="switch"` / `aria-checked` sync (4.3) |

- Keep `createElement` / `textContent` only.
- No new libraries. No changes to `practice-core.js`.
- Add focused DOM tests only if `dom_fixture.cjs` already supports it for that file. Otherwise rely on the browser review.

---

## 6. Files

| File | Change |
| --- | --- |
| `wwwroot/css/study-kit.css` | **new**: section 3 |
| `wwwroot/css/personal-sets.css` | update: sections 4.1–4.4; remove rules made obsolete (`.ts-study-icon` etc.) |
| `wwwroot/css/practice.css` | update: sections 4.5–4.12 |
| `Views/Practice/_ModeArt.cshtml` | **new**: 3.10 |
| `Views/Set/{Index,Detail,CardDetail,AddCards,Import,CreateSet,Settings,EditCard,Shared}.cshtml`, `_SetTile`, `_SetActionsMenu`, `_ShareSetModal`, `_SetInfoForm`, `_CardDetailFields` | markup + `study-kit.css` link |
| `Views/Practice/{Index,Session,HardWords}.cshtml`, `_ModeCard` | markup + `study-kit.css` link |
| JS files in 5.2 | as listed |
| `docs/design-system/team-ui-workflow.md` §6 tracker | update the two existing rows ("Bộ thẻ", "Luyện tập") to mention `study-kit.css`. This is allowed: these are the owner's rows. |
| `.ai/context/known-issues.md` | only if a limitation is found |

---

## 7. Phases

**Run mode: continuous.** Go from U0 to U10 in one run without stopping to ask the owner. A phase ends only when its **gate (7.1) is fully green**. Then commit and start the next phase immediately. Do not pause for confirmation between phases, and do not end the run with a question while phases remain.

Each phase:
1. Open the mockup(s) and the listed sections.
2. Implement.
3. Run the **phase gate (7.1)**.
4. Fix and re-run the whole gate until it is green (7.2).
5. Write the phase gate record into section 11 (7.3).
6. Commit locally with `tct-commit-message`.
7. Continue to the next phase.

### 7.1 Phase gate (every phase, every item, in this order)

| # | Check | Command / method | Pass when |
| --- | --- | --- | --- |
| G1 | Build | `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` | 0 errors, **no new warnings** vs U0 |
| G2 | Area tests | `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~PersonalSet\|FullyQualifiedName~Practice\|FullyQualifiedName~CriticalFlow"` | 0 failed; count ≥ U0 for these filters |
| G3 | Full tests | `dotnet test TCTEnglish.Tests -c Release` | 0 failed; passed ≥ U0; skipped = U0 (explain any change) |
| G4 | Node tests | `node --test scripts/tests/*.cjs` | 0 failed; count ≥ U0 |
| G5 | Hooks | Re-run the hook inventory grep from U0 (5.1). Diff it against the saved U0 list. | No hook removed or renamed |
| G6 | Tokens-only | The `Select-String` check in section 8 + `git diff --check` | Prints nothing |
| G7 | Scope | `git diff --name-only <previous phase commit>` | Only files allowed by section 6; no forbidden file |
| G8 | Browser: render | For each page touched in this phase: open it at **1440, 1024, 768, 390, 320** and at 200% zoom. Save screenshots to `.tmp/ui-refresh/<phase>/`. | No horizontal scroll, nothing clipped or overlapping; no console errors (read the console); no failed network requests for CSS/JS |
| G9 | Browser: mockup match | Compare 1440 and 390 screenshots side by side with the phase's mockup(s). | Layout, order, sizes, colours and states match, except the differences recorded with a reason |
| G10 | Browser: behaviour | Run the **behaviour checklist** for the phase (7.4), with mouse **and** keyboard only. | Every item works as before the phase |
| G11 | Accessibility | Tab through each touched page: focus visible on every control; one `<h1>`; labels on icon buttons; live regions still announce (inspect the DOM text); reduced-motion emulation removes motion | All pass |
| G12 | Contrast | Script the computed colours of new or changed text/control pairs on the touched pages | Text ≥ 4.5:1 (≥ 3:1 for ≥24px or ≥18.66px bold), control boundaries and icons ≥ 3:1 |
| G13 | Regression spot-check | Open `/Home/Study/{id}`, `/Home/QuizMode/{id}`, `/Dashboard` (or home), one Vocabulary page | Unchanged from U0 screenshots |

U0 only runs G1, G3, G4, G8 (before-screenshots) and records the baselines. U10 runs the full gate on **every** page and mode.

### 7.2 When the gate fails

- Fix the cause, then re-run **the whole gate**, not only the failed item.
- **Up to 3 fix attempts per failing item.** If an item still fails after 3 attempts:
  - **(a)** If the failure is caused by this phase's change, revert just that change (keep the old look for that element). Record it as "deferred" with the reason. Re-run the gate.
  - **(b)** If the failure is outside scope (an existing bug, an environment problem such as the app not starting or no browser), record the exact evidence, mark the item "blocked". Continue only when every in-scope item is green.
- Never mark a check as passed if it was skipped or blocked.
- Never weaken, skip or delete a test to make the gate pass.
- Never edit a forbidden file to make the gate pass.
- **Stop the whole run** (and report) only if:
  - the build or the full test suite cannot be made green without touching a forbidden file; or
  - the base branch state differs from section 0 in a way that makes the plan unsafe.

### 7.3 Phase gate record (write into section 11 before each commit)

```text
### Uk — <name>
- Commit: <hash after commit>
- G1 build: pass (warnings: n, U0: n)
- G2 area tests: passed x / failed 0 / skipped y
- G3 full tests: passed x / failed 0 / skipped y (U0: …)
- G4 node: passed x / failed 0
- G5 hooks: unchanged (n checked)
- G6 tokens/diff-check: clean
- G7 scope: files …
- G8 render: pages × widths checked, screenshots .tmp/ui-refresh/Uk/
- G9 mockup: differences …
- G10 behaviour: checklist items 1..n pass
- G11 a11y: …
- G12 contrast: pairs + ratios
- G13 regression: pass
- Fix attempts / deferred / blocked: …
```

### 7.4 Behaviour checklists (G10)

| Phase | Items to run, one by one |
| --- | --- |
| U1 | Every in-scope page still loads; Bootstrap dropdowns, modals and toasts open and close; buttons are clickable and pressable with Enter/Space |
| U2 | Tabs switch relation; tile opens its set; "Ôn" quick button opens due review; "Tạo bộ thẻ" and the add tile open create; "Luyện tập" opens `/Practice`; empty state for a user with no sets |
| U3 | Search (type, clear); each filter select and "active" styling; sort; list/grid toggle; selection mode, select all, bulk suspend/move/topic/delete (cancel and confirm); infinite scroll / load more; speak button; suggestion badge appears on the right tile; each study tile link URL equals U0; actions menu items (share modal on/off/copy/regenerate, settings, delete); "Theo học" for a non-owner; "Thêm từ"; mobile layout at 390 |
| U4 | Speak US/UK; image credit; "Học từ này" and "Gõ từ này" URLs; "Sửa"; overflow menu → delete modal (cancel/confirm); suspend switch on → reload shows on; off again; `aria-checked` and `aria-pressed` agree; synonym chip links and "+" add links |
| U5 | AddCards: add rows, AI suggestion (stubbed), save, validation errors; Import: paste/CSV preview and import; Create and Settings: save and validation; EditCard: save; Shared page as anonymous: follow/clone prompts; share modal |
| U6 | Each mode card selects (mouse and arrow keys); count resets per mode; test type checkboxes (last one cannot be unchecked) and ask radios; source radios with due count; switch; summary line updates; "Bắt đầu luyện" URL equals U0 for the same choices; remembered setup after reload; "Từ hay quên" link |
| U7 | Quiz, typing, listening, cloze: answer right, wrong and (typing) close; keys 1–4/A–D, R, Enter; hint reveals letters; diff shows; "Không nhớ"; listening auto-speaks once and "Nghe chậm"; cloze without examples shows the empty state; feedback sheet focus moves to "Tiếp tục"; sheet not under the AI launcher at 390/320; exit modal; summary buttons ("Luyện tiếp", "Học các từ sai", "Từ hay quên", "Xong") URLs; "Tính vào ôn tập" on changes a card's status, off does not |
| U8 | Flashcard: flip by click/Space/Enter, ←/→, 1/2, R; swipe left/right with pointer emulation; direction toggle; shuffle; counts; end screen restart options. Match: start screen, timer runs, correct pair leaves a dashed slot, wrong pair shows "+1 giây" and adds 1s, board 2, result, "Kỷ lục mới!" on a faster run, record shown next time |
| U9 | Test: answer every type, jump grid scrolls/focuses, unanswered-confirm modal, submit, result filter "Chỉ câu sai", "Làm lại câu sai", draft restore after reload. Learn: stage map renders done/current/locked, start stage, miss re-queues, round end, resume prompt after reload, completion links. Hard words: filter, table, both action URLs, empty state |
| U10 | All of the above once more, end to end, on a fresh browser profile |

| Phase | Work | Acceptance |
| --- | --- | --- |
| U0 | Baseline: branch/HEAD/tree, create `feature/study-ui-refresh` from `feature/practice-modes`, build, full `dotnet test -c Release`, all node tests, record counts. Save this plan. Open all 12 mockups. Grep and save the hook inventory (5.1) into section 11. Start the app (`scripts/start-local.ps1 -NoBrowser`) and capture **before** screenshots of every page in scope at 1440/390. | Counts recorded; before screenshots exist |
| U1 | `study-kit.css` (section 3) + link it in every in-scope view + `_ModeArt.cshtml` | All pages render, no visual regressions beyond the new button look; tokens-only grep clean |
| U2 | `/Sets` (4.1) | Matches mockup 01 within the rules; tests green |
| U3 | `/Sets/{id}` (4.2) incl. `set-detail.js` badge move; mobile per mockup 11 | Filters, search, selection, bulk, grid/list toggle, share modal, infinite scroll all still work (manual check) |
| U4 | `/Sets/Cards/{id}` (4.3) incl. switch | One visible term, h1 present (hidden), suspend toggles and persists, delete via menu works |
| U5 | Other Set pages (4.4) | AddCards composer behaviour test green; import preview works; settings save works |
| U6 | Practice hub (4.5) | Mode select, defaults, suggestion, stat lines, submit URL unchanged (compare with U0) |
| U7 | Session shell + drills + summary (4.6, 4.7) | Each drill completes with keyboard and mouse; feedback sheet clears the AI launcher; mockups 05 and 12 |
| U8 | Flashcard (4.8) + Match (4.9) | Swipe, keys, marks, end screen; penalty float, matched slots, record badge |
| U9 | Test (4.10) + Learn (4.11) + Hard words (4.12) | Draft restore, retry missed, result filter; stage map with keyboard/screen-reader text; resume |
| U10 | Full verification (section 8), docs, section 11 | Definition of done |

---

## 8. Verification

```powershell
dotnet build TCTEnglish/TCTEnglish.csproj -c Release
dotnet test TCTEnglish.Tests -c Release
node --test scripts/tests/*.cjs
git diff --check
# tokens-only checks on the three owner CSS files (must print nothing):
Select-String -Path TCTEnglish/wwwroot/css/study-kit.css,TCTEnglish/wwwroot/css/personal-sets.css,TCTEnglish/wwwroot/css/practice.css -Pattern '#[0-9a-fA-F]{3,8}\b|rgba?\(|:root|!important|font-weight:\s*(8|9)00'
```

- Counts must not drop below U0.
- **Browser review** (`tct-ui-review`, `team-ui-workflow.md` §7):
  - every in-scope page and every practice mode at 1440 / 1024 / 768 / 390 / 320 and 200% zoom;
  - keyboard-only run;
  - focus visible on every new control (mode cards, seg items, tiles, switch, stage nodes);
  - reduced-motion emulation;
  - long Vietnamese definitions and multi-word terms ("look forward to");
  - no horizontal scroll;
  - the feedback sheet never hides under the AI launcher;
  - side-by-side compare with each mockup, writing the differences into section 11.
- **Contrast:** scripted check of the new rendered pairs, at least:
  - white on `--tct-success-text` / `--tct-danger` / `--tct-warning-text` / `--tct-info-text`;
  - `--k-fg` on `--k-bg` for all six hues, including the accent tint;
  - text on `--tct-disabled-bg` (seg track);
  - `--tct-text-secondary` on soft tiles;
  - star outline.
- **Regression:**
  - legacy `/Home/Study|QuizMode|WriteMode|MatchingMode` untouched;
  - Dashboard, Vocabulary and Admin unchanged;
  - an AI chat citation link still opens.
- Before/after screenshots stay in ignored `.tmp/ui-refresh/` (never committed).

## 9. Definition of done

- [ ] Every page in section 2 follows its section-4 spec and its mockup, with the differences recorded.
- [ ] No route, API, service, view model, migration or `practice-core.js` change; shared files untouched.
- [ ] All hooks in 5.1 still present; all tests green at or above the U0 counts.
- [ ] Tokens-only grep is clean; every `color-mix` is listed in section 11.
- [ ] Accessibility floor (R7) is verified in the browser; contrast results are recorded.
- [ ] Reduced motion disables all transforms and keyframes.
- [ ] No mascot, no new fonts, packages, CDNs or images.
- [ ] The tracker rows are updated; the Vietnamese report separates verified from not verified.

## 10. Risks

| Risk | Mitigation |
| --- | --- |
| Scoped `.btn` overrides leak into modals (Bootstrap modals render inside the page wrapper) | Intended: the modals belong to these pages. Check `_ShareSetModal` and `#tsDeleteCard`. |
| Bottom sheet overlaps the AI launcher on phones | Right padding rule (4.6), checked at 390 and 320 |
| `:has()` support | Already used by the current hub (`.tp-mode-card:has(input:checked)`). Keep using it, without adding a JS fallback. |
| Hidden `<h1>` on card detail reduces visible heading semantics | The visible term is the same text; record it, and keep the breadcrumb visible. |
| Test strings tied to markup | Run the integration tests every phase; never rename asserted strings. |
| `color-mix` unsupported in old browsers | A plain-token fallback is declared first. |

## 11. Execution notes (agent fills in)

Add one "Phase gate record" (7.3) per phase, U0 → U10, in order.

- Branch / HEAD at U0: `feature/study-ui-refresh` created from `feature/practice-modes` at `f690ff5`. Tree: modified `TCTEnglish/appsettings.Development.json` (never staged), untracked `Claude outputs/`. Matches section 0.
- Baseline counts (Release, U0):
  - build: 0 warnings, 0 errors;
  - area filter `PersonalSet|Practice|CriticalFlow`: passed 106 / failed 0 / skipped 0;
  - full `dotnet test`: passed 1287 / failed 0 / skipped 22 (MariaDB-gated tests, as before), total 1309;
  - `node --test scripts/tests/*.cjs`: pass 115 / fail 0.
- Hook inventory (5.1): 390 unique entries, regenerated each gate by one script (JS class literals `tp-*` from `practice-*.js`, `ts-*` from `set-detail.js`, `set-card-detail.js`, `set-composer.js`, plus `id=` / `data-tp-*` / `data-ts-*` attributes in `Views/Set/*.cshtml` and `Views/Practice/*.cshtml`). Baseline list kept in ignored `.tmp/ui-refresh/hooks-u0.txt`.
  - JS `tp-*` classes at U0 (the ones that must stay): `tp-bar-row(__count|__fill|__fill--done|--learning|--new|__label|__track)`, `tp-chip__*`, `tp-chip-word(s)`, `tp-cloze(__blank)`, `tp-diff*`, `tp-dot(s)`, `tp-example(-vi)`, `tp-feedback(__actions|__answer|__def|__term|__title)`, `tp-flash*`, `tp-hint-line`, `tp-jump*`, `tp-keyhint`, `tp-kpi*`, `tp-listen(__play)`, `tp-match-*`, `tp-meaning*`, `tp-missed*`, `tp-option(__key|__label|__mark|--correct|--wrong)`, `tp-options`, `tp-pill`, `tp-pop`, `tp-prompt`, `tp-q__*`, `tp-result*`, `tp-ring(__value)`, `tp-seg(__part)`, `tp-slide-in`, `tp-stage-card__meta`, `tp-stages`, `tp-stars`, `tp-step(__name|-list)`, `tp-summary*`, `tp-swipe-hint`, `tp-term(-line)`, `tp-test*`, `tp-tf-*`, `tp-tile__label`, `tp-timer(-text)`, `tp-typing*`, `tp-visual`.
- Environment: Docker Desktop was stopped at U0; started with `docker desktop start`. App served from `scripts/start-local.ps1 -NoBrowser -Configuration Release -WebPort 5100`. Dev data: user Premium has 3 sets (Practice deck 14 words, two Smoke decks 60 words); set id 5, card id 141 used for screenshots. Screenshots are driven by a small headless-Chrome DevTools script kept outside the repo (no browser MCP in this environment).
- Routes used: `/Sets`, `/Sets/{id}`, `/Sets/Cards/{id}`, `/Sets/{id}/Add|Import|Settings`, `/Sets/Cards/{id}/Edit`, create form at `/Home/CreateSet` (`/Sets/Create` is POST only), `/Practice`, `/Practice/HardWords`, `/Practice/{mode}?set={id}`.
- U0 before-screenshots: `.tmp/ui-refresh/U0/<page>-<1440|390>.png` for 19 pages (zero console errors, zero failed requests; one h1 per page).
- `color-mix` uses (each has a plain-token fallback declared first; all in `study-kit.css`): danger edge `--k-edge` (danger 70% + text); accent tint `--k-bg` (accent 30% + surface); `.k-btn--success|danger|warning` edges (65% + text); `.k-progress__fill` inset shadow (text 12% + transparent); `.btn .k-kbd` chip (surface 22% + transparent); `.ts-hero-art` panel in `personal-sets.css` (surface 75% + transparent); `.tp-listen__play` edge in `practice.css` (sq colour 65% + text). Later phases add to this list.
- Mockup vs rule differences, per page:
  - `/Sets` (mockup 01): relation tabs show no counts (see data list); tiles have no CEFR tag and no "Mới" status (the model has neither); the quick "Ôn" button straddles the cover edge (top-right of the body) so it never covers the status badge, instead of sitting bottom-right; the accent hue is the 30% accent tint from plan 3.1 (mockup shows a similar light yellow).
  - `/Sets/{id}` (mockups 02, 11): the "‹ Bộ thẻ" back link is the breadcrumb (rule); on phones all filter selects wrap under the search instead of hiding behind a "more" control (there is none; plan 4.2 says not to hide controls without an alternative); at ≤575px IPA, CEFR and the speak button are hidden in the word list as the plan says (they remain on the card page); the study tile "Học" shows the suggestion badge only when no review is due (existing JS rule), so mockup 02's badge appears on sets without due words; the word-row CEFR column shows a tag only when a card has a level (dev data has none, so the tag is covered by code review, not observed).
  - `set-detail.js` edits beyond the plan's 5.2 row (DOM class/attribute only, no logic): `lang="en"` on term links, CEFR text wrapped in `.k-cefr`, a `ts-hue-*` class on grid cards from the status key. The suggestion badge keeps its insertion point (first child of `.ts-study-text`, which is already after the art box) and gains the wand icon.
  - `/Sets/Cards/{id}` (mockup 03): the "‹ Giao tiếp văn phòng" back link is the breadcrumb (rule; the breadcrumb is kept visible); the progress card has three stat tiles (quên, nhớ liên tiếp, ôn tới) plus a muted "Lần ôn gần nhất" line, not the mockup's four, because the view has no total review count (plan 4.3 says to use only existing fields); the third tile spans the row; the speak button sits inside each US/UK chip, and the single-pronunciation case shows one chip; with no IPA at all the lone speak button stays beside the term; the examples/synonym/family cards stay separate cards (the mockup merges synonyms, phrases and word family into one card, which would change section headings the page already has); the mockup's POS/CEFR/topic row sits below the hero card as specified; the term stays aria-hidden while the single `<h1>` is visually hidden.
  - `/Practice` (mockup 04): the hard-words link and the setup panel live inside the existing `<form id="tpSetup">` (the grid needs one container; a link inside a form is harmless), with the panel plus start bar wrapped in `.tp-side` (a sticky single card at ≥1200px, `display: contents` below so the start bar stays the sticky bottom bar as before); the hard-words link therefore comes after the setup panel in tab order on desktop; the checked-state badge uses `--k-sq-bg` with `--k-sq-fg` instead of `--k-fill` with white (white on the success fill is 2.54:1, below the 3:1 icon floor; rule wins; the sq pair gives 5.48 for success, 7.65 for primary, 11.41 for accent); phones keep two columns of big mode cards (plan 4.5), not the old single column; the practice summary line reads like the mockup because `core` builds it (unchanged); the mockup's "Học" card shows the suggestion only when a set is chosen (existing JS rule).
  - Session shell, drills and summary (mockups 05, 10, 12): the question wrapper `tct-card` that makes the card look is created in `practice-drill.js`, not in `practice-ui.js`, so the class was removed there (plan 5.2 lists the card removal under practice-ui.js; the effect is the same); the top bar keeps a background equal to the shell canvas (`--tct-surface-muted`) so it stays invisible but still hides content that scrolls under the sticky bar; the feedback sheet extends into the shell's bottom padding (negative margin) so it sits on the viewport edge; the AI launcher box measures 68×68 px (ring included) at 390 and 320 px, so the sheet's right padding at ≤1100px is `target-min + 3 × space-md` (92px; the plan's `+ 2 ×` left a 6px overlap, measured), clear by 8px and never moved; the listening play button uses the dark `--k-sq-bg` (info text) with a white icon (white on the cyan fill is 2.4:1) and its edge is a `color-mix` of that colour; the speak buttons are always the primary hue (they inherited the page hue and turned red in cloze); typing diff rows sit on white pills inside the coloured sheet so the soft backgrounds show; the summary has no sentence under the title (the mockup's "Bạn trả lời đúng 8/10 câu…" is not a quoted string in the plan, R8) and the "Chính xác" tile is replaced by "Câu hỏi" because the ring now shows the percentage; unearned stars are solid `--tct-border` stars (not outlines); `tp-page--wide` is added for learn and test, and the width rules also key on `data-tp-mode` (test 820px, wide 1080px).
  - `practice-hub.js` edits: pill labels use `k-seg__item` instead of the Bootstrap button classes; the stat node is plain text (`tp-stat`).
  - `set-card-detail.js`: only the switch (`aria-checked` mirrors `aria-pressed`, label text no longer rewritten).
- Data not available (elements left out):
  - `/Sets/Cards/{id}`: the "lần ôn" (total reviews) tile; the dev data has no card with IPA, CEFR, topic, synonyms or an emoji/image, so those sub-layouts were checked on a synthetic DOM render of card 128 (`.tmp/ui-refresh/U4/card-rich-*.png`: US/UK chips, relation rows, chips) and by code review, not on stored data.
  - `/Sets`: tab counts (`SetIndexViewModel` returns only the lists of the active filter, so counts for the other tabs do not exist); tile CEFR tag; "Mới" badge.
- Contrast results:
  - `/Sets/{id}` (browser, U3): suggestion badge 6.84; study note 5.48 (success text on white); study name 17.10; "Luyện riêng" link 7.31; hero meta 8.17–8.95; due count 6.23 (warning-text on the accent tint); title 16.44; breadcrumb link 6.72–7.35, breadcrumb current 5.16; hero term (40px bold) 5.30; result count 5.61; table header 5.61; term 17.10; meaning 17.10; status badge 8.90; speak icon 6.84.
  - `/Sets` (measured in the browser, U2): tile term 15.02 (accent tint) / token pairs in U1; tile name 17.10; meta 5.87; "cần ôn" badge 5.91; group count 5.61; tab text 17.10 (selected); add-tile title 8.90, note 5.61; quick button 5.28; visibility icon 9.31.
- Browser coverage / not verified:
- Final counts:

### U0 — Baseline
- Commit: see `git log` ("docs(ui): save study UI refresh plan and prompt, record U0 baseline")
- G1 build: pass (warnings: 0, U0: 0)
- G2 area tests: passed 106 / failed 0 / skipped 0 (baseline)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (baseline)
- G4 node: passed 115 / failed 0 (baseline)
- G5 hooks: baseline saved (390 entries)
- G6 tokens/diff-check: not applicable (no CSS change yet)
- G7 scope: `docs/superpowers/plans/2026-11-11-study-ui-refresh.md`, `.ai/prompts/claude-code-study-ui-refresh.md` only
- G8 render: 19 pages × 1440 and 390 captured to `.tmp/ui-refresh/U0/` (before state); no console errors, no failed requests
- G9–G13: not run in U0 (per plan 7.1)
- Fix attempts / deferred / blocked: none

### U1 — Shared kit
- Commit: see `git log` ("feat(ui): add study-kit.css and mode art partial")
- G1 build: pass, 0 errors. A full rebuild (`--no-incremental`) reports 44 warnings, all in files this phase did not touch. U0's incremental build printed 0 because nothing recompiled; the true baseline is the same 44.
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: unchanged (390 checked)
- G6 tokens/diff-check: clean. The U0 header comments in `personal-sets.css` / `practice.css` contained the words the grep looks for, so they were reworded (the grep now prints nothing). `git diff --check` is clean for this phase's files; it only reports a blank line at EOF in the unrelated, never-staged `appsettings.Development.json`.
- G7 scope: `wwwroot/css/study-kit.css` (new), `Views/Practice/_ModeArt.cshtml` (new), the stylesheet link in 12 views, the header comment of the two page CSS files, this plan. No forbidden file.
- G8 render: 19 pages × 1440/1024/768/390/320 in `.tmp/ui-refresh/U1/`; 0 console errors, 0 failed requests. No horizontal overflow except two items not caused by U1: the `/Practice/HardWords` table header at 320px (recheck in U9) and the legacy home decoration at ≤390px. 200% zoom: 320/390 px widths cover the same CSS viewport as 640/780 px at 200%; a literal zoom pass is done in U10.
- G9 mockup: not applicable (the kit is not a page). Buttons now show the pressed edge on every page, as designed.
- G10 behaviour: all pages load; a Bootstrap dropdown opens, a modal opens and closes (checked on set detail); buttons are real links/buttons, so Enter/Space work.
- G11 a11y: Tab order and visible focus ring checked on `/Practice` (shell, mode cards, radios, select, start button); one `<h1>` per page; reduced-motion rules in the kit remove the :active transform and the keyframes.
- G12 contrast (computed from token values; page pairs are re-measured in each page phase): white on success-text 5.48, danger 6.46, warning-text 7.09, info-text 5.36, primary 5.28; fg on bg per hue: primary 6.84, success 5.21, warning 6.84, danger 5.91, info 5.15, accent tint 15.02; text-secondary on seg track 8.16; text on seg track 14.99; text-secondary on every soft tint ≥ 8.3; kbd 8.16; control boundary `--tct-border-control` on white 3.66; switch knob 3.66; star outline `--tct-warning-text` on canvas 6.62 (the accent fill alone is 1.40, so the outline is required).
- G13 regression: `/Home/Index`, `/Home/Study/5`, `/Home/QuizMode/5` render without console or network errors; the kit is linked only from Set/Practice views (grep). Not verified: a Vocabulary page (the route tried returned 404, so it was not compared).
- Fix attempts / deferred / blocked: form-switch label gap fixed after the first render (1 attempt). Environment note: the running app must be stopped (through its background task) before a rebuild, because Razor views compile into the DLL.

### U2 — `/Sets`
- Commit: see `git log` ("feat(sets): restyle the personal sets list")
- G1 build: pass, 0 errors; no new warnings (the same 44 as U1; none in `Index.cshtml` / `_SetTile.cshtml`)
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: nothing removed or renamed (one prefix literal `ts-hue-` appears in the Detail work that is in the same working tree, not in this commit)
- G6 tokens/diff-check: clean
- G7 scope: `Views/Set/Index.cshtml`, `Views/Set/_SetTile.cshtml`, `wwwroot/css/personal-sets.css`, this plan
- G8 render: `/Sets` at 1440/1024/768/390/320 in `.tmp/ui-refresh/U2/` (plus the empty state, Admin account); 0 console errors, 0 failed requests, no horizontal overflow
- G9 mockup: compared 1440 and 390 with mockup 01 — differences listed above; tabs, section headings, tile cover/body/progress, add tile and header buttons match
- G10 behaviour: tabs navigate to `?filter=own|subscribed|shared` with `aria-current` on the active one; tile link opens `/Sets/5`; the quick "Ôn" button shows on hover and points at `/Home/Study/5?mode=due`; the add tile and the header "Tạo bộ thẻ" both go to `/Home/CreateSet`; "Luyện tập" goes to `/Practice`; the empty state (Admin has no sets) shows the message and both buttons
- G11 a11y: one `<h1>`; the add tile is `aria-hidden` + `tabindex=-1` because the header action does the same job; tab focus ring on the segmented tabs is a 2px solid ring; tile hover lift is removed under reduced motion
- G12 contrast: see "Contrast results" above
- G13 regression: Home/Study/QuizMode unchanged (they do not load the kit); the `/Practice` hub still renders
- Fix attempts / deferred / blocked: none

### U3 — `/Sets/{id}`
- Commit: see `git log` ("feat(sets): restyle the set detail page")
- G1 build: pass, 0 errors; no new warnings (the same 44; none in the changed views)
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: nothing removed or renamed (one prefix literal `ts-hue-` added by the JS)
- G6 tokens/diff-check: clean
- G7 scope: `Views/Set/Detail.cshtml`, `wwwroot/js/set-detail.js`, `wwwroot/css/personal-sets.css`, `wwwroot/css/study-kit.css` (the `.btn.btn-lg` specificity fix found here), this plan
- G8 render: `/Sets/5` at 1440/1024/768/390/320 in `.tmp/ui-refresh/U3/`; also grid view, bulk bar and share modal at 1440 and a 720px render (= 200% zoom of 1440); 0 console errors, 0 failed requests, no horizontal overflow
- G9 mockup: compared 1440 and 390 with mockups 02 and 11; differences recorded above
- G10 behaviour (mouse + scripted keyboard, all pass): 14 rows load with count "14 từ"; search "lib" gives 1 result and marks the search active; status filter "Đã thuộc" gives 11 rows and marks the select active; sort A–Z; list/grid toggle (14 cards, `aria-pressed` follows, cards carry a status hue); selection mode shows checkboxes; bulk bar shows "Đã chọn 1"; bulk delete modal opens and cancels; speak buttons are 36px; actions menu lists import, practice, share, export, settings; share modal opens and closes; study tile URLs equal U0 (`/Practice/{learn|test|match|flashcard}?set=5&…&from=set`); the suggestion badge appears on "Học" for sets without due words (sets 3 and 4), sits between the art box and the name, and has the icon; hero buttons are 56px high. Not exercised: infinite scroll (the 14-word dev set fits one page), bulk suspend/move/topic/delete confirm (cancel path only, to keep dev data untouched), share copy/regenerate, "Theo học" for a non-owner, "Thêm từ" click-through, the CEFR tag (no card has a level)
- G11 a11y: one `<h1>`; every icon-only button has a name (0 without); live regions kept (`ts-count`, `ts-bulk-count`, toast host); tab order hero → study tiles → search, 2px focus ring on each; reduced-motion emulation: tile hover transform is `none` and transitions are 0.01ms
- G12 contrast: see "Contrast results" (all ≥ 4.5, large hero term 5.30)
- G13 regression: Home, Study and QuizMode render with no console or network errors; they do not load the kit
- Fix attempts / deferred / blocked: 3 fixes in one pass, none repeated: (1) mobile column rules lost to `td { display: block }` — selectors raised to `td.ts-col-*`; (2) hero column flex-basis left a gap on phones; (3) `.btn-lg` min-height lost to the kit's `.btn:not(.btn-sm)` rule (hero buttons were 45px, now 56px)

### U4 — `/Sets/Cards/{id}`
- Commit: see `git log` ("feat(sets): restyle the card detail page")
- G1 build: pass, 0 errors; no new warnings (the same 44; none in `CardDetail.cshtml`)
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: no id, data attribute or class was removed from any view. The only line that left the grep list is the JS literal `ts-suspend-label` in `set-card-detail.js`: plan 4.3 says the script stops rewriting the label, so the script no longer looks it up; `#ts-suspend-label` is kept in the markup as a visually hidden span with the fixed text "Tạm ẩn khỏi ôn tập" (the switch is named by it)
- G6 tokens/diff-check: clean
- G7 scope: `Views/Set/CardDetail.cshtml`, `wwwroot/js/set-card-detail.js`, `wwwroot/css/personal-sets.css`, this plan
- G8 render: `/Sets/Cards/141` and `/Sets/Cards/128` at 1440/1024/768/390/320 in `.tmp/ui-refresh/U4/` (also the edit page, which only picks up the kit), 0 console errors, 0 failed requests, no horizontal overflow; 720px (= 200% zoom of 1440) has no overflow
- G9 mockup: compared 1440 and 390 with mockup 03 — differences listed above; two-column body at ≥992px with the sticky progress card, progress first below 992px, 6px primary top bar, 64px term, soft stat tiles, switch row
- G10 behaviour: one `<h1>` (visually hidden, term text) and one visible aria-hidden term (64px); actions are Học từ này / Gõ từ này / Sửa (URLs `/Practice/learn?words=128&from=set`, `/Practice/typing?words=128`, `/Sets/Cards/128/Edit`) + overflow menu; the menu opens and "Xóa từ" opens the delete modal (title "Xóa “run”?"), cancel closes it (delete was not confirmed, to keep dev data); speak buttons appear; the suspend switch: `role=switch`, 46×26, initial `aria-checked=false`; Space toggles it on (toast "Đã tạm ẩn run", `aria-checked` and `aria-pressed` both true, track turns primary), reload keeps it on, a click turns it off and it is restored; layout: side column right of the main one at ≥992px, stacked above it at 768 and 390
- G11 a11y: Tab order actions → overflow menu → suspend switch → example speak, each with a 2px solid focus ring; 0 controls without an accessible name; reduced motion makes the switch knob transition 0.01ms
- G13 regression: Home, Study and QuizMode were re-checked in U3 and are unaffected (they do not load the kit)
- G12 contrast: term 17.10; Vietnamese definition 16.35; POS badge 6.84; example 17.10, highlighted term 7.65, translation 5.87; speak icon 6.84; stat tiles danger 5.91, success 5.21, info 5.15; last reviewed 5.87; suspend title 17.10, help 5.87; status badge 5.21; primary button 5.28; outline button 17.10; breadcrumb link 7.31
- Fix attempts / deferred / blocked: none

### U5 — Other Set pages
- Commit: see `git log` ("feat(sets): apply the study kit to the add, import, create, settings, edit and shared pages")
- G1 build: pass, 0 errors; no new warnings
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0 (includes the set composer behaviour test)
- G5 hooks: no id, data attribute or class was removed from a view; the only line still absent from the JS grep list is the U4 `ts-suspend-label` literal (see U4)
- G6 tokens/diff-check: clean
- G7 scope: `Views/Set/AddCards.cshtml` (the mode toggle becomes `.k-seg`, keeping `data-ts-mode`, `aria-pressed` and the `active` class the composer script toggles), `wwwroot/css/personal-sets.css`, this plan. Import, Create, Settings, EditCard and Shared change through CSS only (they already use the kit buttons, `.tct-card` panels and the `.ts-hero` styles)
- G8 render: add, import, create, settings, edit at 1440/1024/768/390/320 in `.tmp/ui-refresh/U5/`, plus the filled composer, the import preview and the anonymous shared page at 1440 and 390; 0 console errors, 0 failed requests (one 400 from `/api/vocab/enrich` while a test term was typed in the composer, an existing endpoint rejecting a nonsense word, not a rendering problem), no horizontal overflow
- G9 mockup: no mockups exist for these pages; the kit look is applied as plan 4.4 describes: pressed-edge buttons, 20px panels, numbered 28px circle on composer rows, 18px term input, dashed suggestion chips, dashed add-row button, edge shadow on the save bar. Visibility choices on create/settings keep their title + description as selectable cards (a segmented control would lose the descriptions), with a 2px border and an edge when selected
- G10 behaviour: composer adds a row (numbers 1, 2), a card with only a term is marked "thiếu nghĩa" and the status line updates, the mode toggle switches to the paste pane (`aria-pressed` and `active` agree) and "Tách thành thẻ" splits two lines into rows and returns to the single pane; import: paste 3 lines, "Xem trước" shows 3 rows "3 từ sẵn sàng · 0 trùng · 0 lỗi", "Sửa danh sách" goes back; create: submitting an empty name stays on the form with 2 validation messages; settings: clearing the name shows 1 validation error and nothing is saved (name still "Practice deck" on reload), the delete modal opens with the confirm button disabled and cancels; edit card: fields load (term, definition); shared page as anonymous: one `<h1>`, "Đăng nhập để học bộ này" button 56px, 5 tiles. Not exercised, to keep dev data and AI usage untouched: saving cards, running the import, saving settings/edit, AI suggestions (stubbed in the node tests), follow/clone as a signed-in non-owner
- G11 a11y: one `<h1>` per page; the new mode toggle is a real button pair with `aria-pressed`; focus rings come from the shared rule
- G12 contrast: composer number 6.84; term input 17.10; mode toggle 17.10; add-row text 7.65; save status 9.31; sub-label 5.87; choice title 15.45 and text 5.30 (selected tint); form preview letter 15.02; danger button 6.46
- G13 regression: unchanged pages do not load the kit
- Fix attempts / deferred / blocked: the settings form's "Hủy" link text sat at the top of its stretched box next to the primary button; fixed with an inline-flex rule scoped to `.ts-form .d-flex > .btn` (1 attempt)

### U6 — `/Practice` hub
- Commit: see `git log` ("feat(practice): restyle the practice hub")
- G1 build: pass, 0 errors; no new warnings
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: no id, data attribute or class was removed from a view (`data-tp-card`, `data-tp-stat`, `data-tp-suggest`, `data-tp-hub`, `#tpSetup`, `#tpSet`, `#tpCountPills`, `#tpTypePills`, `#tpAskPills`, `#tpSourcePills`, `#tpSrs`, `#tpSrsField`, `#tpSummary`, `#tpStart`, `#tpHubConfig`); the JS still creates `tp-pill`
- G6 tokens/diff-check: clean
- G7 scope: `Views/Practice/Index.cshtml`, `Views/Practice/_ModeCard.cshtml`, `wwwroot/js/practice-hub.js`, `wwwroot/css/practice.css`, this plan
- G8 render: `/Practice` at 1440/1024/768/390/320 in `.tmp/ui-refresh/U6/`, plus a render with synthetic stats (`tct:set-stats:5` written to localStorage, then removed) and the test-mode options; 0 console errors, 0 failed requests, no horizontal overflow; a 720px render (= 200% zoom of 1440) has no overflow
- G9 mockup: compared 1440 with mockup 04 — art tiles with the suggestion badge between art and name and the stat as 700-weight text, 4 skill rows with 48px icon squares, setup panel on the right with segmented controls and the 46×26 switch, start bar with the summary above the button, hard-words row with the red icon square; differences listed above
- G10 behaviour (mouse and keyboard): default form is mode learn, set mine, 10 từ, source all, summary "Học · Tất cả bộ của tôi · 10 từ"; ArrowRight on the checked mode radio moves to Kiểm tra and the counts change to 10/20/30; clicking each of the 8 mode cards resets the counts (learn 10/20/50, test 10/20/30, match 6/12/18, flashcard 20/50/cả bộ, skills 10/20/30), shows the type options only for test, hides the "Tính vào ôn tập" row for match, and shows the check badge on the selected card; the test types: unchecking everything leaves exactly one checked; choosing "Practice deck" shows "Đến hạn (3)" and the suggestion badge on "Học"; with synthetic stats the cards show "Đã thuộc 38%", "Lần trước 75%", "Kỷ lục 42,3 giây"; start goes to `/Practice/quiz?set=5&n=30&srs=1&from=hub` and the choices are remembered after a reload (quiz, set 5, 30, switch on); `?set=4` preselects set 4; the hard-words link goes to `/Practice/HardWords` with the badge 9. Submit URLs: the form field names/values and the JS that builds the URL are untouched, so they equal U0 by construction (no U0 capture of an identical choice set was taken)
- G11 a11y: one `<h1>`; 0 controls without a name; Tab goes set select → count → source → switch → start, each with a 2px ring; a mode card shows a 2px ring (offset 2px) when its radio has keyboard focus; reduced motion removes the card press transform. The AI launcher can cover the last ~20px of the start button at the right edge on viewports under ~800px tall (the launcher is fixed at the bottom right and the sticky panel reaches it); the label is not covered. Not moved or hidden (plan 4.6)
- G12 contrast: group title 8.90; mode name 16.24–17.10; hint 8.84–9.31; setup title 17.10; legend 17.10; segmented item 17.10; switch hint 5.87; summary 17.10; hard-link name 17.10, hint 9.31, count badge 5.91, icon 5.91; suggestion badge 6.84; check badge 5.48 (success) / 7.65 (primary)
- G13 regression: the legacy pages and the Set pages were re-rendered earlier in this run; this phase touches only Practice files
- Fix attempts / deferred / blocked: check badge colour changed once (contrast); phones changed to two columns after the first render (plan 4.5)

### U7 — Session shell, drills and summary
- Commit: see `git log` ("feat(practice): restyle the session shell, the four drills and the summary")
- G1 build: pass, 0 errors; no new warnings
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22)
- G4 node: passed 115 / failed 0
- G5 hooks: no id, data attribute or class literal was removed from the JS or the views (checked by diffing the U0 `tp-*` class list against the current JS); `ts-suspend-label` is the known U4 change
- G6 tokens/diff-check: clean
- G7 scope: `Views/Practice/Session.cshtml`, `wwwroot/js/practice-ui.js`, `wwwroot/js/practice-drill.js`, `wwwroot/css/practice.css`, this plan
- G8 render: quiz at 1440/1024/768/390/320, typing at 1440/390/768, listening at 1440/390, cloze at 1440/320, each with the first question, the first feedback sheet and the summary, in `.tmp/ui-refresh/U7/`; 0 console errors, 0 failed requests; no horizontal overflow (page width equals viewport at every size; a 720px render = 200% zoom of 1440)
- G9 mockup: compared the 1440 feedback sheet (wrong answer) with mockup 05, the 390 sheet with mockup 12, and the 1440 summary with mockup 10; differences listed above
- G10 behaviour (mouse and keyboard): quiz, typing, listening and cloze each run 9–10 questions to the summary; keys 1–4 answer, Enter continues (from the focused button and from the page); an option marks right/wrong, the other options dim, and the sheet appears in the outcome colour with a full-width "Tiếp tục" focused (button colour success/warning/danger by outcome); typing: the hint shows `r__` in the monospace mask and the button becomes "Gợi ý thêm", an exact answer is correct, a one-letter slip on a long word is "Gần đúng!" (warning sheet, button 7.09), a wrong answer shows the "Bạn gõ"/"Đáp án" diff; "Không nhớ" records a wrong answer; the exit modal opens after an answer, "Ở lại" closes it and returns focus to the exit button; cloze on a set without examples shows "Các từ này chưa có câu ví dụ để điền." with a link back to the hub for that set; listening shows the round play button and "Nghe chậm" (audio itself is browser speech and was not heard); the summary shows ring, stars, title, three KPI tiles, the missed list, "Luyện tiếp" (`/Practice/quiz?set=5&n=10&from=hub`), "Học các từ sai" (`/Practice/learn?set=5&words=…&from=hub`), "Từ hay quên" (`/Practice/HardWords`) and "Xong" (`/Practice?set=5`); progress writes: `srs` off gave 0 requests to `/Home/UpdateCardProgress` over 3 answers, `srs=1` gave 1 request for 1 answer
- G11 a11y: one `<h1>` (visually hidden); Tab order exit → options (each a button with a 2px ring); Enter on an option answers; the feedback region stays `aria-live=polite` and is read as "Chưa đúng … Tiếp tục" (the Enter chip is `aria-hidden`); the progress bar keeps `role=progressbar` with live `aria-valuenow`; reduced motion turns the sheet's rise animation off and the press transforms off; the sheet's button stays clear of the AI launcher at 390 and 320 px (launcher left edge 312/242, button right edge 304/234)
- G12 contrast (measured): prompt 5.61; counter 8.90; option label 17.10; key badge 8.16; key hint text 5.61 and chip 8.16; cloze sentence 14.99; translation 5.61; meaning 16.35; typing input 17.10; primary button 5.28; outline button 17.10; accent hint button 15.02; listening play 5.36 and "Nghe chậm" 5.15. From tokens: sheet title success 5.21 / warning 6.84 / danger 5.91; sheet buttons 5.48 / 7.09 / 6.46; ring and KPI pairs 5.15–6.84; star outline `--tct-warning-text` 6.62 on canvas
- G13 regression: Home, Study and QuizMode do not load the kit (checked in U3); the Set and hub pages were not touched in this phase
- Fix attempts / deferred / blocked: (1) feedback sheet floated 40px above the viewport bottom: fixed with a negative bottom margin; (2) speak buttons turned red in cloze: forced the primary hue; (3) top bar tint did not match the canvas: set to `--tct-surface-muted`; (4) launcher overlap of 6px: padding raised to 3 × space-md. Each fixed once

### U8 — Flashcard and Match
- Commit: see `git log` ("feat(practice): restyle the flashcard and match modes")
- G1 build: pass, 0 errors; no new warnings (no C# or view change in this phase; the full test run rebuilt it with the app stopped)
- G2 area tests: passed 106 / failed 0 / skipped 0 (U0: 106)
- G3 full tests: passed 1287 / failed 0 / skipped 22 (U0: 1287 / 22). The first attempt with the app running failed to copy `TCTEnglish.exe` ("file is locked by TCTEnglish"), an environment lock and not a test failure; the run was repeated with the app stopped through its background task
- G4 node: passed 115 / failed 0
- G5 hooks: no `tp-*` class literal, id or data attribute was removed (diff of the U0 class list against the current JS is empty)
- G6 tokens/diff-check: clean for the three owner CSS files
- G7 scope: `wwwroot/js/practice-flashcard.js`, `wwwroot/js/practice-match.js`, `wwwroot/css/practice.css`, this plan
- G8 render: flashcard and match at 1440/1024/768/390/320 in `.tmp/ui-refresh/U8/` (front, back, mid-swipe, end screen; start, selected, wrong pair, matched slots, result); 0 console errors, 0 failed requests
- G9 mockup: compared 1440 with mockups 08 and 07. Differences: flashcard — one toggle button drawn as a segmented control (label and `aria-pressed` kept on the button), "Xáo trộn" text added to the shuffle button, the speak button sits over the card's top-right instead of inside it (a button inside the card button is invalid HTML), the tag shows the first part of speech and the CEFR level when they exist (dev data has no level, so only "Động từ" was seen), the POS label map is repeated in the JS because the server list is not sent to the page; match — the top progress bar and counter are hidden in match (CSS) and the page title "Ghép thẻ" is inserted into the bar by the script, the stopwatch icon is gone (the number is the focus), the HUD record badge appears only when a record exists at the start of the session
- G10 behaviour (mouse, keyboard, pointer swipe): flashcard — flip by click and by Space, ArrowRight/ArrowLeft change card (counter 2/14, 1/14), direction toggle switches to "Nghĩa trước" (label, `aria-pressed` and the active part follow), shuffle shows the toast "Đã xáo trộn các thẻ còn lại.", keys 1 and 2 mark and advance (chips Chưa thuộc/Đã thuộc update), a 160px pointer swipe right marks known and swipe left marks unknown (chips 12|2 then counter 5/14), the end screen after the last card shows ring 86% "đã thuộc", "Hết thẻ rồi!", KPIs 12/2/00:10, the two missed cards, "Học tiếp 2 thẻ chưa thuộc", "Học lại cả bộ" (restart verified: counter 1/14, chips 14/0), "Học bằng chế độ Học" (`/Practice/learn?set=5&words=…&from=hub`) and "Xong"; match — start screen with the art, rule text, "Chưa có kỷ lục" and "Bắt đầu"; board 1 of 2 shows 12 tiles in 4 columns (3 at 390/320, tile height 104/88), the timer counts, selecting a tile gives the primary look and a 2px lift, a wrong pair turns both tiles red with the "+1 giây" pill, the badge "1 lần ghép sai" and the live text "Sai, cộng 1 giây"; correct pairs leave dashed, empty, `aria-hidden` slots; after board 1 the label becomes "Bảng 2/2"; the result shows "Xong rồi!" with "Kỷ lục mới!", KPIs 14,0 giây / 1 / 12, "Chơi lại", "Thử chế độ Học", "Xong"; the next visit shows "Kỷ lục: 14,0 giây". Progress writes: match never writes (unchanged)
- G11 a11y: Tab order flashcard: toggle → shuffle → card → speak → Chưa thuộc → Đã thuộc → next, each with a 2px or 3px ring; match: tiles in reading order, Enter selects; 0 controls without a name; one `<h1>`; live regions kept (flashcard card text, match result text); reduced motion: flip and swipe transitions are 0.01ms and the selected-tile lift is `none`; the keyboard shortcuts (Space, arrows, 1, 2, R) are unchanged
- G12 contrast: chips warning 6.84 / success 5.21; tag 5.15; flip hint 5.61; toggle 17.10 (active) and 8.16 (inactive); shuffle 17.10; "Chưa thuộc" 7.09 and "Đã thuộc" 5.48; term 17.10; key chips 8.16; match record line 8.90, title 16.35. From tokens: selected tile 6.84, wrong tile 5.91, "+1 giây" pill 6.46, record badge 11.41
- G13 regression: Set and hub pages untouched in this phase
- Fix attempts / deferred / blocked: phones: the two mark buttons wrapped to two lines at 390px, so the controls gap and padding were reduced (1 attempt)
