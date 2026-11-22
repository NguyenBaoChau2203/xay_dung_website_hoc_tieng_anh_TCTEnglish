# UI_DESIGN_SYSTEM — Auth Organic Shape ∩ Clay

Date: 2026-11-20  
Canonical tokens: [`tct-learning-ui.css`](../../../TCTEnglish/wwwroot/css/tct-learning-ui.css) · [DESIGN.md](../../../DESIGN.md)  
Related: [UX_PRINCIPLES.md](UX_PRINCIPLES.md) · [ORGANIC_SHAPE_DIRECTION.md](ORGANIC_SHAPE_DIRECTION.md) · [COMPONENT_ARCHITECTURE.md](COMPONENT_ARCHITECTURE.md)

## 1. Positioning

Auth uses **Organic Shape composition** (soft blobs, rounded geometry, illustration-led brand stage)
built on the shared **Claymorphism** token system. This is not a second design system.

ui-ux-pro-max “Soft UI Evolution” informs depth and radius discipline only. Do **not** import
Pro Max generic hex palettes or alternate fonts (Varela Round / Nunito).

## 2. Color system (mapped to `--tct-*`)

| Role | Token / value | Auth use |
| --- | --- | --- |
| Primary | `--tct-primary` `#4255ff` | Primary buttons, tabs selected, focus |
| Primary hover / strong | `--tct-primary-hover`, `--tct-primary-strong` | Hover, links |
| Primary soft | `--tct-primary-soft` / `--tct-clay-canvas` | Page canvas, soft fills |
| Accent | `--tct-accent` `#ffcd1f` | Quiet decorative blob only (`color-mix` low opacity) |
| Background | `--tct-clay-canvas` | Right column / recovery surface |
| Surface | `--tct-clay-surface` | Form card, inputs fill |
| Text primary | `--tct-text` / `--tct-text-main` | Headings, labels |
| Text secondary | `--tct-text-secondary`, `--tct-text-muted` | Subcopy, hints |
| Border | `--tct-border`, `--tct-border-control` | Card edge, fields |
| Success | `--tct-success*` | Success alerts |
| Warning | `--tct-warning*` | Blocked account |
| Error / danger | `--tct-danger*` | Validation / auth errors |
| Focus | `--tct-focus-ring` | `:focus-visible` rings |

Illustration campus blues/greens live **inside the image asset**, not as new CSS brand colors.

## 3. Typography

| Role | Token | Notes |
| --- | --- | --- |
| Family | `--tct-font` (Inter) | Auth shell may still load Inter; no new families |
| Page title | `.tct-page-title` / clamp heading | One H1 per Auth view |
| Body / subheading | `--tct-text-base`, muted secondary | Short supporting line under H1 |
| Label | `--tct-text-sm` + semibold | Always visible |
| Input | `--tct-text-base` | Comfortable touch typing |
| Button | semibold, min-height `--tct-target-min` | |
| Helper / error | `--tct-text-sm` | Below field or in alert |

## 4. Shape system (Organic ∩ Clay)

| Element | Rule | Token |
| --- | --- | --- |
| Control (input, tab, small btn) | Soft rounded | `--tct-clay-radius-control` 16px |
| Panel / auth card | Softer panel | `--tct-clay-radius-panel` 24px |
| Dialog / loader box | Softest | `--tct-clay-radius-dialog` 28px |
| Pill actions | Optional | `--tct-radius-pill` |
| Organic blob | CSS/SVG decorative, `aria-hidden` | `color-mix` with primary/accent soft |
| Decorative float | Max 1–2 quiet shapes | No badge stickers |

Shadows: `--tct-clay-shadow*`, field inset `--tct-clay-shadow-field`, button press `--tct-clay-shadow-pressed`.

## 5. Spacing system

| Context | Tokens |
| --- | --- |
| Field stack gap | `--tct-space-md` between groups |
| Card padding | `--tct-space-lg` – `--tct-space-xl` |
| Section (OAuth → separator → form) | `--tct-space-md` / `lg` |
| Page gutters (right column) | `--tct-space-lg` – `xl` |
| Icon gaps | `--tct-space-xs` / `sm` |

Prefer 4/8 rhythm already encoded in `--tct-space-*`. Spacious density for Auth (learner-friendly).

## 6. Component system (Auth-scoped)

| Component | Class / pattern | Rules |
| --- | --- | --- |
| Input | `.auth-form-input` | Opaque surface, clay field shadow, visible label |
| Password input | `.auth-password-wrap` + toggle | Toggle ≥44px hit area; `aria-controls` |
| Button primary | `.auth-btn-submit` / `.btn-primary` | Full width in card; loading disables |
| Button social | `.auth-btn-social` | Equal weight Google/Facebook; keep brand icon colors |
| Checkbox | terms + `.auth-checkbox-wrap` | Label wraps associated control |
| Divider | `.auth-separator` | “hoặc bằng email” |
| Tabs | `.auth-tabs` tablist | Slider indicator; keyboard arrows |
| Alert | Bootstrap alert + tct danger/success soft | `role="alert"` |
| Toast / modal | SweetAlert2 success; Bootstrap blocked modal | Existing libraries only |
| Loading | `#loader-overlay` | Honest recovery actions |
| Brand stage | `.auth-col-left` layers | Campus BG + mascot img (after alpha) |

Recovery pages reuse `.auth-surface` / `.recovery-card` with the same radii/shadow language.

## 7. Interaction system

| State | Treatment |
| --- | --- |
| Default | Clay surface + quiet shadow |
| Hover | Lift / primary hover; 150–300ms |
| Focus | Independent focus ring; never rely on border alone |
| Active | Pressed clay shadow on buttons |
| Disabled | Reduced emphasis + `disabled` semantics |
| Loading | Spinner + `aria-busy` / disabled submit |
| Error | Danger border/text + alert; keep values |
| Success | TempData Swal or success alert |

## 8. Motion

- Optional soft blob float on brand stage — disabled under `prefers-reduced-motion`.
- Tab/form switch: short, optional; respect reduced motion.
- No GSAP dependency; no new animation libraries.

## 9. Anti-patterns

- New `:root` palette hex in `account-auth-ui.css`
- Glass panels over busy art that fail contrast
- Emoji as structural icons
- Duplicate combined hero + layered mascot fighting for the same role after dual-asset ship

## 10. Next

Layout choice and art direction: [ORGANIC_SHAPE_DIRECTION.md](ORGANIC_SHAPE_DIRECTION.md).
