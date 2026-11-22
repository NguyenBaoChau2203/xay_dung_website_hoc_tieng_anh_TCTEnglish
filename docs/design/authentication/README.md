# Authentication design package — Organic Shape

Date: 2026-11-20  
Status: Design documentation complete (UI implementation separate)

This folder is the design source of truth for TCT English Authentication visuals
(Organic Shape ∩ Clay). Read in order for onboarding; jump by topic via links.

## Document index

| # | File | Topic |
| --- | --- | --- |
| 1 | [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) | Stack, source map, assets, MCP/skills, Clay constraints |
| 2 | [BUSINESS_FLOW.md](BUSINESS_FLOW.md) | Verified Account endpoint contracts |
| 3 | [USER_FLOW.md](USER_FLOW.md) | Journeys A–J |
| 4 | [UX_PRINCIPLES.md](UX_PRINCIPLES.md) | Form, a11y, feedback, taste |
| 5 | [UI_DESIGN_SYSTEM.md](UI_DESIGN_SYSTEM.md) | Tokens, components, states mapped to `--tct-*` |
| 6 | [ORGANIC_SHAPE_DIRECTION.md](ORGANIC_SHAPE_DIRECTION.md) | Layout A locked; dual-asset layering |
| 7 | [RESPONSIVE_GUIDELINES.md](RESPONSIVE_GUIDELINES.md) | 1440 → 360 behavior |
| 8 | [COMPONENT_ARCHITECTURE.md](COMPONENT_ARCHITECTURE.md) | Shells, hooks, JS boundaries |
| 9 | [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) | Post-docs build tasks |
| 10 | [DESIGN_REVIEW_CHECKLIST.md](DESIGN_REVIEW_CHECKLIST.md) | Acceptance checks |

## Locked decisions

- Layout: **Full-screen campus** + floating Clay auth card (no solid right panel)
- Left stage: logo/slogan + mascot + floating educational icon tiles
- Assets: `auth-campus-bg` + `auth-mascot-pencil` (alpha)
- No Confirm Password / Remember Me on Login-Register
- Presentation-only; no business-rule changes

## Related historical docs

- [`docs/superpowers/specs/2026-11-20-account-auth-organic-shape-design.md`](../../superpowers/specs/2026-11-20-account-auth-organic-shape-design.md) — earlier combined-hero Login/Register spec (superseded for dual-asset design scope)
- [`docs/superpowers/plans/2026-11-20-account-auth-organic-shape.md`](../../superpowers/plans/2026-11-20-account-auth-organic-shape.md) — earlier implementation plan
