# E. Test Case List — Reading

Actual Result / Status / Evidence filled **only** for cases executed in the 2026-11-09 QA session. Others remain Not Run.

### TC-READ-001
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Authentication |
| Test Scenario | TS-AUTH-04 Anon Admin |
| Test Type | Security |
| Priority | Critical |
| Preconditions | Logged out; app on :5100 |
| Test Data | GET `/Admin/ReadingManagement` |
| Test Steps | 1) Open URL without cookie 2) Observe redirect |
| Expected Result | 302 to Login with ReturnUrl |
| Actual Result | 302 → `/Account/Login?ReturnUrl=%2FAdmin%2FReadingManagement` |
| Status | Pass |
| Evidence | HTTP probe 2026-11-09 |
| Related Requirement | AuthN |

### TC-READ-002
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Authorization |
| Test Scenario | TS-AZ-01 Student Admin UI |
| Test Type | Security |
| Priority | Critical |
| Preconditions | Student session |
| Test Data | `student@tct.local` |
| Test Steps | 1) Login student 2) Open `/Admin/ReadingManagement` |
| Expected Result | Access denied, not admin data |
| Actual Result | AccessDenied page; no Admin Panel nav |
| Status | Pass |
| Evidence | Chrome MCP student isolated context |
| Related Requirement | AuthZ |

### TC-READ-003
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Create Draft |
| Test Scenario | TS-ADM-02 |
| Test Type | Functional |
| Priority | High |
| Preconditions | Admin logged in |
| Test Data | Title `QA-READ-001 Sample Draft`, Level A1 |
| Test Steps | 1) Create 2) Fill fields 3) Lưu nháp |
| Expected Result | Redirect Edit; Status Draft |
| Actual Result | Edit #3; toast "Đã lưu bản nháp."; Draft |
| Status | Pass |
| Evidence | Admin MCP Create→Edit/3 |
| Related Requirement | FR-ADM-02 |

### TC-READ-004
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Submit review validation |
| Test Scenario | TS-REV-02 |
| Test Type | Functional |
| Priority | High |
| Preconditions | Draft #3 without questions |
| Test Data | Passage #3 |
| Test Steps | Click Gửi kiểm duyệt |
| Expected Result | Validation error; not PendingReview |
| Actual Result | Status ValidationFailed; "At least one question is required." |
| Status | Pass |
| Evidence | Admin Edit #3 |
| Related Requirement | FR-ADM-06 |

### TC-READ-005
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Self-approve block |
| Test Scenario | TS-REV-03 / TS-AZ-04 |
| Test Type | Security |
| Priority | Critical |
| Preconditions | Passage #4 PendingReview, CreatedBy=admin |
| Test Data | Approve POST as author |
| Test Steps | 1) Submit #4 2) Approve as same Admin |
| Expected Result | Rejected; status remains PendingReview |
| Actual Result | Alert Authors cannot review their own readings.; Status PendingReview |
| Status | Pass |
| Evidence | HTTP + ReviewLogs |
| Related Requirement | Two-Admin review |

### TC-READ-006
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Other Admin approve + publish |
| Test Scenario | TS-REV-04 / TS-PUB-01 |
| Test Type | Functional |
| Priority | Critical |
| Preconditions | #4 PendingReview; admin2 exists |
| Test Data | admin2 session |
| Test Steps | Approve then Publish |
| Expected Result | Approved then Published; IsPublished=1; logs |
| Actual Result | Status Published; PublishedByUserId=4; logs 2→3→4 |
| Status | Pass |
| Evidence | MariaDB ReadingPassages/ReadingReviewLogs |
| Related Requirement | FR-ADM-06/07 |

### TC-READ-007
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Excel template |
| Test Scenario | TS-IMP-01 |
| Test Type | Functional |
| Priority | High |
| Preconditions | Admin |
| Test Data | GET DownloadTemplate |
| Test Steps | Download template |
| Expected Result | 200 xlsx with required sheets |
| Actual Result | 200; content-type spreadsheetml; unit also asserts sheets |
| Status | Pass |
| Evidence | HTTP + ReadingModuleTests |
| Related Requirement | FR-ADM-03 |

### TC-READ-008
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Excel valid import |
| Test Scenario | TS-IMP-02 |
| Test Type | Functional |
| Priority | High |
| Preconditions | Admin; fixture workbook |
| Test Data | `fixtures/QA-READ-import.xlsx` |
| Test Steps | ImportPreview → ImportConfirm |
| Expected Result | Preview OK; Draft created |
| Actual Result | Preview valid alert; Draft #4 `QA-READ-002 Import Sample` |
| Status | Pass |
| Evidence | HTTP + DB |
| Related Requirement | FR-ADM-04 |

### TC-READ-009
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Excel invalid file |
| Test Scenario | TS-IMP-04 |
| Test Type | Functional |
| Priority | High |
| Preconditions | Admin |
| Test Data | `.txt` uploaded as file |
| Test Steps | ImportPreview |
| Expected Result | Invalid; no DB write |
| Actual Result | Invalid alert; no new passage from bad file |
| Status | Pass |
| Evidence | HTTP |
| Related Requirement | FR-ADM-04 |

### TC-READ-010
| Field | Value |
|---|---|
| Module | Reading |
| Feature | CEFR over-level |
| Test Scenario | TS-CEFR-02 |
| Test Type | Security |
| Priority | Critical |
| Preconditions | Student ReadingLevel A1; Published B2 #5 |
| Test Data | GET `/Reading/Study/5` |
| Test Steps | Open as student |
| Expected Result | 404; not in library |
| Actual Result | 404; library_has_b2=False |
| Status | Pass |
| Evidence | HTTP |
| Related Requirement | FR-STU-01 |

### TC-READ-011
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Answer key secrecy |
| Test Scenario | TS-SEC-01 |
| Test Type | Security |
| Priority | Critical |
| Preconditions | Student; published passage |
| Test Data | Study #2 / #4 HTML |
| Test Steps | Fetch Study before submit; search IsCorrect/explanation |
| Expected Result | No answer key fields |
| Actual Result | hasIsCorrect=False; explanation absent until after submit |
| Status | Pass |
| Evidence | evaluate_script + HTTP HTML scan |
| Related Requirement | FR-STU-05 |

### TC-READ-012
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Scoring + explanation |
| Test Scenario | TS-SCR-01/03 |
| Test Type | Functional |
| Priority | Critical |
| Preconditions | Student; published #4 with MCQ |
| Test Data | Select correct option Sleep |
| Test Steps | Save answer; SubmitAttempt |
| Expected Result | Score correct; explanation/evidence shown |
| Actual Result | Kết quả 1/1; ✓; explanation + evidence + paraphrase; network 200 |
| Status | Pass |
| Evidence | Chrome MCP Study #4 |
| Related Requirement | FR-STU-05 |

### TC-READ-013
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Dictionary lookup |
| Test Scenario | TS-VOC-01 |
| Test Type | Functional |
| Priority | Medium |
| Preconditions | Passage #4 vocab sleep |
| Test Data | GET Lookup passageId=4 word=sleep |
| Test Steps | Call Lookup |
| Expected Result | found true with meaning |
| Actual Result | `found:true`, vietnameseMeaning ngủ |
| Status | Pass |
| Evidence | HTTP JSON |
| Related Requirement | FR-STU-04 |

### TC-READ-014
| Field | Value |
|---|---|
| Module | Reading |
| Feature | Export Excel |
| Test Scenario | TS-EXP-01 |
| Test Type | Functional |
| Priority | Medium |
| Preconditions | Admin; passage #2 |
| Test Data | GET Export/2 |
| Test Steps | Download |
| Expected Result | 200 xlsx |
| Actual Result | 200; ~10KB spreadsheetml |
| Status | Pass |
| Evidence | HTTP |
| Related Requirement | FR-ADM-05 |

### TC-READ-015 … TC-READ-040 (Not Run — ready for formal suite)

| ID | Feature | Priority | Summary Expected |
|---|---|---|---|
| TC-READ-015 | Invalid login | High | Error message; stay logged out |
| TC-READ-016 | Logout | High | Admin URL redirects login |
| TC-READ-017 | Student POST Approve | Critical | 302/403 AccessDenied; no status change |
| TC-READ-018 | Publish from Draft | High | Rejected |
| TC-READ-019 | RequestRevision empty reason | High | Rejected |
| TC-READ-020 | Reject empty reason | High | Rejected |
| TC-READ-021 | Edit while PendingReview | High | Blocked |
| TC-READ-022 | Import missing Questions sheet | High | Preview errors |
| TC-READ-023 | Import wrong Question_Type | High | Row error |
| TC-READ-024 | Export→Import round-trip | High | Draft equivalent content |
| TC-READ-025 | Timed auto-submit | High | Submits at 0 |
| TC-READ-026 | Autosave reconnect | Medium | Answers restored |
| TC-READ-027 | GapFill scoring normalize | High | Case/trim rules |
| TC-READ-028 | Matching scoring | High | Pair set correct |
| TC-READ-029 | ShortAnswer scoring | High | Normalized match |
| TC-READ-030 | Notebook save/delete/export | Medium | CRUD works |
| TC-READ-031 | Progress after attempts | Medium | Stats increase |
| TC-READ-032 | ReadingLevel unchanged by Practice | High | User.ReadingLevel same |
| TC-READ-033 | IDOR SubmitAttempt other user | Critical | NotFound/BadRequest |
| TC-READ-034 | IDOR annotation other user | High | NotFound |
| TC-READ-035 | Forge score in client body | Critical | Server ignores; DB score authoritative |
| TC-READ-036 | Oversized xlsx | Medium | Rejected safely |
| TC-READ-037 | Mobile study tabs 390 | Medium | Passage/Questions usable |
| TC-READ-038 | Desktop 1440 split scroll | Medium | Independent panes |
| TC-READ-039 | Keyboard-only study | Medium | Focus/submit reachable |
| TC-READ-040 | Regression ReadingModuleTests | Critical | 11/11 pass |

**Session unit regression:** TC-READ-040 Actual = 11 passed; Status = Pass.
