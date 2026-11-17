# D. Test Scenario List — Reading

Only scenarios confirmed in project scope. Status legend for formal run: use E-test-cases.md.

## 1. Authentication

- TS-AUTH-01 Login valid Admin / Student
- TS-AUTH-02 Login invalid credentials
- TS-AUTH-03 Logout ends session for Admin pages
- TS-AUTH-04 Unauthenticated access to protected Reading routes redirects to Login

## 2. Authorization / roles

- TS-AZ-01 Student cannot open Admin Reading
- TS-AZ-02 Student cannot POST Admin Approve/Import
- TS-AZ-03 Anon cannot POST Admin mutate
- TS-AZ-04 Author cannot approve own PendingReview
- TS-AZ-05 Other Admin can approve PendingReview
- TS-AZ-06 Student cannot study passage above ReadingLevel (404)
- TS-AZ-07 Unpublished/Draft not in student library

## 3. Admin — content management

- TS-ADM-01 List + search + status filter
- TS-ADM-02 Create Draft with title/level/content
- TS-ADM-03 Edit Draft fields
- TS-ADM-04 Cannot edit PendingReview/Published without revision path
- TS-ADM-05 Delete passage (confirm)

## 4. Admin — Excel import

- TS-IMP-01 Download template has required sheets
- TS-IMP-02 Import valid workbook → preview → confirm Draft
- TS-IMP-03 Import missing required sheet/columns → errors, no write
- TS-IMP-04 Import non-xlsx → reject
- TS-IMP-05 Import missing questions → validation errors

## 5. Admin — Excel export

- TS-EXP-01 Export published/draft as xlsx
- TS-EXP-02 Re-import exported file round-trip (formal)

## 6. Admin — review / approval

- TS-REV-01 Submit Draft with valid questions → PendingReview
- TS-REV-02 Submit without questions → ValidationFailed
- TS-REV-03 Self-approve blocked
- TS-REV-04 Other Admin Approve → Approved + ReviewLog
- TS-REV-05 RequestRevision requires reason
- TS-REV-06 Reject requires reason

## 7. Admin — publishing

- TS-PUB-01 Publish from Approved → Published + IsPublished
- TS-PUB-02 Publish from Draft/Pending rejected
- TS-PUB-03 Published appears in eligible student library

## 8. Student — library

- TS-LIB-01 Shows only Published + allowed CEFR
- TS-LIB-02 Links to Progress and Notebook
- TS-LIB-03 Search/filter chips

## 9. Student — CEFR access

- TS-CEFR-01 A1 sees A1
- TS-CEFR-02 A1 deep-link B2 → 404
- TS-CEFR-03 Higher student level can see lower passages

## 10. Student — reading interface

- TS-UI-01 Passage + questions render
- TS-UI-02 Font size controls
- TS-UI-03 Desktop split / mobile layout
- TS-UI-04 Mark/flag question if exposed

## 11. Student — timer / submission

- TS-SUB-01 Practice mode submit
- TS-SUB-02 Timed mode shows timer
- TS-SUB-03 Autosave SaveAnswer before submit
- TS-SUB-04 Auto-submit on timer expiry (formal)

## 12. Student — scoring / explanation

- TS-SCR-01 Correct MCQ scores
- TS-SCR-02 Incorrect shows correct option after submit
- TS-SCR-03 Explanation/evidence/paraphrase only after submit
- TS-SCR-04 GapFill/Matching/ShortAnswer scoring (formal; Excel-authored)

## 13. Vocabulary / progress

- TS-VOC-01 Lookup word from passage vocab
- TS-VOC-02 Save to notebook / delete / export
- TS-VOC-03 Progress page stats update after attempts
- TS-VOC-04 Practice score does not change User.ReadingLevel

## 14. API

- TS-API-01 SubmitAttempt requires auth + antiforgery
- TS-API-02 SaveAnswer ownership by attempt user
- TS-API-03 Lookup returns vocab JSON without answer key

## 15. Database

- TS-DB-01 ReviewLogs written on transitions
- TS-DB-02 Attempt/answers persisted on submit
- TS-DB-03 Status/IsPublished sync on publish

## 16. Security

- TS-SEC-01 Answer key not in Study GET
- TS-SEC-02 Score not client-trust
- TS-SEC-03 IDOR other user attempt
- TS-SEC-04 Upload size/type abuse (formal)

## 17–19. UI / Responsive / Regression

- TS-UX-01 Empty/error toasts Admin
- TS-UX-02 No console errors on Study happy path
- TS-RSP-01 1440 / 768 / 390 library + study
- TS-REG-01 `ReadingModuleTests` green after changes
