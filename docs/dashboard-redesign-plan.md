# Learner Dashboard Redesign — Design Plan

**Date:** 2026-11-24  
**Direction:** Soft Claymorphism + Functional Bento Workspace  
**Approach:** Evolve-in-place on `/Home/Index`

## 1. Locked product decisions

| Topic | Decision |
| --- | --- |
| Structure | Keep current Index shell; polish CSS/JS; light markup for Tico + float hosts |
| Tico | Greeting = welcome; Challenge pending = thinking; Challenge done = success. Nowhere else. Static only. |
| Floating Data | Hybrid: Quick Tooltip (hover/focus) + Detail Card (click). Same `data-*` sources. |
| Quick Access | Flashcards, Speaking, Reading, Listening, Writing, **Goals** (replace duplicate Vocabulary) |

## 2. Visual system

- Canvas / surfaces: `--tct-clay-canvas`, `--tct-surface`, clay shadows from `tct-learning-ui.css`
- Panel radius 24px (`--tct-clay-radius-panel`), controls 16px
- Primary `#4255FF`, accent CTA `#FFCD1F` with `#111C2D` text
- Typography: Inter 400–700 only; greeting ~26–32px desktop; section labels uppercase muted
- Learning Command: primary left border + primary-tint → white gradient; yellow primary CTA
- Streak: warning-soft surface; no XP repeat inside streak

Anti-slop: no violet glow, no glass overload, no emoji icons, no marketing hero, no fake charts.

## 3. Bento layout

### Desktop ≥1024px

```
Greeting (+ welcome Tico)
Command col-8 | Streak col-4
Goals full
Stats full
Chart col-7 | Heatmap col-5
Quick Access col-6 | Challenge col-6 (+ contextual Tico)
Continue Reading (if data)
```

### Tablet 768–1023

Two-column where Bootstrap already stacks; chart/heatmap may stack vertically.

### Mobile &lt;768

Stack: Greeting → Command → Streak → Goals → Stats → Chart → Heatmap → Quick Access → Challenge → Continue.  
Full-width CTAs; Quick Access 3×2 when space allows; tap targets ≥44px.

## 4. Component notes

- **Greeting:** time greeting + highlighted name + XP chip + role + static welcome image  
- **Command:** folder resume or empty explore; secondary folder chips unchanged  
- **Goals:** summary/list + single progress track from real goal %  
- **Stats:** differentiated icon tints; vocab bar interactive via float  
- **Charts:** real minutes / reviewed cards only  
- **Challenge:** existing answer flow; Tico beside status copy  

## 5. Accessibility

- Semantic headings; focus rings on float triggers and controls  
- Tooltip/detail keyboard: focus shows tooltip; Enter/Space opens detail; Escape closes  
- Color not sole signal (labels + counts + ARIA on bars/cells)  
- `prefers-reduced-motion`: final states without lift/scale grow  

## 6. Implementation files

`Index.cshtml`, `_DailyChallenge.cshtml`, `Dashboard.css`, `dashboard-float-card.js`, plus this doc set under `docs/`.
