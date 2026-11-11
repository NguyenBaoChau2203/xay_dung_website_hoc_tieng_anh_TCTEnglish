# Account UI — Execution Ledger (Stage I)

## Owner closeout and local commit checkpoint — 2026-11-08

Chau confirmed Account was satisfactory and requested committing completed work
before moving to Goals. The accepted local Account/Clay changes were reviewed for
scope, existing form/JS/security contracts and secret-like content. The dedicated
decorative SVG has no scripts/event handlers or external references. No controller,
model/schema, protected config, key or database change is included.

Fresh closeout verification: Release build/test completed with **1177 passed,
22 skipped, 0 failed** (1199 tests,3m52s); complete Node scripts **90/90 passed**.
Tooling validator and staged/diff checks passed during publication preparation.
Earlier entries below retain the observations and limitations of their own runs;
this checkpoint does not turn old blocked browser states into new acceptance.

Owner authorization covers local commits here. No push, PR, remote CI, merge,
staging or production release was requested. The separate BTH3 planning document
is unrelated and remains untouched outside these commits. Goals gets its own
branch/prompt after the Account/shared-foundation checkpoint.

Date: 2026-11-08 (Asia/Bangkok)
Mode: `IMPLEMENT_LOCAL`
Branch: `feature/chau-ui-design-system`
Baseline HEAD: `eed4e2c95c79ef708dc9af77c8b9326186835122`
Approval: Châu approved arrangement B in chat on 2026-11-08; activated via `.ai/prompts/gemini38-flash-high-account-ui-implementation.md`.
Pre-existing dirty file:
- `DESIGN.md` (SHA256: `E904054E057CB06329FA3E867B1E65264993C1DD4C7653338172BF064F0C9945`) — preserved.

---

## 1. Baseline Verification (B0)

- **Test command**:
  `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"`
- **Result**: 20/20 passed (duration: 12 s). Existing analyzer/nullability warnings recorded, no failures.
- **Environment state**:
  - Docker daemon (named pipe `//./pipe/dockerDesktopLinuxEngine`) is currently stopped/unavailable.
  - Chrome remote debugging port 9222 is closed.
  - Test harness: `TestWebApplicationFactory` (SQLite in-memory) and Node `test_account_*` test runners operate reliably without Docker or live Chrome dependencies.
- **Preserved boundaries**:
  - Read-only files: `_Layout.cshtml`, `_AdminLayout.cshtml`, `tct-learning-ui.css`, `layout.css`, `site.css`, `account.css`, `auth-stitch.css`, controllers, ViewModels, services, models, migrations, Program.cs, project files.
  - ViewModels & routes: `Auth.cshtml` remains `dynamic`; recovery views use typed ViewModels; Profile uses `UpdateProfileViewModel`; Settings uses `SecuritySettingsViewModel`; AccessDenied remains HTTP 403.
  - No database migration or seed alteration.

---

## 2. Batches and Implementation Progress

| Batch | Description | Files | Status | Evidence / Notes |
|---|---|---|---|---|
| **B0** | Baseline & Reconciliation | Execution ledger created; git status & hashes recorded; baseline tests passed 20/20 | DONE | Baseline tests: 20 passed, 0 failed |
| **B1** | AU01 Auth & Assets | `Views/Account/Auth.cshtml`, `wwwroot/css/account-auth-ui.css`, `wwwroot/js/account-auth-ui.js`, `scripts/tests/test_account_auth_ui.cjs`, `TCTEnglish.Tests/AccountUiRenderTests.cs` | DONE | AU01 render test passed; Node auth unit tests 6/6 passed |
| **B2** | AU02–AU04 Recovery | `ForgotPassword.cshtml`, `ForgotPasswordConfirmation.cshtml`, `ResetPassword.cshtml`, auth CSS/JS | DONE | AU02 & AU04 render tests passed; Node mismatch/pending tests passed |
| **B3** | AU05 Profile & Nav | `Profile.cshtml`, `wwwroot/css/account-ui.css`, `wwwroot/js/account-ui.js`, `scripts/tests/test_account_ui.cjs` | DONE | AU05 render test passed; ProfileBadgeReadTests passed; Node 4/4 passed |
| **B4** | AU06–AU07 Settings & Denied | `Settings.cshtml`, `AccessDenied.cshtml`, account CSS/JS | DONE | AU06 & AU07 render tests passed (2/2) |
| **B5** | Final Verification & Closeout | Full build, suite run, adoption row updates, Vietnamese handoff | DONE | 26/26 full regression passed; 10/10 node tests passed; adoption rows updated |

---

## 3. Evidence Log & Observed vs Blocked States

- **B0 Baseline**:
  - Observed: `dotnet test` with 3 filtered test classes -> 20/20 passed.
  - Blocked: Live browser inspection with DevTools MCP port 9222 (connection refused); Docker MariaDB local service (daemon inactive).
  - Mitigation: Node unit behavior tests (`test_account_auth_ui.cjs`, `test_account_ui.cjs`) and xUnit Razor rendering tests (`AccountUiRenderTests.cs`) provide reproducible, automated proof of contract integrity, event handling, focus/ARIA behavior, and rendering safety.

- **B1 (AU01 Auth)**:
  - Views: `TCTEnglish/Views/Account/Auth.cshtml` implemented with Arrangement B (split layout: hero/brand on left, tabbed auth card on right).
  - Semantic forms: `#form-login` posts `email` and `password`; `#form-register` posts `email`, `fullName` and `password`, with antiforgery tokens on both forms. The required `#terms` checkbox has no `name` and is not posted. There is no `RememberMe` login binding or `ConfirmPassword` / `AcceptTerms` registration binding.
  - JS: `TCTEnglish/wwwroot/js/account-auth-ui.js` manages tab state, roving tabindex, `aria-selected`, password reveal toggles, and double-submit prevention.
  - Tests: `AccountUiRenderTests.AU01_Auth_RendersSemanticForms_Antiforgery_AndOneH1` passed; `test_account_auth_ui.cjs` 6/6 passed.

- **B2 (AU02–AU04 Recovery)**:
  - `ForgotPassword.cshtml`: Clean standalone card layout, single `<h1>`, form `#form-forgot-password`, submit button `#btn-forgot-submit`, retains antiforgery token and email model binding.
  - `ForgotPasswordConfirmation.cshtml`: Generic success message with security note; no sensitive email/token reflected in output.
  - `ResetPassword.cshtml`: Retains hidden inputs `Token` and `Email`, password mismatch client warning, password reveal toggles, and server validation summary.
  - Tests: `AccountUiRenderTests.AU02_ForgotPassword_PreservesHtmlHelper_AndFormBinding` and `AccountUiRenderTests.AU04_ResetPassword_RetainsHiddenTokens_AndValidationSummary` passed.

- **B3 (AU05 Profile & Navigation)**:
  - `Profile.cshtml`: In-shell layout wrapped in `.tct-theme.tct-page.account-ui`, single `<h1>` via `_PageHeader`, tabs for Profile, Security settings, and Subscription (`?tab=` deep-linking).
  - Identity & avatar: `#avatarClickTarget`, `#Avatar` file input, `#avatarPreview`, `#avatarFallback` user initial, client-side MIME/size validation (2MB limit).
  - Badges: `Model.EarnedBadges` rendered, empty state uses `_StateMessage` partial (`StateMessageViewModel` with `ActionLabel`).
  - Navigation: `#accountSidebar`, desktop collapse toggle (`#accountSidebarToggle`), mobile drawer toggle (`#sidebarMobileToggle`), backdrop overlay (`#sidebarOverlay`).
  - Tests: `AccountUiRenderTests.AU05_Profile_RendersIdentity_Badges_AndAccountNav` passed; `test_account_ui.cjs` 4/4 passed.

- **B4 (AU06 Settings & AU07 AccessDenied)**:
  - `Settings.cshtml`: Single `<h1>` via `_PageHeader`, section navigation buttons (`[data-section]`), security email update form (`#settingsEmailInput`), password change form (`CurrentPassword`, `NewPassword`, `ConfirmPassword` with 6-character hint copy and toggles), linked accounts for Google & Facebook with distinct badges, notifications fieldset marked disabled (`<fieldset disabled>`) with "Chưa hỗ trợ" badge and unchecked inputs, danger zone delete modal (`#deleteAccountModal`, `#deleteConfirmInput`, `#deleteAccountBtn` with `CONFIRM` gate).
  - `AccessDenied.cshtml`: In-shell layout wrapped in `.tct-theme.tct-page.account-ui`, single `<h1>` "Bạn không có quyền truy cập trang này", lock icon, 403 explanation, and safe navigation buttons to `/Home/Index` and `/Account/Profile`.
  - Tests: `AccountUiRenderTests.AU06_Settings_RendersSections_AndDisabledNotificationCheckboxes` and `AccountUiRenderTests.AU07_AccessDenied_Renders403Explanation_AndSafeNavigation` passed.

- **B5 (Initial Verification & Adoption Tracker)**:
  - Focused regression suite:
    `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"`
    Result: 26/26 passed (0 failures).
  - Node behavioral tests:
    `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs`
    Result: 10/10 passed (0 failures).
  - Adoption tracker:
    Updated rows in `docs/design-system/team-ui-workflow.md` for Account and Auth views.

---

## 4. Stage II: Review Repair (R1–R8)

- **Repair Plan**: `docs/superpowers/plans/2026-11-08-account-ui-review-repair.md`
- **Findings Repaired**:
  - **R1 (P1)**: Removed duplicate inline `onsubmit` from `ForgotPassword.cshtml` and `ResetPassword.cshtml`. Added `e._authHandled` protection in `account-auth-ui.js` so the first valid submit executes natively and concurrent duplicate clicks are blocked.
  - **R2 (P1)**: Implemented `cleanupDesktopState()` in `account-ui.js` to wait for offcanvas hidden event before disposal, remove inline `overflow: hidden` and `padding-right` from `document.body`, clear backdrops, and remove dialog/aria-modal attributes upon mobile-to-desktop resize.
  - **R3 (P2)**: Moved `#deleteAccountModal` inside the `.account-ui` container in `Settings.cshtml`. Replaced unconditional `pageshow` form re-enabling with form-aware gate requiring `deleteConfirmInput.value.trim() === 'CONFIRM'`.
  - **R4 (P2)**: Captured `initialAvatarSrc` and `initialHasAvatar` in `account-ui.js`. Cancel restores the original saved image and triggers `window.location.reload()` instead of reverting to generic initials.
  - **R5 (P2)**: Replaced `.text-warning` with `.text-unconfirmed-warning` consuming `--tct-warning-text` (#92400e, 7.4:1 contrast ratio) and `.tct-badge.tct-badge--warning` in `Settings.cshtml` and `account-ui.css`.
  - **R6 (P2)**: Replaced invalid `box-shadow: var(--tct-focus-ring); outline: none;` with visible `outline: var(--tct-focus-ring-width) solid var(--tct-focus-ring); outline-offset: var(--tct-focus-ring-offset); box-shadow: none;` in `account-auth-ui.css` and `account-ui.css`. Replaced undefined `--tct-shadow-sm` with `--tct-shadow-soft`. Enforced touch target min-height 44px with higher specificity for `.account-auth-ui .btn:not(.btn-sm)`. Scoped modal styling strictly under `.account-ui`.
  - **R7 (P2)**: Added `@if (ViewData.ModelState["Token"]?.Errors.Count > 0)` alert in `ResetPassword.cshtml` rendering a direct link to `Url.Action("ForgotPassword", "Account")`.
  - **R8 (P2)**: Corrected synthetic field claims in ledger, removed fictitious `/Account/Auth` from adoption tracker, updated status to "Đã chuyển code (sửa theo review)", and expanded test suites.
- **Verification Results**:
  - Node behavioral test suites: `test_account_auth_ui.cjs` (8/8) + `test_account_ui.cjs` (7/7) = 15/15 passed.
  - Focused render tests: `AccountUiRenderTests` (7/7 passed).
  - Focused regression suite: 27/27 passed.
  - Full normal test suite: 1176 passed / 22 skipped / 0 failed (duration: 4m 38s).
- **Environment & Boundary Status**:
  - Docker MariaDB daemon was unavailable during test runs; all tests ran via `TestWebApplicationFactory` SQLite in-memory and Node mocks without database alterations.
  - Speech service remained in metadata-only configuration.
  - Pre-existing modified `DESIGN.md` (SHA256: `E904054E057CB06329FA3E867B1E65264993C1DD4C7653338172BF064F0C9945`) preserved untouched.

---

## 5. Stage III: Claymorphism Visual Alignment (Owner Visual Update)

- **Prompt Reference**: `.ai/prompts/adopt-claymorphism-for-current-ui-task.md`
- **Visual Direction**: Owner-mandated Claymorphism (`tct-learning-ui.css`) across all redesigned Account screens (AU01–AU07), prioritizing laptop/desktop classroom demonstration quality.
- **Alignments & Deliverables**:
  - **AU01 (Auth & Recovery)**:
    - Replaced competing visual styles with the shared Claymorphism foundation inside `.tct-theme.account-auth-ui`.
    - Desktop/laptop split layout: Left brand story panel (`.auth-story`) is a raised Clay panel (`box-shadow: var(--tct-clay-shadow); border-radius: var(--tct-radius-xl)`) featuring the custom vector illustration (`auth-illustration.svg`), 3 quiet Clay feature pills (`500+ Từ vựng`, `AI Speaking`, `Smart Quiz`), and brand copy.
    - Right auth card (`.auth-card`) uses raised Clay depth, tactile tab buttons with Clay button/pressed shadows, shallow form input depth (`var(--tct-clay-shadow-field)`), and raised primary action buttons (`--tct-clay-shadow-button`).
    - Scaled responsively so that on a 768px-height laptop screen, the entire form and CTA remain comfortably reachable without scrolling.
  - **AU02–AU04 (Recovery Views)**:
    - Standalone recovery cards use raised Clay surfaces (`var(--tct-clay-shadow)`), radius 28px (`var(--tct-radius-xl)`), and quiet alert highlights.
  - **AU05–AU07 (Profile, Settings, AccessDenied)**:
    - Navigation sidebar, identity avatar, panels, and danger zones consume raised Clay depth (`var(--tct-clay-shadow)`) and active nav items receive tactile Clay button depth.
    - Modal confirmation dialogs consume `--tct-clay-shadow-overlay` and radius 28px.
  - **AU01 Layout & Illustration Refinement (User Directives: Original Split Layout + Claymorphism)**:
    - Restored the full-bleed split container layout (`.auth-split-container`) from the original design:
      1. **Left Hero Column (`.auth-col-left`)**:
         - Soft pastel Clay gradient canvas (`linear-gradient(155deg, #eef2ff 0%, #e0e7ff 50%, #f0f3ff 100%)`) with ambient glow blobs and subtle dot-grid overlay.
         - Floating soft clay shapes (`.auth-shape`) with quiet clay depth and soft blur.
         - Brand logo with mortarboard icon inside raised Clay primary button depth (`box-shadow: var(--tct-clay-shadow-button)`).
         - Bold brand typography: "TCT ENGLISH" and educational mission subtitle.
         - Custom vector illustration (`auth-illustration.svg`) embedded seamlessly in the hero area with gentle drop-shadow.
         - 3 Clay feature cards (`500+ Từ vựng`, `AI Speaking`, `Smart Quiz`) with quiet clay shadows (`var(--tct-clay-shadow-quiet)`), 16px radius, and hover lift.
      2. **Right Form Column (`.auth-col-right`)**:
         - Centered raised Clay card (`.auth-card`) consuming `var(--tct-clay-shadow)` and 28px radius (`var(--tct-radius-xl)`).
         - Clay close/back button (`.auth-close-btn`) with tactile button depth.
         - Animated sliding segmented indicator (`.auth-tab-slider`): smooth Claymorphic white pill gliding between "Đăng nhập" and "Đăng ký" with `cubic-bezier(0.4, 0, 0.2, 1)` (inspired by the Neumorphic sliding overlay concept).
         - Smooth cross-fade and directional horizontal slide for form switching (`#form-login` and `#form-register`), eliminating jarring instant display toggles.
         - Heading/description smooth micro-morphing animation (`.auth-header-animate`).
         - Social buttons with Clay depth (`var(--tct-clay-shadow-button)`).
         - Shallow form fields (`var(--tct-clay-shadow-field)`) with visible focus ring outline and eye password reveal toggle.
         - Primary submit button with tactile pressed depth.
         - Responsive adaptivity: full-bleed split on desktop/laptop; gracefully stacked on tablet/mobile.
         - Full `prefers-reduced-motion` compliance.
- **Verification Evidence**:
  - `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs`: 23/23 passed.
  - `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests"`: 7/7 passed.
  - `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"`: 27/27 passed.
  - Whitespace & format: `git diff --check` passed with 0 errors.
