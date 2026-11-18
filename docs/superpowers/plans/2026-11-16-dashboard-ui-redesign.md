# Dashboard UI Redesign — PLAN_ONLY
**Date:** 2026-11-16  
**Status:** DRAFT — awaiting owner answers to open questions  
**Mode:** PLAN_ONLY · UI-only (no backend change)  
**Files in scope (Step 2 only):** `Views/Home/Index.cshtml`, `Views/Home/_DailyChallenge.cshtml`, `wwwroot/css/Dashboard.css`  
**Files out of scope:** `DashboardViewModel.cs`, `HomeController.cs`, `tct-learning-ui.css`, `_Layout.cshtml`, `site.css`, `layout.css`, any migration

---

## 0. Source truth — verified fields vs rendered state

| Field in VM | Type | Rendered in current view? | Note |
|---|---|---|---|
| `FullName` | string? | ✅ greeting | |
| `Role` | string? | ✅ badge | inline style → token fix needed |
| `Streak` | int | ✅ streak card | |
| `Goal` | int | ✅ CTA title ("Mục tiêu: N từ") | |
| `FolderCount` | int | ✅ mini-stat | |
| `SetCount` | int | ✅ mini-stat | |
| `CardCount` | int | ✅ mini-stat | |
| `MasteredCount` | int | ✅ radial chart + legend | |
| `LearningCount` | int | ✅ radial chart + legend | |
| `NewCount` | int | ✅ radial chart + legend | |
| `DailyChallenge` | partial | ✅ _DailyChallenge partial | real 4-answer + token |
| `TodayFolders` | List | ✅ col-4 list (up to 4) | semantics: other users' folders, NOT own |
| `FeatureTimeBreakdown` | List | ✅ stacked bar ApexCharts | |
| `HeatmapCells` | List | ❌ built, NOT rendered | — |
| `RecentInProcessReading` | object? | ❌ built, NOT rendered | — |

### REMOVE LIST (fictional / policy-violating data)

| Element | Location | Why remove |
|---|---|---|
| `xpCurrent = CardCount*5 + Streak*20` | Index.cshtml L7 | Fake formula; not in VM |
| `xpNext = Max(xpCurrent+500, 1000)` | Index.cshtml L8 | Invented milestone |
| `xpPct` | Index.cshtml L9 | Derived from fake XP |
| "Level Progress" label | Row 1 | References fake XP |
| "Lv Explorer" pill | Row 1 | Hardcoded, not from VM |
| `db-xp-track` / `db-xp-fill` progress bar | Row 1 | Fake XP viz |
| "N XP đến level tiếp theo" hint text | Row 1 | Fake |
| `litDays`, `streakStart` week-dot logic | Index.cshtml L10,15 | Complex local formula — **[Q5]** keep simpler version? |
| `:root { --brand: #4255ff; ... }` in Dashboard.css | Dashboard.css L7-19 | Violates no-`:root`-in-page-file rule |
| All raw hex values in Dashboard.css | Entire file | Migrate to `var(--tct-*)` |
| Inline `style=` on badge spans (L46,50,54) | Index.cshtml | Replace with `.tct-badge` |
| `font-weight: 800/900` in Dashboard.css | Multiple selectors | Not loaded (Inter 400-700 only) |

---

## 1. Proposed composition

> **[Q1] Owner must choose one of the following before implementation begins.**

### Option A — Split Workbench (recommended)
One dominant study surface left, supporting signals right. Mobile stacks top-to-bottom.

```
DESKTOP (≥1024px, inside learner shell .main)
┌─────────────────────────────────────────────────────────────────┐
│ GREETING  "Chào buổi [sáng/chiều/tối], Tên!"      [Role badge] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────────────────────┐  ┌────────────────────┐  │
│  │  A. STUDY NEXT  (col-lg-7)       │  │  B. STREAK         │  │
│  │                                  │  │  🔥 N days         │  │
│  │  [Folder name]                   │  │  ○ ● ● ● ○ ○ ○    │  │
│  │  N sets  ·  CreatorName          │  │  T2 T3 T4 T5 …    │  │
│  │                                  │  ├────────────────────┤  │
│  │  [Học ngay →]                    │  │  C. MEMORY         │  │
│  │                                  │  │  Mastered  N       │  │
│  │  — or empty state —              │  │  Learning  N       │  │
│  │  Chưa có folder. [Khám phá →]   │  │  New       N       │  │
│  └──────────────────────────────────┘  └────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  D. WEEKLY TIME CHART  (col-12)   [phút]                │  │
│  │  ApexCharts stacked bar — Flashcard/Quiz/Speaking/…     │  │
│  └──────────────────────────────────────────────────────────┘  │
│                                                                 │
│  ┌──────────────────────────┐  ┌─────────────────────────────┐ │
│  │  E. QUICK ACCESS (col-6) │  │  F. DAILY CHALLENGE (col-6) │ │
│  │  Flashcards  Speaking    │  │  [term]                     │ │
│  │  Reading     Listening   │  │  ○ Ans A   ○ Ans B          │ │
│  │  Writing     Vocabulary  │  │  ○ Ans C   ○ Ans D          │ │
│  └──────────────────────────┘  └─────────────────────────────┘ │
│                                                                 │
│  [G. OPTIONAL — see Q2]                                         │
└─────────────────────────────────────────────────────────────────┘

MOBILE (≤768px) — stacked full-width
┌──────────────────────────┐
│ GREETING + Role badge    │
├──────────────────────────┤
│ A. STUDY NEXT (full)     │
├──────────────────────────┤
│ B. STREAK  │ C. MEMORY   │  ← col-6 / col-6
├──────────────────────────┤
│ D. WEEKLY TIME (full)    │
├──────────────────────────┤
│ E. QUICK ACCESS (full)   │
├──────────────────────────┤
│ F. DAILY CHALLENGE (full)│
├──────────────────────────┤
│ G. OPTIONAL (full)       │
└──────────────────────────┘
```

### Option B — Command Strip (minimal)
Greeting → one primary CTA strip → stats row → challenge. No heatmap/chart.
Leaner but loses the weekly time chart entirely.

### Option C — Keep current Stitch mock layout, strip fictional data only
Preserve 4-row layout. Remove fake XP row; compact remaining cards.
Lowest visual risk; still improves by removing fake data + token migration.

---

## 2. Section → field mapping (Option A, fully verified)

| Section | Label (VI) | Field | Fallback / empty |
|---|---|---|---|
| Greeting | "Chào buổi …, [Tên]!" | `FullName ?? User.Identity.Name ?? "Bạn"` | — |
| Greeting | Role badge | `Role` | "Standard" if null/unknown |
| A. Study Next | Folder name | `TodayFolders.First().FolderName` | "Chưa có folder để học" |
| A. Study Next | Sets · Creator | `SetCount`, `CreatorName` | — |
| A. Study Next | CTA button | `TodayFolders.Any()` → FolderDetail route; else Folder/Index | real routes preserved |
| A. Study Next | Goal copy | `Goal` ("Mục tiêu: N từ/ngày") | hide if `Goal == 0` |
| B. Streak | N days + week dots | `Streak` | 0 / all dots off |
| C. Memory | Mastered/Learning/New counts | `MasteredCount`, `LearningCount`, `NewCount` | 0 each |
| C. Memory | Radial chart or list | Same 3 fields | skeleton while loading |
| D. Weekly time | Stacked bar | `FeatureTimeBreakdown` (7 × 7 features) | zero bars = "belum ada data" |
| E. Quick Access | 6 feature links | Hardcoded routes (Vocabulary, Speaking, Reading, Listening, Writing, Vocabulary/Index) | — |
| F. Daily Challenge | Term + 4 options | `DailyChallenge.Term`, `.Options`, `.ChallengeToken` | "Daily challenge hiện chưa khả dụng" |
| Stats strip | Cards / Sets / Folders | `CardCount`, `SetCount`, `FolderCount` | 0 |
| G. Optional | **[Q2]** | HeatmapCells or RecentInProcessReading | — |

> **TodayFolders semantics verified:** `GetTodayFoldersAsync` filters `f.UserId != userId` — these are **other users' folders** (community discovery), NOT the user's own folders. UI copy must reflect this: "Khám phá hôm nay" not "Folder của tôi".

---

## 3. Open questions — BLOCKED until owner answers

### Q1 — Composition
Which layout to implement?
- **A. Split Workbench** (recommended — one dominant study surface, signals right)
- **B. Command Strip** (minimal, no chart)
- **C. Strip fictional data only** (preserve current structure, lowest risk)

### Q2 — Optional block G
What goes in the bottom optional slot?
- **Heatmap** (`HeatmapCells` — 28-day card activity grid; needs HTML/CSS, no extra backend)
- **Continue Reading** (`RecentInProcessReading` — title + link to Study/Reading; minimal markup)
- **Omit both** (leave that area empty; simpler implementation)

### Q3 — Weekly time chart
- **Keep ApexCharts** (current CDN dep — `https://cdn.jsdelivr.net/npm/apexcharts` loaded per-page)
- **Replace with CSS bars** (pure HTML/CSS, no CDN dependency, simpler maintenance)
- **Defer chart polish** (keep ApexCharts as-is; only fix token colors)

### Q4 — Vietnamese copy
Below is the current UI copy. Mark any that must stay literal, or note preferred rewrites:

| Current copy | Used in |
|---|---|
| "Mục tiêu: N từ cần ôn mỗi ngày" | CTA card title |
| "Hãy bắt đầu ngay để không gián đoạn streak!" | CTA sub |
| "Học ngay!" / "Tạo folder mới" | CTA button |
| "Truy cập nhanh" | Quick Access label |
| "Học gì hôm nay" | TodayFolders section label |
| "Bạn chưa có folder nào" | TodayFolders empty (INCORRECT — these are other users' folders) |
| "Daily challenge hiện chưa khả dụng" | Challenge empty |
| "Thời gian học trong tuần" | Chart label |

**Proposed correction:** "Học gì hôm nay" → "Khám phá cộng đồng" (reflects actual semantics: other users' folders).  
**[Q4]** Accept this correction? Any other copy to fix?

### Q5 — Razor-local display logic
May the view retain simple local calculations (time-based greeting, `todayInWeek` for week dots, `totalProgress` for Memory legend percentages)?
- **Yes, keep simple view calculations** (greeting hour, week-dot index, percent display)
- **No, only render fields directly from ViewModel** (move even display math to backend)

> Note: The fake XP formula (`CardCount*5 + Streak*20`) is removed regardless. This question is about the **remaining** simple display helpers.

### Q6 — Browser verification
- Required on local start-local port before PR?
- Which roles to test? Standard / Premium / Admin / all three?
- Is there a test account with `Streak > 0` and `TodayFolders` populated for meaningful visual?

---

## 4. Contracts to preserve (non-negotiable, UI-only step)

| Contract | Detail |
|---|---|
| `#daily-challenge-content` div ID | JS event delegation target — must not rename |
| `.answer-btn[data-id][data-token]` | `CheckAnswer` POST payload — must not rename or move |
| `POST /Home/CheckAnswer` | antiforgery header `RequestVerificationToken` from `input[name="__RequestVerificationToken"]` |
| `GET /Home/RefreshDailyChallenge` | Returns partial HTML — `container.innerHTML = html` |
| `asp-controller/asp-action` Tag Helpers | All existing routes preserved (Folder/FolderDetail, Folder/Index, Vocabulary/Index, Speaking/Index, Study/Reading, Study/Listening, Study/Writing, Goals/Index) |
| `@Html.AntiForgeryToken()` | Must stay at top of view |
| `<partial name="_DailyChallenge" model="Model.DailyChallenge" />` | Partial reference preserved |
| ApexCharts `#dbRadialChart` / `#dbTimeChart` | If chart kept: IDs preserved for JS init |

---

## 5. States required for every interactive section

| Section | Loading | Empty | Error / unavailable | Success |
|---|---|---|---|---|
| A. Study Next | — (server-rendered) | "Chưa có folder để học. Khám phá →" (Folder/Index) | — | Shows first folder name + Học ngay |
| B. Streak | — | 0 days, all dots grey | — | N days + lit dots |
| C. Memory | Skeleton (`.tct-skeleton`) while ApexCharts loads | Shows 0/0/0 counts in list | — | Radial chart or count list |
| D. Weekly chart | Skeleton while ApexCharts loads | Zero bars shown (all features = 0 min) | — | Stacked bars |
| E. Quick Access | — | — | — | 6 links always present |
| F. Daily Challenge | — | "Daily challenge hiện chưa khả dụng" | — | 4 buttons; disable all after answer |
| G. Heatmap (if chosen) | — | All cells grey (0 cards) | — | Cells colored by intensity |
| G. Continue Reading (if chosen) | — | Hidden entirely | — | Title + "Tiếp tục đọc →" |

**Post-answer state (Daily Challenge):** After JS receives response, disable ALL `.answer-btn` elements (add `disabled` attr or `pointer-events:none`), mark correct green / wrong red, then `setTimeout(refreshChallenge, 1200)`. Current code marks only the clicked button — gap to fix.

---

## 6. CSS migration rules (Step 2)

| Rule | Action |
|---|---|
| Remove `:root {}` block from Dashboard.css | Move all custom properties to consume `var(--tct-*)` |
| Replace `--brand` with `var(--tct-primary)` | Throughout Dashboard.css |
| Replace `--surface` with `var(--tct-bg)` | |
| Replace `--text-head` with `var(--tct-text)` | |
| Replace `--text-muted` with `var(--tct-text-muted)` | |
| Replace `--shadow-sm` with `var(--tct-shadow-soft)` | |
| Replace `--radius` (18px) with `var(--tct-radius-lg)` | |
| Remove `font-weight: 800/900` | Replace with `var(--tct-weight-bold)` (700) |
| Inline `style=` on badges | Replace with `.tct-badge .tct-badge--danger/warning/secondary` |
| Wrap page in `.tct-theme` | Add to Index.cshtml outer container |
| `.db-chart-skeleton` | Replace with `.tct-skeleton` |

---

## 7. Files allowed to change in Step 2

```
TCTEnglish/Views/Home/Index.cshtml          ← markup, Razor local display logic
TCTEnglish/Views/Home/_DailyChallenge.cshtml ← partial markup only
TCTEnglish/wwwroot/css/Dashboard.css        ← full CSS rewrite to tokens
```

**NOT touched in Step 2:**
- `DashboardViewModel.cs` — no field additions
- `HomeController.cs` — no builder changes  
- `tct-learning-ui.css` — no token additions (only consuming existing tokens)
- `_Layout.cshtml`, `site.css`, `layout.css` — shared impact, out of scope
- Any `.csproj`, `Program.cs`, `appsettings.json`

---

## 8. Acceptance checks (for Step 2 completion)

- [ ] `dotnet build` passes, no warnings introduced
- [ ] Dashboard renders at 1440px: greeting, study-next, streak, memory, chart, quick-access, challenge visible
- [ ] Dashboard renders at 768px: all sections full-width stacked, no horizontal overflow
- [ ] Dashboard renders at 390px: same stacking, touch targets ≥ 44px
- [ ] No element uses a raw hex value (all via `var(--tct-*)`)
- [ ] No `font-weight: 800/900` in Dashboard.css
- [ ] No `:root {}` block in Dashboard.css
- [ ] Fake XP / "Lv Explorer" / XP bar absent from DOM
- [ ] `CardCount=0, Streak=0, MasteredCount=0` → empty states render without JS errors
- [ ] `TodayFolders` empty → "Chưa có folder để học. Khám phá →" links to `Folder/Index`
- [ ] `DailyChallenge` empty → graceful empty state text shown
- [ ] Daily Challenge: selecting wrong answer marks correct green + wrong red, all buttons disabled, refreshChallenge called after 1200ms
- [ ] `#daily-challenge-content`, `.answer-btn[data-id][data-token]` IDs/attributes preserved
- [ ] `@Html.AntiForgeryToken()` present in view
- [ ] `prefers-reduced-motion` respected (skeleton stops, no fill animation)
- [ ] One `<h1>` on page (greeting title)
- [ ] Role badge uses `.tct-badge` variant (no inline style)
- [ ] **[Q6-dependent]** Verified in browser at stated roles

---

## 9. Out of scope (this UI step)

- Goals page redesign
- Sidebar / header / landing redesign
- New backend metrics, new VM fields, new DB queries
- EF migrations
- ApexCharts version upgrade or replacement (unless owner answers Q3: replace)
- Heatmap / Continue Reading blocks (unless owner answers Q2)
- Class features, Reading passage content
- Admin dashboard

---

## 10. Backend handoff gaps (Step 3 — not implemented here)

These are gaps visible from the current UI that would require backend work later:

| Gap | What backend would need |
|---|---|
| `HeatmapCells` computed but not rendered | View markup + intensity mapping (data ready) |
| `RecentInProcessReading` computed but not rendered | View markup (data ready) |
| `TodayFolders` returns random other-user folders; no "my folders" panel | Separate VM field for user's own recent folders |
| `Goal == 0` case: no way to set goal from dashboard | Link to Goals/Index or inline quick-set |
| Streak week-dots show calendar days, not actual study days | VM needs `StudiedDaysBitmask` or similar |
| Memory radial uses % of owned cards, not study sessions | Could add `SessionsToday` to VM |
| Daily Challenge: no "already answered today" state | VM needs `HasAnsweredTodayChallenge` bool |

---

## Vietnamese handoff summary

**Plan đã lưu tại:** `docs/superpowers/plans/2026-11-16-dashboard-ui-redesign.md`

**Tóm tắt wireframe:** Split Workbench — 1 surface học chính (trái, col-7) + streak/memory (phải), chart tuần (full), quick-access + daily challenge (ngang).

**Remove list chính:**
- Fake XP bar ("Lv Explorer", điểm XP, thanh tiến độ cấp độ) — không có trong VM
- `:root {}` trong Dashboard.css — vi phạm design system
- `font-weight: 800/900` — Inter không tải weight đó
- Inline `style=` trên badge role

**6 câu hỏi chờ owner trả lời trước khi bắt đầu Prompt 2:**
1. Chọn layout: A (Split Workbench) / B (Command Strip) / C (giữ cũ, bỏ fiction)?
2. Block optional G: heatmap / continue-reading / bỏ cả hai?
3. ApexCharts: giữ / thay bằng CSS bars / defer?
4. Vietnamese copy: có fix "Học gì hôm nay" → "Khám phá cộng đồng" không? Copy nào cần giữ literal?
5. Cho phép computed display trong view (greeting hour, week-dot index) hay chỉ render field thẳng từ VM?
6. Browser test local: role nào cần kiểm? Có account có Streak > 0 không?

**STOP — Chờ owner approve plan + trả lời 6 câu hỏi. Chưa implement gì.**
