# Toast Class + Floating Dashboard cards — ledger

**Date:** 2026-11-24  
**Order:** A (Clay toast, no `alert`) → B (Dashboard float cards)  
**Authority:** local only — no push / PR / deploy

## A — Class toast harden

### Done
- [x] `Class-Detail.js` `classToast`: never `window.alert`; uses `TctFeedback.toast` or minimal Clay host fallback
- [x] `classConfirm`: Clay modal first; `window.confirm` only as last resort
- [x] `class-join-code.js` pending join → success toast “Đã gửi yêu cầu tham gia” (no `alert`); modal errors stay in-form
- [x] Browse/Class join scripts: no inline `alert(`

### Verify (local `:5100`, student@tct.local)
| Check | Result | Evidence |
|---|---|---|
| `TctFeedback` present on Dashboard | Pass | `hadTct: true` via Chrome DevTools |
| Toast copy-style message (no native dialog) | Pass | Title “Đã sao chép mã lớp”; `alertCalls: []` |
| Toast warning copy-fail style | Pass | Title “Không sao chép được”; no `alert` |
| Browse join script wired, no `alert(` | Pass | `/Home/BrowseClasses` HTML |
| Class Detail script order | Blocked | Student account has no `ClassDetail` membership on this DB |
| Regen / kick confirm UX | Blocked | Needs owner Class Detail session |

## B — Floating Data Card

### Done
- [x] `data-db-float` + real fields on weekly / vocab / heatmap segments in `Views/Home/Index.cshtml`
- [x] Host `#dbFloatCard` + `wwwroot/js/dashboard-float-card.js` (hover/move/leave/focus/touch + viewport flip)
- [x] Styles in `Dashboard.css` (Clay tokens, fade ~150ms, z-index 1080)

### Real fields used (from ViewModel — not invented)

| Region | Source fields | Card shows |
|---|---|---|
| Weekly `.db-time-seg` | DayLabel, FullDate, skill minutes, TotalMinutes | Skill, day/date, skill minutes, day total, % if total > 0 |
| Vocab `.db-mem-seg` | Mastered/Learning/New counts + totals | Group label, card count, total, % |
| Heatmap `.db-heat-cell` | DayLabel, WeekLabel, ReviewedCards (+ intensity label derived) | Day/week, reviewed count or “Chưa có hoạt động học tập” |

### Missing (reported — not added in this scope)
- Heatmap: minutes studied, review attempt count, full ISO date string beyond DayLabel/WeekLabel
- No schema / migration / API change

### Verify
| Check | Result | Evidence |
|---|---|---|
| Float host + JS on Index | Pass | `id="dbFloatCard"`, `dashboard-float-card.js` |
| Vocab hover card | Pass | “Mới · 6 thẻ”; fixed z-index 1080 |
| Heatmap empty cell | Pass | “Chưa có hoạt động học tập” |
| Weekly segments | N/A (data) | Student `FeatureTimeBreakdown` had no minute segments this session — markup present when minutes > 0 |
| Viewport flip / no H-overflow @390 | Pass | Emulate 390×844 touch; vocab/heat cards fit; `overflowX: false` |
| @768 | Pass | Emulate 768×1024; outside-tap hides card; no H-overflow |
| @1440 | Pass | Emulate ~1440 desktop; right-edge hover flips; no console errors |
| Console errors | Pass | No error/warn on Dashboard after load |
| Sidebar open/close visual | Not observed | Manual owner pass recommended |

## Files
- `TCTEnglish/wwwroot/js/Class-Detail.js`
- `TCTEnglish/wwwroot/js/class-join-code.js`
- `TCTEnglish/Views/Home/Index.cshtml`
- `TCTEnglish/wwwroot/css/Dashboard.css`
- `TCTEnglish/wwwroot/js/dashboard-float-card.js` (new)

## Out of scope (unchanged)
- Main Khóa học menu, heatmap schema fields, XP/streak/goals, Admin alert sweep, push/PR/deploy
