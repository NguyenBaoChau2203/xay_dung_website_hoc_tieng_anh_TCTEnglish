# F. Risk Assessment — Reading QA

| ID | Risk | Severity | Likelihood | Why | Mitigation / test priority |
|---|---|---|---|---|---|
| R1 | Shared-data Sync fails on `team.onboarding.reading` | High | Confirmed | Package/registry field mismatch or duplicate stable key; blocks `start-local` | Fix package + Status before team pull; do not force overwrite |
| R2 | Answer key leak via future ViewModel change | Critical | Low now | Verified absent today; regression must keep unit+HTML checks | Keep TC-READ-011 / unit study assertion in CI |
| R3 | Self-approve regression | Critical | Low now | Backend gate verified; UI still shows Duyệt button to author | Keep TC-READ-005; consider hiding button when actor==author |
| R4 | CEFR bypass by deep link | Critical | Low now | 404 verified for B2 | Keep TC-READ-010 |
| R5 | Manual Admin UI weak for 5 question types | Medium | High | Form tells authors to use Excel | Document; formal Excel suite TC-READ-022–029 |
| R6 | Import preview JSON in hidden field size limit | Medium | Medium | Large workbooks may fail confirm | Load-test large fixture; document max size |
| R7 | Timed auto-submit not E2E verified | Medium | Medium | Timer attr present; expiry path Not Run | TC-READ-025 |
| R8 | IDOR attempt/annotation cross-user | Medium | Unknown | Pattern uses userId; not fully fuzzed | TC-READ-033/034 |
| R9 | Publish-by-author after others approved | Medium | Possible | Allowed by current code | Product clarify; add TC if forbidden |
| R10 | Empty study page title | Low | Confirmed | `<title>- TCTEnglish` | Set ViewData Title |
| R11 | Local QA data left in Docker | Low | Confirmed | QA-READ-* / admin2 / B2 #5 | Cleanup script optional before shared demos |
| R12 | SRS file missing in repo | Medium | Confirmed | Gap analysis uses plan mapping only | Store SRS under docs/requirements |

## Recommended test order

1. AuthN/AuthZ + answer-key + CEFR + self-approve (Critical)
2. Import/export + review/publish happy path (High)
3. Scoring all 5 types + timer (High)
4. Notebook/progress/UI responsive (Medium)
5. Regression unit + shared-data Sync health (High ops)
