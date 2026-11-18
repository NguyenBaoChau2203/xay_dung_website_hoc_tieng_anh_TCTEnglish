# C. Security & Permission Report

**No passwords, tokens, or cookies are recorded here.**

## Role matrix (Reading)

| Action | Anon | Student (Standard/Premium) | Admin author | Admin other |
|---|---|---|---|---|
| View Admin Reading list | Deny (302 Login) | Deny (AccessDenied) | Allow | Allow |
| Create/Edit draft | Deny | Deny | Allow | Allow* |
| Submit for review | Deny | Deny | Allow (own) | Limited |
| Approve / Revision / Reject | Deny | Deny | **Deny if author** | Allow if PendingReview |
| Publish | Deny | Deny | Allow if Approved | Allow if Approved |
| Import/Export/Template | Deny | Deny | Allow | Allow |
| Student library | Deny | Allow (level-gated) | Allow as learner | Allow as learner |
| Study over CEFR | Deny | **404** | N/A | N/A |
| See answer key before submit | — | **No** | — | — |
| See explanation after submit | — | Yes | — | — |

\*Edit of PendingReview/Approved/Published blocked by service `EnsureEditable`.

## Cases verified this session

| Case | Expected | Actual | Severity if fail |
|---|---|---|---|
| Anon Admin URL | Redirect login | 302 → `/Account/Login?ReturnUrl=...` | Critical |
| Anon Study | Redirect login | 302 | Critical |
| Anon Approve POST | Reject | 302 | Critical |
| Student Admin UI | Denied | AccessDenied page | Critical |
| Self-approve #4 | Rejected | Alert: Authors cannot review their own readings; status stays PendingReview | Critical |
| Other admin approve/publish | Allowed | Approved then Published by UserId 4 | Critical |
| A1 → B2 Study | NotFound | 404 | Critical |
| A1 library hides B2 | Hidden | `library_has_b2=False` | High |
| Study HTML answer key | Absent | `hasIsCorrect=False`, explanation absent pre-submit | Critical |
| Invalid Excel | No DB write | Invalid alert; no new row from bad file | High |

## Residual risks

| Risk | Notes | Severity |
|---|---|---|
| Shared-data Sync conflict | `team.onboarding.reading` invalid fields/duplicate key; package still legacy shape vs expanded registry | High (ops) |
| Publish allowed to any Admin including author after Approved | Spec focuses on review self-block; publish-by-author after other approved not separately forbidden | Medium (clarify with product) |
| IDOR on attempts/annotations | Service filters by `userId`; not fully re-probed with cross-user attempt IDs in browser this session | Medium (covered partly by unit ownership patterns; needs dedicated TC) |
| Client cannot change score | Scoring server-side on SubmitAttempt; not fuzzed with forged score body | Medium |
| Malicious xlsx beyond wrong extension | Only `.txt` and schema-invalid cases run | Medium |

## No third role

Reviewer remains Admin ≠ creator. No Reviewer role created.
