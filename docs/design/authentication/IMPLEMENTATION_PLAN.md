# IMPLEMENTATION_PLAN — Auth Organic Shape (post-docs)

Date: 2026-11-20  
Status: Updated 2026-11-20 — full-screen campus + floating edu icons + floating card (no right panel fill)  
Related: [COMPONENT_ARCHITECTURE.md](COMPONENT_ARCHITECTURE.md) · [ORGANIC_SHAPE_DIRECTION.md](ORGANIC_SHAPE_DIRECTION.md) · [DESIGN_REVIEW_CHECKLIST.md](DESIGN_REVIEW_CHECKLIST.md)

> For agentic workers: single-agent execution per AGENTS.md. Do not spawn subagents.
> Preserve antiforgery, AJAX login, Register POST, OAuth, Vietnamese copy.

**Goal:** Dual-asset Organic Split Auth + recovery visual parity without changing business rules.

**Architecture:** Layer campus BG + alpha mascot in Auth left column; Clay form right; reuse
`account-auth-ui.css` / `.tct-theme` tokens only.

**Tech stack:** ASP.NET Core Razor, Bootstrap 5, `--tct-*`, vanilla JS.

## Global constraints

- `--tct-*` only; no new fonts/CDN/palette/GSAP.
- No `Program.cs`, migrations, or AccountController rule changes.
- Desktop + mobile in the same PR/change.
- Mascot: remove black → PNG alpha; do not redraw or recolor characters.
- Supersedes design guidance in
  [`docs/superpowers/specs/2026-11-20-account-auth-organic-shape-design.md`](../../superpowers/specs/2026-11-20-account-auth-organic-shape-design.md)
  for dual-asset + recovery scope; that file remains historical for the earlier combined-hero ship.

---

### Task 1: Place campus background

**Files:**
- Create: `TCTEnglish/wwwroot/images/auth/auth-campus-bg.jpg`
- Optional: `auth-campus-bg.webp`

- [x] Copy owner campus illustration into the path above
- [x] Verify aspect (~1024×576) and file size reasonable for web

### Task 2: Mascot alpha prep

**Files:**
- Create: `TCTEnglish/wwwroot/images/auth/auth-mascot-pencil.png`

- [x] Remove opaque black background from owner mascot PNG
- [x] Preserve character colors, clothing, pencil, proportions
- [x] Spot-check edges (hair, scarf, fur) on light blue sample
- [x] Do not commit intermediate scratch files outside wwwroot/auth

### Task 3: Auth markup layers

**Files:**
- Modify: `TCTEnglish/Views/Account/Auth.cshtml`

- [x] Structure left column: campus layer + optional blobs + mascot `<img>` with meaningful `alt`
- [x] Keep forms, antiforgery, OAuth, loader, blocked modal contracts
- [x] Ensure `#server-error-alert` remains near form top

### Task 4: Auth CSS Organic Split

**Files:**
- Modify: `TCTEnglish/wwwroot/css/account-auth-ui.css`

- [x] Wire campus `image-set` / background on left (stop relying solely on `auth-organic-hero`)
- [x] Position `.auth-mascot` for desktop; compact/hide ≤992px per RESPONSIVE_GUIDELINES
- [x] Clay radii/shadows on card/inputs; quiet blobs via `color-mix` + `--tct-*`
- [x] Extend `prefers-reduced-motion` to any new float animations
- [x] No horizontal overflow at 360–390

### Task 5: JS polish (minimal)

**Files:**
- Modify: `TCTEnglish/wwwroot/js/account-auth-ui.js` only if needed

- [x] Focus `#server-error-alert` when present on load (already present; unchanged API)
- [x] Do not change Login/Register API behavior

### Task 6: Recovery visual parity

**Files:**
- Modify as needed: `ForgotPassword.cshtml`, `ForgotPasswordConfirmation.cshtml`, `ResetPassword.cshtml`
- Modify: `account-auth-ui.css` recovery section

- [x] Align radii, canvas, focus, spacing with Auth Clay language
- [x] Optional small mascot accent on desktop only
- [x] Do not change Forgot anti-enumeration copy or Reset fields

### Task 7: Preview / legacy hero

**Files:**
- Optionally update: `docs/design-system/account-ui-preview.html`
- Leave `auth-organic-hero.*` until cutover verified; note deprecation in PR

- [x] Point preview at campus + mascot when practical

### Task 8: Verify

- [x] Run through [DESIGN_REVIEW_CHECKLIST.md](DESIGN_REVIEW_CHECKLIST.md) (local browser)
- [x] Local routes: `/Account/Login`, `/Account/Register`, Forgot (Reset not email-driven in this pass)
- [x] Widths ~1440 (CDP), default/narrow; keyboard/reduced-motion not fully instrumented
- [x] Vietnamese handoff: outcome, files, checks, risks, gaps

---

## Phase mapping (docs → build)

| Docs phase | Build task |
| --- | --- |
| Analysis / assets | Tasks 1–2 |
| Direction / responsive | Tasks 3–4 |
| Components | Tasks 3–6 |
| Verification | Task 8 |

## Risks

| Risk | Mitigation |
| --- | --- |
| Alpha fringing | Manual edge QA; re-export if needed |
| Short laptop viewport | Form-first; keep submit reachable |
| Test selector drift | Update `test_account_auth_ui.cjs` only when necessary |

## Non-goals

- Confirm Password / Remember Me on Login-Register
- Backend validation changes
- Production deploy without separate owner authority
