---
version: alpha
name: TCT English
description: Shared visual standard for Razor and Bootstrap work. Every token below exists in TCTEnglish/wwwroot/css/tct-learning-ui.css.
colors:
  primary: "#4255ff"
  primary-hover: "#2f3fe0"
  primary-strong: "#2136e7"
  primary-soft: "#eef2ff"
  primary-tint: "#f0f3ff"
  accent: "#ffcd1f"
  on-accent: "#111c2d"
  background: "#f6f7fb"
  surface: "#ffffff"
  surface-muted: "#f8fafc"
  text: "#111c2d"
  text-secondary: "#444656"
  text-muted: "#5b6578"
  border: "#e5e7eb"
  border-control: "#7c869b"
  success: "#10b981"
  success-text: "#047857"
  warning: "#f59e0b"
  warning-text: "#92400e"
  danger: "#ba1a1a"
  info: "#06b6d4"
  info-text: "#0e7490"
  focus-ring: "#4255ff"
  focus-ring-on-dark: "#ffffff"
typography:
  body:
    fontFamily: "Inter, Arial, sans-serif"
    fontSize: 1rem
    fontWeight: 400
    lineHeight: 1.5
  title:
    fontFamily: "Inter, Arial, sans-serif"
    fontSize: "clamp(1.5rem, 1.25rem + 1vw, 2rem)"
    fontWeight: 700
    lineHeight: 1.25
  reading:
    fontFamily: "Inter, Arial, sans-serif"
    fontSize: 1.125rem
    fontWeight: 400
    lineHeight: 1.7
rounded:
  sm: 8px
  md: 12px
  lg: 18px
  xl: 20px
  pill: 9999px
spacing:
  xs: 0.25rem
  sm: 0.5rem
  md: 1rem
  lg: 1.5rem
  xl: 2rem
  2xl: 3rem
components:
  navigation:
    accent: "{colors.primary}"
    surface: "{colors.surface}"
  search:
    background: "{colors.background}"
    radius: "{rounded.pill}"
  button-primary:
    background: "{colors.primary}"
    radius: "{rounded.md}"
  card:
    background: "{colors.surface}"
    radius: "{rounded.lg}"
  field:
    border: "{colors.border-control}"
    radius: "{rounded.md}"
---

## Overview

TCT English helps employees study vocabulary, practise speaking and track progress.
Product screens make the next learning action easy to find and give clear feedback.
Study, dashboard and Admin screens prioritize task clarity. Marketing pages may use
more expressive composition when requested.

**Status.** This file is the canonical visual specification for new and changed
screens. It documents the existing TCT identity (blue `#4255ff`, yellow accent,
Inter) and consolidates the drifting per-page palettes into one token set. It is
**not** a finished site redesign: legacy screens keep their own CSS until a page
owner adopts the tokens (adoption tracker: section 6 of [team-ui-workflow.md](docs/design-system/team-ui-workflow.md)).

Related files, each with one job:

| Need | Where |
| --- | --- |
| Token values and component CSS (source of truth) | `TCTEnglish/wwwroot/css/tct-learning-ui.css` |
| Live, copyable examples | `docs/design-system/preview.html` (open from disk) |
| How a teammate builds a page, PR checklist | `docs/design-system/team-ui-workflow.md` |
| Execution prompt for an agent | `.ai/prompts/implement-page-with-team-design-system.md` |

### Mandatory versus page-level

| Mandatory for every new or changed screen | Page owner decides |
| --- | --- |
| Colors, fonts and weights from the tokens | Content arrangement, grid and illustration |
| Shell (header, sidebar, `.main`) and its offsets | Section order and information density |
| Control look: buttons, inputs, focus ring, validation | Page-specific components built from the tokens |
| States: loading, empty, error, success | Copy (keep existing Vietnamese product copy) |
| Accessibility floor (see Accessibility) | Motion beyond the shared durations, if it explains state |
| One `<h1>` per page | Which Bootstrap grid columns to use |

Brand changes, a different primary color, removing navigation, changing the root
font size or re-theming Bootstrap globally are **proposals for Chau**, not applied here.

## Colors

Blue is for primary actions, active navigation and meaningful emphasis. Surfaces
are white on the pale canvas. `border` separates; it never carries meaning.

- **Fill versus text.** `success`, `warning`, `info` are for icons, bars and
  borders. When the color is *text*, use `*-text`. `#10b981` on white is 2.54:1 and
  `#d97706` is 3.19:1, so they fail as copy. `danger` works for both.
- **Soft backgrounds** (`primary-soft`, `*-soft`) pair with their `*-text` color.
- **Never color alone.** Status needs a word, and ideally an icon, next to the color.
- **Control borders** use `border-control` (3.66:1 on white). `border` and the legacy
  `--tct-border-input` are decorative only.

Contrast, computed from these values (the gallery recomputes them live):

| Pair | Ratio |
| --- | --- |
| `text` on surface / on background | 17.10 / 15.98 |
| `text-secondary` on surface | 9.31 |
| `text-muted` on surface / background / tint | 5.87 / 5.48 / 5.30 |
| `primary-strong` on surface | 7.65 |
| white on `primary` / `primary-hover` | 5.28 / 7.22 |
| `on-accent` on `accent` | 11.41 |
| `*-text` on its `*-soft` (primary, success, warning, danger, info) | 6.84, 5.21, 6.84, 5.91, 5.15 |
| `border-control` on surface (non-text, needs 3) | 3.66 |
| `focus-ring` on surface / background (needs 3) | 5.28 / 4.93 |
| `focus-ring-on-dark` on `#212529` (needs 3) | 15.43 |

Disabled controls are exempt from 4.5:1 (`--tct-disabled-text` renders 4.24:1 on its
background). The gallery's components were also measured as rendered (computed
foreground against the effective background): 31 of 32 component pairs pass, the
exception being that exempt disabled field; results are in the ledger. Token values alone do not prove
rendered contrast on a real page.

## Typography

**Inter** (weights 400, 500, 600, 700, with the Vietnamese subset) is loaded by the
shell and is the only product font. Do not use 800/900: they are not loaded and are
synthesized. `Be Vietnam Pro` exists only on Auth/Account pages (legacy
`--tct-font-family`); do not use it in new work.

| Token | Value | Use |
| --- | --- | --- |
| `--tct-title-size` | `clamp(1.5rem, 1.25rem + 1vw, 2rem)` | The page `<h1>` |
| `--tct-text-3xl` / `2xl` / `xl` | 2 / 1.5 / 1.25rem | Hero, section, card titles |
| `--tct-text-lg` | 1.125rem | Reading text (`.tct-prose`), lead paragraphs |
| `--tct-text-base` | 1rem | Body |
| `--tct-text-sm` | `max(.875rem, 13px)` | Secondary text, table cells |
| `--tct-text-xs` | `max(.75rem, 12px)` | Captions, badges; never below 12px |

Line height 1.25 for headings, 1.5 for UI text, 1.7 for reading. Reading width
`--tct-measure` = 70ch (`.tct-prose`). One `<h1>`; do not skip levels. Do not truncate
essential learning content; `.tct-break` wraps long words and `.tct-card` wraps by
default.

- **Phonetics.** `.tct-phonetic` (IPA verified rendering in Inter: ə ʃ θ ð ŋ ʊ ɪ ˈ ˌ).
- **Font cascade gotcha.** `layout.css` sets `font-family` on `*`, so a wrapper's
  `font-family` never reaches its children. Set it on the element itself.
- **Zoom.** Sizes are in `rem` so browser text size and 200% zoom work. `site.css`
  makes the root 14px below 768px; that is why `xs` and `sm` have px floors.
  Raising the root to 16px on mobile is a **proposal** (it changes every rem value).

## Layout

Use Razor feature views and shared partials with Bootstrap 5.3.2 utilities.

| Token | Value | Notes |
| --- | --- | --- |
| `--tct-header-h` | 72px | Fixed header. A logged-in header measures 83/83/79/75px at 1440/1024/768/390, so it covers 3-11px of the `.main` top padding and sidebar top edge. Known, unchanged (see proposals). |
| `--tct-sidebar-w` / `-md` | 280px / 240px | Above / at or below 1024px |
| `--tct-main-pad` / `-sm` | 40px / 16px | Above / at or below 768px |
| `--tct-container-max` / `-narrow` | 1280px / 960px | `.tct-page` / `.tct-page--narrow` (forms) |
| `--tct-admin-sidebar-w` | 260px | Admin sidebar |

`layout.css` consumes these tokens (with fallbacks); do not hardcode 72/280/240 again.
Shell breakpoints: 1024px (sidebar 240), 768px (drawer), 576px (search placeholder),
480px (small header). Admin collapses at 991.98px. The learner shell zeroes margin and
padding on `*`; Admin does not, so components set their own margins.

Check around 1440, 768 and 375/390 plus 200% zoom, long Vietnamese and English text,
and focus visibility. Preserve Tag Helpers, form actions, antiforgery tokens, IDs and
data attributes consumed by JavaScript.

## Elevation & Depth

Restrained: `--tct-shadow-soft` for cards, `--tct-shadow-lift` for hover/interactive,
`--tct-shadow-overlay` for dialogs. Prefer spacing and alignment over nested cards.

Layer scale (existing values; new layers choose from it, never an arbitrary number):

| Token | z-index | Used by |
| --- | --- | --- |
| `--tct-z-sidebar` | 900 | Learner sidebar |
| `--tct-z-header` | 1000 | Fixed header (Bootstrap dropdown is also 1000) |
| `--tct-z-dropdown` | 1050 | Notification panel (Bootstrap modal 1055, popover 1070, tooltip 1080, toast 1090) |
| `--tct-z-launcher` | 1600 | AI chat launcher (sits above Bootstrap modals) |
| `--tct-z-overlay` | 2000 | Legacy custom modal overlay (above Bootstrap toasts) |
| `--tct-z-popover` | 10000 | User menu, skip link |

## Shapes

`--tct-radius-sm` 8px (small buttons, pagination), `md` 12px (buttons, inputs), `lg`
18px (cards, panels, tables), `xl` 20px (dialogs), `pill` for search and badges.
Circles for avatars and compact icon buttons (44px target).

## Spacing

rem-based, aligned to Bootstrap spacers: xs 0.25 (`1`), sm 0.5 (`2`), md 1 (`3`),
lg 1.5 (`4`), xl 2 (no utility; use the token), 2xl 3 (`5`). Prefer Bootstrap
utilities for ordinary spacing and `var(--tct-space-*)` inside scoped CSS. Controls
are `--tct-control-h` (`max(2.5rem, 40px)`) tall; touch and icon-only targets are
`--tct-target-min` (44px). Avoid global resets and inline layout styles in feature views.

## Components

Standard controls are **Bootstrap**; wrap the page in `class="tct-theme"` to map the
TCT tokens onto Bootstrap for that subtree only (buttons, inputs, checkboxes,
validation, pagination, cards, tables, modals). Without `tct-theme`, Bootstrap keeps
its default blue `#0d6efd`: that app-wide difference is the largest visible
inconsistency and the primary-color decision for Chau.

| Pattern | How | Status |
| --- | --- | --- |
| Page heading, breadcrumb, actions | `<partial name="_PageHeader" model="PageHeaderViewModel">` | Implemented |
| Loading / empty / error / success | `<partial name="_StateMessage" model="StateMessageViewModel">` | Implemented |
| Buttons | `.btn` + `btn-primary`/`btn-outline-primary`/`btn-danger`; `.tct-btn-icon` for icon-only | Implemented |
| Pending button | `disabled` + `aria-busy="true"` + Bootstrap `.spinner-border`; restore after failure | Pattern |
| Fields | `.form-label`, `.form-control`/`.form-select`, `.form-text`, `.invalid-feedback`, `aria-describedby` | Implemented |
| Search / filter bar | `.tct-toolbar` with `__search`, `__filters`, `__actions` | Implemented |
| Cards | `.tct-card` (+ `--interactive`); Admin may use Bootstrap `.card` inside `tct-theme` | Implemented |
| Badges | `.tct-badge` + `--primary/--success/--warning/--danger/--info`, always with text | Implemented |
| Tables | `.tct-table-wrap` (focusable scroll region) + `.tct-table`, or Bootstrap `.table-responsive` | Implemented |
| Pagination | Bootstrap `.pagination`; Admin reference: `_CatalogPager.cshtml` | Implemented |
| Modal | Bootstrap modal inside the `tct-theme` wrapper | Implemented |
| Alerts / toast | Bootstrap `.alert` (`role="alert"` for errors) and `.toast` | Implemented |
| Confirm dialog | SweetAlert2 pinned in `wwwroot/lib/pinned` only where the page already uses it | Existing |
| Skeleton | `.tct-skeleton`, `--line`, `--short`, `.tct-skeleton-group` | Implemented |
| Reading / phonetic text | `.tct-prose`, `.tct-phonetic`, `.tct-break`, `.tct-clamp-2` | Implemented |

Icons: the learner shell loads Font Awesome 6.0.0 and Material Symbols; Admin loads
Bootstrap Icons 1.11.3 and Font Awesome 6.5.1. Font Awesome is the only family in both.
Match the page's family; do not add another.

Study: quick feedback, duplicate-submit protection and authoritative progress; keep
keyboard and media controls. Navigation: preserve active meaning, collapse behavior
and keyboard access. Feedback: never raw exceptions.

## Accessibility and motion

- Visible focus on every interactive element: `:focus-visible` outline 2px
  `--tct-focus-ring`, offset 2px (inside `tct-theme` and `.tct-focusable`). On dark
  surfaces add `.tct-on-dark` (white ring). The learner header search input currently
  has `outline: none` and no replacement (legacy, see proposals).
- Text 4.5:1 (large 3:1), non-text and control boundaries 3:1.
- Targets 44px for icon-only controls; keyboard reachability and Escape for dialogs
  and drawers (Bootstrap modal and `layout.js` already provide focus trap/return).
- Motion explains a state change and stays short: `--tct-dur-fast` 150ms, `--tct-dur`
  250ms, `--tct-ease`. Both durations become ~0 under `prefers-reduced-motion`;
  `.tct-skeleton` stops animating. Prefer CSS and browser APIs over new dependencies.

## CSS load order and override rules

Learner shell: Bootstrap 5.3.2 → `site.css` → **`tct-learning-ui.css`** → `layout.css`
→ `notifications.css` → `ai-chat-launcher.css` → the page's `Styles` section.
Admin: Bootstrap → Bootstrap Icons → Font Awesome → **`tct-learning-ui.css`** →
`account.css` → inline shell `<style>` → page styles. Standalone pages
(`Layout = null`, e.g. Auth) link `tct-learning-ui.css` themselves.

1. New values go into `tct-learning-ui.css` as tokens; page CSS consumes `var(--tct-*)`.
2. Page CSS stays scoped (`.feature-page .thing`) in `wwwroot/css/{feature}.css`.
3. No new `:root` palette in a page file, no unprefixed custom properties.
4. No global element selectors, no blanket `!important`. Override Bootstrap through
   its variables inside `.tct-theme`, not by raising selector specificity.
5. Legacy `--tct-*` names (`--tct-white`, `--tct-text-main`, `--tct-border-input`,
   `--tct-font-family`, ...) are kept because `auth-stitch.css` and
   `listening-index.css` use them. Do not use them in new code.

## Do's and Don'ts

Preserve identity, encoded text, typed ViewModels and feature boundaries. Use
`.tct-theme` plus tokens instead of private hex values. Do not copy another brand as
TCT's identity, replace Razor with React/Tailwind, add packages, CDNs, fonts or icon
families, invent statistics, or remove useful study states to simplify a screenshot.

## Open proposals for Chau (not applied)

1. Make `tct-theme` the default (apply to `<body>`/`main` in both layouts) so Bootstrap
   buttons become `#4255ff` everywhere. Brand-visible; needs a pass over every screen.
2. Raise the mobile root font size from 14px to 16px.
3. Header height: make the logged-in header exactly `--tct-header-h`, or set the token
   to the measured 83px and re-verify every page.
4. Legacy contrast and focus failures, measured in a real browser: `.workspace-title`
   `#9ca3af` 11px on white (2.54:1); the header search placeholder is the browser
   default `#757575` on `#f6f7fb` (4.30:1) and the input has `outline: none` with no
   replacement; Admin sidebar section labels render `#745b00` on `#212529` (2.38:1)
   because `account.css` line 960 forces `.text-secondary { color: #745b00 !important }`
   on every Admin page (it is linked by `_AdminLayout.cshtml`); Admin sidebar links
   show only a faint Bootstrap blue shadow, and the brand link a dark `auto` outline,
   on the dark background (`.tct-on-dark` supplies the white ring but is not applied);
   `site.css` paints a template focus ring `#258cfb` on `.btn:focus`.
5. Replace the custom `#folderModal` overlay with a Bootstrap modal (it lacks a focus trap).
6. Retire the dead `--spk-sidebar-w` and consolidate the 11 page palettes, one screen at a time.

Format: [Google DESIGN.md](https://github.com/google-labs-code/design.md).
Reviewed spec/license: .ai/vendor/google-design-md/. Pins: .ai/tooling/sources.lock.json.
