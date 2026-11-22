# Account Auth Organic Shape — Design Spec

Date: 2026-11-20  
Status: Approved for implementation (owner plan)  
Scope: `/Account/Login` and `/Account/Register` (shared `Auth.cshtml`)

> **Design docs supersede (2026-11-20):** For dual-asset Organic Split (campus BG +
> mascot alpha), recovery visual parity, and the full Auth design system, use
> [`docs/design/authentication/`](../../design/authentication/README.md).
> This Superpowers spec remains the historical record for the earlier
> combined-hero Login/Register direction.

## Outcome

Redesign the Auth split page so the left column is brand-first Organic Shape
(illustration-led, soft blobs, rounded skill icons) while the right column stays
a Clay form surface. Preserve all Login/Register/OAuth contracts and Vietnamese
product copy.

## Constraints (DESIGN.md)

- Claymorphism inside `.tct-theme` / `.account-auth-ui`.
- Colors, radii, shadows, focus, and motion from `--tct-*` only.
- No new fonts, CDNs, palettes, or GSAP packages.
- Desktop/laptop composition first; mobile + a11y in the same change.
- Do not change `AccountController` business rules, schema, or `Program.cs`.

## Visual direction

**Organic Shape ∩ Clay:** large soft radii (control 16px, panel 24px, dialog 28px),
irregular blob backgrounds, dual Clay shadows, soft button press. Sky atmosphere
from `--tct-primary-soft` / `--tct-clay-canvas` with a quiet yellow accent blob
(`--tct-accent` / `--tct-warning` at low opacity via `color-mix`).

### Left column (one composition)

1. Brand logo + short subtitle (existing copy, optionally one line).
2. Hero image: owner Organic Shape illustration
   (`wwwroot/images/auth/auth-organic-hero.jpg` + optional WebP).
3. Four decorative skill icons (speak / read / listen / progress), `aria-hidden`,
   no emoji.
4. Soft SVG/CSS blobs behind the hero; no floating milestone badges
   (“7 Ngày”, “Top 5%”, emoji pills).

### Right column (one job: authenticate)

- Clay card: tabs, OAuth, separator, active form, footer switch.
- Shallow opaque fields with labels; independent focus ring.
- ≤992px: hero compact or secondary; form remains primary; no horizontal overflow.

## UX principles

1. One goal per viewport region (brand vs form; form primary on mobile).
2. Visible labels + correct `autocomplete`; paste allowed.
3. Errors: `role="alert"` summary and/or focused error box; not color-only.
4. Submit: `aria-busy`, spinner, double-submit guard (existing `setPending`).
5. Password toggle: `aria-pressed` + Hiện/Ẩn label.
6. Tablist keyboard: ArrowLeft/Right (existing).
7. Touch targets ≥ `--tct-target-min` (44px).
8. `prefers-reduced-motion`: stop blob float, tab/form slide, loader pulse.

## Business flows (unchanged)

| Flow | Contract |
| --- | --- |
| GET Register / Login | `View("Auth")` + `ActiveTab` |
| POST Register | antiforgery; email/name/password/readingLevel; `ViewBag.ErrorMessage`; success → Login + TempData |
| POST Login | AJAX JSON via `account-auth-ui.js`; redirect Home; blocked UI |
| OAuth | Google/Facebook `ExternalLogin` URLs unchanged |
| Loader | post-login / external overlay + recovery actions |

## Non-goals

- ForgotPassword / ResetPassword / Profile restyle.
- Backend validation or redirect changes.
- Replacing Font Awesome / Bootstrap / SweetAlert2 already on the page.
- Persisting a second design-system palette from ui-ux-pro-max generic output.

## Acceptance

- Both routes open the correct tab; POST/OAuth behavior unchanged.
- Desktop: Organic brand left + Clay form right; no emoji milestone badges.
- Mobile 375–390: usable form, ≥44px targets, no page overflow.
- Contrast ≥4.5:1 for body text; focus visible; reduced-motion safe.
- Browser evidence at ~1440, 768, 375–390; record what was not observed.

## Owner note (VI)

Trang Auth dùng minh họa Organic Shape thân thiện (bé + cáo + bút chì) trong
Clay TCT. Chỉ đổi giao diện; nghiệp vụ đăng nhập/đăng ký giữ nguyên.
