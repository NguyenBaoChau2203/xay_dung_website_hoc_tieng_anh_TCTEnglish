# G. Test Execution Plan — Reading

## Environment

| Item | Requirement |
|---|---|
| OS | Windows 10+ with Docker Desktop |
| DB | Local MariaDB via `compose.local.yml` (`127.0.0.1:3307` / `tctenglish_dev`) |
| App | `http://localhost:5100` Debug or Release |
| Browser | Chrome (+ DevTools); MCP Chrome DevTools optional |
| Tools | PowerShell 7, `dotnet test`, ClosedXML-compatible Excel client |

### Boot notes (current)

1. Prefer: `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`
2. If Shared-data Sync fails on `team.onboarding.reading`: record Open Issue; do **not** force Sync. For local UI QA only, start web after MariaDB healthy + migrations with the same connection env as `start-local` (see scripts). Confirm `/health/ready`.
3. Distinguish: **app ready** vs **shared-data Sync clean**.

## Accounts to prepare

| Account | Role | Purpose |
|---|---|---|
| admin@tct.local | Admin | Author / import / export |
| Second Admin (e.g. admin2@tct.local) | Admin | Approve/publish only |
| student@tct.local | Standard | Library/study/CEFR |
| Optional premium@tct.local | Premium | Regression parity |

Passwords: local ignored `.appdata/local/access.json` only. Never paste into reports/tickets.

## Test data

| Data | Notes |
|---|---|
| `fixtures/QA-READ-import.xlsx` | Valid A1 MCQ + vocab (this audit) |
| Invalid `.txt` / broken headers | Negative import |
| Published B2+ while student A1 | CEFR negative |
| Prefix `QA-READ-*` | Easy cleanup |
| Excel samples for GapFill/Matching/ShortAnswer/TrueFalse | Needed for TC-READ-027–029 |

## Execution order

1. Smoke: health, login Admin/Student, Admin Index, Student library  
2. Security Critical: TC-READ-001,002,005,010,011,017,033–035  
3. Admin content + Excel: TC-READ-003,004,007–009,014,022–024  
4. Review/publish: TC-READ-006,018–021  
5. Student study/scoring/timer: TC-READ-012,025–029  
6. Notebook/progress/UI: TC-READ-013,030–032,037–039  
7. Regression: `dotnet test --filter FullyQualifiedName~ReadingModuleTests`  
8. Ops: `shared-data.ps1 -Action Status` must be green before claiming team-ready

## Entry criteria

- Local MariaDB up; migrations applied  
- At least Admin + Student accounts  
- Second Admin available for review cases  
- Testers have fixture workbook  
- Known Sync conflict documented if still open  

## Exit criteria

- All Critical TC Pass (or waived with owner sign-off)  
- No open Critical security defects  
- High defects have owner disposition  
- Unit ReadingModuleTests green  
- Report Actual/Evidence filled; no Pass without run  
- Cleanup decision for QA rows documented  

## Out of scope this plan

- Production / Plesk deploy  
- Creating a Reviewer role  
- Auto-CEFR / SRS 10.2 extras  
- Destructive DB reset  
