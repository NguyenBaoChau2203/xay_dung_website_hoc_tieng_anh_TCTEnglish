# B. Website Inspection Report

## Environment

| Item | Value |
|---|---|
| URL | `http://localhost:5100` |
| `/health/ready` | 200 `{"status":"ready"}` |
| Boot path | MariaDB healthy; `start-local` Sync failed; web via `dotnet run` Debug on :5100 |
| Browser | Chrome DevTools MCP |
| Accounts used | `admin@tct.local`, `student@tct.local`, temporary `admin2@tct.local` (local only) |

## Pages visited

| Page | Role | Result |
|---|---|---|
| `/` | Anon | Home marketing OK; no console errors |
| `/Account/Login` | Anon/Student | Login works |
| `/Home/Index` | Admin/Student | Dashboard OK |
| `/Admin/ReadingManagement` | Admin | List + status filter + actions |
| `/Admin/ReadingManagement/Create` | Admin | Draft form; saved #3 |
| `/Admin/ReadingManagement/Edit/3` | Admin | Workflow buttons; validation messages |
| `/Admin/ReadingManagement/Import` | Admin | Upload UI present |
| `/Admin/ReadingManagement` (student) | Student | AccessDenied |
| `/Home/Reading` | Student | Level A1 banner; published A1 only |
| `/Reading/Study/2` | Student | Passage + MCQ; submit result |
| `/Reading/Study/4` | Student | Imported passage; score 1/1 + explanation |
| `/Reading/Notebook` | Student | 200 |
| `/Reading/Progress` | Student | 200 with stats markers |

## Admin flows exercised

1. Create draft `QA-READ-001 Sample Draft` → Edit #3, Status Draft, success toast.
2. Submit for review without questions → Status `ValidationFailed`, message "At least one question is required."
3. Approve while not pending → "Reading is not pending review."
4. Download Template → HTTP 200 xlsx (~11KB).
5. Export #2 → HTTP 200 xlsx (~10KB).
6. Import invalid `.txt` → alert invalid, no DB write.
7. Import valid workbook → preview "Dữ liệu hợp lệ… Draft"; confirm → passage #4 Draft.
8. Submit #4 → PendingReview + ReviewLog.
9. Self-approve #4 as author → blocked.
10. Approve+Publish as `admin2` → Published, `PublishedByUserId=4`, logs Approved/Published.

## Student flows exercised

1. Library shows A1 only; after publish, includes `QA-READ-002 Import Sample`.
2. Does not list draft/ValidationFailed or B2 over-level item.
3. Study #2: options visible; no IsCorrect in HTML; submit showed result (0/1 when answer not persisted correctly via synthetic click on that run).
4. Study #4: select Sleep → SaveAnswer 200 → SubmitAttempt 200 → `Kết quả: 1/1`, explanation + evidence + paraphrase.
5. Lookup `sleep` → `found:true`, Vietnamese meaning.
6. Progress/Notebook reachable.
7. Viewport 390 on library: nav collapsed to hamburger; content usable.

## Console / Network

| Check | Result |
|---|---|
| Home console errors | None |
| Study #4 console errors | None |
| Study #4 network | GET Study 200; POST SaveAnswer 200; POST SubmitAttempt 200 |
| Sensitive answer fields in Study GET HTML | Not found (`IsCorrect`, explanation text absent pre-submit) |

## UI notes (non-blocking)

- Study page `<title>` often `- TCTEnglish` (empty ViewData title) — Low.
- Create/Edit form relies on Excel for full 5 question types (stated in UI) — documented product choice / Medium UX risk for manual authors.
- Community translation block still on Study (legacy) — out of SRS core.
