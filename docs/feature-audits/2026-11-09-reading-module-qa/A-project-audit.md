# A. Project Audit Report — Reading Module

## Stack / architecture

| Item | Evidence |
|---|---|
| Backend | ASP.NET Core MVC, net9.0 |
| ORM | EF Core + Pomelo MySQL; MariaDB 10.5.23 local Docker `:3307` |
| Frontend | Bootstrap 5, jQuery/vanilla JS (`reading-study.js`) |
| Excel | ClosedXML (`ReadingExcelService`) |
| Auth | Cookie auth; roles Admin / Premium / Standard |
| Context | `DbflashcardContext`; Reading entities under legacy `TCTVocabulary.Models` + `TCTEnglish.Models.Reading` |

## AuthN / AuthZ (code)

- Admin: `[Authorize(Roles = Roles.Admin)]` on `Areas/Admin/Controllers/ReadingManagementController.cs`
- Student: `[Authorize]` on `Controllers/ReadingController.cs`
- Mutating POSTs: `[ValidateAntiForgeryToken]`
- Over-level / missing published: controller returns `NotFound()` (not Forbid)
- Self-review: `ReadingAdminService.EnsureReviewer` blocks `CreatedByUserId == actorUserId`
- Publish: only from `Approved`

## Database (Reading-related)

Observed entities/services: `ReadingPassage` (+ Status/IsPublished/CreatedBy/PublishedBy), `ReadingParagraph`, `ReadingQuestion`, `ReadingOption`, `ReadingVocabularyItem`, `ReadingAttempt`, `ReadingStudentAnswer`, `ReadingAnnotation`, `ReadingReviewLog`, `StudentVocabulary`, `User.ReadingLevel`, legacy `UserReadingHistory` / community translation tables.

Migrations present: `20261109091316_AddReadingModuleSchema`, `20261109092305_FixReadingQuestionPointsDefault` (applied on local).

## API / routes (student)

| Route | Purpose |
|---|---|
| `GET /Home/Reading` | Level-gated library |
| `GET /Reading/Study/{id}` | Study UI + auto-start attempt |
| `POST /Reading/StartAttempt` | Practice/Timed |
| `POST /Reading/SaveAnswer` | Autosave |
| `POST /Reading/SubmitAttempt` | Score + explanations |
| `GET/POST Annotations`, `Lookup`, Notebook, Progress | Learning tools |
| Legacy community translation endpoints | Still present (out of SRS core but kept) |

## Admin routes

Index / Create / Edit / SubmitForReview / Approve / RequestRevision / Reject / Publish / Delete / DownloadTemplate / Import / ImportPreview / ImportConfirm / Export.

## Feature status matrix

| Feature | Code | Unit | Browser/HTTP this session |
|---|---|---|---|
| Admin authorize + antiforgery | Yes | Indirect | Verified |
| Draft create | Yes | — | Pass (QA-READ-001 → #3) |
| Submit review validation | Yes | — | Pass (no questions → ValidationFailed) |
| Self-approve block | Yes | Pass | Pass (HTTP alert) |
| Other-admin approve/publish | Yes | Pass | Pass (#4 logs) |
| Excel template/export | Yes | Template sheets | Pass 200 xlsx |
| Excel import preview/confirm | Yes | ImportConfirm draft | Pass valid; Pass invalid reject |
| Student CEFR library gate | Yes | Pass | Pass (no B2 for A1) |
| Over-level Study | Yes | Pass | Pass 404 |
| Hide answer key pre-submit | Yes | Pass | Pass HTML scan |
| Submit + explanation | Yes | Pass | Pass 1/1 + explanation |
| Dictionary lookup | Yes | — | Pass JSON found sleep |
| Notebook / Progress pages | Yes | — | Pass 200 |
| Timer Timed mode | Yes | — | Attr present; full auto-submit countdown not timed end-to-end |
| Mobile tabs / split | Yes (CSS/JS) | — | Library @390 observed; study mobile tabs not fully exercised |
| Shared-data Sync for Reading | Registry updated | — | **Blocked** (`team.onboarding.reading`) |
| Formal Audit Log UI beyond ReviewLogs | ReviewLogs in DB/Edit | — | Logs confirmed in DB; no separate global audit screen |

## Reuse vs new

Reuses cookie auth, Admin area pattern, Goals completion hook, shared-data registry roots. Large new surface: workflow Status, Excel, attempts/scoring, notebook/progress, CEFR gate.
