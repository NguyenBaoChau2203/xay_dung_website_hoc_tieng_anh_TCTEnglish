# DESIGN_REVIEW_CHECKLIST — Auth Organic Shape

Date: 2026-11-20  
Use after implementation Tasks in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md).  
Principles: [UX_PRINCIPLES.md](UX_PRINCIPLES.md) · [RESPONSIVE_GUIDELINES.md](RESPONSIVE_GUIDELINES.md)

## 1. Contract smoke (must pass)

- [ ] `/Account/Login` opens Login tab
- [ ] `/Account/Register` opens Register tab
- [ ] Authenticated visit redirects Home
- [ ] Register validation error keeps Register tab + alert
- [ ] Login wrong password shows `#login-error-msg` (JSON path)
- [ ] OAuth buttons still hit `ExternalLogin` Google/Facebook
- [ ] Forgot Password still shows generic confirmation (no email enumeration)
- [ ] Reset with bad/expired token shows error; success redirects Login
- [ ] Antiforgery still present on Login/Register/Forgot/Reset POSTs

## 2. Visual / Organic Split

- [ ] Desktop ~1440: campus BG + alpha mascot left; Clay form right
- [ ] Mascot has no black rectangle; edges acceptable on sky
- [ ] No emoji milestone badges / fake stats on brand stage
- [ ] Tokens only (`--tct-*`); no new font/palette
- [ ] Tico/boy identity unchanged (colors, costume)

## 3. Responsive

- [ ] ~1280: split usable; CTA reachable
- [ ] ~768: form primary; no horizontal overflow
- [ ] ~390 / ~360: form usable; targets ≥44px; mascot compact or hidden deliberately
- [ ] Recovery pages readable at all widths

## 4. Accessibility

- [ ] Visible labels on all fields
- [ ] Password toggles announce state (`aria-pressed` / label)
- [ ] Error alerts `role="alert"`; focus moves to server error when shown
- [ ] Keyboard: Tab order sensible; tablist arrows work
- [ ] Focus ring visible
- [ ] `prefers-reduced-motion`: no essential content hidden; floats stop
- [ ] Paste / password managers not blocked
- [ ] Decorative blobs `aria-hidden`; mascot `alt` meaningful

## 5. Interaction

- [ ] Hover/focus/disabled/loading states on primary controls
- [ ] Login loading / loader overlay still works
- [ ] Double-submit guarded
- [ ] Typed values retained on recoverable Register errors

## 6. Taste gate

- [ ] First viewport reads as one brand composition + one form job
- [ ] Not “AI template” (no purple haze, glass overload, sticker clutter)
- [ ] Motion restrained (≤ subtle entrance / button press)

## 7. Evidence to record

| Check | Observed? | Notes |
| --- | --- | --- |
| 1440 screenshot / browser | | |
| 768 | | |
| 390 | | |
| Keyboard pass | | |
| Reduced motion | | |
| Not observed | | List gaps honestly |

## 8. Sign-off

| Role | Status |
| --- | --- |
| Implementer | |
| Owner visual review | |

Vietnamese handoff must distinguish local verification vs CI vs production (no production claim without release evidence).
