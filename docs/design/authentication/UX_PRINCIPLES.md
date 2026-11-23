# UX_PRINCIPLES — Authentication

Date: 2026-11-20  
Sources: ui-ux-pro-max (accessible auth, forms, errors), design-taste-frontend (anti-slop), EdTech audience  
Related: [UI_DESIGN_SYSTEM.md](UI_DESIGN_SYSTEM.md) · [USER_FLOW.md](USER_FLOW.md) · [DESIGN_REVIEW_CHECKLIST.md](DESIGN_REVIEW_CHECKLIST.md)

## 1. Audience

Primary users are learners (students, working adults) who may be new to the product.
Prefer plain Vietnamese copy, short steps, and one clear primary action per region.

## 2. Form usability

| Principle | Do | Don't |
| --- | --- | --- |
| Recognition | Visible `<label for>` on every field | Placeholder-only fields |
| Order | Natural order: identity → credentials → extras (CEFR) → submit | Interleave OAuth mid-field |
| Autocomplete | `email`, `current-password`, `new-password`, `name` | Block paste / disable password managers |
| Password | Show/hide toggle with `aria-pressed` + Hiện/Ẩn | Hidden forever with no toggle |
| Primary CTA | Verb + object (“Đăng nhập”, “Đăng ký”, “Gửi liên kết khôi phục”) | Vague “OK” / “Tiếp” |
| Extra fields | Only contract fields (see BUSINESS_FLOW) | Invent Confirm Password / Remember Me on Login-Register |

## 3. Feedback and errors

| Principle | Do | Don't |
| --- | --- | --- |
| Submit feedback | Loading → success or error | Silent click |
| Error clarity | Say what failed and how to fix | Color-only red border |
| Announcement | `role="alert"` / live region | Visual-only toast without field link |
| Placement | Summary near form top + retain inline where present | Toast-only replacing fields |
| Data retention | Keep typed values on recoverable errors | Wipe form on every fail |
| Focus | Move focus to error summary/alert after failed submit | Steal focus on every blur |

Aligned with Pro Max: accessible authentication, form labels, submit feedback, focusable error summary, password visibility, loading states.

## 4. Interaction states

Every interactive control needs: default, hover, focus-visible, active/pressed, disabled, loading (where async).
Touch targets ≥ `--tct-target-min` (44px). Keyboard: Tab order matches visual order; Auth tabs support ArrowLeft/Right.

## 5. Loading and navigation honesty

- Login AJAX: disable submit, show busy state, then loader overlay on success navigation.
- Overlay recovery if navigation stalls (existing LO01 pattern).
- Prefer progress honesty over decorative spinners alone.

## 6. Accessibility (WCAG-oriented)

- Text contrast ≥ 4.5:1 on Clay surfaces and over illustration overlays used under text.
- Decorative art: `aria-hidden` or empty alt when redundant; meaningful `alt` on brand subject image.
- Focus ring uses `--tct-focus-ring`; never remove outline without replacement.
- `prefers-reduced-motion`: stop blob float, non-essential slide/pulse; show final state.
- Auth must allow paste and password managers (Pro Max critical).

## 7. Taste / anti-template (design-taste-frontend)

- One job per region: left = brand; right = authenticate.
- No emoji milestone pills, fake stats, floating promo stickers on the hero.
- No glassmorphism / neon gradients / purple “AI template” looks.
- Motion: subtle entrance + button press; not scroll-jacking or constant blob chaos.
- Brand first on the illustration stage; form hierarchy still wins for task completion.

## 8. Responsive UX

- Desktop/laptop: split stage + form (demo priority).
- ≤992px: form is primary; illustration becomes compact or secondary (see RESPONSIVE_GUIDELINES).
- One-handed mobile: full-width primary button, generous spacing, no hover-only actions.

## 9. Copy tone

Friendly, educational, short. Error messages stay specific (match existing Vietnamese strings where the controller already defines them). Do not invent security-sensitive copy that reveals whether an email exists on Forgot Password.

## 10. Next

Token and component rules: [UI_DESIGN_SYSTEM.md](UI_DESIGN_SYSTEM.md).
