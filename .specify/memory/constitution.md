# TCT English Constitution

Binding principles for Spec Kit workflows and AI-assisted changes in this
repository. Derived from verified project policy ([AGENTS.md](../../AGENTS.md),
[DESIGN.md](../../DESIGN.md), [SPEC.md](../../SPEC.md), coding conventions).
Do **not** treat unverified `NEEDS_CONFIRMATION` items in SPEC.md as mandatory
rules here.

**Version**: 1.0.0 | **Ratified**: 2026-11-20 | **Last Amended**: 2026-11-20

## Core Principles

### I. Architecture boundaries

- Keep ASP.NET Core MVC + EF Core + MariaDB/Pomelo; do not introduce a parallel
  app stack or rewrite layers without owner authority.
- Controllers stay thin; business logic lives in constructor-injected services.
- Views receive ViewModels only — never raw EF entities for rendering.
- Feature ownership: HomeController/Views/Home remain dashboard/public; Reading
  learner flows use ReadingController + Study Reading list; Admin Reading stays
  in `Areas/Admin`. Do not restore feature business logic into HomeController.
- Canonical namespace is `TCTEnglish.*`. Touch legacy `TCTVocabulary.*` only when
  editing existing files that already use it; do not expand legacy namespaces.
- Preserve HTTP, JSON, and SignalR contracts unless the change explicitly updates
  them with review.

### II. Business and product fidelity

- Prefer evidence from current source and SPEC.md over plans, tickets, or
  memory. Do not invent business rules.
- Do not duplicate modules that already exist (check controllers/services first).
- Do not change existing learner or Admin Reading workflow semantics (status
  machine, CEFR gate, published-only study, Practice vs Timed) without owner
  confirmation.
- Practice scoring must not silently alter `User.ReadingLevel` unless a reviewed
  change explicitly requires it.
- Admin Reading review: Admin role; reviewer must not be the passage author
  (`CreatedByUserId`) when that rule is enforced in services.

### III. Database principles

- Persisted entity/DbContext/mapping changes require the `tct-ef-migration`
  procedure; ship migration, Designer, and snapshot with the feature.
- Validate schema on disposable MariaDB matching hosting; SQLite alone is not
  provider proof.
- No destructive DROP TABLE/COLUMN without explicit owner approval.
- Never edit applied migrations or invent/delete `__EFMigrationsHistory` rows.
- Preserve existing user and content data; plan backfills and FK/index order.

### IV. Coding standards

- Async/await for all I/O; never `.Result` or `.Wait()`.
- Naming and file placement follow `.ai/context/coding-conventions.md`.
- Register new DI services in Program.cs only with explicit task authority.
- Do not modify Program.cs, appsettings.json, or `.csproj` without authority.
- Never hardcode connection strings, OAuth/SMTP credentials, or API tokens.

### V. Security

- Enforce ownership/access on every parameterized read/update/delete.
- Use `GetCurrentUserId()` / verified BaseController helpers — not ad-hoc claim
  parsing.
- IDOR and missing resources return `NotFound()`, never `Forbid()`.
- Authenticated flows use `[Authorize]`; Admin-only uses
  `[Authorize(Roles = "Admin")]`.
- POST/PUT/DELETE require antiforgery validation.
- Secrets stay in User Secrets / environment — never commit credentials, SQL
  dumps of private data, or runtime uploads.

### VI. UX / UI

- Follow DESIGN.md and `wwwroot/css/tct-learning-ui.css` (`--tct-*`, `.tct-*`,
  `.tct-theme`).
- Page CSS stays in `wwwroot/css/{feature}.css` and consumes tokens; no new
  `:root` palette, hex brand colors, fonts, icon packs, or CDNs without owner
  brand approval.
- UI changes deliver the whole feature for mobile and desktop in the same change.
- Prefer shared partials (`_PageHeader`, `_StateMessage`) and Bootstrap 5
  utilities; avoid new inline styles for standard layouts.
- Shared CSS/layout edits are shared-impact: list consumers and preserve JS
  selectors/IDs.

### VII. Testing

- Prefer meaningful regressions before behavior changes when adding unit tests.
- Run `dotnet build` and focused tests for touched areas (Reading:
  `ReadingModuleTests`, assist tests as applicable).
- UI work: verify at desktop/tablet/mobile widths when claiming UI complete;
  state what was not observed.
- Do not claim CI or production success from local checks alone.

### VIII. Maintenance, Spec Kit, and expansion

- Read [SPEC.md](../../SPEC.md) before planning Reading or Dashboard work; read
  this constitution before implementing.
- Spec Kit feature artifacts under `.specify/specs/` describe **changes**; they
  do not replace SPEC.md project context.
- When SPEC.md drifts from source, stop inventing — report drift and propose a
  SPEC update.
- Do not add major libraries or technologies without owner confirmation.
- Local-first: commit, push, PR, remote CI, merge, and deploy only with
  explicit owner stage authority. Keep release gates intact.
- Spec Kit skills under `.cursor/skills/speckit-*` coexist with TCT skills under
  `.agents/skills`; do not delete either set to “clean up.”

## Governance

- **Priority on conflict:** Owner chat instructions for the active task override
  older prompts. AGENTS.md owner policy overrides Spec Kit defaults that would
  spawn subagents, auto-commit, or auto-deploy.
- Constitution amendments require updating this file’s version/date and aligning
  SPEC.md / Cursor rules when principles change.
- Unverified ideas belong in SPEC.md as `TODO` / `NEEDS_CONFIRMATION`, not as
  new constitutional MUST rules.
