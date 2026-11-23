# Ledger — Archive trùng Reading Published (local)

Date: 2026-11-22  
DB: local Docker MariaDB `127.0.0.1:3307` / `tctenglish_dev`  
Rule: keep one Published per `(Title, Level)` with Duration mid-range (A1=6…C2=28); Archive losers via `ReadingAdminService.ArchiveAsync`.

## Results

| Metric | Value |
|--------|-------|
| Duplicate groups found | 12 |
| KEEP | 12 |
| ARCHIVED | 16 |
| Failures | 0 |
| Remaining duplicate Published groups | 0 |

## KEEP (after apply)

| Level | Title | Id | Key | Duration |
|-------|-------|----|-----|----------|
| A1 | At School | 30 | RD-A1-008 | 6 |
| A1 | My Morning Routine | 29 | RD-A1-007 | 6 |
| A2 | A Weekend Trip | 31 | RD-A2-003 | 8 |
| A2 | Cooking with Grandma | 32 | RD-A2-004 | 8 |
| B1 | Protecting City Parks | 33 | RD-B1-003 | 12 |
| B1 | Smartphones in Daily Study | 34 | RD-B1-004 | 12 |
| B2 | Reading News in the Digital Age | 36 | RD-B2-004 | 18 |
| B2 | Remote Work and Team Trust | 35 | RD-B2-003 | 18 |
| C1 | Automation and Meaningful Work | 38 | RD-C1-004 | 22 |
| C1 | Public Trust in Scientific Advice | 37 | RD-C1-003 | 22 |
| C2 | Taste, Class, and Cultural Criticism | 40 | RD-C2-004 | 28 |
| C2 | The Limits of Consensus in Academic Debate | 39 | RD-C2-003 | 28 |

## ARCHIVED ids

13–28 (prior import copies with older Duration or lower RD sequence).

## Checks

- Dry-run after apply: `Duplicate groups: 0`
- `/health/ready` → 200 ready
- Browser `/Home/Reading`: one card per former duplicate title (e.g. At School **6 phút** only)

## Tooling (local only)

- `.appdata/local/run-reading-dedupe.ps1` (`-Apply` to archive)
- Reports: `.appdata/local/reading-dedupe/dedupe-*.{json,tsv}`

## Notes

- Hard-delete not used; learner Attempts/History on archived ids remain in DB but cards leave catalog.
- Progress that was on archived Duration-10 “At School” etc. no longer appears as “Đang học” on the kept Guidelines card (no merge).
