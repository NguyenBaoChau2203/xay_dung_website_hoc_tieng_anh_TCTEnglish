# PROJECT_ANALYSIS — Authentication Organic Shape

Date: 2026-11-20  
Status: Design documentation (planning package)  
Related: [BUSINESS_FLOW.md](BUSINESS_FLOW.md) · [ORGANIC_SHAPE_DIRECTION.md](ORGANIC_SHAPE_DIRECTION.md) · [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)

## 1. Purpose

Document the current TCT English authentication UI stack, assets, constraints, and
drift before the Organic Shape redesign docs and a later presentation-only
implementation. This file is evidence-based; it does not invent business rules.

## 2. Product and stack

| Item | Verified value |
| --- | --- |
| Product | TCT English — online English learning |
| Framework | ASP.NET Core MVC / Razor, net9.0 |
| UI | Bootstrap 5.3.3, Font Awesome 6.4, SweetAlert2 (Auth success), vanilla JS |
| Design tokens | `TCTEnglish/wwwroot/css/tct-learning-ui.css` (`--tct-*`, Claymorphism) |
| Canonical visual spec | [DESIGN.md](../../../DESIGN.md) — Clay for every redesigned page |
| Auth shell | `Layout = null` (no learner sidebar) |

## 3. Source map

### Controllers

- [`TCTEnglish/Controllers/AccountController.cs`](../../../TCTEnglish/Controllers/AccountController.cs)
  - `Login` / `Register` (GET → shared `Auth` view; authenticated users → `Home/Index`)
  - `Login` POST → JSON (`success`, `message`, optional `blocked`)
  - `Register` POST → antiforgery; full-page re-render or redirect Login + TempData
  - `ExternalLogin` / `ExternalLoginCallback` (Google, Facebook)
  - `ForgotPassword` / confirmation; `ResetPassword` (token 15 minutes)
  - Cookie sign-in uses `IsPersistent = true` (no UI “Remember Me” checkbox)

### Views

| Route / view | File | Layout pattern |
| --- | --- | --- |
| `/Account/Login`, `/Account/Register` | `Views/Account/Auth.cshtml` | Split shell + Clay card |
| `/Account/ForgotPassword` | `ForgotPassword.cshtml` | Centered recovery card |
| Forgot confirmation | `ForgotPasswordConfirmation.cshtml` | Recovery card |
| `/Account/ResetPassword` | `ResetPassword.cshtml` | Recovery card |
| Access denied | `AccessDenied.cshtml` | Separate utility |

### Styles and scripts

| File | Role |
| --- | --- |
| `wwwroot/css/tct-learning-ui.css` | Shared Clay tokens and `.tct-theme` |
| `wwwroot/css/account-auth-ui.css` | Scoped Auth + recovery styles |
| `wwwroot/js/account-auth-ui.js` | Tabs, AJAX login, password toggle, loader, pending guard |
| `scripts/tests/test_account_auth_ui.cjs` | Focused auth UI script checks |

### Preview / prior design artifacts

- `docs/design-system/account-ui-preview.html` (mirrors Auth patterns)
- Spec (narrow Login/Register): [`docs/superpowers/specs/2026-11-20-account-auth-organic-shape-design.md`](../../superpowers/specs/2026-11-20-account-auth-organic-shape-design.md)
- Plan: [`docs/superpowers/plans/2026-11-20-account-auth-organic-shape.md`](../../superpowers/plans/2026-11-20-account-auth-organic-shape.md)

**Supersede note:** This `docs/design/authentication/` package is the design source
of truth for the full Auth visual system (Login, Register, Forgot, Reset). The
2026-11-20 Superpowers spec remains historically accurate for the earlier combined-hero
Login/Register ship; implementers should follow this package when dual-asset Organic
Split and recovery parity are in scope. See [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).

## 4. Current Auth UI behavior (as shipped)

- Left column: full-bleed CSS `background-image` from
  `wwwroot/images/auth/auth-organic-hero.{webp,jpg}` (combined illustration).
- Right column: Clay card with Login/Register tabs, OAuth, email forms, loader overlay.
- Register fields: email, display name (`fullName`), CEFR `readingLevel`, password (≥6),
  terms checkbox (client-required only). **No** confirm-password on Register.
- Login: email + password + show/hide; Forgot Password link; AJAX submit.
- Recovery pages: shared `account-auth-ui` / `tct-theme` but **not** the split brand stage.

## 5. Assets inventory

### In repo today (`wwwroot/images/auth/`)

| File | Role | Notes |
| --- | --- | --- |
| `auth-organic-hero.webp` / `.jpg` / `.png` | Combined hero used as left BG | ~728×433 PNG measured; to be superseded by layered assets |
| `auth-illustration.svg` | Legacy / preview | Not primary Auth left art |

### Owner-supplied (chat attachments — not yet in wwwroot)

| Asset | Measured size | Intended role |
| --- | --- | --- |
| Campus sky / school illustration (JPG) | 1024×576 | Full-bleed left **background** |
| Boy + Tico on pencil (PNG) | 1024×713 | Brand **subject** overlay |

**Asset gate (owner decision 2026-11-20):** mascot PNG currently has an opaque black
background. Implementation may remove black → transparent PNG alpha while preserving
character colors, clothing, and proportions. Target paths (planned):

- `wwwroot/images/auth/auth-campus-bg.jpg` (+ optional `.webp`)
- `wwwroot/images/auth/auth-mascot-pencil.png` (alpha)

Do not redraw mascot or campus with AI substitutes. Do not recolor brand identity.

### Brand mascot library (existing, out of Auth hero scope unless reused)

`wwwroot/brand/mascot/` stickers/poses remain available for loader/moments; Auth hero
subject for this redesign is the owner pencil scene.

## 6. DESIGN.md / Clay constraints (non-negotiable)

- Visual direction: Claymorphism ∩ Organic Shape composition (soft radii, soft shadows).
- Colors: TCT blue `#4255ff`, accent yellow `#ffcd1f`, Inter — via `--tct-*` only.
- No new font CDN, no second palette, no new UI framework.
- Desktop/laptop composition first; mobile + a11y in the same later change.
- Page CSS consumes `var(--tct-*)`; no new `:root` brand hex in feature CSS.

## 7. Skills and MCP inventory (this planning session)

| Capability | Status | Use for Auth design |
| --- | --- | --- |
| Superpowers brainstorming / writing-plans | Available | Process + implementation task breakdown |
| ui-ux-pro-max CLI | Available | UX guidelines; Soft UI Evolution shape language |
| design-taste-frontend | Available | Anti-template, one composition, motion restraint |
| sequential-thinking MCP | Available | Design decision sequencing |
| cursor-ide-browser / Chrome DevTools MCP | Available | Later visual verification |
| Context7 MCP | Available | External library docs only (not private source) |
| Figma MCP | Not connected | Wireframes in markdown/mermaid instead |

**Pro Max caution:** Generic design-system output (e.g. `#0EA5E9`, Varela Round /
Nunito) must **not** override DESIGN.md. Adopt only UX rules and Soft UI Evolution
depth/radius guidance mapped onto `--tct-*`.

## 8. Drift and gaps

| Topic | Gap |
| --- | --- |
| Dual-asset layout | Spec/plan 2026-11-20 assumed one combined hero; owner now provides campus + mascot layers |
| Recovery visual parity | Forgot/Reset use Clay card but lack Organic brand stage |
| Confirm password on Register | Mentioned in some briefs; **absent** in controller — do not invent |
| Remember Me UI | Absent; cookie already persistent |
| Mascot alpha | Required before layered UI ship |
| `docs/design/authentication/` | Created by this package |

## 9. Out of scope for this documentation package

- Changing authentication business rules or schema
- Production deploy / PR / Program.cs edits
- Profile / Settings security surfaces (except shared token language)
- Replacing Font Awesome / Bootstrap already on Auth

## 10. Handoff

Next: [BUSINESS_FLOW.md](BUSINESS_FLOW.md) for endpoint contracts, then
[USER_FLOW.md](USER_FLOW.md) for end-to-end journeys.
