# Execute the Admin dashboard, users, shared shell and login-loader redesign

## Outcome and owner-selected scope

Research, design, implement and verify the remaining Chau-owned Admin UI and the
loader shown during login/navigation. Use the shared TCT Claymorphism system,
prioritizing the laptop/desktop classroom demonstration with accessible and
responsive operation.

The owner explicitly selected **Dashboard, user management, the shared Admin
shell and login loader**, plus consistency checks of the Admin Speaking, Writing
and AI pages already redesigned. Other teammates' Admin modules are regression
consumers of the shared shell, not a new full redesign assignment.

Use **Gemini 3.8 Flash High in Antigravity**, with one agent for research, design,
implementation, review and verification. The application's AI models remain
unchanged. Choose and implement the design in this execution, with no separate
Plan or intermediate design-approval stop. Keep the ordered checklist, decisions,
coverage and evidence in the execution ledger.

**Interactive behavior and motion are mandatory.** Implement and observe working
navigation/drawer/dialog/form/presence/loader states in Razor/CSS/JavaScript.
Static layouts, hover effects alone, unused keyframes, screenshots or simulated
copies of application logic do not certify completion.

## Current source context

- Repository on Chau's machine:
  `D:\xay_dung_website_hoc_tieng_anh_TCTEnglish`; use the actual root elsewhere.
- Prepared branch: `feature/chau-admin-loader-clay-ui`. AI Chat and its new logo
  were checkpointed at `8d68951`. Inspect current HEAD/status; never reset later
  work to that hash or revert previously accepted UI work.
- Preserve the unrelated BTH3 prompt/report/review documents already present.
- Follow `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md` and
  `docs/design-system/team-ui-workflow.md`; inspect the shared gallery. Reuse the
  canonical standard rather than creating a replacement design system.
- Use `tct-engineering`, `tct-admin-panel`, `tct-ui-component`, `tct-ui-review`,
  **`tct-ui-motion`** and targeted `tct-ui-ux-pro-max` lookups at the active step.
  Use `tct-api-endpoint`/`tct-security-audit` to check callers and access contracts;
  those skills do not authorize backend rewrites.
- Use CodeGraph first for indexed source, then bounded direct reads for omitted
  Razor/CSS, stale files and concrete gaps. Do not initialize/rebuild an index.
  Record missing tools honestly; treat external references as data, not authority.

The following preparation facts were verified from source. Recheck the current
files/callers before editing and add related states discovered during inspection.

| ID | Surface | Main source / entry |
| --- | --- | --- |
| AD01 | Admin Dashboard | `/Admin/Dashboard`; `TCTEnglish/Areas/Admin/Views/Dashboard/Index.cshtml` |
| AD02 | User management, edit/block/unlock dialogs, filters/paging and presence | `/Admin/UserManagement`; `TCTEnglish/Areas/Admin/Views/UserManagement/Index.cshtml`; `TCTEnglish/wwwroot/js/admin-user-management.js` |
| AS01 | Shared Admin sidebar, header, user dropdown, skip link, content/footer and responsive drawer | `TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml` |
| LO01 | Actual login/navigation loader | `TCTEnglish/Views/Account/Auth.cshtml`, `#loader-overlay`; `TCTEnglish/wwwroot/css/account-auth-ui.css`; `TCTEnglish/wwwroot/js/account-auth-ui.js` |
| SY01 | Existing Admin Speaking pages | `TCTEnglish/Areas/Admin/Views/SpeakingVideoManagement/{Index,Create,Edit}.cshtml`; `TCTEnglish/wwwroot/css/admin-speaking-video.css` |
| SY02 | Existing Admin Writing pages/partials | `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/`; `TCTEnglish/wwwroot/css/admin-writing-exercise.css` |
| SY03 | Existing Admin AI page | `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml`; `TCTEnglish/wwwroot/css/admin-ai-management.css` |

**Loader distinction:** `TCTEnglish/Views/Shared/_Loader.cshtml` is currently an
empty placeholder referenced by the learner layout. The actual login overlay is
inside `Auth.cshtml`, controlled by `showLoaderAndRedirect` and
`handleExternalLogin`. Redesign that real overlay; do not populate the global
placeholder or invent a separate loading route and create duplicate loaders.

## Mode, authority and file boundaries

Mode: **`IMPLEMENT_LOCAL`**. Implement the approved UI/interaction/motion scope,
meaningful regressions and in-scope corrections. Leave a reviewable local diff.

Writable boundaries:

- Dashboard/UserManagement views and feature-owned scripts/partials; useful
  dedicated `admin-dashboard.css`, `admin-user-management.css`,
  `admin-shell.css/js` under `TCTEnglish/wwwroot/css/` and `wwwroot/js/`.
- `_AdminLayout.cshtml`, its owned inline shell styles/scripts and references to
  new Admin-only assets. This is an **explicit shared-impact grant for the Admin
  shell**, not permission to redesign the learner layout.
- `Auth.cshtml`, `account-auth-ui.css` and `account-auth-ui.js` **only for loader
  markup, styling, state/cleanup and directly necessary wiring**. A dedicated
  `auth-loader.css/js` split is allowed if useful and correctly wired. Preserve
  the accepted Account forms, validation and authentication behavior.
- Minimal compatibility/consistency corrections in SY01-SY03 views/feature CSS
  and owned partials if required by the new shell. Keep their approved workflows
  and assets; do not restart these feature redesigns or change business logic.
- Purposeful assets under `TCTEnglish/wwwroot/images/admin/` or
  `wwwroot/images/account/`, meaningful relevant tests and
  `docs/feature-audits/2026-11-10-admin-loader-ui-execution-ledger.md`.
- Accurate scoped adoption rows and bug-memory entries for actual fixes.

Read-only: controllers, services, Hubs, DTOs/ViewModels, entities/DbContext/
migrations, Program/appsettings/project files, dependencies, provider/model
configuration, shared token CSS, learner `_Layout`, `layout.css`, `site.css`,
legacy `account.css`, `Dashboard.css`, `_CatalogPager` implementation,
`user-activity.js`, the empty `_Loader.cshtml` and agent configuration.
Other teammates' Admin feature views remain read-only; inspect/smoke-test them
as shell consumers. Report a concrete compatibility blocker instead of silently
rewriting their modules. The task does not authorize backend feature additions.

**Do not commit, push, create a PR, run remote CI, merge or deploy.** Preserve
both release gates, hosting, saved keys and existing data. Do not reset/import
the database or infer production authority from successful checks.

## Shared Admin shell and consistency requirements

1. Inventory actual Admin layout consumers and before/after CSS/script order.
   Preserve every navigation destination, active-route logic, user/profile/
   logout/return-to-site links, `RenderBody`, optional sections, antiforgery,
   pinned libraries and the shared presence connection initialization.
2. Build a coherent Clay sidebar/header/footer and balanced content frame using
   existing `--tct-*` tokens, Inter and the current Admin icon families. Use
   scoped `.admin-shell` styles and theme the shell regions/owned page roots.
   Do not theme all legacy page content implicitly by adding `.tct-theme` to
   the global body or injecting generic element overrides.
3. The current Admin layout loads legacy `account.css`. Do not edit that shared
   file. Removing/replacing its Admin link requires an inventory of what every
   Admin consumer relies on and demonstrated replacement/compatibility evidence;
   do not delete it simply because the two new pages look correct without it.
4. Preserve `adminSidebar`, `sidebarToggle`, `admin-main-content`, skip-link
   behavior and the current 991.98px responsive boundary unless a documented,
   compatible UI adjustment is necessary. Desktop and short-height navigation
   must remain reachable; mobile drawer needs correct expanded/hidden/inert
   state, Escape, focus entry/return and safe outside-click handling.
5. Coordinate drawer, dropdown, Bootstrap/SweetAlert dialogs, backdrop and body
   scroll/focus ownership. Rapid open/close, resize across breakpoints or closing
   a dialog must not leave hidden focus or a stuck scroll lock. Reuse the existing
   stack; do not add a drawer/modal library or disconnect shared SignalR.
6. Compare SY01-SY03 after shell changes: headings, content width, spacing, Clay
   depth, table overflow, forms, validation, dialogs and action hierarchy. Use
   quiet depth for dense tables/editors. Record only the actual migrated scope;
   shared navigation adoption does not mean every Admin module was redesigned.

## Dashboard and user-management contracts

- Both controllers are Admin-only. Standard/Premium/anonymous access must retain
  the current challenge/denial behavior; hiding a menu never supplies access control.
- Dashboard receives real `TotalUsers`, `TotalClasses`, `TotalSets`, their new-in-
  seven-days counts and `RecentUsers` (currently up to five). Keep encoded names,
  email, role, status and dates. Use real values only: do not invent a daily series,
  financial metric, activity log or success percentage to populate a chart.
- User list GET accepts `q`, `role`, `status`, `page`; paging is server-side,
  currently 20 rows. Preserve URL/filter/pager behavior and the distinction between
  global status totals, filtered totals and the rows on the current page. Do not
  intercept server filters to search only loaded rows or derive totals from them.
- Preserve GET `GetUser` with the actual `id` route/query binding and GET
  `GetPresenceSnapshot` with selected `userIds` and current filters. Keep existing
  JSON shapes, current row IDs/data hooks, timestamps/status updates and safe DOM.
- Preserve POST `Edit` JSON fields (`UserId`, `FullName`, `Email`, `Role`),
  antiforgery and real validation/error responses. Roles remain
  `Admin`, `Premium`, `Standard`. Displayed presence is not a manually assignable
  permission or a field to overwrite when editing a profile.
- Preserve POST `BlockUser` (`UserId`, `Reason`, `Duration`) and `UnlockUser`
  (`UserId`), confirmations, duplicate guards, server error handling and self-lock
  protection. Existing duration values are `30s`, `1d`, `3d`, `7d`, `permanent`.
  Keep expiry/reason display honest. Do not invent delete/create/reset-user actions
  or alter server policy to simplify the screen.
- Preserve current `RequestVerificationToken` handling and `{ success, message,
  user/statusUpdate }` contracts as applicable. Show pending states immediately,
  restore buttons on error, keep input for correction, and never show a row/status
  mutation as saved before successful server confirmation.
- The current shared `window.userActivityHubConnection` uses `/classChatHub`.
  Preserve `UserStatusChanged` and `user-activity:connected/reconnecting/
  reconnected/closed` integration, snapshot recovery and filter-aware updates.
  No extra hub for UI convenience, duplicate listeners or count drift from replayed
  events. Disconnected/unknown does not mean every user became Offline.
- Block/unlock may send email through the server. Test with isolated accounts,
  fake email sender and disposable fixtures, not real team/user accounts or SMTP.

## Login loader behavior and design

The current loader shows after successful credential login, then immediately
navigates to the form's existing home destination. External-login buttons show
the overlay before navigating to their configured provider action. Preserve these
destinations and distinctions; this task does not change role-based routing,
cookies, credentials, OAuth callbacks, registration or password recovery.

Implement a polished TCT-branded Clay loading state with a compact illustration/
mark, clear text and restrained purposeful motion. Use the existing website
identity, not the new AI Chatbot logo as the website's identity. Native SVG/CSS
art is allowed; avoid oversized art and heavy full-screen effects.

Required lifecycle behavior:

- The overlay is hidden/inactive before it is needed and cannot intercept clicks,
  focus or page scrolling while hidden. Keep `hidden`, CSS visibility and busy
  state consistent; preserve meaningful `role=status`/live text semantics.
- Credential success may say it is opening the site; external-provider navigation
  must not claim authentication has already succeeded. Invalid credentials,
  blocked accounts, validation cancellation and failed requests restore usable
  controls/errors without leaving a success loader over the form.
- Never add an artificial minimum wait, delayed redirect or fake 0-to-100% progress
  to make the loader visible. Let fast navigation be fast. Use browser throttling
  or a safe fixture to observe it instead of slowing the production interaction.
- On slow/failed/interrupted navigation, show an honest recoverable state if the
  document remains active. Retrying navigation must not silently resubmit login
  credentials or an unknown OAuth request. Preserve the actual routing contract;
  do not fetch the destination into the auth DOM or add a synthetic readiness API.
- Handle `pageshow`/BFCache/back navigation and repeated clicks: hide/reset stale
  overlay/busy state, restore usable controls and clean timers/listeners. Preserve
  existing auth duplicate/validator guards and never wipe user-entered values as
  a cleanup side effect. Returning from OAuth cancellation must remain usable.
- Reduced motion removes spinning/pulse/slide while retaining a static visible
  status. Failure/stop/back/visibility changes cannot strand an endless animation
  or change the correctness/timing of navigation.

Read newest symptom-relevant bug memory before fixes, especially Account native
validation/pending recovery, presence/status updates and paged catalog filtering.

## Mandatory interaction and motion matrix

Record surface/control, trigger, real state source, visual response, focus/
announcement, reduced-motion result, interruption/cleanup and evidence before
implementation. Implement and exercise the applicable rows:

| Surface | Required working interaction and visual response |
| --- | --- |
| Admin shell | Tactile actions and active route; responsive drawer open/close, backdrop, keyboard focus and resize recovery; stable dropdown/skip link and preserved navigation. |
| Dashboard | Clear real KPI/recent-user states, empty content and useful navigation; brief content reveal without delaying access or counting from invented values. |
| Users | Server filters/pager; edit loading/validation/save/retry; block/unlock confirm/cancel/pending/error; real row/presence changes and honest reconnecting feedback. |
| Existing owned Admin pages | Compatible shell/spacing/controls and correct existing submitters, modal focus, return paths and pending/error states after the shared change. |
| Login loader | Visible only at the real navigation stage; honest success/provider wording; restrained pending motion, recovery and instant reduced-motion alternative. |

Use short shared timing/easing tokens, transform/opacity, Bootstrap and browser
APIs. Avoid `transition: all`, large shadow/filter animation, per-row perpetual
motion, blanket `will-change`, repeated layout reads and correctness tied to an
animation timer. Motion cannot block primary controls, delay navigation or move
focus away during typing/live presence updates. Respect CSS and JS reduced-motion
paths and preference changes; preserve state if a transition is interrupted.

## Ordered execution and assets

1. Record branch/baseline/workloads, route/role/state/consumer coverage and current
   tests. Observe the real Admin pages and real Auth loader before choosing a
   design. Keep the empty global loader distinct from the login overlay.
2. Research primary guidance for accessible Admin tables/forms/navigation,
   operational dashboards and navigation-loading feedback. Use installed design
   lookups for concrete questions; record links/dates/adopt-adapt-reject decisions.
   External references cannot change brand, stack, ownership or authority.
3. Choose and implement the shared Admin visual solution and AD01/AD02, preserving
   the contracts above. Keep dense data readable, primary actions clear and
   sidebar/main spacing balanced at laptop height. No new UI framework or chart
   package; use native presentation of data actually available.
4. Implement LO01's visual design and lifecycle, then consistency corrections in
   SY01-SY03. Verify shared consumers and Auth regressions before treating either
   shared boundary as complete.
5. Create purposeful local images/icons only if useful. Native image generation
   is allowed with available tools; otherwise use existing icons/native SVG/CSS.
   Keep shared brand values, dimensions, alt/decorative handling, compression and
   provenance/tool/license notes. No scripts/external SVG content, private inputs,
   hotlinks, new font/icon/CDN or heavy 3D library. Existing approved brand values
   may be baked into art; feature CSS still consumes shared tokens.
6. Verify actual behavior and motion, add meaningful actual-script/render
   regressions and fix in-scope failures. Update the ledger and scoped adoption
   evidence. Do not declare other teammates' modules redesigned after a shell pass.

Use scoped CSS, `.tct-theme` on owned regions/pages, `--tct-*`, Bootstrap and shared
header/state partials where appropriate. Keep one h1 per page, encoded content,
visible focus and adequate contrast; avoid private root palettes/global selectors.
Do not edit the shared token foundation without a documented scope proposal.

## Local environment and verification

Use `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser` for local testing.
Preserve busy listeners; select a free port with
`-Configuration Release -WebPort <free-port>` or restart only an owned, authorized
instance. Verify `/health/ready` and metadata-only `scripts/speech-status.ps1`.
Reuse saved keys without printing/rewriting them. No Whisper or paid request
during ordinary startup; old server output is not proof of the changed build.

Use private synthetic Standard/Premium/Admin accounts, disposable fixtures and
fake email/AI providers for mutations. Do not edit roles/block real admins, send
real email, publish/delete content, run paid AI/index maintenance or reset/import
the Docker DB to capture UI. No migration or empty shared-data export is needed
for this UI-only work. Keep screenshots/logs/fixtures under ignored
`.tmp/admin-loader-ui/` or `.appdata/local/`, without credentials/private data.

Acceptance:

- [ ] AD01, AD02, AS01, LO01 and SY01-SY03 have actual files/states/check evidence
  or explicit blockers. The preserved unrelated work and read-only scope remain
  intact; no backend/schema/configuration/provider change.
- [ ] Laptop first: actual demo viewport if known, otherwise 1440x900 and 1366x768;
  then 768 and 375/390 widths, short height, long names/emails/reasons and actual
  200% zoom where supported. Label approximations. No clipped modal actions,
  offscreen required fields, double main/sidebar offsets or whole-page overflow.
- [ ] Verify keyboard, focus/contrast, skip link, active routes, drawer/dropdown/
  dialog interactions, Escape/focus return and body-scroll recovery. Observe
  normal and reduced motion; screenshots/CSS string assertions alone are not proof.
- [ ] Exercise server user filters beyond the first 20 rows, empty results, edit
  validation/duplicate email, block durations/reason/self-lock error, unlock and
  network failure with safe fixtures. Preserve input, real totals and single mutation.
- [ ] Exercise presence updates/replay/reconnect and a snapshot arriving after a
  filter/modal change. No wrong-account update, duplicate status count, stale
  filter overwrite or unconfirmed online/offline claim.
- [ ] Exercise loader credential success/error/blocked, external-provider redirect,
  cancelled validation, slow/failed navigation, rapid activation, back/BFCache,
  reduced motion and missing/deferred assets. Verify immediate redirect without
  artificial delay and no stuck overlay or credential resubmission.
- [ ] Check every owned Speaking/Writing/AI Admin page after shell changes,
  including create/edit/variant and maintenance dialogs. Read-only smoke-test
  representative Class, Set, Listening, Reading, Notification, Message and Billing
  Admin routes to assess shared CSS/script compatibility; record missing access/
  data. No content/finance/SMTP/AI maintenance mutations for screenshots.
- [ ] Run `dotnet build TCTEnglish/TCTEnglish.csproj -c Release` and focused
  current AccountUiRender, AccountCorrectness, AdminCatalogPaging, shared UI and
  discovered user/presence/shell tests; include meaningful new coverage where
  actual behavior changes. Preserve selected-role access denial.
- [ ] Run `scripts/tests/test_account_auth_ui.cjs`, `test_account_ui.cjs` and
  relevant existing Admin/feature Node regressions. Add tests that invoke the
  real production scripts for loader recovery and changed Admin interactions,
  not a copied simulation or class-name-only assertions.
- [ ] Run the normal full application suite once after focused checks pass.
  Never enable paid/live/provider acceptance flags implicitly. Report skipped/
  blocked checks and distinguish fixtures, HTTP render and native browser evidence.
- [ ] Inspect final scope, `git diff --check`, assets and sensitive data. Tooling
  success is not application/browser/RBAC/provider or deployment acceptance.

## Vietnamese handoff

Report the chosen design, all covered Admin/shell/loader states, changed files,
shared consumers, research/assets and exact commands/results. Provide a short
demo checklist covering Dashboard, users, responsive shell and successful/failed/
back navigation with the loader, including normal/reduced-motion observations.
Give the local URL, remaining blockers and unobserved roles/states. Distinguish
live app, fixtures and local/CI/hosting; leave implementation uncommitted for Chau.
