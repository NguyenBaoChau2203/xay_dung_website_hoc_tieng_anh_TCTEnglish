# Account Auth Organic Shape — Implementation Plan

> **For agentic workers:** Execute task-by-task in this session (single-agent).
> Steps use checkbox syntax for tracking.

> **Design docs supersede (2026-11-20):** Prefer
> [`docs/design/authentication/IMPLEMENTATION_PLAN.md`](../../design/authentication/IMPLEMENTATION_PLAN.md)
> when implementing dual-asset campus + mascot layers and Forgot/Reset visual parity.
> This plan remains valid for the earlier combined-hero Login/Register tasks.

**Goal:** Ship Organic Shape Auth UI for Login/Register without changing backend contracts.

**Architecture:** Presentation-only changes to `Auth.cshtml`, scoped
`account-auth-ui.css`, optional focus helper in `account-auth-ui.js`, and a new
hero image under `wwwroot/images/auth/`.

**Tech Stack:** ASP.NET Core Razor, Bootstrap 5, `tct-learning-ui.css` tokens,
vanilla JS.

## Global Constraints

- `--tct-*` tokens only in new/changed page CSS; no new fonts/CDN/palette.
- Preserve antiforgery, AJAX login, Register POST, OAuth URLs, Vietnamese copy.
- Desktop + mobile in the same change; Auth shell has `Layout = null` (no learner sidebar).
- No `Program.cs` / migration / controller business edits.

---

### Task 1: Place Organic Shape hero asset

**Files:**
- Create: `TCTEnglish/wwwroot/images/auth/auth-organic-hero.jpg`
- Keep: `TCTEnglish/wwwroot/images/auth/auth-illustration.svg` (unused by Auth after swap; may remain for preview)

- [ ] Copy owner illustration into `wwwroot/images/auth/auth-organic-hero.jpg`
- [ ] Optionally add WebP sibling if tooling available; JPG alone is acceptable

### Task 2: Restyle Auth markup

**Files:**
- Modify: `TCTEnglish/Views/Account/Auth.cshtml`

- [ ] Remove floating milestone badge cluster (emoji pills)
- [ ] Point hero `<img>` to `auth-organic-hero.jpg` with meaningful `alt`
- [ ] Replace three feature text cards with four soft skill icon buttons (decorative)
- [ ] Add organic blob elements (`aria-hidden`) behind hero
- [ ] Keep forms, antiforgery, OAuth, loader, blocked modal contracts
- [ ] Ensure server error alert remains focusable / near form top

### Task 3: Organic Shape + Clay CSS

**Files:**
- Modify: `TCTEnglish/wwwroot/css/account-auth-ui.css`

- [ ] Sky gradient + blob layers using `color-mix` with `--tct-*`
- [ ] Enlarge hero image for brand-first composition
- [ ] Style skill icons as soft rounded clay tiles
- [ ] Soften form radii/shadows to Clay panel tokens
- [ ] Mobile ≤992px: compact/hide heavy hero art; form primary
- [ ] Extend `prefers-reduced-motion` to blob float animations

### Task 4: JS a11y polish (minimal)

**Files:**
- Modify: `TCTEnglish/wwwroot/js/account-auth-ui.js` only if needed

- [ ] On DOMContentLoaded, if `#server-error-alert` is present, focus it
- [ ] Do not change Login/Register fetch/POST APIs

### Task 5: Preview sync (light)

**Files:**
- Modify: `docs/design-system/account-ui-preview.html` if it still mirrors Auth left column

- [ ] Point preview hero to the new organic image path when practical

### Task 6: Verify

- [ ] Open `/Account/Register` and `/Account/Login` locally
- [ ] Check ~1440, 768, 375–390; keyboard tab order; reduced-motion
- [ ] Confirm Register validation error path and Login error box still work
- [ ] Record evidence and gaps in the Vietnamese handoff

## Acceptance checklist

- Correct ActiveTab per route
- No emoji milestone overlays
- Tokens-only page styling for changed rules
- OAuth buttons still navigate to `ExternalLogin`
- Touch targets ≥44px on primary controls
