# Multi-platform feature audit queue

Date: 2026-11-09. Spec:
[2026-11-09-multi-platform-ui-design.md](../specs/2026-11-09-multi-platform-ui-design.md).
Tracker: [team-ui-workflow.md](../../design-system/team-ui-workflow.md) §6 (cột **Đa nền**).

## Rules

- One feature (or shell) per authorized drop.
- Audit → findings → owner fix authority → fix **whole feature** mobile + desktop →
  update tracker. Do not ship a single viewport.
- Shared-impact files need a consumer list and before/after evidence.
- UI-only: no migration / shared-data package unless the drop also changes those.
- Preserve routes, JSON/SignalR, selectors, antiforgery, access checks.
- Local evidence under ignored `.tmp/multi-platform/<feature>/` (synthetic data).

## Pass / fail (minimum)

| Check | Pass |
| --- | --- |
| 1440 sidebar open | Main + footer clear of sidebar |
| 1440 sidebar collapsed | Full width; no leftover offset |
| ≤1024 | Sidebar 240; matching offsets |
| ≤768 / 375–390 | No page overflow; drawer correct; touch ≥44px |
| 200% zoom | Primary task still completable |
| Keyboard | Tab, visible focus, Escape return-of-focus |
| States | Loading / empty / error / success (as applicable) |

## Queue order

| # | Feature | Primary surfaces | Đa nền (start) |
| --- | --- | --- | --- |
| 1 | Learner shell | `_Layout.cshtml`, `layout.css`, `layout.js`, footer/header/sidebar | Chưa audit |
| 2 | Auth | `/Account/Login`, ForgotPassword, `auth-stitch.css` | Chưa audit |
| 3 | Dashboard | `/Home/Index`, `Dashboard.css` | Chưa audit |
| 4 | Vocabulary | `/Vocabulary/*`, `vocabulary-system.css` | Chưa audit |
| 5 | Listening | Listening index + practice CSS | Chưa audit |
| 6 | Reading | Reading index + study CSS | Chưa audit |
| 7 | Writing | Writing index / exercises / editor CSS | Chưa audit |
| 8 | Speaking | `/Speaking`, Practice, `speaking.css` | Chưa audit |
| 9 | Goals | `/Goals`, `goals.css` | Chưa audit |
| 10 | Class + Folder | `/Class/*`, `/Folder/*` | Chưa audit |
| 11 | Account | Profile, Settings, `account.css` | Chưa audit |
| 12 | Notification | `/Notification` | Chưa audit |
| 13 | AI Chat | `/Ai/Chat` | Chưa audit |
| 14 | Contact | `/Home/Contact` | Chưa audit |
| 15 | Admin | `/Admin/*` shell + priority screens | Chưa audit |

## Per-drop findings template

```markdown
### <Feature> multi-platform audit — YYYY-MM-DD

**Surfaces inventoried**: <routes / views / css / js>

**Browser evidence**: <1440 open/collapsed, 768, 390, zoom; what was not observed>

**Findings** (severity / file / trigger / impact / smallest fix):
1. ...

**Fix authority**: <none | IMPLEMENT_LOCAL granted>

**After fix**: update workflow §6 Đa nền → `Audit xong` or `Đạt đa nền`.
```

## Repair prompt stub (English operational block)

Adapt `.ai/templates/execution-prompt.md` and
`.ai/prompts/implement-page-with-team-design-system.md`:

- Outcome: resolve listed findings for **<Feature>** so desktop and mobile both
  pass the multi-platform acceptance matrix for every in-scope surface.
- Scope: only the inventoried files; shared-impact only if listed and authorized.
- Non-goals: no bulk token migration unless that feature drop includes it; no
  brand/root-font changes; no Git/release unless granted.
- Verification: matrix above + `dotnet build` when code changed.

## Ledger

Record drop status below as work proceeds.

| # | Feature | Audit | Fix | Đa nền |
| --- | --- | --- | --- | --- |
| 1 | Learner shell | done 2026-11-09 | done (footer offset, menu 44px, footer wrap, aria-expanded default) | Đạt đa nền |
| 2 | Auth | done (CSS 960/480 + Login HTML fetch; live logout blocked in session) | none required this drop | Audit xong |
| 3 | Dashboard | done 390 overflow clear | answers grid stacks ≤768 | Audit xong |
| 4 | Vocabulary | done `/Vocabulary/Index` 390 clear | none | Audit xong |
| 5 | Listening | done `/Home/Listening` 390 clear | delete btn min-height 44px | Audit xong |
| 6 | Reading | done `/Home/Reading` 390 clear | none on index | Audit xong |
| 7 | Writing | done `/Home/Writing` 390 clear | none on index | Audit xong |
| 8 | Speaking | done `/Speaking` 390 clear | topic chips use in-region scroll (OK) | Audit xong |
| 9 | Goals | done `/Goals` 390 clear | none | Audit xong |
| 10 | Class + Folder | done `/Home/Class`, `/Home/Folder` 390 clear | none | Audit xong |
| 11 | Account | done Profile/Settings 390; main ~382px | none | Audit xong |
| 12 | Notification | done `/Notification/Index` 390 clear | none | Audit xong |
| 13 | AI Chat | done `/AI/Chat` 390; shell ~358px | none | Audit xong |
| 14 | Contact | done `/Home/Contact` 390 clear | none | Audit xong |
| 15 | Admin | done `/Admin/Dashboard` 390; table-responsive OK | ReadingManagement wrap; Health workers wrap; toggle 44px | Audit xong |

### Round-1 notes (2026-11-09)

- Index/list surfaces audited at 390 for page overflow; learner shell also verified at 1440 open/collapsed.
- Practice deep links (Listening/Reading/Writing/Speaking practice) not fully exercised this round — keep as follow-up before promoting those rows to `Đạt đa nền`.
- Auth live browser pass skipped (session logout blocked by tooling); CSS + anonymous Login HTML checked.
- Horizontal chip/filter rows that scroll inside a region are accepted per the multi-platform spec.
