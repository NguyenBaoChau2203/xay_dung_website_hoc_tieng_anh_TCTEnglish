# COMPONENT_ARCHITECTURE — Auth UI structure

Date: 2026-11-20  
Related: [UI_DESIGN_SYSTEM.md](UI_DESIGN_SYSTEM.md) · [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) · [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md)

## 1. Principles

- Presentation-only changes; preserve HTTP/JSON/OAuth contracts ([BUSINESS_FLOW.md](BUSINESS_FLOW.md)).
- Reuse Bootstrap 5 + existing Font Awesome / SweetAlert2 — **no new UI libraries**.
- Scope styles under `body.account-auth-ui` / `.account-auth-ui`.
- Prefer shared classes already in `account-auth-ui.css`; add layer hooks without duplicating a second auth CSS stack.

## 2. Page shells

| Shell | Views | Structure |
| --- | --- | --- |
| Split Auth | `Auth.cshtml` | `.auth-split-container` → `.auth-col-left` + `main.auth-col-right` → `.auth-card` |
| Recovery | Forgot / Confirmation / Reset | `.auth-surface` → `main.recovery-card` |
| Utility | AccessDenied | Align tokens/radii when touched; not full split |

All use `Layout = null`, load `tct-learning-ui.css` + `account-auth-ui.css`, body `.tct-theme.account-auth-ui`.

## 3. Brand stage components (full-screen Auth)

| Layer | Hook | Responsibility |
| --- | --- | --- |
| 1 Campus | `body.auth-page` | Full-bleed `auth-campus-bg` cover |
| 2 Decor | `.auth-decor` | Blobs, sparkles, trails (`aria-hidden`) |
| 3 Edu icons | `.auth-edu-icons` | Soft clay tiles + FA glyphs |
| 4 Mascot | `.auth-mascot` > `.auth-mascot-img` | Alpha PNG, `object-fit: contain` |
| 5 Brand | `.auth-brand` | Logo SVG + slogan |
| 6–7 Card | `.auth-card` | Opaque white Clay form |

Right column background must stay **transparent** (card only). No interactive controls in decor/icon layers.

## 4. Form card components

| Piece | Existing hooks | Notes |
| --- | --- | --- |
| Header | `.auth-header`, `#auth-heading` | Update copy with tab switch |
| Tabs | `.auth-tabs`, `#tab-login`, `#tab-register` | `role="tablist"` |
| Server alert | `#server-error-alert` | Focus on load if present |
| Social | `.auth-social-btns` | ExternalLogin URLs unchanged |
| Separator | `.auth-separator` | |
| Login form | `#form-login` | AJAX `handleLogin` |
| Register form | `#form-register` | Classic POST `handleRegister` |
| Password toggle | `.auth-toggle-password` | Shared JS |
| Blocked UI | `#blocked-alert`, modal | |
| Submit | `.auth-btn-submit` | Pending guard |
| Footer switch | `.auth-form-footer` | Tab switch links |
| Close | `#btn-close-auth` | `history.back` |

## 5. Overlay / system components

| Piece | Hook | Notes |
| --- | --- | --- |
| Loader | `#loader-overlay`, `.loader-box` | LO01 recovery buttons |
| Success Swal | TempData script in Auth | Confirm color stays TCT primary |

## 6. Recovery components

| Piece | Hook | Organic alignment |
| --- | --- | --- |
| Brand row | `.brand` | Optional small mascot later |
| Fields | `.field` + labels | Same input radius tokens |
| Back link | `.back-link` | Keep ≥44px |

Shared JS: password toggles on Reset via `account-auth-ui.js`.

## 7. JavaScript boundaries

File: `wwwroot/js/account-auth-ui.js`

| Allowed in redesign | Not allowed without authority |
| --- | --- |
| Focus server error on DOMContentLoaded | Changing Login JSON schema |
| CSS class hooks for layers | New analytics beacons |
| Reduced-motion checks if needed | New frameworks |

## 8. Asset pipeline (components depend on)

| Asset | Path (target) | Consumer |
| --- | --- | --- |
| Campus | `wwwroot/images/auth/auth-campus-bg.jpg` (+ webp) | Left BG |
| Mascot alpha | `wwwroot/images/auth/auth-mascot-pencil.png` | `.auth-mascot-img` |
| Legacy combined | `auth-organic-hero.*` | Deprecated after cutover |

## 9. Testing touchpoints

- `scripts/tests/test_account_auth_ui.cjs` — update only if selectors required by tests change.
- Manual browser matrix: [DESIGN_REVIEW_CHECKLIST.md](DESIGN_REVIEW_CHECKLIST.md).

## 10. Next

Ordered implementation tasks: [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).
