# Ledger — Reading Content Authoring Guidelines + packs import/publish/covers

Date: 2026-11-22 (local verification)  
Scope: Guidelines A1–C2, rewrite 12 Excel packs, local import → Published, real Pexels covers.

## Outcomes

| Item | Result |
|------|--------|
| Guidelines 3 layers | `docs/content/reading-content-authoring-guidelines.md` |
| Guide sheet + SPEC + Excel design cross-link | Done (soft word bands; Duration mid-range) |
| `ReadingLevelPacks` A1–C2 rewrite | Done; L1/L2; 5 question types; Duration A1=6 … C2=28 |
| Generator tests | `ReadingImportContentGeneratorTests` — **Passed** (Release) |
| Regen xlsx | `.appdata/local/reading-imports/TCT_Reading_Import_{A1–C2}.xlsx` |
| Local import + publish | 12 new Drafts → Submit → Approve → Publish |
| Covers | Pexels JPEGs under `wwwroot/images/reading/`; `ImageUrl` set |
| Catalog check | `/Home/Reading` lists new titles with mid-range durations |

## Soft word-count bands (Passage only)

| CEFR | Range | Packs (approx words) |
|------|-------|----------------------|
| A1 | 80–120 | 87 / 89 |
| A2 | 120–180 | 142 / 131 |
| B1 | 200–300 | 203 / 205 |
| B2 | 300–450 | 315 / 307 |
| C1 | 450–600 | 455 / 471 |
| C2 | 550–750 | 576 / 590 |

Parser does **not** hard-fail on word count (by design).

## Newly published local codes (this run)

| Key | Title | Duration | Cover file | Pexels photo id |
|-----|-------|----------|------------|-----------------|
| RD-A1-007 | My Morning Routine | 6 | cover-morning-routine.jpg | 376464 |
| RD-A1-008 | At School | 6 | cover-at-school.jpg | 256395 |
| RD-A2-003 | A Weekend Trip | 8 | cover-weekend-trip.jpg | 457882 |
| RD-A2-004 | Cooking with Grandma | 8 | cover-cooking-grandma.jpg | 2284166 |
| RD-B1-003 | Protecting City Parks | 12 | cover-city-parks.jpg | 1162251 |
| RD-B1-004 | Smartphones in Daily Study | 12 | cover-smartphone-study.jpg | 4145153 |
| RD-B2-003 | Remote Work and Team Trust | 18 | cover-remote-work.jpg | 3184292 |
| RD-B2-004 | Reading News in the Digital Age | 18 | cover-digital-news.jpg | 518543 |
| RD-C1-003 | Public Trust in Scientific Advice | 22 | cover-science-trust.jpg | 2280571 |
| RD-C1-004 | Automation and Meaningful Work | 22 | cover-automation.jpg | 3861969 |
| RD-C2-003 | The Limits of Consensus in Academic Debate | 28 | cover-academic-debate.jpg | 256541 |
| RD-C2-004 | Taste, Class, and Cultural Criticism | 28 | cover-cultural-criticism.jpg | 237297 |

Older published copies of the same titles (earlier Duration values) were **left in place** per plan non-goal (no bulk delete).

## Local tooling used (ignored / not for production)

- `.appdata/local/run-reading-pack-import.ps1` + `ReadingPackImport/` — ConfirmImport + Admin workflow + ImageUrl against Docker MariaDB `127.0.0.1:3307`
- `.appdata/local/assign-reading-covers.ps1` — prior cover download helper
- Cover source: Pexels CDN photographer stock (not AI-generated)

## Checks

- `dotnet test --filter FullyQualifiedName~ReadingImportContentGeneratorTests -c Release` → Passed 2/2
- HTTP `200` for `/images/reading/cover-*.jpg` via local app `:5100`
- Browser `/Home/Reading`: new cards visible (e.g. A1 **6 phút**, A2 **8 phút**, C2 **28 phút**)
- `/health/ready` → ready

## Non-goals (unchanged)

- No parser hard-fail word-count
- No Excel schema / question-type change
- No push / PR / deploy / Program.cs / shared-data production
- No bulk unpublish of older Reading rows

## Risks / notes

- Local DB has multiple Published rows sharing titles from repeated import experiments; learner catalog shows duplicates until an owner decides cleanup.
- Import tool is local-only under `.appdata`; do not promote without review.
