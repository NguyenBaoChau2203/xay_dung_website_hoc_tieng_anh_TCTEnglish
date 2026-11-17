# H. Open Issues — Reading QA Session 2026-11-09

| ID | Issue | Impact | Status |
|---|---|---|---|
| OI-01 | SRS Reading 1.1 file not found in repository | Gap analysis uses locked plan/ledger only | Open |
| OI-02 | `shared-data.ps1` Status/Sync fails: `Invalid fields/references or duplicate stable key: team.onboarding.reading` | `start-local` aborts before long-run web; team content Sync unclean | Open — **High** |
| OI-03 | Chrome DevTools MCP `upload_file` denied for workspace paths during Import UI | Import verified via authenticated HTTP multipart instead | Workaround used |
| OI-04 | Timed-mode auto-submit at expiry not observed end-to-end | Timer UI/attrs present; TC-READ-025 Not Run | Open |
| OI-05 | Full responsive matrix (768 + study mobile tabs + keyboard) incomplete | Library @390 observed; study primarily desktop | Open |
| OI-06 | GapFill / Matching / ShortAnswer / TrueFalse browser scoring not run | Unit + Excel import MCQ covered; need type-specific fixtures | Open |
| OI-07 | Cross-user IDOR fuzz on attempts/annotations not completed | Ownership code path exists; needs dedicated probe | Open |
| OI-08 | Temporary local users/data: `admin2@tct.local`, passages `QA-READ-*`, B2 #5 | Local Docker only; decide cleanup | Open |
| OI-09 | Study page document title often empty (`- TCTEnglish`) | Cosmetic | Open — Low |
| OI-10 | Admin Create/Edit JSON question UX limited vs Excel | Product known limitation from ledger | Documented |
| OI-11 | Whether author may Publish after another Admin Approved | Code allows; product clarification | Needs decision |
| OI-12 | Production / CI not in this session | Do not claim remote verification | N/A |

## Shared-data note (OI-02)

Package `data/shared/packages/team.onboarding/1` still uses legacy ReadingPassage fields (`IsPublished` without newer Status/Description/etc. alignment). Registry now includes expanded Reading fields. Local DB also has the same stable key with evolved schema. Conflict must be resolved with reviewed package update / Status procedure — **no force overwrite**.

## What was verified despite blockers

Critical AuthZ, self-approve, CEFR 404, answer-key secrecy, Excel template/import/export, two-Admin publish, student submit+explanation, lookup, notebook/progress pages, unit tests 11/11.
