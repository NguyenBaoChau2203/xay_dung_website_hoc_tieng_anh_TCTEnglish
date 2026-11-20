# TCT English Project Structure

This document is the authoritative map for the post-refactor repository layout on
the `architecture-hardening` branch. It describes the intended folder
responsibilities after the controller split, ViewModel normalization, and
repository cleanup work.

If you need the docs taxonomy first, start with [README.md](README.md).

Use this together with [post-refactor-followup-plan.md](post-refactor-followup-plan.md)
for ongoing architecture work. The branch handoff note remains historical
context; this file describes the structure we intend to keep.

## Repository Root

| Path | Responsibility |
| --- | --- |
| `TCTEnglish/` | Main ASP.NET Core MVC application |
| `TCTEnglish.Tests/` | Smoke, regression, and integration tests |
| `docs/` | Architecture notes, handoff docs, and cleanup guidance |
| `scripts/` | Local helper scripts such as encoding or validation utilities |
| `.ai/context/` | Project-specific architecture, conventions, glossary, and bug history |
| `.agent/workflows/` | Repo workflows used to guide implementation and review passes |
| `.agents/skills/` | Canonical discoverable TCT project skills |
| `.agent/skills/` | Antigravity adapters pointing to canonical skills |
| `.claude/skills/`, `CLAUDE.md` | Claude Code adapters and shared rules imports |
| `.cursor/`, `.mcp.json`, `.agents/*config.json`, `.agents/hooks.json` | Host-native MCP and lifecycle hook definitions |
| `SETUP-AGENTS.cmd`, `.vscode/tasks.json` | Click/folder-open team setup; see docs/team-agent-setup.md |
| `.ai/roles/` | Review lenses applied inline by the same agent |
| `.ai/vendor/` | Selected attributed upstream references, not auto-loaded plugins |
| `.ai/tooling/` | Source lock and archived replaced guidance |
| `.codex/config.toml` | Project-scoped single-agent and Git MCP configuration |
| `.codex/hooks.json` | Project lifecycle definitions; require host review/trust |
| `.githooks/` | Staged tooling checks; enable with scripts/install-agent-hooks.ps1 |
| `tooling/mcp/` | Isolated locked Python dependencies for the Git MCP reference server |
| `tooling/codegraph/` | Pinned CodeGraph package/lock and read-only MCP adapter |
| `tooling/.runtime/` | Ignored local bootstrap binaries, locks and install fingerprints |
| `DESIGN.md` | Canonical visual standard; every token matches `TCTEnglish/wwwroot/css/tct-learning-ui.css` |
| `docs/design-system/` | `team-ui-workflow.md` (team process, PR checklist, legacy-screen tracker) and `preview.html` (local component gallery that loads the real shared CSS/JS) |

## Main Application Layout

| Path | Responsibility |
| --- | --- |
| `TCTEnglish/Controllers/` | MVC controllers for public and authenticated user flows |
| `TCTEnglish/Areas/Admin/Controllers/` | Admin-only controllers |
| `TCTEnglish/Services/` | Business workflows, reusable domain services, and small infrastructure abstractions |
| `TCTEnglish/ViewModels/` | Typed UI models for the main application |
| `TCTEnglish/Areas/Admin/ViewModels/` | Typed UI models used only by the admin area |
| `TCTEnglish/Views/` | Razor views organized by feature/controller responsibility |
| `TCTEnglish/Models/` | EF Core entities, `DbflashcardContext`, and seeding helpers only |
| `TCTEnglish/Security/` | Authentication and current-user helper extensions |
| `TCTEnglish/Hubs/` | SignalR hub endpoints |
| `TCTEnglish/Realtime/` | Shared presence/state helpers and SignalR message contracts used across hubs, admin flows, and workers |
| `TCTEnglish/Workers/` | Background workers such as auto unlock processing |
| `TCTEnglish/wwwroot/` | Static assets, library files, and runtime upload roots |

## Controller And View Boundaries

The current app structure is feature-oriented, but route compatibility is still
preserved where useful. That means some flows now live in dedicated controllers
even when the public URL still uses `/Home/...`.

### Controllers

- `HomeController` is intentionally limited to dashboard and public pages such as landing, contact, search, privacy, and dashboard widgets.
- `ClassController` owns class browsing, creation, and class detail flows.
- `FolderController` owns the legacy folder detail (`/Home/FolderDetail/{id}`) and its edit routes; `/Home/Folder` redirects to `/Sets`. The folder list and folder page now live in `SetController.Personal` (`/Sets/Folders/*`) with rules in `PersonalSetService`.
- `SetController` (partial: `SetController.cs` legacy `/Home/*` set routes, `SetController.Personal.cs` personal-set workspace) owns set create/edit/delete, `/Sets`, `/Sets/Folders/{id}[/Rename|/Delete|/Unsave]`, `/Sets/Folders/Create`, `/Sets/{id}/MoveToFolder`, `/Sets/{id}[/Settings|/Add|/Import|/Export]`, `/Sets/Cards/{id}[/Edit]`, `/s/{slug}`, follow/clone/samples.
- `SetApiController` (`/api/sets/*`) and `VocabApiController` (`/api/vocab/*`, Premium/Admin AI enrich) serve the personal-set JSON endpoints; all rules live in `Services/Sets/PersonalSetService`.
- `PracticeController` (`/Practice`, `/Practice/{mode}`, `/Practice/HardWords`) serves the practice hub, the eight session pages (learn, test, match, flashcard, quiz, typing, listening, cloze) and the hard-words list. It only selects words (`IPersonalSetService.GetPracticeHubAsync/GetPracticeAsync/GetHardWordsAsync`, `PracticeModes`); grading, Learn/Match state and the test draft run in the browser (`wwwroot/js/practice-core.js`, `practice-ui.js`, one file per mode, `wwwroot/css/practice.css`). Review schedules move only through the existing `POST /Home/UpdateCardProgress`, and only when the user turns on "Tính vào ôn tập". The legacy `/Home/Study|QuizMode|WriteMode|MatchingMode` pages are unchanged.
- `StudyController` owns study-mode views such as quiz, matching, write, reading, listening, and related practice pages.
- System content authoring (plan [2026-11-19-system-content-fill](superpowers/plans/2026-11-19-system-content-fill.md), guide [system-content-sources](content/system-content-sources.md)): local tools only. `tools/content/*.py` build, image, IPA-check and validate the vocabulary manifests in `data/content/vocabulary/**` and the Listening manifest `data/listening/system-a1-c2-2026-11.json`; `tools/ContentImport` (+ `scripts/content-import.ps1`) imports them into the local Docker database and stages captions; `TCTEnglish.Tests/ContentManifestTests.cs` guards the manifests in CI. Packages go through `scripts/shared-data.ps1`.
- Listening (direction F, plan [2026-11-18-listening-redesign](superpowers/plans/2026-11-18-listening-redesign.md)): the catalog `Views/Study/Listening.cshtml` was rebuilt as direction B on 2026-11-20 ([plan](superpowers/plans/2026-11-20-listening-catalog-b.md)): a photo hero (`_ListeningHero`) beside the "Bài nghe từ YouTube" card with the import form (`_ListeningYoutubeCard`), band pills, status toggles and topic select, a "Bài nghe của tôi" shelf and level shelves of `_ListeningLessonCard` / `_ListeningMyLessonCard`, driven by `wwwroot/js/listening-index.js` (the hero mode, lesson and hint come from `ListeningService.ApplyHero`; bands and the hint text live in `ListeningBands` / `ListeningHeroHint`); the practice page `ListeningPractice.cshtml` uses `_ListeningStepCard` and five tabs (transcript, dictation, fill-in, quiz, vocabulary) in `listening-practice.js`. `listening-core.js` holds the pure, node-tested logic (grading, word spans, dictation store, level summary, subtitles); `listening-word.js` is the word popover/bottom sheet; styles are `wwwroot/css/listening.css` (scope `.tl-page`). `Controllers/ListeningApiController.cs` serves `GET api/listening/word` and `POST api/listening/word/save`; the rules live in `Services/Listening/ListeningWordService` (with `ListeningWordForms`, the C# mirror of `baseForms`). Tests: `ListeningRedesignRenderTests`, `ListeningWordServiceTests`, `scripts/tests/test_listening_core_redesign.cjs`.
- `ChatController` owns chat upload and class chat HTTP endpoints.
- First-run onboarding ([feature note](features/onboarding.md)): `OnboardingApiController` (`/api/onboarding/*`) and `Services/Onboarding/*` (catalogue of goals and the 48 system-set topics, deterministic deck builder, `OnboardingService`) create 2–4 personal starter decks from the system vocabulary for a new account; state lives in the table `UserOnboardings` (`Models/UserOnboarding.cs`, `DbflashcardContext.Onboarding.cs`). The pop-up is hosted by `Views/Shared/_OnboardingHost.cshtml` (one line on Trang chủ, Từ vựng and Bộ thẻ của tôi) and built by `wwwroot/js/onboarding.js` on top of the pure `onboarding-core.js`, styles in `onboarding.css`. `PersonalSetService` adds mosaic covers and the starter chip to the set tiles, and the deck detail gets `Views/Set/_StarterRefill.cshtml` with `onboarding-refill.js`.
- `VocabularyController` serves the system Vocabulary module: home (`/Vocabulary`, `/Vocabulary/Index`, `FolderDetail`), course page (`Detail`, `Topics`, `TopicDetail`) and the lesson screen (`Study`, `Review`). It leans on `IStudyService` (`StudyService.Vocabulary.cs`); lessons are computed by `Services/Vocabulary/VocabularyLessonBuilder.cs`, progress is saved through its own `POST /Vocabulary/Review/Record` (memory level 0-6 in `LearningProgress.RepetitionCount`, intervals 10 min, 1, 3, 7, 14, 30, 60 days, computed by `Services/Vocabulary/SpacedRepetitionScheduler.cs`); `VocabularyEmojiMap.cs` supplies word pictures. `POST /api/LearningApi/record` is still used by personal sets only. Client logic: `wwwroot/js/vocabulary-core.js` (pure), `vocabulary-home.js`, `vocabulary-course.js`, `vocabulary-lesson.js`; styles: `vocabulary.css` on top of `study-kit.css` and `practice.css`.
- `SpeakingController` remains the speaking playlist and practice area.
- `BillingController` owns the learner payment flow: `/Premium` (plans and one checkout form), `POST /Billing/Checkout`, `/Billing/Order/{code}` (one self-updating page per order), `POST /Billing/Order/{code}/Refresh`, `/Billing/History`, the status JSON `/Billing/OrderStatus` and the provider callbacks (`/api/billing/vnpay/ipn`, `/api/billing/momo/ipn`, `/Billing/VnPayReturn`, `/Billing/MoMoReturn`). `/Billing/MoMoQr` only redirects to the order page. Admin billing lives in `Areas/Admin` (`BillingManagementController`).
- Payment rules live in `Services/Billing`: an order becomes `paid` in exactly one place, `PaymentConfirmationService` (a guard test enforces it), and every status change is checked by `PaymentOrderStateMachine`. `PaymentQueryService` asks the provider (VNPay QueryDR, MoMo query) for a return, a refresh or the reconciliation worker; `PaymentOrderLabels`, `PaymentOrderPresentation` and `BillingTime` are the shared learner wording, stage and Vietnam-time helpers.

### Views

- `Views/Home/` now contains only dashboard/public pages and shared home-specific partials.
- The public landing page (`Views/Home/landing.cshtml`, `Layout = null`) is a standalone dark page: styles in `wwwroot/css/landing.css` (scoped `--lp-*` tokens), behaviour in `wwwroot/js/landing.js` plus the pure, node-tested `wwwroot/js/landing-core.js` (the client-only demo), screenshots and the share image in `wwwroot/images/landing/`, tests in `TCTEnglish.Tests/LandingPageTests.cs` and `scripts/tests/test_landing_core.cjs`.
- `Views/Class/`, `Views/Folder/`, `Views/Set/`, and `Views/Study/` are the canonical feature view folders created by the refactor.
- `Views/Vocabulary/` (`Index`, `Detail`, `Study` and the partials `_CourseCard`, `_LessonRow`, `_WordRow`, `_CourseArt`, `_LevelDots`, `_MemoryPanel`, `_ReviewForecast`) and `Views/Speaking/` remain feature folders for those domains.
- `Views/Billing/` (`Pricing`, `Order`, `History`, `InvalidReturn` and the partials `_PremiumStatus`, `_OrderSummary`, `_OrderStatusChip`, `_Receipt`, `_BenefitsTable`) is the Clay-styled payment UI: `wwwroot/css/billing.css` (scope `.tb-page`, on top of `study-kit.css`) and `wwwroot/js/billing-core.js` (pure logic, node-tested) with `billing-pricing.js` and `billing-order.js`; the QR script is the pinned `wwwroot/lib/pinned/qrcode-1.0.0.min.js`.
- Notifications: `NotificationController` serves `/Notification` (and `/Notification/Index`) plus `api/notifications*` (list by page or by keyset cursor, unread count, mark read / mark all with counts). `Services/Notifications/NotificationDisplay` is the one display rule (category, tone, icon, clean title, Vietnam-time text, day group) for the page, the bell and the API; `NotificationLinks` accepts only local paths. `NotificationService` owns paging, category counts, `CreateForUserAsync` (idempotent by event key; billing uses it for the Premium activation notice), batch announcements (`announce:{admin}:{batch}`, chunks of 500 in one transaction) and the history grouped in the database. UI: `Views/Notification/` (`Index`, `_NotificationItem`, `_NotificationSidePanel`) with `notification-page.css` (scope `.tn-page`, on top of `study-kit.css`) and `notification-page.js`; the shell bell is `_NotificationDropdown.cshtml` + `notifications.css` + `notifications.js`; `notification-core.js` holds the pure logic (node-tested) and the two scripts talk through the `tct:notifications-changed` event. Admin announcements: `NotificationManagementController` (logic and copy only).
- `Areas/Admin/Views/` stays isolated inside the admin area.
- Admin content modules (vocabulary sets, listening lessons, payments, announcements) share one kit: `Services/Admin/` holds the tested rules (`AdminInputRules`, `AdminResult`, `AdminVocabularyService`, `AdminListeningService`, `SystemVocabulary` export inside the vocabulary service, read-only `AdminBillingLabels`); `Areas/Admin/Views/Shared/_Ac*.cshtml` (page header, figures, status tag, alerts, filter bar, table header, pager, type-the-name delete) with models in `Areas/Admin/ViewModels/AdminContentShared.cs`; `wwwroot/css/admin-content.css` (scope `.ac-page`, tokens only) and `wwwroot/js/admin-content.js` (pure helpers, node-tested) plus `admin-listening-editor.js`. `SetManagementController` and `ListeningManagementController` are thin and call the services (listening editor routes: `Editor/{id}?tab=info|transcript|quiz|vocab`; the old `ManageTranscript/Quiz/Vocab` GET routes redirect to the tabs). `BillingManagementController.Index` and `NotificationManagementController.Create` only gained read-only figures and the recipient count. Rules: [plan](superpowers/plans/2026-11-18-admin-content-complete.md).

## ViewModel Placement Rules

- Main-site typed models belong in `TCTEnglish/ViewModels/`.
- Admin-only typed models belong in `TCTEnglish/Areas/Admin/ViewModels/`.
- EF entities and persistence types stay in `TCTEnglish/Models/`.
- Do not reintroduce UI-only types under `Models/` or the removed legacy `ViewModel/` folder.

## Important Normalizations Kept By This Branch

| Old path | New path |
| --- | --- |
| `TCTEnglish/ViewModel/*` | `TCTEnglish/ViewModels/*` |
| `TCTEnglish/Models/ErrorViewModel.cs` | `TCTEnglish/ViewModels/ErrorViewModel.cs` |
| `TCTEnglish/Models/ForgotPasswordViewModel.cs` | `TCTEnglish/ViewModels/ForgotPasswordViewModel.cs` |
| `TCTEnglish/Models/ResetPasswordViewModel.cs` | `TCTEnglish/ViewModels/ResetPasswordViewModel.cs` |
| `TCTEnglish/Models/UpdateProfileViewModel.cs` | `TCTEnglish/ViewModels/UpdateProfileViewModel.cs` |
| `TCTEnglish/Views/Home/Class*.cshtml` | `TCTEnglish/Views/Class/` |
| `TCTEnglish/Views/Home/Folder*.cshtml` | `TCTEnglish/Views/Folder/` |
| `TCTEnglish/Views/Home/CreateSet.cshtml`, `EditSet.cshtml` | `TCTEnglish/Views/Set/` |
| `TCTEnglish/Views/Home/Study.cshtml`, `QuizMode.cshtml`, `WriteMode.cshtml`, `MatchingMode.cshtml`, `Speaking.cshtml`, `Reading.cshtml`, `Listening.cshtml`, `Writing.cshtml`, `Grammar.cshtml` | `TCTEnglish/Views/Study/` |

## Repository Hygiene Rules

- Local-only directories such as `.tmp/`, `.dotnet-home/`, `.dotnet/`, `.dotnet_cli/`, `.appdata/`, `bin/`, and `obj/` are not source and should stay ignored.
- Temporary artifacts should not be committed under the repository root or inside `docs/`.
- Runtime uploads stay under the existing ignored subfolders in `wwwroot/`.
- Keep documentation in `docs/` and keep ad hoc scratch notes inside ignored local folders instead of source directories.

## Intentional Non-Changes

These points are intentional and should not be treated as cleanup misses:

- The canonical project namespace is `TCTEnglish.*`. Older
  `TCTVocabulary.*` references may still appear in legacy files and should be
  treated as transition leftovers.
- `TCTEnglish/Realtime/` remains separate from `TCTEnglish/Hubs/` because it holds shared real-time support types, not hub endpoints.
- `Views/Home/` still exists because the app still has real home/public pages. The goal is not to remove the folder, only to keep feature screens out of it.
- Existing routes and behavior should stay stable even when implementation files moved to cleaner locations.

## Quick Scan Checklist For Future Changes

- New controller-heavy workflow: prefer a dedicated controller plus service, not more `HomeController` growth.
- New UI model: place it in `ViewModels/` or `Areas/Admin/ViewModels/`.
- New real-time endpoint: hub in `Hubs/`, shared payload/state types in `Realtime/` if reused elsewhere.
- New background job: place it in `Workers/`.
- New documentation about architecture or structure: place it in `docs/`.
