# Learner Dashboard — Motion Spec

**Date:** 2026-11-24  
**Tokens:** prefer `--tct-dur-fast` (~150ms) and `--tct-dur` (~250ms). Map brief timings into these existing tokens; do not invent conflicting `:root` durations on the page.

## 1. Principles

1. Motion is for UI feedback only.  
2. **Mascot animation is prohibited** — no keyframes, transitions, transforms, idle loops, GIF/video, or JS animation on `.db-tico` / mascot `img`.  
3. Card hover may lift the surface; mascot image must stay untransformed (`transform: none` on the image).  
4. No elastic bounce, parallax, scroll-triggered card fade-in, or continuous floating — **except** the scoped Streak flame (`.db-streak-flame`) continuous burn loop below.  
5. Respect `prefers-reduced-motion: reduce`.

## 2. Timing map

| Intent | Target | Implementation |
| --- | --- | --- |
| Hover / focus | ~120–150ms | `--tct-dur-fast` |
| Quick Tooltip enter | ~140–180ms | opacity + translateY(4px→0) |
| Quick Tooltip exit | ~120–150ms | opacity out |
| Detail Card enter | ~180–220ms | opacity + scale(0.98→1) |
| Detail Card exit | ~140–180ms | reverse |
| Card / CTA clay lift | ≤300ms | `--tct-dur` / `--tct-dur-fast`, translateY(-2px) |
| Progress / chart grow | ~450–600ms | `--tct-dur` or one-shot CSS animation |

Easing: UI enter ease-out; exit ease-in; progress ease-in-out. No spring/elastic.

## 3. Feature motion

| Feature | Allowed | Forbidden |
| --- | --- | --- |
| Greeting | Stable text; primary name color | Typewriter, blink, mascot motion |
| Learning Command | Card lift 2px; CTA color/elevation | Mascot move with card |
| Streak | Dot color; today outline; hover scale ≤1.05; **`.db-streak-flame` living SVG** (path morph + glow + sparse embers, hover intensify ~1.06–1.08) | Fake countdown; blinking copy; animating Tico; flame that is only scale/opacity without silhouette change |
| Goals | Fill width transition to real % | Looping progress |
| Stats | Optional first-load counter (if added: once only) | Icon bounce/spin |
| Weekly chart | Bars grow from baseline once; hover dim siblings | Re-animate unchanged data on click |
| Heatmap | Hover scale ≤1.04 + border | Stagger rainbow |
| Quick Access | Lift 2px; icon tint | Random per-tile animations |
| Challenge | State from server; checkmark CSS class | Auto state machine |
| Float layers | Tooltip vs detail exclusive | Stacking both |

## 4. Floating Data interaction

- Desktop: hover/focus → Quick Tooltip; click → Detail Card (hides tooltip).  
- Hover-intent: short leave delay so pointer can enter tooltip before hide.  
- Detail: ESC, outside click, close button.  
- Touch: tap toggles detail (or shows compact then detail); no hover-only path.  
- Positioning: fixed; flip near viewport edges; no layout shift of page content.  
- Content: only existing `data-*` fields.

## 5. Reduced motion

When `prefers-reduced-motion: reduce`:

- Disable translate/scale on cards, CTAs, heatmap, tooltip/detail.  
- Charts/progress show final widths immediately.  
- Tooltip and detail still open/close (instant or opacity-only).  
- Mascot remains static (already always static).  
- Streak flame: stop morph/embers/float; show static glowing SVG frame (`.db-streak-flame__still`).

## 6. Explicit mascot ban

Do **not** create `TicoMotionController`, expression state machines, CSS `@keyframes` on mascot selectors, or parent-hover rules that scale/translate `.db-tico img`.
