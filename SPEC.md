# TCT English — Project Specification (SPEC.md)

> **Scope (locked):** Project overview for the whole repository + deep specification for **Learner Dashboard** and **Reading** only.  
> Other modules are listed as `OUT_OF_SCOPE` / `NEEDS_CONFIRMATION`.  
> **Rules:** Evidence from source and reviewed docs only. Unverified items are marked `TODO` or `NEEDS_CONFIRMATION`.  
> **Last surveyed:** 2026-11-20 (local checkout).  
> **Related:** [AGENTS.md](AGENTS.md) (owner policy), [`.specify/memory/constitution.md`](.specify/memory/constitution.md), [DESIGN.md](DESIGN.md).

---

## A. Project Overview

| Field | Value |
| --- | --- |
| Name | TCT English (`web_tctEnglish`) |
| Goal | Web app for English learning: vocabulary/flashcards, study modes, speaking, reading, goals, classes, and Admin content management |
| Spec deep-dive scope | Learner Dashboard (`Home/Index`) + Reading (learner + Admin authoring) |
| Primary users | Authenticated learners; Admin role for content/ops |
| Stack (verified) | ASP.NET Core MVC **net9.0**; EF Core **9.0.4**; Pomelo MySQL **9.0.0**; hosting MariaDB **10.5.23**; Bootstrap 5 + jQuery/vanilla JS; SignalR (class chat); cookie/OAuth auth |
| Canonical namespace | `TCTEnglish.*` (legacy `TCTVocabulary.*` still present in many files) |

**Out of this deep SPEC:** Class, Folder, Set, Vocabulary detail flows, Speaking, Listening, Writing, Grammar, Goals, Notifications, Admin non-Reading panels — `OUT_OF_SCOPE` until separately surveyed.

---

## B. System Architecture

### Overall

```
Browser (Razor + wwwroot JS/CSS)
    → Controllers (MVC / Area Admin)
        → Services (business logic, DI)
            → EF Core DbflashcardContext → MariaDB
    → SignalR Hubs (realtime; not central to Reading/Dashboard deep dive)
    → Workers (background; not central to this SPEC)
```

### Key directories

| Path | Role |
| --- | --- |
| `TCTEnglish/Controllers/` | Learner/public MVC controllers |
| `TCTEnglish/Areas/Admin/` | Admin-only controllers/views/viewmodels |
| `TCTEnglish/Services/` | Domain services (`Services/Reading/*` for Reading) |
| `TCTEnglish/Models/` | EF entities + `DbflashcardContext` |
| `TCTEnglish/ViewModels/` | UI contracts (views must not get raw entities) |
| `TCTEnglish/Views/` | Razor by feature |
| `TCTEnglish/wwwroot/css|js/` | Page CSS/JS; shared tokens in `tct-learning-ui.css` |
| `TCTEnglish.Tests/` | Unit/integration tests |
| `docs/` | Architecture, design system, plans |
| `.specify/` | Spec Kit scaffolding (constitution, templates, workflows) |

### Layer rules (from AGENTS.md / coding conventions)

1. Controllers stay thin; business logic in services via constructor DI.
2. Views receive ViewModels only; use `.AsNoTracking()` for display reads.
3. Ownership/access checks on every parameterized read/update/delete; use `GetCurrentUserId()` / base helpers; IDOR → `NotFound()` (not `Forbid()`).
4. POST/PUT/DELETE require antiforgery; `[Authorize]` / `[Authorize(Roles = "Admin")]` as appropriate.
5. Do not change `Program.cs`, `appsettings.json`, or `.csproj` without explicit authority.

### Data flow — Dashboard

`HomeController.Index` (authenticated) → `BuildDashboardViewModelAsync(userId)` → aggregates Users/Folders/Sets/Cards/LearningProgress/UserDailyActivity/UserFeatureTimeLogs/UserReadingHistories → `Views/Home/Index.cshtml`.

### Data flow — Reading (learner)

`StudyController.Reading` lists published passages (level-gated for non-Admin) → `ReadingController.Study` opens study UI → `IReadingLearningService` manages attempts/answers/submit → optional Assist / Notebook / Translations via dedicated services.

### Data flow — Reading (Admin)

`Areas/Admin/ReadingManagementController` → `IReadingAdminService` + `IReadingExcelService` → draft/review/publish workflow + Excel import/export.

---

## C. Business Requirements

### C.1 Module inventory (repo-level, shallow)

| Module | Status in this SPEC |
| --- | --- |
| Learner Dashboard (`Home`) | **In scope** |
| Reading (learner + Admin) | **In scope** |
| Account / Auth | Overview only — `NEEDS_CONFIRMATION` for full auth matrix |
| Class / Folder / Set / Cards | `OUT_OF_SCOPE` |
| Study modes (Quiz, Matching, Write, …) | `OUT_OF_SCOPE` except Reading list route on `StudyController` |
| Vocabulary / Speaking / Listening / Writing / Grammar | `OUT_OF_SCOPE` |
| Goals / Notifications / Chat | `OUT_OF_SCOPE` |
| Admin (non-Reading) | `OUT_OF_SCOPE` (Admin Dashboard controller exists but not surveyed) |

### C.2 Learner Dashboard — verified behavior

**Actor:** Authenticated user (`HomeController.Index`). Unauthenticated → redirect `Landing`.

**Capabilities observed in code:**

- Profile summary: `FullName`, `Role`, `Streak`, `Goal`.
- Content counts: folders, sets, cards; progress buckets Mastered / Learning(+Reviewing) / New from `LearningProgress` statuses.
- Heatmap: 28 Mon–Sun cells from `UserDailyActivities.CardsReviewed`.
- Cards reviewed today from `UserDailyActivities` for business today.
- Daily challenge widget (`GetDailyChallenge`, `CheckAnswer`, `RefreshDailyChallenge`) with data-protected challenge token.
- Today folders list.
- Feature time breakdown for current week (`UserFeatureTimeLogs` features: Flashcard, Quiz, Speaking, Reading, Listening, Writing, Grammar).
- Recent in-progress Reading: newest `UserReadingHistories` where `IsCompleted == false`.

**Business rules (verified):**

- Dashboard is owned by `HomeController`; feature screens must not grow back into Home beyond dashboard/public pages.
- Reading deep-link on dashboard uses passage id from history (`RecentReadingViewModel.Id` = `ReadingPassageId`).

**Unknowns:** Exact XP/streak update rules beyond daily challenge constant `DailyChallengeXp = 10` — `NEEDS_CONFIRMATION` for full gamification rules.

### C.3 Reading — actors and permissions

| Actor | Access |
| --- | --- |
| Learner (authenticated) | Library (published + level gate), study, attempts, annotations, notebook, assist, own translations/votes |
| Admin (`Roles.Admin`) | Full Admin ReadingManagement CRUD/workflow/import; learner library level gate bypassed in list (`StudyController` sets `ReadingLevelGated = !IsAdminUser()`) |
| Anonymous | Limited: `Translate` (MyMemory), public translation list/detail endpoints marked `[AllowAnonymous]` |

### C.4 Reading — learner flows (verified)

1. **Library:** `GET /Reading` → published passages; non-Admin filtered by `CefrLevels.CanAccess(studentLevel, passage.Level)` using `Users.ReadingLevel` (default A1).
2. **Study:** `GET /Reading/Study/{id}` → load published passage; ensure open attempt (`Practice` default); render `ReadingStudy`.
3. **Attempt:** Start / SaveAnswer / SubmitAttempt (Timed vs Practice); legacy `SubmitReading` still maps to attempt pipeline.
4. **Scoring:** Submit returns correct/total + explanations/evidence (service-side); Practice scores do **not** change `ReadingLevel` (per implementation ledger).
5. **Annotations:** save/list/delete per user+passage.
6. **Lookup / Assist:** vocab lookup on passage items; Assist for 1–8 selected words (`IReadingAssistService`).
7. **Notebook:** save/delete/export CSV; backed by `StudentVocabulary` with optional `SourceReadingPassageId`.
8. **Progress / Notebook pages:** `Reading/Progress`, `Reading/Notebook`.
9. **Community translation:** submit AI-scored translation, publish, vote, view public items.

### C.5 Reading — Admin workflow (verified)

Statuses (`ReadingStatuses`): Draft → ValidationFailed | PendingReview → RevisionRequired | Rejected | Approved → Published | Archived.

Rules from ledger + constants:

- Reviewer is Admin and must not be the same as `CreatedByUserId` (no separate Reviewer role).
- Question types: MultipleChoice, TrueFalse, GapFill, Matching, ShortAnswer.
- Excel import creates **Draft** only; publish requires review path.
- Excel `ReadingCode` is a **temporary join key** only (unique per reading in the workbook). Import **always** assigns `ExternalReadingKey = RD-{CEFR}-NNN` (next free 3-digit sequence per CEFR). Duplicate file keys hard-fail; never overwrite existing DB keys. Preview shows system code + “Mã file: …”. Media ZIP matches the Excel/ZIP filename via `ImportFileReadingCode`.
- Excel template v2 (`DownloadTemplate` → `Reading_Template_v2.xlsx`): Guide + 5 data sheets; sample uses join key `L1` with all five question types; `Reading_Info` includes optional `ImageUrl`; Answers include `Match_Key`/`Match_Side`. Optional media ZIP at Import wins over ImageUrl.
- Authoring standards (soft; not parser-enforced): Passage word-count bands and Duration midpoints per CEFR — see [`docs/content/reading-content-authoring-guidelines.md`](docs/content/reading-content-authoring-guidelines.md) (Layers 1–3: content / assessment / import). Difficulty is not judged by length alone.
- `IsPublished` synced from `Status == Published` via `SyncPublishedFlag()`.

### C.6 Business rules summary (Reading)

- Student may study only published passages (`IsPublished` and `Status == Published`).
- Level gate: student CEFR ordinal ≥ passage level (`CefrLevels.CanAccess`).
- Attempt ownership: answers/submit scoped to current user (service enforces; IDOR → NotFound pattern).
- Antiforgery on mutating POSTs.

---

## D. Database Specification

**Provider:** MariaDB via Pomelo; schema via EF migrations.  
**Views / triggers / stored procedures:** none observed in Reading-related EF migrations — treat as **absent** unless DBA confirms (`NEEDS_CONFIRMATION` for hosting-only objects).

### D.1 Reading-centric tables (entities)

| Entity / table | PK | Important fields | FKs / notes |
| --- | --- | --- | --- |
| `ReadingPassages` | `Id` | Title, Content, Level, Topic, ImageUrl, Description, EstimatedTimeMinutes, Status, IsPublished, CreatedByUserId, PublishedByUserId, ExternalReadingKey, timestamps | CreatedBy/PublishedBy → Users |
| `ReadingParagraphs` | `Id` | ReadingPassageId, ParagraphOrder, ParagraphContent, ExternalParagraphKey | → ReadingPassages |
| `ReadingQuestions` | `Id` | PassageId, QuestionText, QuestionType, OrderIndex, Points, CorrectAnswerText, Explanation, Evidence*, ExternalQuestionKey | → Passages; optional EvidenceParagraph |
| `ReadingOptions` | `Id` | QuestionId, OptionText, IsCorrect, MatchKey, MatchSide, OptionOrder | → Questions |
| `ReadingVocabularyItems` | `Id` | ReadingPassageId, Word, IPA/POS/defs, ParagraphId? | → Passages / Paragraphs |
| `ReadingAttempts` | `Id` | UserId, ReadingPassageId, Mode (Practice\|Timed), StartedAt, SubmittedAt, IsSubmitted, TimeLimitSeconds, ElapsedSeconds, Score, MaxScore | → Users, Passages |
| `ReadingStudentAnswers` | `Id` | AttemptId, QuestionId, SelectedOptionId?, AnswerText?, MatchingJson?, IsCorrect?, IsFlagged | → Attempts, Questions, Options |
| `ReadingAnnotations` | `Id` | UserId, ReadingPassageId, AnnotationType, offsets, SelectedText, NoteText, Color | → Users, Passages |
| `ReadingReviewLogs` | `Id` | ReadingPassageId, ActorUserId, FromStatus, ToStatus, Reason, CreatedAt | → Passages, Users |
| `UserReadingHistories` | `Id` | UserId, ReadingPassageId, ViewedAt, IsCompleted, Score? | unique index (UserId, ReadingPassageId) |
| `ReadingUserTranslations` | `Id` | ReadingPassageId, UserId, TranslatedTitle?, TranslatedContent (JSON), AiScore/Feedback, IsAiApproved?, IsPublic, Like/Dislike counts | unique (UserId, ReadingPassageId) |
| `ReadingTranslationVotes` | `Id` | Translation + User + VoteType | → Translations, Users |
| `StudentVocabulary` | `Id` | Word fields + `SourceReadingPassageId?`, `SourceVocabularyItemId?` | Notebook backing |
| `Users.ReadingLevel` | — | `varchar(2)`, default `A1` | Added in `AddReadingModuleSchema` |

### D.2 Dashboard-related tables (read by Home dashboard)

| Table / entity | Use on dashboard |
| --- | --- |
| `Users` | Name, Role, Streak, Goal, Folders, Sets |
| `Folders` / `Sets` / `Cards` | Counts and ownership |
| `LearningProgress` | Mastered / Learning / New |
| `UserDailyActivities` | Heatmap + cards reviewed today |
| `UserFeatureTimeLogs` | Weekly feature minutes |
| `UserReadingHistories` (+ `ReadingPassages`) | Recent in-process reading |

### D.3 Integrity notes

- Indexes exist for passage Status, IsPublished+Level, attempt user/passage/started, review logs, translation uniqueness (see `DbflashcardContext`).
- Destructive DROP TABLE/COLUMN migrations require explicit owner approval (project policy).

---

## E. API & Integration

Auth: cookie auth; most Reading learner endpoints `[Authorize]` on controller; Admin area `[Authorize(Roles = Admin)]`. Mutating POSTs: `[ValidateAntiForgeryToken]` (Home also has `[AutoValidateAntiforgeryToken]`).

### E.1 Learner Dashboard / Home (relevant)

| Method | Endpoint / action | Notes |
| --- | --- | --- |
| GET | `/` / `Home/Index` | Dashboard ViewModel |
| GET | `Home/GetDailyChallenge` | Partial `_DailyChallenge` |
| POST | `Home/CheckAnswer` | Daily challenge; antiforgery |
| GET | `Home/RefreshDailyChallenge` | Refresh challenge |
| GET | `Home/Landing`, `Contact`, `Search`, `Privacy*` | Public/home pages (shallow) |

### E.2 Reading — Study list

| Method | Path | Controller |
| --- | --- | --- |
| GET | `/Reading` | `StudyController.Reading` |

### E.3 Reading — learner API/pages (`ReadingController`)

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/Reading/Progress` | Progress view |
| GET | `/Reading/Notebook` | Notebook view (`q` search) |
| GET | `/Reading/Study/{id}` | Study page; may start attempt |
| POST | `/Reading/StartAttempt` | JSON; antiforgery |
| POST | `/Reading/SaveAnswer` | JSON body; antiforgery |
| POST | `/Reading/SubmitAttempt` | JSON; antiforgery |
| POST | `/Reading/SubmitReading` | Legacy form map → attempt |
| POST | `/Reading/Annotations` | Save |
| GET | `/Reading/Annotations/{passageId}` | List |
| POST | `/Reading/Annotations/Delete` | Delete by id |
| GET | `/Reading/Lookup` | Word lookup |
| POST | `/Reading/Assist` | AI/assist selection |
| POST | `/Reading/Notebook/Save` | Save vocab |
| POST | `/Reading/Notebook/Delete` | Delete + redirect |
| GET | `/Reading/Notebook/Export` | CSV download |
| GET | `Reading/Translate` | `[AllowAnonymous]` MyMemory `en|vi` |
| GET | `/Reading/MyTranslation/{passageId}` | Own translation |
| GET | `/Reading/Translations/{passageId}` | Public list `[AllowAnonymous]` |
| POST | `/Reading/SubmitTranslation` | Submit + AI score |
| POST | `/Reading/PublishTranslation` | Make public |
| POST | `/Reading/DeleteTranslation` | Delete own |
| POST | `/Reading/VoteTranslation` | Vote |
| GET | `/Reading/CommunityTranslation/{id}` | Detail view `[AllowAnonymous]` |

### E.4 Admin Reading (`/Admin/ReadingManagement/...`)

| Action | Method | Purpose |
| --- | --- | --- |
| Index | GET | List + filter search/status |
| Create | GET/POST | Draft create (12MB limit) |
| Edit | GET/POST | Draft update |
| SubmitForReview / Approve / RequestRevision / Reject / Publish / Delete | POST | Workflow |
| DownloadTemplate / Export | GET | Excel |
| Import / ImportPreview / ImportConfirm / ImportResult | GET/POST | Excel (+ optional media zip, preview up to 40MB request / 10MB xlsx) |
| DownloadImportErrorReport | GET | Error workbook |
| ManageQuestions | GET | Redirect to Edit |

### E.5 External services (Reading/Dashboard)

| Service | Usage |
| --- | --- |
| MyMemory Translate API | `ReadingController.Translate` HTTP GET |
| AI Assist (`IReadingAssistService`) | Passage word assist — provider/config `NEEDS_CONFIRMATION` beyond service interface |
| AI translation scoring | Via `IReadingTranslationService` — model/key wiring `NEEDS_CONFIRMATION` (project AI uses OpenAI settings elsewhere) |
| SMTP / contact | Home Contact form (`IAppEmailSender`) — dashboard-adjacent public page |

---

## F. UX/UI Specification

### Design system (project-wide, verified)

- Canonical: [DESIGN.md](DESIGN.md) + `wwwroot/css/tct-learning-ui.css` (`--tct-*` tokens).
- Primary `#4255ff`, accent `#ffcd1f`, background `#f6f7fb`, Inter stack.
- Wrap pages in `tct-theme`; reuse Bootstrap 5; page CSS under `wwwroot/css/{feature}.css` consuming `var(--tct-*)`.
- Multi-platform: same feature change must cover mobile + desktop ([multi-platform UI design](docs/superpowers/specs/2026-11-09-multi-platform-ui-design.md)).
- Shared partials: `_PageHeader`, `_StateMessage` where adopted.

### Reading UI (verified)

| Asset | Role |
| --- | --- |
| `Views/Study/Reading.cshtml` | Library (`tct-theme`, `reading-hero`) |
| `Views/Study/ReadingStudy.cshtml` | Study shell, mobile tabs, toolbar |
| `Views/Shared/_ReadingLessonCard.cshtml` | Card partial |
| `wwwroot/css/Reading.css`, `reading-study.css` | Page styles |
| `wwwroot/js/Reading.js`, `reading-study.js`, `reading-hybrid-toolbar.js`, `reading-translation.js` | Client behavior |
| Admin: `admin-reading.css` / `admin-reading.js` + ReadingManagement views | Authoring UI |

### Dashboard UI (verified)

- `Views/Home/Index.cshtml` uses `db-page tct-theme`.
- Partial `_DailyChallenge.cshtml`.
- Observation: some inline `style=` remain on dashboard — legacy inconsistency, not a license to add more.

### Responsive / interaction (Reading)

- Study page implements mobile tabs + desktop grid (observed in markup/JS).
- Timer UI for Timed mode (`readingTimer`).
- States: loading/error messaging via existing patterns — full inventory `TODO` if expanding UX polish.

---

## G. Coding Standards

Authoritative detail: [`.ai/context/coding-conventions.md`](.ai/context/coding-conventions.md) + [AGENTS.md](AGENTS.md).

| Area | Rule |
| --- | --- |
| Naming | PascalCase types/methods; `_camelCase` fields; `I` interfaces; `Async` suffix |
| Organization | Feature controllers/services; Admin `*ManagementController`; ViewModels not in Models |
| Async | Always async I/O; never `.Result` / `.Wait()` |
| Validation | ModelState on forms; service `InvalidOperationException` → NotFound/BadRequest as mapped |
| Security | Antiforgery; Authorize; ownership checks; no hardcoded secrets |
| Logging | `ILogger<T>` in controllers/services as present |
| Testing | `TCTEnglish.Tests` — Reading: `ReadingModuleTests`, `ReadingAssistServiceTests` |
| EF | Migrations reviewed; no invent/delete history rows; MariaDB validation required for schema changes |

---

## H. Development Workflow

1. **Feature work:** Prefer Spec Kit flow for *new changes*: `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-implement` → `/speckit-converge`, with this `SPEC.md` + constitution as context. Existing TCT skills under `.agents/skills` remain valid.
2. **Code review:** Owner reviews/merges unless task grants merge authority. CI green ≠ merge/deploy authority.
3. **Database:** Schema changes → `tct-ef-migration`; ship migration + Designer + snapshot in same PR; validate on disposable MariaDB.
4. **Testing:** `dotnet build` / focused `dotnet test`; browser multi-viewport for UI (1440/768/375).
5. **Local run:** `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`; check `/health/ready`.
6. **Deploy:** Master + gates per AGENTS.md; do not enable Plesk deploy without explicit owner authority. Stabilization phase: local-first.

---

## I. Current Implementation Status

### Completed (evidence: code + `docs/superpowers/plans/2026-11-09-reading-module-implementation.md`)

- Reading schema migrations `AddReadingModuleSchema`, `FixReadingQuestionPointsDefault`.
- Admin draft/review/publish + Excel template/import/export.
- Learner library level gate, study attempts, autosave, scoring/explanations, notebook, progress.
- Translation + voting entities/endpoints.
- Assist endpoint + tests.
- Dashboard aggregates including Reading minutes + recent in-process reading.
- `ReadingModuleTests` delivered in ledger (11 passed at that time).

### In progress / local dirty tree

- Working tree may contain uncommitted Reading Admin/test edits **unrelated to this SPEC task** — treat as developer WIP; do not assume merged (`NEEDS_CONFIRMATION` vs `origin/master`).

### Not implemented / gaps (do not invent)

- Full typed Admin question UX beyond Excel-first authoring (ledger risk).
- Practice mode does not advance `ReadingLevel`.
- Spec deep-dive for non-Reading modules.
- Hosting DB views/triggers inventory.

### Known technical issues (document only)

- Import confirm embeds preview JSON in hidden field (size limits).
- Legacy namespaces `TCTVocabulary.*` coexist with `TCTEnglish.*`.
- `ReadingController.Translate` uses anonymous MyMemory HTTP — availability/ToS/rate limits `NEEDS_CONFIRMATION`.
- Dashboard still has some inline styles vs design-system purity.

---

## J. Constraints & Technical Decisions

### Must keep

- ASP.NET Core MVC + EF Core + MariaDB/Pomelo stack and net9 targets.
- Feature controller boundaries; Home stays dashboard/public.
- Design tokens in `tct-learning-ui.css`; no new root palette/CDN icon/font packages without Chau/owner approval.
- Reading status constants and CEFR helpers as source of truth for workflow/levels.
- Owner execution policy in AGENTS.md (single agent, local-first, explicit stage authority).

### Must not change without confirmation

- Public routes/contracts for Reading and dashboard widgets.
- Auth cookie model and antiforgery patterns.
- Destructive schema changes.
- Adding major libraries/tech (beyond existing ClosedXML for Reading Excel already in tree).

### Confirmed decisions (Reading)

- `User.ReadingLevel` at register (default A1); Admin editable.
- Reviewer = Admin ≠ author.
- Question type set locked as in `ReadingQuestionTypes`.
- ClosedXML for Excel authoring path.

### Spec Kit vs this file

- **`SPEC.md` (this file):** durable project context for Reading + Dashboard.
- **`.specify/specs/.../spec.md`:** Spec Kit *change* specs created later via `/speckit-specify` — do not replace this file.

---

## Open items for owner confirmation

1. Whether Admin Dashboard (`Areas/Admin/DashboardController`) should be added to a future SPEC pass.
2. Production inventory of non-EF DB objects (views/triggers/SPs).
3. Exact AI provider/model bindings for Assist and translation scoring in each environment.
4. Whether MyMemory Translate should remain for production or be replaced.
5. Sync status of local Reading WIP vs published master.
