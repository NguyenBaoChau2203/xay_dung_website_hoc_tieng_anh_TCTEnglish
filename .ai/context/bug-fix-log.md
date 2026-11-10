# TCT English — Bug Fix Log

After every bug fix, the agent **must append one entry** to this file.
This is a historical record of actual fixes — not a list of pending issues (see `known-issues.md`).

---

## How Agents Should Use This File

1. Read this file before fixing any bug.
2. Start from the newest entries and search for matching symptoms, stack traces, entities, controllers, or regression patterns.
3. Reuse a previous fix pattern only after confirming the same root cause exists in the current code.
4. If a previous entry influenced the solution, mention that relationship in `Notes`.
5. After the fix is verified, append one new entry below the marker in the "Fix History" section.

---

## Entry Format

```
### [BUG-ID or short description] — [fix date]

**Symptom**: What the user observed (HTTP code, broken UI, wrong data...)

**Root Cause**: Why it happened — which layer, file, or logic was wrong

**Solution**: What was changed and where — be specific

**Files Changed**:
- `path/to/file.cs` — line X: brief explanation

**Verification**: How the fix was confirmed (manual steps, automated test, regression check)

**Commit**: `fix(scope): message` — hash if available

**Notes**: Regression warnings, edge cases to watch, or related previous fixes (if any)
```

---

## Fix History

<!-- Agent: append new entries BELOW this line, newest first -->

### Account full-bleed light blue canvas background polish — 2026-11-08

**Symptom**: On the Account pages, a visible white vertical stripe/gutter appeared along the left edge and around the view because the light blue clay canvas background (`#f0f3ff`) was contained only inside `.account-ui`, while the parent `<main class="main">` had a 40px padding and a grayish-white `#f8fafc` background.

**Root Cause**: `<main class="main">` had `background: #f8fafc` and `padding: 40px`, causing the light blue background of `.account-ui` to render as an inner box surrounded by a 40px white border.

**Solution**:
- Added `.main:has(.account-ui), body:has(.account-ui) { background-color: var(--tct-clay-canvas, #f0f3ff) !important; }` so the entire screen background renders in the cohesive light blue clay canvas tint.
- Added negative margins on `.account-ui` (`margin: calc(-1 * var(--tct-main-pad, 40px)); padding: var(--tct-main-pad, 40px);` on desktop and `16px` on mobile) to bleed the light blue canvas 100% edge-to-edge across all viewports.
- Set `.account-ui .account-surface` to `max-width: 100%; width: 100%;` so the layout comfortably fills the full window.

**Files Changed**:
- `TCTEnglish/wwwroot/css/account-ui.css` — added full-bleed canvas expansion rules and negative margin cancellation

**Verification**:
- `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs` -> 23/23 tests passed.
- `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~AccountUiRenderTests"` -> 8/8 tests passed.
- `git diff --check` -> 0 errors.

**Commit**: Uncommitted local change.

### Account views layout upgrade — full-width responsive dashboard & spacious Claymorphism — 2026-11-08

**Symptom**: The Account views (Profile AU05 and Settings AU06) felt cramped and pushed into a corner with massive empty gutters on laptop/desktop screens; forms inside panels were artificially squeezed into a 550px (68ch) left-aligned column.

**Root Cause**:
- `.tct-page` and `.account-surface` were both hard-capped at `--tct-container-max` (1280px), creating hundreds of pixels of wasted margin on screens $\ge 1366\text{px}$.
- `.account-surface` had redundant `padding: var(--tct-main-pad)` (40px) inside `.main` which already applied 40px padding, causing 80px double inset padding on all edges.
- `.security-form` was restricted to `max-width: var(--tct-measure)` (68ch = 550px), leaving more than half of each settings card completely blank on the right.
- Three password fields were squeezed into 550px, giving each field only ~150px.

**Solution**:
- Overrode `.tct-page.account-ui` to `width: 100%; max-width: 100%;`.
- Updated `.account-surface` to `max-width: 1600px; width: 100%; margin: 0 auto; padding: 0;` using a balanced `16rem minmax(0, 1fr)` grid with sticky sidebar.
- Removed the 68ch clamp on `.security-form`, letting forms breathe across 100% of the content card.
- In Settings, expanded `.password-fields` to 3 well-proportioned columns (~280px–360px each) with clean responsive collapse on mobile/tablet.
- Upgraded `.account-sidebar`, `.identity`, and `.panel` to the Claymorphism design system with refined padding, border-radius, and clay shadows.

**Files Changed**:
- `TCTEnglish/wwwroot/css/account-ui.css` — expanded container widths, updated sidebar/panel styles and form field layouts

**Verification**:
- `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs` -> 23/23 tests passed.
- `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~AccountUiRenderTests"` -> 8/8 tests passed.
- `git diff --check` -> 0 errors.

**Commit**: Uncommitted local change.

### Account reset password UI polish — hide empty validation summary on initial load — 2026-11-08

**Symptom**: On the ResetPassword screen (AU04), an empty pink box with a red outline was rendered between the header description and the first password input on initial load before any submission.

**Root Cause**: In ASP.NET Core MVC, `@Html.ValidationSummary(excludePropertyErrors: false, ...)` outputs `<div class="alert alert-danger validation-summary-valid" ...>` even when `ModelState.IsValid` is true. Because `account-auth-ui.css` styled `.alert` and `.alert-danger` with padding and borders, but lacked the standard `.validation-summary-valid { display: none !important; }` rule, the empty container was visibly rendered.

**Solution**:
- Added `.account-auth-ui .validation-summary-valid, .account-auth-ui .alert.validation-summary-valid { display: none !important; }` to hide the container when there are no errors.
- Added explicit styling for `.account-auth-ui .validation-summary-errors` so when real validation errors occur upon submission, it displays properly.
- Updated brand icon from `fa-key` to `fa-graduation-cap` in `ResetPassword.cshtml` to maintain uniform brand identity with AU01, AU02, and AU03.

**Files Changed**:
- `TCTEnglish/wwwroot/css/account-auth-ui.css` — added `.validation-summary-valid` and `.validation-summary-errors` rules
- `TCTEnglish/Views/Account/ResetPassword.cshtml` — updated brand icon to `fa-graduation-cap`

**Verification**:
- `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs` -> 23/23 tests passed.
- `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~AccountUiRenderTests"` -> 8/8 tests passed.
- Verified live HTML: `validation-summary-valid` is generated and hidden via CSS; empty box no longer appears.

**Commit**: Uncommitted local change.

### Account recovery UI polish — orphan word wrapping and vertical rhythm on ForgotPasswordConfirmation — 2026-11-08

**Symptom**: On the ForgotPasswordConfirmation screen (AU03), the note "Kiểm tra cả thư mục thư rác. Liên kết khôi phục có thời hạn." broke with an orphan word "hạn." on a second line; the brand logo and the recovery envelope icon rendered inline on the same row due to `text-center` with `inline-flex`.

**Root Cause**: `.recovery-card` had `max-width: 28rem` (448px) with 32px padding leaving only 384px content width, causing the 59-character note to wrap awkwardly; `.brand` and `.recovery-icon` were both `inline-flex` without a block break container; the primary CTA button had `mb-2` adding redundant spacing to `.back-link`.

**Solution**:
- Updated `.recovery-card` to `max-width: 30rem` (480px, conforming with `account-ui-design.md` spec `<=30rem`).
- Wrapped brand in `.recovery-brand-wrap` (block flex container centered) and converted `.recovery-icon` to block flex (`display: flex; margin: 0 auto var(--tct-space-md);`) to ensure brand and icon appear on separate, centered rows.
- Added `.recovery-hint` with `white-space: nowrap` for viewports $\ge 460\text{px}$ so the hint stays strictly on 1 single line on desktop/laptop.
- Removed `mb-2` from the primary button and added icon with `me-2` to `.back-link` for consistent 20px spacing rhythm and hover micro-interaction.

**Files Changed**:
- `TCTEnglish/wwwroot/css/account-auth-ui.css` — added `.recovery-brand-wrap`, `.recovery-hint`, updated `.recovery-card` and `.recovery-icon`
- `TCTEnglish/Views/Account/ForgotPasswordConfirmation.cshtml` — updated markup structure and classes
- `TCTEnglish.Tests/AccountUiRenderTests.cs` — added `AU03_ForgotPasswordConfirmation_RendersSemanticMarkup_AndOneH1` test

**Verification**:
- `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs` -> 23/23 tests passed.
- `dotnet test TCTEnglish.Tests -c Release --filter "FullyQualifiedName~AccountUiRenderTests"` -> 8/8 tests passed.
- `git diff --check` -> 0 errors.

**Commit**: Uncommitted local change.

### Account functional follow-up — responsive lifecycle and cancelled submits — 2026-11-08

**Scope / authority**: The owner authorized backend and functional logic fixes after Gemini's Account repair. UI/UX remains with the owner and Gemini. One agent worked locally on `feature/chau-ui-design-system`; no CSS, Razor, C# controller, schema, protected configuration or real user data was changed in this follow-up.

**Symptoms and evidence**:
- Opening the Settings delete modal at 390px and resizing to 1440px left the modal visible but cleared `body.style.overflow` and `modal-open`.
- Opening the Account drawer, resizing mobile → desktop → mobile during hide, then reopening produced a drawer without a Bootstrap instance, dialog semantics or scroll lock.
- A cancelled submit event still set `_inFlight` and disabled its button, although no request started. This was reproduced using synthetic cancellation listeners on actual rendered recovery forms; tests also cover cancellation later in propagation.

**Root cause**: Desktop cleanup removed global body styles/backdrops and restored inert state it did not own. A stale `hidden` callback and timeout cleaned desktop state without respecting the latest breakpoint or Bootstrap transitions. Submit guards did not inspect cancellation before or after propagation.

**Fix**:
- Let Bootstrap restore its own scroll state before `hidden.bs.offcanvas`; dispose only after the current transition completes and the current breakpoint is desktop.
- Defer Account close requests during opening, keep the mobile instance through rapid breakpoint changes, and restore only shell inert values captured by this drawer.
- Respect pre-cancelled submits. Native submit guards restore pending state in a microtask if a later listener cancels; preserve duplicate-request blocking and the `CONFIRM` gate. AJAX login respects prior cancellation.
- Correct the execution ledger's binding description to the actual lower-case `email` / `password` / `fullName` contract; there is no posted `RememberMe`, registration `ConfirmPassword` or `AcceptTerms`.

**Regression evidence**: New tests failed before the production fix (16 passed / 7 failed), then all 23 Account Node tests passed. Focused .NET tests: 27 passed. Build: 0 warnings / 0 errors. Chrome: 13 functional cases passed using actual Razor and Bootstrap through a GET-only SQLite-memory fixture; all browser POSTs were intercepted. Synthetic pageshow is not a real BFCache round-trip. Full comparison suite: 1176 passed / 22 skipped / 0 failed. The first full run had one unrelated RAG deadline assertion failure; it passed in isolation and in the full comparison run without RAG source edits. Both runs and the unresolved cause of the initial failure are recorded in `docs/feature-audits/2026-11-08-account-logic-verification.md`.

**Files**: `TCTEnglish/wwwroot/js/account-ui.js`, `TCTEnglish/wwwroot/js/account-auth-ui.js`, their two `scripts/tests/test_account_*.cjs` regression files, the Account execution ledger and the follow-up plan/report. Existing visual work and `DESIGN.md` are preserved.

**Commit**: uncommitted; no push, remote CI, database reset or deployment.

### Account UI review repair R1–R8 — 2026-11-08

**Symptom**: Review reproduced 2 P1 regressions and 5 P2 issues across Account UI implementation:
1. R1 (P1): ForgotPassword and ResetPassword forms failed to submit on first attempt because duplicate submit ownership (inline `onsubmit` plus JS event listener) marked the request as in-flight and called `e.preventDefault()`.
2. R2 (P1): Disposing an open mobile Offcanvas during breakpoint resize to desktop left `document.body` scroll-locked (`overflow: hidden; padding-right`), backdrop lingering, and dialog/aria-modal attributes stuck.
3. R3 (P2): `pageshow` listener indiscriminately enabled submit buttons on all forms, enabling the Delete Account button without requiring the confirmation text.
4. R4 (P2): Clicking Cancel on Profile with a pre-existing saved avatar hid the image and defaulted to generic initials instead of restoring the saved avatar.
5. R5 (P2): Unconfirmed email badge and text used low-contrast `.text-warning` on light background.
6. R6 (P2): Focus outline was suppressed on auth/account controls because `box-shadow: var(--tct-focus-ring); outline: none;` passed a hex color to `box-shadow` (invalid CSS); undefined token `--tct-shadow-sm` was used; `.tct-theme .btn` 40px height overrode `.account-auth-ui :where(button, .btn)` 44px min-height due to 0 specificity.
7. R7 (P2): Invalid or expired ResetPassword token displayed a generic error with no actionable navigation to request a new recovery link.
8. R8 (P2): Execution ledger and tracker contained inaccurate field names (`loginForm`, `UsernameOrEmail`, `AcceptTerms`) and fictitious route `/Account/Auth`.

**Root Cause**:
- R1: Both inline `onsubmit="handle*Submit(event)"` in Razor and `form.addEventListener('submit', ...)` were attached. The inline handler marked `_inFlight = true` and disabled the button, so the subsequent event listener aborted native submission.
- R2: Bootstrap Offcanvas disposal (`offcanvas.dispose()`) without awaiting hide completion leaves inline styles on `document.body` and fails to clean up backdrop/inert elements.
- R3: A global `pageshow` handler iterating over forms enabled all submit buttons unconditionally.
- R4: `setAvatarState('default')` assumed empty state instead of inspecting whether an existing avatar URL was loaded.
- R5: Reliance on Bootstrap default `.text-warning` rather than design system semantic token `--tct-warning-text`.
- R6: Misconception that `--tct-focus-ring` was a box-shadow declaration rather than a raw color token (`#4255ff`), CSS `:where()` having 0 specificity, and typo referencing non-existent `--tct-shadow-sm`.
- R7: Razor view did not inspect `ViewData.ModelState["Token"]` to render contextual recovery guidance with a direct link to `ForgotPassword`.
- R8: Discrepancy between early design draft names and actual MVC/ViewModel contract names.

**Solution**:
- Removed inline `onsubmit` bindings from `ForgotPassword.cshtml` and `ResetPassword.cshtml`; guarded `handle*Submit` in `account-auth-ui.js` against duplicate execution during the same event cycle.
- Added `cleanupDesktopState()` in `account-ui.js` to wait for offcanvas hidden event before disposal, clear inline body overflow/padding, remove lingering backdrops, and reset dialog/inert attributes.
- Moved `#deleteAccountModal` inside `.account-ui` in `Settings.cshtml`; updated `pageshow` and form handlers to verify `deleteConfirmInput.value.trim() === 'CONFIRM'`.
- Saved `initialAvatarSrc` and `initialHasAvatar` at initialization in `account-ui.js`, restoring image display on Cancel and calling `window.location.reload()`.
- Replaced `.text-warning` with `.text-unconfirmed-warning` consuming `--tct-warning-text` (#92400e, 7.4:1 contrast ratio) and `.tct-badge.tct-badge--warning`.
- Updated focus rules to use `outline: var(--tct-focus-ring-width) solid var(--tct-focus-ring); outline-offset: var(--tct-focus-ring-offset); box-shadow: none;`; replaced `--tct-shadow-sm` with `--tct-shadow-soft`; increased selector specificity for `.account-auth-ui .btn:not(.btn-sm)`.
- Added `@if (ViewData.ModelState["Token"]?.Errors.Count > 0)` in `ResetPassword.cshtml` rendering a direct link to `Url.Action("ForgotPassword", "Account")`.
- Corrected field names and routes in `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md` and `docs/design-system/team-ui-workflow.md`.

**Files Changed**:
- `TCTEnglish/Views/Account/ForgotPassword.cshtml` — removed inline `onsubmit`.
- `TCTEnglish/Views/Account/ResetPassword.cshtml` — removed inline `onsubmit`; added actionable recovery link when Token is invalid/expired.
- `TCTEnglish/Views/Account/Settings.cshtml` — moved delete modal inside `.account-ui`; replaced warning badge/text with high-contrast tokens.
- `TCTEnglish/wwwroot/js/account-auth-ui.js` — guarded first native submit and blocked duplicate in-flight requests.
- `TCTEnglish/wwwroot/js/account-ui.js` — complete offcanvas lifecycle at breakpoint change; form-aware pageshow gate; avatar restoration on Cancel.
- `TCTEnglish/wwwroot/css/account-auth-ui.css` — fixed visible focus ring; replaced `--tct-shadow-sm` with `--tct-shadow-soft`; enforced 44px touch targets.
- `TCTEnglish/wwwroot/css/account-ui.css` — fixed visible focus ring; replaced `--tct-shadow-sm` with `--tct-shadow-soft`; added `.text-unconfirmed-warning`; scoped modal styles.
- `TCTEnglish.Tests/AccountUiRenderTests.cs` — updated tests and added `AU04_ResetPassword_WhenTokenInvalidOrExpired_RendersDirectLinkToForgotPassword`.
- `scripts/tests/test_account_auth_ui.cjs` — added regressions for R1 duplicate submit ownership.
- `scripts/tests/test_account_ui.cjs` — added regressions for R2 breakpoint cleanup, R3 delete button pageshow gate, and R4 avatar cancel restoration.
- `docs/design-system/team-ui-workflow.md` — updated adoption rows and removed fictitious route.
- `docs/feature-audits/2026-11-08-account-ui-execution-ledger.md` — updated contract documentation and added Stage II repair ledger.

**Verification**:
- `node --test scripts/tests/test_account_auth_ui.cjs scripts/tests/test_account_ui.cjs`: 15/15 passed (duration: 167ms).
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests"`: 7/7 passed.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --filter "FullyQualifiedName~AccountUiRenderTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~ProfileBadgeReadTests|FullyQualifiedName~SharedUiPartialsRenderTests"`: 27/27 passed.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release`: 1176 passed / 22 skipped / 0 failed (duration: 4m 38s).

**Commit**: uncommitted (left dirty for owner review per prompt instructions)

**Notes**:
- Verified that pre-existing modified `DESIGN.md` (SHA256: `E904054E057CB06329FA3E867B1E65264993C1DD4C7653338172BF064F0C9945`) is preserved intact.
- Preserved single-agent execution policy without spawning subagents. No commits, pushes, remote CI, or deployments performed.

### Hybrid RAG Phase 7 repair round 1 — retry budget, evaluation protocol, provider clock — 2026-11-08

**Symptom**: The submitted Phase 7 envelope asked the owner to pick plan A or B, and treated 169,087 as if it covered embedding retries. A 36,276-byte batch that loses its response leaves only 3,724 bytes in a 40,000-byte job, so that retry cannot be sent. The 16-row demo did not lock the frozen actor, date, corpus or recall denominator. Under the full suite, `CallerCancellation_DuringBackoff_Propagates` saw 2 sends instead of 1, and `Deadline_CoversBodyRead_AndDoesNotResetForRetry` measured 2.8705s outside the 0.8–2.5s window after `ErrorCodeTimeout` had already matched. Isolated reruns of those two tests passed.

**Root Cause**: The envelope added the unique corpus byte upper to a chat reservation and called that a retry guarantee. Production already keeps unknown usage and refuses a retry that does not fit the current job remainder; the acceptance write-up and harness did not. The demo script did not state that all 120 fixture cases are `actor-lan-standard` on `2026-11-06` in `synthetic-phase0`, or that a missing vector index makes zero query-embedding calls. The provider tests armed a 60ms caller token and a wall-clock stopwatch around a 250ms backoff, so parallel scheduling could cross an attempt boundary. The product client already used one timeout window; the tests did not control that clock.

**Solution**: Replace the A/B envelope with a nominal success path of 14 embedding POSTs and 123,119 known bytes, and an explicit stop when a lost batch does not fit the job remainder. `AiAcceptanceRunGuard` admits each fake send, retains unknown usage for the whole run, and refuses promotion while unknown remains or the chunk count is short. A coordinated transport clock drives backoff, body reads and the original deadline. The 0.8–2.5s window and the one-call/two-call assertions stay. Production still uses `Stopwatch`, `CancelAfter` and `Task.Delay` through `SystemAiTransportClock`. The optional fourth constructor argument defaults to that clock, so `Program.cs` is unchanged.

**Files Changed**:
- `TCTEnglish/Services/AI/IAiTransportClock.cs` — production clock and the test seam.
- `TCTEnglish/Services/AI/OpenAiProviderClient.cs` — one budget for send, body read and backoff.
- `TCTEnglish.Tests/OpenAiProviderClientTests.cs` — coordinated cancellation, body deadline, and the 503-plus-backoff case.
- `TCTEnglish.Tests/AiAcceptanceRunGuard.cs` — run-scoped post, token, duration and promotion guard.
- `TCTEnglish.Tests/AiRagPhase7FixRound1Tests.cs` — lost 36,276-byte batch, 10-POST corpus replay, later-turn cap, frozen fixture.
- `TCTEnglish.Tests/AiRagHybridRetrievalTests.cs` — missing vector index returns `lexical_degraded` with zero embedding calls.
- `TCTEnglish.Tests/Fixtures/AI/phase7-corpus-admission-bounds.json` — 487 chunk byte uppers and the three job batches.
- `docs/feature-audits/hybrid-rag/phase-7/dry-run-envelope.md` — corrected envelope; 169,087 and 282,974 withdrawn.
- `docs/feature-audits/hybrid-rag/phase-7/evaluation-protocol.md` — actor, date, denominators, disposable seed rules.
- `docs/feature-audits/hybrid-rag/phase-7/demo-script.md` — rehearsal labels, not an API grant.

**Verification**: Submitted scope archived at `scope-manifest-input.json` and recomputed as 155/155, fingerprint `4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`, before the repair edits. Focused `Phase7FixRound1`: 46 passed, 0 failed. Full suite on that binary: 1142 passed, 22 opt-in skipped, 0 failed, total 1164, 2 m 29 s. Node chat guards 14/14. App build `--no-restore`: 0 warnings, 0 errors. `git diff --check` clean. Docker Linux pipe missing and port 3307 closed, so MariaDB dry-run and browser rehearsal did not run. No paid text, embedding or speech call.

**Commit**: none. Local repair only. HEAD remains `6af768bd32d16cc6d012d484ee82df4cab805c69`.

**Notes**: No matching known-issues row. The 14 POST / 123,119 byte figure is a review envelope, not spending authority. Batches of 36,276 and 37,558 bytes cannot be retried inside the same 40,000-byte job. The frozen evaluation fixture was not edited. Live Phase 7 acceptance stays blocked until a separate budget grant and a real MariaDB/browser run.

### Hybrid RAG Phase 6 closeout — late Stop admission, retry isolation and citation spacing — 2026-11-07

**Symptom**: Stop before `AiStreamStarted` removed the local placeholder but never
sent a cancellation when the server id arrived later. An immediate retry could
start another invocation while the old admission/failure was still pending; a
late Started could replace the new stream identity. Razor-reloaded citation
markup also painted template indentation as large blank areas.

**Root Cause**: The earlier stop-before-id repair handled the idle DOM only.
The client had no pending-invocation ownership; reading `streamGeneration` inside
each synchronous event callback did not associate that event with an attempt.
Citation markup inherited `white-space: pre-wrap` from the assistant bubble.

**Solution**: Retain a cancelled attempt until Start settles, cancel its id when
admission arrives, and block another transport while that invocation is pending.
Bind invocation failure to its attempt/generation, accept only one stream id per
attempt and retain a bounded set of retired ids. Preserve the draft and deliberate
retry with no automatic HTTP fallback. Collapse template whitespace in the bubble
and citation region while preserving the markdown body's existing whitespace.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js` — invocation ownership, late cancellation, retired stream ids.
- `TCTEnglish/wwwroot/css/ai-chat.css` — citation/bubble whitespace boundary.
- `scripts/tests/test_ai_chat_phase6_sources.cjs` — late Stop and Stop/retry/replayed Started regressions.

**Verification**: Before the JS repair: 2 failed / 5 passed. After repair: phase-6
Node 7/7; combined chat guards/paging 14/14. Native Chrome on the real Razor/JS
page with explicitly injected SignalR events confirmed one initial Start despite
double Enter, late `StopAiStream`, draft restoration, blocked overlapping retry,
and no stale text/source. A later accepted attempt rendered ordered/deduplicated
chunks and its own source only. Real cookie/MariaDB journeys additionally proved
warm-cache account, entitlement and class revocation; real HTTP and SignalR source
answers survived full/embed reload. Full app 1136 passed / 22 opt-in skipped /
0 failed; focused 224/224; final build succeeded; no pending model change.
See `docs/feature-audits/hybrid-rag/phase-6/browser-closeout.md` for exact evidence.

**Commit**: none. Local only; Phase 6 AWAITING_REVIEW, Phase 7 LOCKED.

**Notes**: This extends the earlier Phase 6 stop-before-id fix above. Its old Node
assertion explicitly expected no remote Stop and has been corrected. No matching
known-issues entry exists. Paid provider behavior/quality is not claimed.

### Hybrid RAG Phase 6 — draft restore, stop-before-start, and retry duplication — 2026-11-07

**Symptom**: Restoring a draft after a failed or stopped send threw `ReferenceError: adjustTextareaHeight is not defined`. Stopping before `AiStreamStarted` left the in-flight turn accepted, so a late completion could still attach sources. Retry after HTTP 429 appended a second user bubble with the same text.

**Root Cause**: `adjustTextareaHeight` was declared inside `init` while `restoreDraftIfEmpty` calls it from the outer function scope. The Stop handler returned until `activeStreamId` existed, so the generation counter was not bumped and the unaccepted chunks stayed eligible. `#typing` remains in `#chatWindow` with `data-role="assistant"` and class `d-none`; `shouldReuseUserBubble` treated that hidden row as the latest assistant answer and refused to reuse the pending user bubble.

**Solution**: Move `adjustTextareaHeight` to the IIFE scope. On Stop, increment `streamGeneration`, discard the unaccepted stream, and finalize before `StopAiStream`; skip the hub invoke when no stream id exists yet. Skip `row.id === 'typing'` in `shouldReuseUserBubble` and `removeTrailingTransportNotice`.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js` — IIFE-scoped height helper, stop-before-id discard, typing-row skip

**Verification**: Node `scripts/tests/test_ai_chat_phase6_sources.cjs`, `scripts/tests/test_ai_chat_submit_guard.cjs`, and `scripts/tests/test_ai_chat_load_older_order.cjs`: 13 passed, 0 failed. That includes the F2 SignalR case that failed while the helper was nested, the stop-before-id case, and the 429 retry case. Browser retry on `http://localhost:5186/AI/Chat?embed=true&phase6=retry`, after a cache-bypass reload of the served script, kept one user bubble and cleared the composer. No commit.

**Commit**: none — HEAD remains `6af768bd32d16cc6d012d484ee82df4cab805c69`

**Notes**: The typing row is a real assistant-role element in `TCTEnglish/Views/Ai/_ChatShell.cshtml`. A DOM harness that omits `data-role="assistant"` on `#typing` will not catch the duplicate-bubble regression.

### Hybrid RAG Phase 5 repair round 2 — missing sources array and post-commit projection deadline — 2026-11-07

**Symptom**: A version-1 envelope without a `sources` field was treated as a verified empty source list, so the stored sentence stayed in provider history. A display projection that waited on its cancellation token kept the accepted send and stream open, because that call used `CancellationToken.None`. The Phase 5 manifest path `predecessorManifest` pointed at the Phase 4 manifest while `predecessorFingerprint` identified the reviewed Phase 5 snapshot.

**Root Cause**: `TryRead` checked `sources` only when the property was present, then replaced a null list with `[]`. Post-commit `ProjectAsync` had an exception handler but no independent deadline. The refreshed manifest reused the Phase 4 path for a different fingerprint.

**Solution**: Require one `sources` array, using the serializer's case-insensitive name policy. A missing, null, or non-array value is unverifiable: display keeps the stored body, and provider context receives the neutral unverified notice. An explicit `sources: []` and legacy NULL metadata stay readable. After commit, display projection uses its own five-second token, the same post-acceptance budget as persistence and quota finalization. Timeout, cancellation, and other projection failures return the unverified notice without a second assistant or another settlement. The next manifest records the reviewed 140-entry fingerprint and names review round 2 as the predecessor reference instead of the Phase 4 manifest path.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs` — mandatory sources array
- `TCTEnglish/Services/AI/AiChatService.cs` — five-second independent projection budget
- `TCTEnglish.Tests/AiRagPhase5FixRound2Tests.cs` — missing-sources, empty-array, legacy, deadline, and stream regressions
- `docs/feature-audits/hybrid-rag/phase-5/scope-manifest.json` — predecessor reference

**Verification**: Round 2 tests were red first (5 failed, 5 passed, including the two missing-sources envelopes and both waiting projections). After the fix, 10/10 passed. Focused Phase 4/5, quota, cancel, and provider-consumer tests: 80 passed, 2 opt-in skipped, 0 failed. Full suite: 1128 passed, 22 skipped, 0 failed, total 1150. No new migration. Disposable MariaDB 10.5.23 applied 15 migrations once and preserved the legacy NULL metadata row. Pending model reported no changes.

**Commit**: none. Local only. HEAD remains `6af768bd32d16cc6d012d484ee82df4cab805c69`.

**Notes**: The five-second budget can return the unverified notice while a slow but valid reauthorization is still running only until that token cancels the awaited call; the saved assistant remains for reload. `known-issues.md` has no matching citation entry.

### Hybrid RAG Phase 5 repair — history, unverifiable envelopes, sentence freshness and post-commit projection — 2026-11-07

**Symptom**: A revoked assistant body could still enter provider history when the next message did not fit the remaining budget. Broken or future metadata kept the stored sentence in provider context. A null source item threw while reading history. Two sentences of one document stored only the first, so editing the second left the stale sentence in context. A projection fault after the assistant commit surfaced as a failed send even though the turn and quota were already accepted.

**Root Cause**: `LoadContextHistoryAsync` returned on budget overflow before `ProjectAsync`. `TryRead` kept the body for every parse failure and dereferenced source members. Capture deduped by source key, and corpus freshness treated a matching first sentence as current after a document-hash miss. Display projection ran after commit with the caller cancellation token and no failure result.

**Solution**: Project each loaded history batch in provider mode before token cost and before every return, and budget the projected text. Reject null or non-object source members without catching database or cancellation failures. Replace unverifiable metadata with a neutral notice only in provider context; display keeps the stored body and an unverified status. Store every cited sentence. A present document hash that matches neither the live document hash nor the title-body hash is changed and does not fall through to the first sentence. A version-1 envelope with no document hash is changed when any live sentence is absent. After commit, projection faults and cancellation return the unverified notice, leave the saved assistant and settlement untouched, and do not add a second row.

**Files Changed**:
- `TCTEnglish/Services/AI/AiChatService.cs` — project history before each budget return; fail closed after commit
- `TCTEnglish/Services/AI/Rag/GroundingCitationService.cs` — null-safe parse, provider notice, sentence provenance, group freshness
- `TCTEnglish.Tests/AiRagPhase5FixRound1Tests.cs` — regressions for HR-P5-R1-F01–F05

**Verification**: The new file was red first: 7 failed, 2 passed, 0 skipped, total 9. After the repair it passed 10/10. Focused Phase 4/5, quota, cancel and provider-consumer tests then passed 70 with 2 opt-in skips. Full suite: 1118 passed, 22 skipped, 0 failed, total 1140. No new migration. Disposable MariaDB 10.5.23 applied 15 migrations once, preserved user 900001 and the legacy message with NULL `GroundingMetadata`, and `has-pending-model-changes` reported no changes.

**Commit**: none. Local only. HEAD remains `6af768bd32d16cc6d012d484ee82df4cab805c69`.

**Notes**: Schema version stays 1. Old rows are not rewritten and missing sentence hashes are not invented. A content hash equal to the document, title-body, or body fingerprint remains document-level provenance, so an existing single-sentence envelope that stored the document hash stays current until that document changes. `known-issues.md` has no citation entry to update.

### Hybrid RAG Phase 5 — cited document must exist before the visible reply keeps the source sentence — 2026-11-07

**Symptom**: `GroundedTurn_KeepsEmbeddingSpendWhenSelectionIsInvalid_AndAnswersOnlySourceText` expected the reply `The bell is red.` and received `Nguồn dùng cho câu trả lời này không còn xem được.` A raw JSON search for that Vietnamese notice also failed while the parsed `content` field held the notice.

**Root Cause**: The send path stores the accepted sentence, then projects the envelope before returning. The scripted evidence key `reading:1` had no published passage, so reauthorization treated the source as revoked and replaced the visible body. Stored `AiMessages.Content` stayed `The bell is red.` ASP.NET JSON escaped the notice, so a substring search of the raw payload missed it.

**Solution**: The Phase 4 regression now inserts published reading passage 1 before send. Revocation still hides a missing, forbidden, or unpublished source. Phase 5 assertions read `content` and `grounding.status` from parsed JSON.

**Files Changed**:
- `TCTEnglish.Tests/AiRagPhase4GroundedTests.cs` — seed the cited published passage
- `TCTEnglish.Tests/AiRagPhase5CitationTests.cs` — assert the parsed message after card deletion

**Verification**: The grounded test passed. `AiRagPhase5CitationTests` passed inside the full suite: 1108 passed, 22 opt-in skipped, 0 failed.

**Commit**: none. Local only. HEAD remains `6af768bd32d16cc6d012d484ee82df4cab805c69`.

**Notes**: Do not show a scripted missing source by skipping projection. Seed the live document when a test needs the source sentence to remain visible. Parse JSON before searching Vietnamese notice text.

### Hybrid RAG Phase 4 closeout — cancellation receipts, per-send guards and atomic promotion — 2026-11-07

**Symptom**: The returned repair lost known usage when callers cancelled at body
EOF, retained an unknown hold after an HTTP 400 as if it were a zero-send preflight,
and could overwrite the actual uncertain send bound with unsent retry allowance.
Later sends lacked ownership/source checks; the single-sentence path and snapshot
replacement could accept stale evidence. Final transient evidence lost document
identity. Windows readers caused intermittent lexical/vector promotion failures.

**Root Cause**: Receipt persistence ran after transport/retrieval returned and used
the caller token. Chat guards did not reach individual retries. Send inference used
error categories. Final checks covered selection only. Sentence conversion omitted
document/snapshot metadata. Atomic rename/replace did not retry native sharing
violations; Directory.Move reports an open child as native 5, file replace as 32.

**Solution**: Add opt-in cumulative receipt callbacks with bounded independent tokens
to both transports, and connect durable ownership/current-source guards per send.
Use actual ledger attempts, preserve reported uncertain cost, clear only true
preflights, recheck all sentence paths/snapshot identity and retain provenance in
the transient answer. Share a bounded atomic-promotion retry helper: 8 attempts,
700 ms total backoff, Windows 32/33 and directory-only native 5; never weaken ACLs
or repeat provider requests. Keep failure diagnostic native codes and assertions.

**Files Changed**: AI provider/embedding options and transports; grounded pipeline,
selector/interfaces/composer/evidence; hybrid retrieval; lexical/vector stores and
CorpusFilePromotion; AiRagPhase4CloseoutTests and MariaDbAiTurnLifecycleTests;
diagnostic assertions in the existing corpus/vector/Phase 3 regression tests.

**Verification**: 13 new regression cases have failing-before/passing-after evidence.
Focused 191/191; final full suite 1100 passed / 22 opt-in skipped / 0 failed.
MariaDB lifecycle 3 passed / 0 skipped, including cancelled receipt 105 settled once.
Disposable MariaDB 10.5.23 upgraded 14 migrations from the reviewed baseline,
preserved user 900001 checksum 2660695814, repeated without history drift; repo-local
EF reported no pending model changes. Tooling validator and git diff --check passed.

**Notes**: Reuses the same-day Phase 4/Phase 3 ledger diagnoses above/below. No paid
API, persisted model/migration/configuration/shared-data change or remote action.
The original intermittent failures lacked native diagnostics; controlled probes
prove the repaired I/O mechanism, and the original tests pass in the final full run.
Details: docs/feature-audits/hybrid-rag/phase-4/review-round-2.md. No commit.

### Hybrid RAG Phase 4 repair — lost and invalid provider usage dropped the known receipt — 2026-11-07

**Symptom**: A lost first embedding response followed by a successful retry kept only the later receipt. An invalid query vector dropped known embedding usage. An incomplete generation response replaced a known total of 105 with an unknown bound of 64. Settlement then billed the unknown bound instead of the receipt.

**Root Cause**: Stage spend cleared the unknown hold and did not keep an earlier uncertain attempt. Chat generation attached usage only after shape checks, so an incomplete but metered response was reclassified as an unknown send. The ledger bill preferred that unknown figure over the known total.

**Solution**: `RecordStageUsageAsync` stores known and unknown amounts together. A lost embedding marks the pending unknown bound, then the retry records the known receipt without clearing that bound. Query-vector validation and incomplete chat output record the provider usage before the answer is rejected. Chat retries add an unknown bound only for a sent attempt that has no receipt. Settlement still prefers the conservative known-plus-unknown charge when it is greater than zero.

**Files Changed**:
- `TCTEnglish/Services/AI/AiDailyQuotaReservationService.cs` — stage usage write returns whether the pending owner was updated
- `TCTEnglish/Services/AI/AiTurnStageJournal.cs` — memory follows a successful durable write
- `TCTEnglish/Services/AI/OpenAiProviderClient.cs` — usage is copied before incomplete or refusal validation
- `TCTEnglish/Services/AI/AiProviderException.cs` — recorded prompt, completion, total and unknown travel with the exception
- `TCTEnglish/Services/AI/Rag/HybridRetrievalService.cs` — embedding attempts keep known prompt tokens and the uncertain upper bound
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs` — generation records the known total and any earlier unknown attempts
- `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs` — lost retry, invalid vector and incomplete generation

**Verification**: `LostEmbeddingResponse_RetainsUnknownBoundAndKnownSeven` sees 2 embedding POSTs, known 7 and a positive unknown bound. `InvalidQueryVector_RetainsKnownSeven` sees 1 POST and known 7. `IncompleteGeneration_RetainsKnownOneHundredFive` keeps KnownGenerationTokens 105 and settles 122 when embedding known is 17. These ran inside the Phase4RepairRound1 suite. No commit hash: uncommitted local repair.

**Notes**: Preflight refusals `invalid_configuration`, `budget_exhausted` and `corpus_stale` still send nothing. Non-chat payloads stay free of the chat `text` schema. Related to the same-day quota-stage entry below; this repair does not add columns.

### Hybrid RAG Phase 4 repair — selector payload was not the admitted generation charge — 2026-11-07

**Symptom**: A short output cap could stand in for the whole generation charge. A 3228-byte selector body was admitted under a planned 328 / request 700 budget. CJK history could be sent even when the serialized request exceeded the room reserved for one attempt.

**Root Cause**: Admission reserved output tokens and then measured evidence slack, but it did not measure the exact JSON body that `OpenAiProviderClient.CreatePayload` sends, including system prompt, question, evidence and schema. Character-divided-by-four was not the invoice, and the output cap did not include input bytes.

**Solution**: `ChatTurnBudget.TryCreate` measures the fixed selector envelope with the same serializer. If that envelope does not fit the per-attempt room, generation is not sent. Otherwise the input upper bound is the room left after output, and the unknown generation charge is attempts times input plus output. The pipeline shrinks oldest user history and then trailing sentences until the measured body fits, or it refuses before HTTP. The provider also rejects a body larger than `MaxInputBytes` before the loop. Evidence slack stays `min(800, room)` so a normal short answer is still admitted.

**Files Changed**:
- `TCTEnglish/Services/AI/ChatTurnBudget.cs` — envelope, attempt count and generation reserve
- `TCTEnglish/Services/AI/AiChatService.cs` — passes admitted generation and embedding attempt counts
- `TCTEnglish/Services/AI/OpenAiProviderClient.cs` — one payload serializer and a pre-HTTP byte ceiling
- `TCTEnglish/Services/AI/Rag/OpenAiChatSentenceSelector.cs` — measures and refuses before send
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs` — shrinks history and evidence to the input ceiling
- `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs` — CJK history and the narrow 700 budget

**Verification**: `CjkHistory_DoesNotSendAnUnboundedSelectorPayload` either refuses with no POST or keeps the body inside 700 bytes. `AdmittedCjkHistory_StaysInsideTheSelectorCeiling_AndNarrowBudgetRefuses` refuses the 3228-byte fixture at planned 328 / budget 700, and a wider admitted turn sends only bodies inside `GenerationInputUpper`. No tokenizer package was added. No commit hash.

**Notes**: The UTF-8 byte count is the conservative upper bound, not a provider invoice. Scripted selectors report measure 0 and are not capped by this ceiling.

### Hybrid RAG Phase 4 repair — multi-sentence chunk answered with the whole chunk — 2026-11-07

**Symptom**: Follow-up `is that true?` after `what color is the bell`, against the chunk `The bell is red. The door is blue.`, could answer from the whole chunk. A completed selector result `{"sentenceIds":[]}` could still fall through to catalog success.

**Root Cause**: Materialize emitted the chunk embed text instead of one evidence item per snapshot sentence. Empty completed `sentenceIds` was treated as “no selection” and the catalog remained eligible.

**Solution**: Materialize emits one item per snapshot sentence whose hash matches, and copies sentence id, sentence hash, route, visibility and corpus/chunker/source versions. `TrySelect` returns false for a completed empty array. The answer uses the sentence hash and the sentence visibility. `LexicalAnswerAdmission` was not relaxed, and no citation table was added.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/Corpus/HybridRetrievalService.cs` — per-sentence evidence and provenance
- `TCTEnglish/Services/AI/Rag/GroundedAnswerComposer.cs` — empty completed selection is not a catalog success
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs` — transient sentence provenance reaches the answer
- `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs` — two-sentence chunk and empty selection

**Verification**: `RealTwoSentenceChunk_FollowUpCopiesOriginalSentenceAndProvenance` returns only the red sentence, with its original sentence id, sentence and document hashes, route, visibility and versions. `CompletedEmptySelection_DoesNotAnswerFromCatalog` does not answer from the catalog. No commit hash.

**Notes**: This stays transient evidence. Phase 5 citation storage was not started.

### Hybrid RAG Phase 4 repair — unpublished source accepted after the selector returned — 2026-11-07

**Symptom**: A source unpublished, rehashed, made private, or replaced in the snapshot while the selector was in flight could still become the accepted assistant sentence.

**Root Cause**: The pipeline checked source currency before retrieval, then accepted the model selection without reading publication, hash and snapshot again after the provider await.

**Solution**: `SourcesStillCurrentAsync` rechecks each grounded identity before selector dispatch and again after the await, before accept. On failure the sentences and model JSON are dropped and the composer returns the existing source-revoked text. Paid usage already recorded is kept. The interface default stays true so scripted hybrids compile. A fixed server refusal is persisted with HTTP 200 so the client does not retry 503.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/Corpus/HybridRetrievalService.cs` — recheck by source key and document hash
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs` — both barriers drop a stale selection
- `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs` — unpublish during selection

**Verification**: `UnpublishDuringSelection_DoesNotAcceptTheOldSentence` does not return the old sentence. An unchanged authorized source still answers through the existing grounded tests in the same Phase4RepairRound1 run. No commit hash.

**Notes**: The recheck is a read of current publication and snapshot identity. It does not store a citation.

### Hybrid RAG Phase 4 repair — expired owner still sent generation and kept a late receipt — 2026-11-07

**Symptom**: After another conversation recovered an expired stage lease, the old owner could still call generation and a late receipt could write KnownGenerationTokens on a terminal row. A missing or non-pending ledger mutation could also report success.

**Root Cause**: Stage mutations committed even when the row was missing, not Pending, or the lease had already expired. The pipeline treated an in-process flag as ownership and did not stop the selector when the durable owner was gone. A late usage write did not require the row to still be pending.

**Solution**: `MutatePendingAsync` returns false, without changing the row, when the ledger is missing, not Pending, or `LeaseExpiresAtUtc` is due. It does not extend an expired lease and does not reopen a terminal question. Begin, record, clear and usage return that result. The journal updates memory only after the durable write succeeds. `OwnsTurnAsync` is a no-tracking read of the pending unexpired owner. A failed begin, a failed usage write, or a lost owner returns the revoked outcome and does not call generation. A late receipt on a terminal or expired row returns false and leaves KnownGenerationTokens unset.

**Files Changed**:
- `TCTEnglish/Services/AI/AiDailyQuotaReservationService.cs` — pending-and-unexpired gate and `OwnsTurnAsync`
- `TCTEnglish/Services/AI/AiTurnStageJournal.cs` — durable result before memory
- `TCTEnglish/Services/AI/Rag/GroundedChatPipeline.cs` — ownership gates embedding and generation
- `TCTEnglish.Tests/AiRagPhase4RepairRound1Tests.cs` — expired owner sends no generation
- `TCTEnglish.Tests/MariaDbAiTurnLifecycleTests.cs` — late generation receipt on MariaDB

**Verification**: `ExpiredOwner_DoesNotSendGenerationAfterRecovery` leaves selector calls at 0 and does not keep the old sentence. `ExpiredOwner_DoesNotAcceptALateGenerationReceipt` on disposable MariaDB 10.5.23 returns false, settles the embedding unknown 22, and leaves KnownGenerationTokens null. The existing live-lease fact still passes on that database: 2 passed, 0 skipped. No commit hash.

**Notes**: The 3-minute lease duration was not increased. No new column and no applied migration edit. Recovery of a due lease remains the other conversation's reserve, and a second settle stays a no-op.

### Hybrid RAG Phase 4 — live quota stage settled by another conversation — 2026-11-07

**Symptom**: After a chat turn recorded embedding usage and before generation
finished, the next conversation's question reservation settled the first turn.
Generation tokens could be dropped, and an unknown in-flight charge could be
treated as already final.

**Root Cause**: `ReserveQuestionAsync` recovered every Pending ledger row with
ActualTokens greater than zero. Embedding spend was stored in ActualTokens while
the same turn was still generating, so conversation B closed conversation A's
live row.

**Solution**: Keep ActualTokens at zero while a stage lease is live. Store known
and unknown embedding/generation amounts on the ledger, with ActiveStage,
LeaseExpiresAtUtc and TerminalIntent. Another reserve recovers a row only when
the lease has expired, the row is AwaitingRecovery, or it is a legacy row with
no stage, no lease and ActualTokens greater than zero. Unknown bounds stay in
the settled total. A second terminal close is a no-op.

**Files Changed**:
- `TCTEnglish/Models/AiQuotaReservationLedger.cs` — nullable stage, lease and usage columns
- `TCTEnglish/Services/AI/AiTurnLifecycle.cs` — recovery and settlement rules
- `TCTEnglish/Services/AI/AiDailyQuotaReservationService.cs` — stage writes and lease-aware recovery
- `TCTEnglish/Migrations/20261106181456_AddAiQuotaTurnStage.cs` — additive nullable columns
- `TCTEnglish.Tests/AiRagPhase4GroundedTests.cs` — barrier race and expired-unknown regressions
- `TCTEnglish.Tests/MariaDbAiTurnLifecycleTests.cs` — same lease rule on MariaDB 10.5.23

**Verification**: SQLite barrier test keeps the first row Pending with
KnownEmbeddingTokens 17 and ActualTokens 0 while a second scope reserves; after
the manual clock passes the 3-minute lease, one reserve settles 17 once.
Expired unknown generation 80 settles once and a later Settle(500) does not
change it. MariaDB acceptance test on `tct_acceptance_phase4rag` passed the
same live-then-expire sequence. Disposable `tct_migration_check` upgraded from
`20260510020134_AddReadingUserTranslation`, preserved user 900001 checksum,
applied 14 migrations including AddAiQuotaTurnStage, and a repeat update changed
no history. No commit hash: this phase is uncommitted local work.

**Notes**: In-process flags are not the lock. The durable stage and lease are.
Do not set ActualTokens during a live stage or the old scan returns. Legacy
MarkKnownUsagePending still moves the row to AwaitingRecovery so closeout
recovery keeps working.

### Hybrid RAG Phase 3 closeout — usage receipts, uncertain retries and bounded progress — 2026-11-07

**Symptom**: The returned repair still lost valid usage when vector validation
failed, reported complete/zero usage after cancelled/malformed/timeout responses,
and reused token allowance after an uncertain send. Cache write failures escaped
with no operation usage result. A cold corpus above the per-job UTF-8 ceiling
could never build an index, even when every individual chunk fit the ceiling.
Preflight configuration failures also invented an HTTP attempt in the service.

**Root Cause**: Usage was recorded only after validating the whole embedding
response. Unknown sends had a flag but no retained token reservation. The service
admitted only a whole-corpus bound and did not preserve its ledger across I/O errors.
Fallback service accounting inferred sends from exceptions without transport evidence.

**Solution**: Record independent valid usage before vector validation. Retain
conservative pending/uncertain bounds across retries and later batches, and mark
unknown outcomes on all failure/cancel paths. Return known costs on index/cache
failure. Support explicit partial jobs that keep validated cache, resume within
the same caps and promote only a complete/current index. No automatic next job.
Keep single-call adapter accounting in the default embedding interface method;
the production client owns actual per-HTTP attempts, including preflight zero sends.

**Files Changed**: EmbeddingOperationBudget.cs, IOpenAiEmbeddingClient.cs, OpenAiEmbeddingClient.cs,
Rag/Corpus/VectorIndexService.cs and AiRagPhase3CloseoutTests.cs.

**Verification**: Seven new regressions failed before repair; cache-I/O and
preflight cases failed separately. After repair RAG/provider 135/135 passed, full app suite
1067 passed / 19 opt-in skipped / 0 failed. All nine new regressions pass.
Native admin dry-run keyboard and
ready200 checked on preserved owner runtime; new backend tested in Phase3Closeout.
See docs/feature-audits/hybrid-rag/phase-3/review-round-2.md for scope/limits.

**Notes**: No paid API, protected config, persisted/shared data or remote stage.
Kept the running Release process; its apphost lock is not a compile failure.
### Hybrid RAG phase 3 repair round 1 — HR-P3-R1-F01–F04 — 2026-11-06

**Symptom**: Ngân sách 1 request vẫn gửi HTTP lần hai khi provider trả 429, rồi promote index. Câu tiếng Việt được ước lượng 7 token và được gửi dù trần là 10; usage 20 đã nhận bị báo thành 0. Hai scope trên cùng index cùng gọi provider. Batch 65 đoạn vẫn gửi đoạn thứ 65 sau khi cả 65 nguồn đã bị gỡ publish, rồi promote.

**Root Cause**: `OpenAiEmbeddingClient` retry bên trong một lần gọi `EmbedAsync`, trong khi `VectorIndexService` chỉ đếm lời gọi đó. Trần token dùng ước lượng ký tự/4 và ghi usage sau khi đã so sánh. Mỗi instance service có `SemaphoreSlim` riêng. Recheck corpus chỉ xảy ra lúc lập kế hoạch, không trước batch, retry hay promote.

**Solution**: Một ledger attempt/usage cho cả job, trừ request trước mỗi POST kể cả retry. Trần trước khi gửi là số byte UTF-8, vì BPE byte-level không thể nhiều token hơn số byte; ký tự/4 chỉ còn là ước lượng có nhãn. Usage đã biết được giữ khi vượt trần, hủy hoặc lỗi; usage không đọc được ghi `unknown`. Admission dùng chung theo thư mục index, service vẫn scoped. Trước mỗi batch, trước mỗi retry và ngay trước promote, đối chiếu snapshot lexical và tập chunk public hiện tại. Lệch snapshot trả `lexical-snapshot-changed`. Lệch nội dung hoặc publication trả `corpus-drift`. Không gửi payload cũ và không promote.

**Files Changed**:
- `TCTEnglish/Services/AI/EmbeddingOperationBudget.cs` — ledger attempt/usage và trần byte
- `TCTEnglish/Services/AI/OpenAiEmbeddingClient.cs` — trừ ngân sách và recheck trước mỗi POST
- `TCTEnglish/Services/AI/Rag/Corpus/VectorIndexAdmission.cs` — lease theo index root
- `TCTEnglish/Services/AI/Rag/Corpus/VectorIndexService.cs` — batch theo ngân sách còn lại, dừng khi corpus lệch
- `TCTEnglish/Program.cs` — đăng ký coordinator singleton trong grant RAG đã có
- `TCTEnglish/Areas/Admin/Controllers/AiManagementController.cs` — hiện safeTokens và usage unknown
- `TCTEnglish.Tests/AiRagPhase3FixRound1Tests.cs` — regression F01–F04 và hủy sau usage đã biết

**Verification**: Review round 1 đã reproduce bốn lỗi trên HTTP/SQLite giả. Sau sửa, `AiRagPhase3FixRound1Tests` 7/7 trên configuration `Phase3FixRound1`. Filter AiRag và OpenAiProviderClientTests 126 pass. Bộ tương thích 84 pass. Bộ app 1058 pass, 19 skip opt-in MariaDB, 0 fail. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Trần byte không phải số token của nhà cung cấp. Corpus public local dry-run báo safeTokens=113887 trong khi form mặc định là 40000, nên embed cả corpus sẽ bị từ chối trước HTTP. Chat thường vẫn lexical. Phase 4–7 vẫn khóa.

### Hybrid RAG phase 2 repair round 2 — HR-P2-R1-F02 and HR-P2-R1-F03 — 2026-11-06

**Symptom**: Câu “Where can I find the tidal harbor marker in this lesson?” và “Can I find the tidal harbor marker in this lesson?” trả câu mở đầu “Here you can find examples in this lesson.” thay vì câu chứa marker. Câu hỏi số người, nguyên nhân phát sáng, vị trí nhìn thấy, thời điểm xây dựng, người xây hoặc màu sắc vẫn được Answered/full khi nguồn chỉ có năm 2008, mệnh đề because của việc sơn lại, hoặc “visible at dawn”.

**Root Cause**: `QueryOverlap` đếm mọi token câu hỏi, nên các từ khung find/in/this/lesson thắng token chủ đề. `LexicalAnswerAdmission` coi bất kỳ chữ số, because, giới từ hoặc frame không nhận ra là đủ ý; test vòng 1 còn coi “Where is the tidal harbor marker visible?” là đáp án đúng của “at dawn”.

**Solution**: Token nội dung được nhân 100 trước khi cộng token khung, nên một token chủ đề thắng mọi từ khung. Admission chỉ nhận span nêu đúng facet được hỏi: số người không phải năm, nguyên nhân phải chứa đúng vị ngữ, vị trí phải là địa điểm không phải thời gian, thời điểm nhìn thấy không phải thời điểm xây, who/màu không rơi về Reference. Câu nhiều facet chỉ full khi cùng một span nêu đủ mọi facet.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs` — chọn span theo token nội dung
- `TCTEnglish/Services/AI/Rag/Corpus/LexicalAnswerAdmission.cs` — đối chiếu facet được hỏi với span
- `TCTEnglish.Tests/AiRagPhase2FixRound1Tests.cs` — đảo oracle where/visible thành negative
- `TCTEnglish.Tests/AiRagPhase2FixRound2Tests.cs` — regression F02/F03 còn mở

**Verification**: Năm regression đỏ trước khi sửa, gồm oracle where/visible. Sau sửa `AiRagPhase2FixRound1Tests` và `AiRagPhase2FixRound2Tests` 9/9. Filter Phase2FixRound2 231 pass. Provider/ML/probe 65 pass. Tooling exit 0. Python 135 pass, 1 skip. Baseline development 15/17, held-out 0, cùng hai miss cũ. Trang admin đã đăng nhập: Dry-run lúc chưa có index không tạo file; Build tạo snapshot; Dry-run sau đó không đổi byte. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Heuristic cue cũ không còn là điều kiện đủ. F01/F04 không bị viết lại. Snapshot v1 vẫn bị từ chối. Hai miss baseline HR-NAV-D04 và HR-NAV-D12 giữ nguyên. Index local vừa build nằm trong `.appdata` đã ignore, không phải production.

### Hybrid RAG phase 2 repair round 1 — HR-P2-R1-F01–F04 — 2026-11-06

**Symptom**: Dry-run trên trang admin gửi thành rebuild vì nút submit bị disable trước khi browser tạo form data. Chunk nhiều câu trả câu chào thay vì câu chứa từ khóa. Câu hỏi nguyên nhân hoặc số lượng được Answered/full dù nguồn chỉ nói “visible at dawn”. Một câu 29.700 ký tự vượt trần 600 token xấp xỉ và đi vào evidence.

**Root Cause**: Handler submit gỡ submitter khỏi form data và `bool dryRun` mặc định false. `ToHit` lấy câu hash-hợp lệ đầu tiên trong chunk. Điểm BM25 dương được nối thành câu trả lời full. `Pack` không tách câu đơn dài hơn trần chunk.

**Solution**: Form gửi `operation=dry-run|build` trước khi disable nút; thiếu hoặc sai giá trị thì không ghi snapshot. Chunker `paragraph-sentence-v2` chia câu quá dài thành span liền kề, hash đúng từng span, và từ chối snapshot v1. Evidence lấy span trùng query. Admission chỉ trả full khi span nêu đúng kiểu yêu cầu; nguyên nhân hoặc số lượng không có trong span thì InsufficientEvidence và evidence rỗng.

**Files Changed**:
- `TCTEnglish/Areas/Admin/Views/AiManagement/Index.cshtml` — giữ operation khi disable nút
- `TCTEnglish/Areas/Admin/Controllers/AiManagementController.cs` — từ chối operation thiếu hoặc không hợp lệ
- `TCTEnglish/Services/AI/Rag/Corpus/CorpusChunker.cs` — span đúng nguồn và trần chunk
- `TCTEnglish/Services/AI/Rag/Corpus/RetrievalCorpusService.cs` — chọn câu khớp
- `TCTEnglish/Services/AI/Rag/Corpus/LexicalAnswerAdmission.cs` — tách candidate khỏi câu trả lời đủ ý
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — không nâng hit không đủ ý thành full
- `TCTEnglish.Tests/AiRagPhase2FixRound1Tests.cs` — regression bốn finding

**Verification**: Bốn regression đỏ trước sửa. Sau sửa `AiRagPhase2FixRound1Tests` 5/5. Filter Phase2FixRound1 227 pass. Provider/ML/probe 65 pass. Tooling exit 0. Python 135 pass, 1 skip. Baseline development 15/17, held-out 0. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Snapshot `paragraph-sentence-v1` không còn được phục vụ. Admin cần rebuild. Hai miss baseline HR-NAV-D04 và HR-NAV-D12 giữ nguyên, không chỉnh trọng số. Trang admin trên launcher local chưa được bấm bằng trình duyệt đăng nhập; serialization Dry-run/Build đã bấm trên handler thật, còn POST admin được chứng minh bằng test host.

### Hybrid RAG phase 1 closeout — complete lookup target and requested field — 2026-11-06

**Symptom**: `Explain why France has seasons` và câu tiếng Việt vẫn Answered/full từ thẻ France chỉ định nghĩa quốc gia. Tra cụm water cycle còn lấy thẻ water, và hỏi phiên âm có thể nhận definition dù chưa có phiên âm.

**Root Cause**: Boolean tra thẻ chỉ nhận sự xuất hiện explain/meaning, không mang mục tiêu đầy đủ hoặc loại nội dung. Retriever có thể tìm từng token; boundary coi bất kỳ card hit là đủ và đưa cả evidence không dùng vào full answer. Control bộ Weather còn kỳ vọng definition của cloud cho yêu cầu weather.

**Solution**: Thêm transient `StoredCardLookupRequest` với kind và target đầy đủ, đọc các frame tra cứu có điểm đầu/cuối rõ. Chỉ chọn card có title chuẩn hóa bằng toàn bộ target và có field cần trả (definition/phonetic/example). Thiếu source/field trả InsufficientEvidence, scope không full, evidence rỗng. Câu không nhận diện có lexical card hit được từ chối. Luồng tra thẻ chỉ đưa các card đã chọn vào evidence. Giữ source qualifier/ACL/query caps trong retriever và mọi typed user/platform flow.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/StoredCardLookupRequest.cs` — kind/target parsing và kiểm tra nguồn/field
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — positive lookup selection và outcome/evidence nhất quán
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — bỏ heuristic boolean/thuộc tính đã thay thế
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — câu hỏi nguyên nhân, partial phrase, complete phrase, phiên âm; sửa control Weather/cloud sai loại câu trả lời

**Verification**: Nhóm hồi quy trước sửa 11 fail/2 pass; sau sửa boundary 36/36. Bộ filter Phase 1 215/215; provider/ML/lifecycle probe 65/65, cấu hình Phase1FixRound6. Probe độc lập 30 quan sát, gồm 10 ca unsupported đã kiểm scope/evidence đều đạt. Không gọi paid API.

**Commit**: Chưa commit. Owner yêu cầu Codex hoàn thiện trực tiếp trong review vòng 6; chưa cấp commit/push/release.

**Notes**: Transient DTO không đổi EF mapping/schema/content. Full Hybrid RAG/grounded generation và citation bền vẫn là các phase tiếp theo; baseline an toàn có thể abstain cho câu không có học liệu phù hợp. F02/F03/F04/F05 và source key card:{id} được giữ.

### Hybrid RAG phase 1 repair round 5 — HR-P1-R1-F01 — 2026-11-06

**Symptom**: “What is the capital of France?” vẫn Answered/full khi definition là “a country; its capital is not given in Unit12”. “Can you tell me the capital of France?” trả “a European country” như thể đó là thủ đô. Token có chữ và số được nhận là giá trị thuộc tính.

**Root Cause**: `HasExplicitAttributeValue` coi token dài từ 4, vừa có chữ vừa có số, là giá trị fact. Parser chỉ nhận vế trái dài 1–3 token, nên câu lịch sự không vào khung fact và thẻ lexical được nâng thành Answered/full. Oracle `capital: LIMA9` cũng dựa trên heuristic đó.

**Solution**: Câu trả lời full từ thẻ chỉ khi câu là tra nội dung đã lưu: từ ngắn, hỏi nghĩa, giải thích từ, kiểm tra từ trong notes, hoặc so sánh bộ. Câu thuộc tính “of/của” không phải hỏi nghĩa thì không được full, kể cả vế trái dài. Không còn dùng token chữ-số, mã Unit/Lesson hay prefix lịch sự để cấp quyền trả lời. “what does Peru mean” vẫn trả definition đã lưu. “What is the capital of Peru?” không còn full.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — bỏ heuristic giá trị tự do; thêm quyết định tra thẻ dương
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — chỉ giữ snippet thẻ khi câu là tra nội dung đã lưu
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — Unit12, Lesson4, câu lịch sự; bỏ oracle fact LIMA9

**Verification**: Hai test đỏ trước khi sửa trên `Phase1FixRound5`: Peru/LIMA9 và France/Unit12 đều Answered. Sau sửa, hai test đó xanh. Filter plan 203 pass. Provider/ML/probe 65 pass, `--no-build`. Script tests 135, 1 skip. Tooling exit 0. Query builder không đổi. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: LIMA9 là oracle tổng hợp của vòng 4, không phải yêu cầu nghiệp vụ. Definition vẫn hiện khi người dùng hỏi nghĩa đã lưu. Fact tự do thuộc Phase 4, đang LOCKED. F02/F03/F04/F05 và `card:{id}` được giữ.

### Hybrid RAG phase 1 repair round 4 — F01 và F04 còn lại — 2026-11-06

**Symptom**: Definition có chữ “capital” nhưng không có giá trị thủ đô vẫn thành Answered/full. “Thủ đô của France là gì?” cũng trả definition đó. “Daily Study” bị cắt thành bộ Daily.

**Root Cause**: Hỗ trợ fact chỉ cần từ khóa thuộc tính và thực thể trùng. “là gì” bị coi là tra nghĩa cho cả câu. Tên bộ bị xóa các từ khung như Study/Reading ở đầu hoặc cuối, nên prefix Daily/Lab được chọn thay tên đầy đủ.

**Solution**: Fact “X of Y” hoặc “X của Y” chỉ Answered khi evidence có token giá trị gắn với thuộc tính, gồm chữ và số. Nhắc thuộc tính trong câu văn không đủ. “là gì” không biến câu hỏi thuộc tính thành tra nghĩa. Tên sau “trong bo/trong set”, kể cả trong ngoặc, được giữ nguyên và khớp SetId trước trần. Cụm “trong bộ thẻ của tôi” không phải tên bộ. Giữ F02/F03/F05, `card:{id}` và trần 25/20/100.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — yêu cầu giá trị tường minh, không chỉ từ khóa
- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs` — không cắt từ khung khỏi tên bộ
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — sửa oracle Kenya; thêm France/Việt, Peru/LIMA9, Daily Study, Study, Reading Lab

**Verification**: Hai hồi quy đỏ trước khi sửa. Sau sửa, nhóm thẻ/fact 11/11 trên `Phase1FixRound4`. Filter chat/retriever 202/202. Provider, ML.NET và probe 65/65, `--no-build`. Script tests 135, 1 skip. Tooling exit 0. Pomelo query builder không đổi hình dạng SQL; test translation cũ vẫn xanh. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Oracle Kenya “the capital city is recorded here” không còn được coi là câu trả lời thủ đô. Giá trị dương phải là token như LIMA9. Baseline không suy fact từ câu văn tự do. Bản dịch Pomelo không phải lần chạy MariaDB.

### Hybrid RAG phase 1 repair round 3 — F01, F04 và F05 — 2026-11-06

**Symptom**: “What is the capital of France?” vẫn thành Answered/full từ definition quốc gia không có dữ kiện thủ đô. “nghia cua forecast trong bo Daily Words” trả nghĩa của bộ Daily. Bare “weather” đổi thành thẻ cloud sau khi thêm bộ tên Weather.

**Root Cause**: Policy fact chỉ nhận mốc thời gian hoặc cụm chủ đề, nên token trùng term được coi là đủ. Qualifier bộ lấy một token và mọi token trùng tên bộ đều thành bộ lọc, kể cả khi câu không chỉ định nguồn.

**Solution**: Câu “what/who is the X of Y” chỉ được Answered khi definition hoặc example chứa thuộc tính và thẻ đúng thực thể. Nhãn sau “trong bo/trong set” hoặc hai vế “giua bo … va …” là tên đầy đủ, khớp bản đã chuẩn hóa với tên bộ được phép xem, rồi lọc `SetId` trước trần. Tên không khớp hoặc chỉ là prefix thì không mượn bộ khác. Hai bộ cùng tên thì hỏi lại. Từ trùng tên bộ không còn là qualifier. Giữ F02/F03, `card:{id}` và trần 25/20/100.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — kiểm tra thuộc tính có được evidence hỗ trợ
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — bỏ thẻ không hỗ trợ và không trả full
- `TCTEnglish/Services/AI/Internal/CardLookupRequest.cs` — tách từ cần tra khỏi ràng buộc nguồn
- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs` — resolve set id trước cap
- `TCTEnglish/Services/AI/Internal/CardLookupSelection.cs` — nhiều bộ chỉ được trả lời khi câu so sánh
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — hồi quy France/Japan/Kenya, tên bộ đầy đủ, bare weather

**Verification**: Ba test mới đỏ trước khi sửa. Sau sửa, nhóm thẻ/fact/Pomelo 10/10 trên `Phase1FixRound3`. Filter chat/retriever 201/201. Provider, ML.NET và probe 65/65, `--no-build`. Script tests 135, 1 skip. Pomelo 10.5.23 chỉ `ToQueryString`, không mở kết nối. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Vòng 2 đã khép weather-today và exact forecast; vòng này khép fact không có mốc thời gian và qualifier nguồn. Tên bộ nhiều từ được nhận khi đứng trọn sau marker. Bản dịch SQL không phải lần chạy trên MariaDB.

### Hybrid RAG phase 1 repair round 2 — F01/F02/F04 còn lại — 2026-11-06

**Symptom**: “What is the weather today?” vẫn thành Answered/full từ thẻ `weather`. “Giải thích forecast trong bộ thẻ của tôi” mất thẻ đúng sau 120 definition. “nghia cua match trong bo Sporting” khi có bốn bộ và nhiều bản trùng thì hỏi lại Alpha/Beta/Gamma, không còn Sporting.

**Root Cause**: Policy fact chỉ khớp cụm tiếng Việt. Exact tier không nhận token nội dung khi câu có từ khung. Qualifier bộ chỉ chạy trên tối đa 3 snippet sau khi xếp theo tên bộ.

**Solution**: Câu hỏi tình trạng hiện tại (today/hôm nay kèm what is/bao nhiêu/thế nào) không được dùng definition thẻ, trừ khi câu hỏi nghĩa đã lưu hoặc dữ liệu học của chính user. Token nội dung, sau khi tách từ khung, vào exact tier trước cap broad. Tên bộ được phép xem được lọc trong SQL trước mọi trần; bộ chỉ định nhưng không truy cập được thì không trả thẻ bộ khác. Giữ `card:{id}`, F03 và trần 25/20/100.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — phân biệt fact hiện tại với tra nghĩa
- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs` — token nội dung và lọc bộ trước cap
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — hồi quy thời tiết, forecast có ngữ cảnh, bốn bộ, bản dịch Pomelo không kết nối

**Verification**: Ba test mới đỏ trước khi sửa. Sau sửa, nhóm thẻ/fact/Pomelo 11/11 trên `Phase1FixRound2`. Filter chat/retriever 198/198. Provider, ML.NET và probe 65/65, `--no-build`. Script tests 135, 1 skip. Không gọi API trả phí và không mở MariaDB.

**Commit**: Chưa commit.

**Notes**: Bản dịch Pomelo được kiểm bằng `ToQueryString` trên provider 10.5.23, không thực thi SQL. Tên bộ nhiều từ chưa được khớp. F03 và identity `card:{id}` được giữ.

### Hybrid RAG phase 1 repair round 1 — HR-P1-R1-F01 đến F04 — 2026-11-06

**Symptom**: Câu “Giá vàng hôm nay là bao nhiêu?” thành Answered/full chỉ vì thẻ `vang`. Thẻ đúng `zephyr-window` biến mất khi hơn 100 definition chứa token đó đứng trước. Từ `go`/`up` không tra được. Hai thẻ `match` khác bộ bị Answered/full và cùng source key theo title.

**Root Cause**: Boundary thêm CardLookup cho mọi OutOfScope rồi nhận bất kỳ card hit. `CardLookupRetriever` loại token ngắn hơn 3 ký tự và `Take(100)` trước khi chấm điểm. `SourceKey` ghép source với title. NeedsClarification chỉ xét title khác nhau.

**Solution**: Câu fact ngoài học liệu không được nâng thành câu trả lời thẻ; câu hỏi nghĩa đã lưu và term lạ vẫn tra được. Tầng exact/phrase có trần riêng chạy trước tầng broad 100. Token 2 ký tự chỉ khớp exact, không LIKE. Snippet mang `EntityId`/`ContainerId`. Source key là `card:{id}`. Term trần trùng nhau thì hỏi bộ nào; một bộ được chỉ định hoặc câu so sánh thì trả lời đúng các thẻ đó.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/ChatExternalFactPolicy.cs` — nhận câu fact ngoài học liệu
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — outcome, evidence key và clarification
- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs` — tầng truy vấn có trần
- `TCTEnglish/Services/AI/Internal/CardLookupSelection.cs` — chọn một bộ, so sánh hoặc hỏi lại
- `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` — câu hỏi chọn bộ
- `TCTEnglish/Services/AI/Internal/KnowledgeSnippet.cs` — identity thẻ/set tạm
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — bốn hồi quy SQLite trên boundary thật

**Verification**: Bốn test mới đỏ trước khi sửa. Sau sửa, cùng bốn test và hai test thẻ cũ xanh 6/6 trên cấu hình `Phase1FixReview`. Filter hồi quy chat/retriever 194/194. OpenAiProviderClientTests và MlNet 61/61, `--no-build`. Không gọi API trả phí.

**Commit**: Chưa commit.

**Notes**: Chưa chứng minh câu SQL trên MariaDB disposable. Tầng exact 25, phrase 20, broad 100. Đây không phải BM25 hay citation bền. Danh sách cụm fact ngoài học liệu không thay classifier tổng quát.

### Hybrid RAG phase 1 — chat boundary và lỗ hổng truy cập nội bộ — 2026-11-06

**Symptom**: Chat production đi thẳng qua `IAiProviderClient`. Retriever cũ trả video Speaking riêng của user khác, bỏ lỡ thẻ nằm sau cửa sổ 500 bản ghi không sắp xếp, đếm “hôm nay” theo ngày UTC, đếm đơn pending chỉ trong 3 đơn mới nhất, và chặn luôn câu hỏi đổi mật khẩu vì có chữ password. Câu confidence thấp bị coi là ngoài phạm vi nên không tìm guide/thẻ.

**Root Cause**: `AiChatService.SendAsync` gọi provider text dùng chung. `SpeakingRetriever` không lọc `OwnerUserId` theo rule public/Premium. `CardLookupRetriever` `Take(500)` trước khi chấm điểm và không gồm folder lớp. Các retriever tiến độ dùng `DateTime.UtcNow.Date`. `BillingRetriever` đếm pending trên cửa sổ `Take(3)`. Bộ lọc mật khẩu là substring, còn `InternalKnowledgeProvider` hạ confidence dưới 0.55 thành OutOfScope trước khi tra cứu.

**Solution**: Thêm `IAiChatAnswerService` scoped, chỉ cho chat. Production không gọi `IAiProviderClient` cho câu trả lời chat. Giữ lifecycle guard/quota/lưu/Stop trong `AiChatService`. Speaking đếm và gợi ý đúng catalog public hoặc bài riêng khi Admin/Premium. Thẻ lọc quyền rồi mới `OrderBy`/`Take(100)`. Ngày học dùng `BusinessDateHelper.Today`. Pending order đếm riêng trên DB. Câu đổi mật khẩu của chính user trả guide 19; rút secret/prompt bị từ chối trước quota. Confidence thấp vẫn tra guide và thẻ. Snippet có “ignore previous instructions” / “system prompt” bị gỡ trước khi soạn câu trả lời.

**Files Changed**:
- `TCTEnglish/Services/AI/Rag/AiChatAnswerService.cs` — ranh giới trả lời nội bộ, outcome và evidence
- `TCTEnglish/Services/AI/AiChatService.cs` — gọi answer service, từ chối secret trước quota
- `TCTEnglish/Program.cs` — một dòng DI scoped; `IAiProviderClient` vẫn là `OpenAiProviderClient`
- `TCTEnglish/Services/AI/Internal/Retrievers/SpeakingRetriever.cs` — lọc catalog và entitlement
- `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs` — lọc quyền trước trần kết quả
- `TCTEnglish/Services/AI/Internal/Retrievers/BillingRetriever.cs` — đếm pending thật
- `TCTEnglish/Services/AI/Internal/Retrievers/LearningProgressRetriever.cs`, `GoalsRetriever.cs`, `StudyRecommendationRetriever.cs`, `LearningAreaProgressRetriever.cs` — ngày nghiệp vụ
- `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` — hồi quy quyền và ranh giới provider

**Verification**: Local Debug, không gọi API trả phí. `AiRagBoundaryAccessTests` 11/11. Bộ lọc phase 1 lần đầu 199 pass / 2 fail ở hai test chat còn kỳ vọng HTTP OpenAI; sau khi sửa hai test đó, `OpenAiConsumerPipelineTests` cùng probe lifecycle 30/30. Lần chạy lại boundary, composer và baseline 49/49. Vocab suggest vẫn ghi nhận payload provider; chat HTTP/SignalR không gửi nội dung riêng sang provider.

**Commit**: Chưa commit. Owner chưa cho phép commit, push, PR hay deploy.

**Notes**: Đây là baseline nội bộ đã kiểm quyền, chưa phải Hybrid RAG. Chưa có BM25, embedding trả phí, citation bền hay UI phase sau. Quota chat vẫn theo ngày UTC. Trần 100 thẻ sau lọc token có thể bỏ lỡ nếu nhiều thẻ được phép cùng chứa token và đứng trước theo `Term`. Nhãn visibility của thẻ dùng chung lớp vẫn là `owner` cho đến phase citation. Không đổi schema nên không có migration.

### Speaking YouTube init, NoSpeech label và Goals skill summary — 2026-11-05

**Symptom**: Speaking showed “...” until YouTube ready; a late callback reset
the selected sentence. NoSpeech feedback disappeared after rendering scores.
Goals showed Writing1/2 but overall0%, and the editor could load a Vocabulary
target for another selected skill.

**Root Cause**: Sentence init lived in onPlayerReady; updateScoreBoard reset
the assessment label; Goals summary/editor still used legacy Vocabulary values.

**Solution**: Init sentence controls independently of YouTube, keep a chosen
sentence on late ready; set NoSpeech status after score render; constrain/wrap
mobile toast. Average percentages of positive skill targets (each capped), select
matching area/target for editor; weekly chart copy explicitly identifies vocabulary.

**Files Changed**: wwwroot/js/speaking.js, wwwroot/css/speaking.css,
Services/GoalsService.cs, Views/Goals/Index.cshtml; test_speaking_behavior.cjs,
test_speaking_word_comparison.cjs, GoalsPhase4IntegrationTests.cs.

**Verification**: Meaningful red→green on2YouTube cases, NoSpeech message and
3Goals summary cases. Goals focused51pass; full .NET929pass/0fail/18skip;
Node58pass. Browser: blocked video API usable; NoSpeech no progress POST;
Writing24→Goals1/2/50%, replay unchanged; Writing editor target2/mobile390 fits.

**Commit**: Not created (local stage only).

**Notes**: Related older Goals fixes restored real data; this follow-up fixes
multi-skill presentation, not schema/reward rules. UAT uses synthetic accounts,
fake speech transport and no paid requests. See05/11owner-closeout audit.

### Writing recentAttempts NeedsReview labeled Cần sửa — 2026-11-04

**Symptom**: After local-review NeedsReview submit, Practice/Data `recentAttempts`
showed `statusLabel: "Cần sửa"` instead of “Cần xem lại”.

**Root Cause**: `WritingService.BuildWritingAttemptHistory` used
`Passed ? "Đạt" : "Cần sửa"` and ignored `EvaluationSource`.

**Solution**: `WritingAssessmentPolicy.ResolveHistoricalStatusLabel` maps via
`ResolveHistoricalStatus`; BuildWritingAttemptHistory calls it.

**Files Changed**:
- `TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs`
- `TCTEnglish/Services/WritingService.cs`
- `TCTEnglish.Tests/WritingLocalAssessmentTests.cs`

**Verification**: WritingLocalAssessmentTests 39/0; live Practice/Data after
restart shows `Cần xem lại` for local-review and `Đạt` for approved variant.

**Commit**: not committed (owner: no commit this stage).

**Notes**: Found during R6 UAT; related to earlier WRITING-REVIEW-STATE fix.

### Chau C1.1 fresh-baseline allowlist + leftover MariaDB — 2026-11-04

**Symptom**: Fresh-baseline helper accepted history0 targets that still had app
tables (e.g. Sets); denylist of 8 names missed leftovers.

**Root Cause**: Guard enumerated a fixed app-table denylist instead of refusing
every current-schema table outside the metadata allowlist.

**Solution**: `RequireFreshBaselineTargetAsync` SELECTs all `TABLE_NAME` for
`DATABASE()`, allows only `__tct_acceptance_sentinel` and empty
`__EFMigrationsHistory`; Theory + MariaDB leftover Sets/Cards/SpeakingVideos
prove reject-before-Migrate and row preservation. No Program/entity/migration
changes.

**Files Changed**:
- `TCTEnglish.Tests/Infrastructure/MariaDbAcceptanceConnectionGuard.cs`
- `TCTEnglish.Tests/Infrastructure/MariaDbFreshBaselineGuardTests.cs`
- `TCTEnglish.Tests/WritingAcceptedAnswersMariaDbUpgradeTests.cs` (leftover class)
- plans/known-issues/ledger under `.tmp/`

**Verification**: Unit 8/8; leftover 1/1 on chau1104h:3334; concurrency+guard 4/4
on chau1104r; fresh upgrade 1/1 on chau1104i. R6 Admin partial UAT separately.

**Commit**: not committed (owner: no commit this stage).

**Notes**: Do not reuse a leftover-dirtied virgin DB for upgrade. R6 3-role/live
still pending.

### Chau final-polish final checks RR1/RR2 — 2026-11-04

**Symptom**: Upgrade test could MigrateAsync down to 03/11 on an already-latest
acceptance DB (drop WritingAcceptedAnswers); speaking “render twice” test did not
exercise production renderer; concurrency test lacked lock-point barrier.

**Root Cause**: Missing fresh-baseline guard before downgrade migrate; Node test
self-appended DOM; HTTP concurrency started two tasks without coordinating the
first FOR UPDATE hold against a second in-flight request.

**Solution**: `RequireFreshBaselineTargetAsync` + WritingAnswersUpgradeFact on
virgin DB; already-upgraded rejection test; production recorder/assess/commit
two-cycle + in-memory mutation; FOR UPDATE contention barrier + existing-list
and save-vs-edit MariaDB tests. No app feature protocol changes.

**Files Changed**: MariaDbAcceptanceConnectionGuard.cs, WritingAcceptedAnswersMariaDbUpgradeTests.cs,
MariaDbFreshBaselineGuardTests.cs, WritingAcceptedAnswersConcurrencyTests.cs,
test_speaking_word_comparison.cjs, known-issues, plans/ledger.

**Verification**: Fresh upgrade 1/1 on chau1104u:3332; concurrency+guard 4/4 on
chau1104r:3331; full .NET 926/0/7; Node test_*.cjs 56/0. No paid API; no commit.

**Notes**: R6 browser/demo/measurement still pending for owner.

### Chau final-polish review repair R1–R5 — 2026-11-04

**Symptom**: Concurrent Admin SaveAcceptedAnswers could persist 10 variants;
stale Hello form approved Hi there after Goodbye edit; Local+cloud feedback
reload flipped NeedsReview→NeedsRevision; structure test 1-fail gate; Speaking
comparison stayed visible on new record/mic deny.

**Root Cause**: Include-inside-txn without row lock; state token omitted sentence
content; cloud path overwrote EvaluationSource to local+ai-feedback; structure
test sliced on retired SyncWritingExerciseSentences name; speaking.js did not
clear comparison at new recording epoch; DOM fixture lacked firstChild/removeChild.

**Solution**: MariaDB FOR UPDATE on WritingExercises + ChangeTracker.Clear +
content-bound token; preserve local decision source and optional cloud provenance;
map legacy local+ai-feedback→NeedsReview; fix structure marker; clear comparison
on record start/mic deny; AcceptanceTables + upgrade preservation for attempts.

**Files Changed**: WritingExerciseManagementController.cs, WritingAssessmentPolicy.cs,
WritingService.cs, Edit/_AcceptedAnswersPanels, speaking.js, dom_fixture.cjs,
WritingAdminControllerStructureTests, WritingAcceptedAnswers* tests,
MariaDbAcceptanceFactory, WritingLocalCloudFeedbackStatusTests, known-issues.

**Verification**: Focused 16/16; full .NET 922 pass / 0 fail / 6 skip; Node
speaking/writing 16/16; MariaDB concurrency on tct_acceptance_chau1104r:3331.
No paid API; no commit/push/PR/deploy.

**Notes**: R6 browser UAT/demo/measurement still pending for owner re-review.

### Chau final polish F1/F2 + Speaking word diff + Writing accepted answers — 2026-11-04

**Symptom**: Writing drafts leaked across accounts on shared browser storage;
NeedsReview showed as “Làm lại”; owner asked for Speaking per-word comparison
and Admin-approved Writing answer variants before UAT.

**Root Cause**: Draft keys omitted owner id; JS normalize dropped AssessmentStatus/
ReviewRequired; AssessSpeaking only returned transcriptPreview; Writing grading
accepted only canonical EnglishMeaning with no Admin variant table.

**Solution**: Owner-scoped draft namespace from server-verified CurrentOwnerUserId;
preserve three assessment states through snapshot/Razor/JS; pure SpeakingTextAlignment
on full transcript with optional comparison JSON/UI; additive WritingAcceptedAnswers
table, Admin SaveAcceptedAnswers form, Assess(canonical + approved variants),
passage-edit guard when variants exist.

**Files Changed**: writing.js, WritingPractice.cshtml, WritingIndexViewModel.cs,
WritingService.cs, WritingAssessmentPolicy.cs, SpeakingTextAlignment.cs,
SpeakingAssessmentService.cs, SpeakingController.cs, speaking.js/css,
Practice.cshtml, WritingAcceptedAnswer entity/DbContext/migration,
WritingExerciseManagementController + Edit/_AcceptedAnswersPanels, tests/docs.

**Verification**: Node draft/status/speaking comparison tests green; WritingLocal/
Schema/AcceptedAnswers integration green; SpeakingTextAlignment + matching assess
comparison JSON green; MariaDB disposable upgrade/repeat/unique-hash pass on
tct_acceptance_chau1104c:3330; local tctenglish_dev applied
AddWritingAcceptedAnswers. No paid API; no commit/push/PR/deploy.

**Notes**: Owner browser A→B draft isolation and full 3-role UAT matrix still
recommended. Production deploy remains OFF.

### Stop local startup from replacing the credential store — 2026-11-04

**Symptom**: Owner entered a new key successfully. A metadata probe returned200
for gpt-6-luna/gpt-transcribe and one tiny text request returned200/completed/OK.
Starting the web then failed with EFS encryption error in Update-LocalSecretStore;
the active secrets.json disappeared. Global OPENAI_API_KEY still being present
is not evidence it is the newly entered/validated key. No substitute key was tested.

**Root Cause**: Ordinary launcher rewrote User Secrets to save Docker connection/
worker flags. File.Move(temp,destination,overwrite:true) could remove the existing
destination before the replacement failed. The finally block deleted the temp.
This is separate from the earlier cross-drive cache relocation mistake: normal
startup also performed an unnecessary destructive credential replacement.

**Solution**: Startup only reads the supported store; Docker/worker flags and
normalized nonsecret AI options are temporary child-process environment settings.
Restore all affected env on success/failure/DatabaseOnly. Explicit key helper
uses File.Replace with same-directory backup for existing files and nonoverwrite
Move for new ones; retain/restore protected backup on failure, no EFS/ACL bypass.

**Files Changed**: scripts/start-local.ps1, scripts/LocalAiConfiguration.psm1,
scripts/tests/test_local_launcher.py, scripts/tests/test_local_ai_config.py,
AGENTS.md, .ai/README.md, relevant local operations guides.

**Verification**: Launcher meaningful regressions red4 →green7; readonly store
mock forbids writes and asserts unchanged bytes/mtime, fresh store not created,
runtime DB flags inherited/restored, failure/DatabaseOnly and no ASR start guards.
Key/config14 pass including Windows locked-target failure preserving original.
Full scripts Ran105/1skip; setup9 pass; tooling verified. Real launcher built
Release, dev DB already up-to-date/no new migration; ready200 and Login200.
It preserved existing users/data and did not create a new secrets file.

**Owner state**: Informed/apologized; owner chose “Tôi sẽ nhập lại sau”. The
running web was deliberately launched without key env in its own process tree;
AI currently unavailable in that instance. Re-enter key via masked helper then
restart that owned launcher; do not reuse the old protected D copy or claim the
pre-incident validation proves the current instance has a working key.

**Later follow-up04/11**: Owner re-entered the key and asked to restart. Stopped
the owned no-key instance, launched the corrected Release script on5100. AI
source=User Secrets, speech source=User Secrets AI:ApiKey, ready200, Login opened.
Secret file length/mtime/EFS attribute metadata unchanged; no secret write or
new paid request. Current configuration is restored; live account/quota/audio
acceptance remains separate from metadata/startup checks.

**Commit**: Not committed; local fix/local-launcher-preserve-user-secrets only.

**Sources**: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-9.0 ;
https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew .

### Upgrade vulnerable vendored DOMPurify before publishing the pending diff — 2026-11-04

**Symptom**: Final pre-merge security review found the new local browser bundle
still pinned DOMPurify3.2.6 while chat renders untrusted Markdown as HTML.

**Root Cause**: Vendoring had retained an older sanitizer. GitHub advisory
GHSA-v8jm-5vwx-cfxm lists3.2.6 as affected; a generic script/onerror smoke test
alone does not establish that an old sanitizer version is free of known issues.
No application-specific exploit of that advisory was claimed or reproduced.

**Solution**: Pin DOMPurify3.4.17 from upstream signed release commit
a77a3e78b59f72fa4d1c83f22e121ad9cc7d3575, update full/embed asset references,
license and SHA256 provenance. Preserve upstream vendor bytes across Git
checkouts and test manifest completeness/hash integrity and Razor asset paths.

**Files Changed**: `TCTEnglish/Views/Ai/{Chat,ChatEmbed}.cshtml`,
`TCTEnglish/wwwroot/lib/pinned/`, `.gitattributes`,
`scripts/tests/test_pinned_browser_assets.py`.

**Verification**: GitHub release metadata identifies3.4.17; upstream immutable
raw asset and npm distribution have identical SHA256. Two asset-integrity tests
pass. Chromium runs production ai-chat.js hydration with Marked/DOMPurify:
Vietnamese bold Markdown survives; script/onerror/javascript URL probes are
removed, no payload execution/page error. This is focused browser evidence,
not a complete browser acceptance or proof against every possible XSS payload.

**Commit**: `feat(ui): refine learner/admin screens and secure chat assets`.

**Sources**: https://github.com/advisories/GHSA-v8jm-5vwx-cfxm ;
https://github.com/cure53/DOMPurify/releases/tag/3.4.17 .

### Local cache/temp relocation to D and credential relocation incident — 2026-11-04

**Symptom**: Owner requested project data/cache off nearly-full C. Shared package/
browser caches occupied about12GiB, and web child temp defaulted to C. During
bulk relocation I also attempted to move User Secrets, disrupting the local AI key.

**Root Cause**: Default cache/TEMP locations followed the Windows profile. I
incorrectly treated the credential directory as ordinary movable cache data:
the moved file is EFS-protected/read-denied to this tool session; C configuration
now has an empty AI:ApiKey. Exact EFS/projection implementation was not established.

**Solution**: Move caches/temp to private D storage with compatibility junctions
and supported user cache preferences. Map launcher child TEMP/TMP to checkout
temp, restore env on exit/failure; preserve global Windows TEMP. Stop credential
relocation, retain standard C key store and protected D copy; do not weaken EFS/
ACL or substitute another key. Owner was informed and explicitly chose to enter
the key later through the existing masked helper. This key step is not complete.

**Files Changed**: scripts/start-local.ps1, scripts/tests/test_local_launcher.py,
AGENTS.md, quickstart; host-only cache preferences/junctions/manifests outside Git;
local-storage plan/audit and private README.

**Verification**: 7 directories/146259 files inventory verified,12.026GiB logical;
C free16.01→~27.83GiB. SQL2 full hashes, cache hash samples. Offline restore from
D; full890/0/12; scripts Ran99/skipped1; TEMP red3→green4/4; setup9/9; tooling/
hooks verified. AI metadata correctly reports not-configured pending owner key.

**Commit**: Not created; no push/remote CI/deploy.

**Notes**: Never include OS-protected credentials/auth profiles in bulk cache
relocation. Standard restore copy was denied; no decryption/security bypass
attempt. Do not call AI ready from offline tests. Docker VHDX was already on D;
no DB/hosting data touched. Audit: docs/feature-audits/2026-11-04-local-storage-on-d.md.

### Retire remaining local Whisper helper, prompts and active cache path — 2026-11-04

**Symptom**: Owner wanted to ensure local web no longer starts Whisper. Runtime
was already migrated, but an unused loopback acceptance helper/test, executable
historical prompts and 43 cached engine/model artifacts still remained.

**Root Cause**: Provider replacement removed active DI/launcher callers but did
not retire all test configuration and old prompt instructions or local cache.

**Solution**: Remove unused ASR loopback helper/parser tests; replace with paid
OpenAI opt-in isolation tests. Poison legacy script/cache/process/download in
launcher fixtures. Mark old prompts/handoffs superseded; reinforce common agent
rules and remove stale branch assumption. Permanent deletion was policy-blocked;
rename the old cache into ignored retired-speech-2026-11-04, preserving audio/logs.

**Files Changed**: AGENTS.md, .ai/README.md, three cursor-auto-whisper prompts,
two premium Speaking historical docs, local-speech guide; old AsrLiveAcceptance
files deleted; OpenAiSpeechLiveConfigurationTests and launcher fixtures.

**Verification**: Launcher4/4, Python Ran99/skipped1, setup9/9; source/tasks/rules
scan; no Whisper Windows process/startup/scheduled task or legacy env/secret key.
Archive absent from runtime path,43 artifacts/109780875 bytes preserved,6 evidence
hashes unchanged. Full verification details in retirement audit.

**Commit**: Not created; local cleanup only.

**Notes**: Automatic review rejected permanent cache deletion with 'blocked by
policy'; no disk-space-reclamation claim. Docker Engine unavailable, so no new
real web/container acceptance. Historical whisper-audio data/fonts/audits stay.
Evidence: docs/feature-audits/2026-11-04-whisper-runtime-retirement.md.

### OpenAI Speech follow-up: verify budget payload, launcher authority and status privacy — 2026-11-04

**Symptom**: The R1–R7 repair still allowed malformed/non-16k/duplicate WAV
payloads through the live-test budget; launcher could override an explicitly
empty app secret with the global key; metadata status could report invalid
boolean/file configuration as ready and print credentials inside invalid URLs.
The fake app test named KeepOneCredit did not assert any persisted credit.

**Root Cause**: Budget divided data bytes by a constant without validating fmt
or selecting exactly one multipart file. Launcher treated missing/empty alike
and generated AI:ApiKey="" for fresh stores. Status omitted file layers and
used truthy fallback plus raw BaseUrl output. App fixture reused the first
sentence transcript for all sentences and checked only HTTP results.

**Solution**: Validate complete PCM16 mono16k RIFF/chunks and a single file
before HTTP budget reservation; preserve buffered original content. Only absent
keys fall back; never create an empty AI key default. Load Development file
layers, fail invalid bool metadata, redact invalid URLs. Return matching fake
transcripts and assert credit/XP/video completion before and after replay.

**Files Changed**:
- `TCTEnglish.Tests/Infrastructure/OpenAiSpeechLiveBudget.cs` and budget tests
- `scripts/LocalAiConfiguration.psm1`, `scripts/start-local.ps1` and fixture tests
- `scripts/speech-status.ps1`, `scripts/tests/test_speech_status.py`
- `TCTEnglish.Tests/OpenAiSpeechAssessmentAppTests.cs`, PCM planner test coverage
- `docs/operations/local-speech.md`, review/ledger/closeout documents

**Verification**: Red budget6, key1, status3 and credit1 reproduced before fixes.
Full optimized SpeechReview897 passed/0 failed/12 skipped; JS12/12; Python Ran99,
skipped1 (98 executed pass); setup9/9; tooling verified; diff check clean.
Final launcher fixture4/4 includes fresh/missing and existing explicit-empty.

**Commit**: Not created; owner authorized local review/fixes only.

**Notes**: Follow-up to R3/R4/R7 dated03/11, not a new provider/model migration.
No real key edited, paid call, DB migration, remote CI or release. Shared live
budget is per test process; repeated runs incur separate budgets. Mic/live
quality remains unverified. Evidence: docs/feature-audits/2026-11-04-openai-speech-rereview.md.

### OpenAI Speech review-repair R1–R7 — 2026-11-03

**Symptom**: Review REQUEST CHANGES — padded PCM windows overlapped past 10s and
import charged provider before failing timestamps; varying-amplitude threshold
could drop all windows; live import lacked shared 8POST/120s budget; shared
AI empty/invalid keys fell through; stream IOException after 200 became 500;
admission retained expired users unbounded; speech-status ignored Enabled/Model/
BaseUrl precedence; Admin Create still said Whisper.

**Root Cause**: MergeWindows refused long unions so padding overlaps survived
split; importer validated per-window during HTTP loop; threshold used noise×3.5
above peak; budget lived as private counters on silence live tests only;
credential FinishExplicit missing on shared AI authorities; response body read
errors unmapped; admission never evicted empty buckets; status helper read
partial config; leftover Admin copy.

**Solution**: Union-normalize overlapping padded intervals then hard-split ≤10s;
pre-validate all windows (0 HTTP on failure); clamp threshold to observed peak;
process-shared `OpenAiSpeechLiveBudget` DelegatingHandler on all live speech
clients; explicit fail-closed shared keys + redacted credentials ToString;
IOException→unavailable; expire+bound admission (capacity refuse); speech-status
mirrors app precedence; Admin Create OpenAI Speech-to-Text wording.

**Files Changed**:
- `PcmSpeechWindowPlanner.cs`, `AudioTranscriptImporter.cs` (+ R1/R2 tests)
- `OpenAiSpeechLiveBudget.cs` / Handler + live acceptance wiring + offline budget tests
- `OpenAiSpeechCredentialResolver.cs`, `OpenAiSpeechTranscriber.cs`, admission
- `speech-status.ps1`, Admin `Create.cshtml`, `OpenAiSpeechAssessmentAppTests.cs`

**Verification**: Focused Debug 66/0/1 then admission 4/4; full Debug retest;
JS 12/12; Python Ran 94 skip 1; agent_tooling + setup 9/9; EF no pending;
browser Debug :5130 + fake ASR :18765 assess/commit/cancel/replay (transcript
`hello from fake openai speech`); live paid still off. Ledger updated.

**Commit**: not created (owner forbade commit/push/merge/deploy)

**Notes**: Fake browser is not paid live proof. MaxTrackedUsers=16384 after
eviction. Old :5112 was CLOSED; new-source evidence is :5130 Debug + fake key.

### Replace active Whisper ASR with OpenAI Speech-to-Text — 2026-11-03

**Symptom**: Local Whisper lifecycle delayed/startup cost and pinned native engine;
owner chose cloud OpenAI file transcription for all active ASR callers.

**Root Cause**: Active runtime depended on whisper.cpp HTTP (`WhisperHttpTranscriber`,
`local-speech.ps1`, launcher Start/probe). gpt-transcribe returns text without
provider word/segment timestamps, so import needed measured PCM windows.

**Solution**: `OpenAiSpeechTranscriber` + credential resolver (speech explicit →
shared AI key → OPENAI_API_KEY); per-user assessment admission; C# PCM window
planner for import (`openai-audio-vad`); retire Whisper scripts/lock/fixtures;
launcher/status/docs/agent tasks for four clients; live opt-in
`TCT_OPENAI_SPEECH_LIVE=1` only. Text model remains gpt-6-luna/low.

**Files Changed**:
- `TCTEnglish/Services/Speaking/OpenAiSpeechTranscriber.cs` (+ options/resolver/admission/planner)
- `AudioTranscriptImporter.cs`, `YoutubeTranscriptService.cs`, `Program.cs`, `appsettings.json`
- `scripts/start-local.ps1`, `speech-status.ps1`, `agent_clients.py`, ops docs/`AGENTS.md`
- Removed `WhisperHttpTranscriber.cs`, `local-speech.ps1`, whisper lock/fixtures

**Verification**: Focused speech Debug green; full Debug **870 pass / 0 fail / 12 skip**
(live speech/text/MariaDB skipped); scripts discover **92 pass / 1 skip**; JS speaking
**12/12**; agent_tooling pass; EF pending-model none; Release bin locked by preserved
web PID 7084 on :5112 (old binary — not used as new-source evidence). Live speech
unverified (flag off). Ledger:
`docs/feature-audits/2026-11-03-openai-speech-replacement-ledger.md`.

**Notes**: No commit/push/PR/deploy; PLESK gate unchanged. Segment-window timing ≠
word alignment. Historical `whisper-audio` rows remain readable.

### Plan2 third-review D1–D3 (quota flush on cancel, Speaking Take24, Load-older multipage) — 2026-11-02

**Symptom**: Cancel after provider reply before Save left orphan assistant (0 user / 1 assistant) and Released quota; Speaking Index returned 24/25 public+private with no continuation so tail-only topics listed without cards; second Load-older page inserted between previously loaded pages.

**Root Cause**: Shared scoped DbContext let quota `SaveChanges` flush caller's Added assistant/success log; cancel treated any `replyCommitted=false` as Release even when provider usage was known; Speaking capped with in-memory Take after full ToList (public) / SQL Take without HasMore; `insertBefore(fragment, loadOlderRow)` pushed the anchor below page1.

**Solution**: Quota DI uses isolated scope/`SaveQuotaChangesAsync` suspension; chat lifecycle tracks `providerReplyKnown` vs `replyCommitted`, persists with independent CT, Settles/marks known pending (never Releases known usage), `MarkKnownUsagePending`/`RecoverKnownPending` recovery; Speaking SQL filter+Skip/Take paging + `/Speaking/Videos` continuation + Index query params; Load-older inserts after the control (`insertBefore(fragment, control.nextSibling)`).

**Files Changed**:
- `AiDailyQuotaReservationService.cs`, `AiChatService.cs`, `Program.cs` (quota + SMTP transport DI)
- `SpeakingService.cs`, `ISpeakingService.cs`, `SpeakingController.cs`, `SpeakingIndexViewModel.cs`, `Views/Speaking/Index.cshtml`, `wwwroot/js/speaking.js`
- `wwwroot/js/ai-chat.js`, `SmtpAppEmailSender.cs`, `ISmtpMailTransport.cs`
- Tests: `ThirdReviewProbeTests.cs`, `SmtpTransportFakeTests.cs`, `test_ai_chat_load_older_order.cjs` (multipage)

**Verification**: ThirdReviewProbeTests 4/4; SmtpTransportFakeTests 4/4; Repair/Cursor focused green; Node submit+order **6/6**; full suite after D1–D3 (see ledger). MariaDB C1 rerun blocked — Docker Linux engine pipe unavailable.

**Commit**: none (local only)

**Notes**: Extends C2/C3/C4/C5; Speaking Writing EXPLAIN/BE0 scale lab/BE7 assets/BE8 browser still open.

### Plan2 re-review C1–C6 (MariaDB simultaneous reserve, Clear/tracking, post-commit settle, Load older order, UpdateEmail) — 2026-11-02

**Symptom**: Simultaneous Serializable reserves deadlocked without recovery; quota `ChangeTracker.Clear` left conversation `UpdatedAt` unsaved; cancel/settle-failure after committed reply Released quota or HTTP500→JS retry double provider; Load older reversed each page; UpdateEmail accepted `not-an-email`.

**Root Cause**: No deadlock retry + Clear detached caller entities; settle used caller CT and Release on OCE after commit; settle exceptions bubbled as 500; per-row `insertBefore(nextSibling)` reversed chronological items; UpdateEmail lacked format validation; ResetPassword lacked atomic consume.

**Solution**: Detach only quota entities; bounded deadlock/lock-wait/duplicate retry with stable requestId; independent finalize token + never Release after replyCommitted (return reply even if settle fails); DocumentFragment insert before load-older control; UpdateEmail validator; ResetPassword `ExecuteUpdate` consume; Profile `GetEarnedBadgesForProfileAsync`; Speaking playlist preview Take(6) + catalog/my Take(24).

**Files Changed**:
- `AiDailyQuotaReservationService.cs`, `AiChatService.cs`, `AccountController.cs`, `wwwroot/js/ai-chat.js`
- `GoalsService.cs` / `IGoalsService.cs`, `SpeakingService.cs`, `SpeakingIndexViewModel.cs`
- Tests: `RepairReviewProbeTests.cs`, `AccountCorrectnessIntegrationTests` concurrent reset, `test_ai_chat_load_older_order.cjs`

**Verification**: RepairReview 4/4; RepairMariaDb simultaneous + prior MariaDB quota 3/3; CursorReviewProbes 9/9; Node 5/5; full `Plan2Be1` after C1–C6 **850/0/5** (pre BE4/BE6 slice); focused post-BE4/6 suite green.

**Commit**: none (local only)

**Notes**: BE4 Writing SQL aggregates / true per-level SQL LIMIT / EXPLAIN still open; BE5 fake SMTP seam, BE7 assets, BE8 browser/ASR unverified.

### Plan2 review-repair R1–R8 (quota ledger, SignalR Stop, history Load older, bcrypt fail-closed) — 2026-11-02

**Symptom**: Review suite had 13 fails; 9 Cursor probes reproduced lost multi-DbContext quota settles, ignored same-day historical usage, SignalR Stop stuck behind Start (parallel=1), cancel orphans after prompt commit, no Load-older path after 30/50 caps, malformed bcrypt 500 / ChangePassword bypass, AiPhase2 InMemory vs transactions.

**Root Cause**: Quota settle mutated stale tracked entities without request identity; empty `AiUserDailyQuotas` row started at 0; hub shared DbContext + `MaximumParallelInvocationsPerClient=1`; cancel cleanup only around provider; history paging without HTTP/UI continuation; structural bcrypt gate still called `Verify` on bad cost/salt; Phase2 tests used EF InMemory which ignores transactions.

**Solution**: `AiQuotaReservationLedgers` + Clear/idempotent finalize + historical log backfill (persist on reject); SignalR parallel=2 with `IServiceScopeFactory` per hub invocation; state-aware cancel after prompt; `Chat/Conversations`+`Chat/Messages` + `data-ai-load-older`; `TryVerifyLocalPassword` + Register email/ChangePassword length; AiPhase2 SQLite shared-memory fixture; official `CursorReviewProbeTests`.

**Files Changed**:
- `TCTEnglish/Services/AI/AiDailyQuotaReservationService.cs`, `Models/AiQuotaReservationLedger.cs`, `Migrations/20261102111404_AddAiQuotaReservationLedger.*`
- `TCTEnglish/Program.cs`, `Hubs/ClassChatHub.cs`, `Services/AI/AiChatService.cs`
- `TCTEnglish/Controllers/{AiController,AccountController}.cs`, `Views/Ai/{Chat,_ChatShell}.cshtml`, `wwwroot/js/ai-chat.js`
- `TCTEnglish.Tests/{CursorReviewProbeTests,AiPhase2ServiceTests,MariaDbAiQuotaConcurrencyTests}.cs`, `Infrastructure/MariaDbAcceptanceFactory.cs`

**Verification**: CursorReviewProbes 9/9; MariaDB disposable migrate+repeat+pending-model clean + quota 2/2; full `dotnet test -c Plan2Be1` **844 pass / 0 fail / 5 skip**; JS submit guard 4/4; `git diff --check` clean.

**Commit**: none (local only per owner)

**Notes**: Supersedes mid-handoff “DONE core” for BE2/BE3. BE4/BE6/BE7 and browser/ASR BE8 still open — see ledger. Disposable acceptance container `tctenglish-mariadb-acceptance-p2rp1102a` left running locally; do not point at `tctenglish_dev`.

### Plan2 BE1–BE3 Account/Chat correctness (L01, L02, L03, L06, L11, AccessDenied, mojibake) — 2026-11-02

**Symptom**: Social/malformed password hashes caused BCrypt `SaltParseException` on local login; AI cancel after save left orphan prompts; Stop could not cancel before `AiStreamStarted`; Standard quota raced across conversations; chat history loaded unbounded; Admin 403 had no page; UserManagement JSON Vietnamese was mojibake.

**Root Cause**: Direct `BCrypt.Verify` without hash shape guard; cancel only handled `AiProviderException`; stream identity published after provider completed; quota counted success logs before call without DB reservation; conversation/message queries materialised full sets; cookie `AccessDeniedPath` lacked view; Admin JSON strings were mis-encoded UTF-8.

**Solution**: BCrypt local-hash gate; AccessDenied 403 view + cookie path; reset null-expiry reject; Settings unconfirmed social marker; cancel cleanup with bounded token + quota release; early `onStreamStarted` before provider; `AiUserDailyQuotas` Serializable reserve/settle/release + additive migration; history paging defaults 30/50 and provider context Take(100); SMTP send bounded by CancellationToken; UserManagement messages re-encoded.

**Files Changed**:
- `TCTEnglish/Controllers/AccountController.cs`, `Views/Account/AccessDenied.cshtml`, `ResetPassword.cshtml`, `Settings.cshtml`
- `TCTEnglish/Services/AI/{AiChatService,AiStreamingService,AiConversationService,AiDailyQuotaReservationService}.cs`
- `TCTEnglish/Hubs/ClassChatHub.cs`, `Program.cs`, `Models/AiUserDailyQuota.cs`, `Migrations/20261102104017_AddAiUserDailyQuota.*`
- `TCTEnglish/Areas/Admin/Controllers/UserManagementController.cs`, `Services/SmtpAppEmailSender.cs`
- Tests: `AccountCorrectnessIntegrationTests`, `AiCancelAndStopIntegrationTests`, `AiHistoryPagingIntegrationTests`, `AiQuotaReservationIntegrationTests`
- Preflight: `_AdminLayout.cshtml` 991.98px; `scripts/tests/test_ai_chat_submit_guard.cjs` behavior harness

**Verification**: `node --test scripts/tests/test_ai_chat_submit_guard.cjs` 4/4; `dotnet test -c Plan2Be1` focused Account+AI cancel/stop/paging/quota **18/18**. Full Release suite / MariaDB multi-instance / browser BE8 chưa chạy.

**Commit**: none (local only per owner)

**Notes**: Migration additive only — apply on disposable/dev locally; not production. Quota SQLite serializable proof is local; MariaDB acceptance still recommended. BE4–BE8 remaining in ledger.

### Plan 1 UI Review Repair (R1 AI submit guard, R2 Avatar preview, R3 Chat short viewport, R4 Sidebar inert/ARIA, R5 Admin skip link) — 2026-11-02

**Symptom**:
- R1 (P1): Pressing Enter in AI Chat while a request was in-flight wiped the user's draft textarea, appended a duplicate user bubble to the DOM, and sent duplicate requests.
- R2 (P2): Selecting a new avatar image file on a user with no avatar or a broken avatar URL read the file successfully via FileReader, but the `<img>` remained `display: none` from fallback state and never showed the preview.
- R3 (P2): In short viewports (390x500 portrait, 844x390 landscape), `min-height: 220px` on `.ai-chat-window` caused it to overflow its flex wrapper (height 127px), clipping the bottom messages and scrollbar by 79.5px so the last line was unreachable.
- R4 (P2): On mobile, keyboard navigation (Tab) entered 17 focusable links inside the closed sidebar off-screen at x = -226px; Account and Admin mobile toggle buttons did not update `aria-expanded`.
- R5 (P2): In Admin layout on mobile 390x844, focusing the skip link positioned it at (x: 8, y: 8) with `z-3`, behind the sticky navbar (`z-index: 1020`), making it invisible and blocked from hit-testing.

**Root Cause**:
- R1: `elForm` submit handler lacked an atomic in-flight guard check before draft extraction and DOM manipulation; keydown handler on Enter called `requestSubmit()` which bypassed button disablement.
- R2: `previewImage` set `preview.src` but did not reset `preview.style.display = ''` nor hide `fallback.style.display = 'none'`.
- R3: Media queries `@media (max-width: 767.98px)` and `@media (max-width: 425px)` applied rigid `min-height: 220px` to `.ai-chat-window` inside `.ai-chat-window-wrapper { overflow: hidden; }`.
- R4: Closed mobile sidebars lacked the `inert` attribute; toggle handlers did not sync `aria-expanded` and lacked Escape return-of-focus.
- R5: Admin skip link had Bootstrap class `z-3` (`z-index: 3`) which was layered behind the sticky navbar's `z-index: 1020`.

**Solution**:
- R1: Added synchronous atomic in-flight flag `let isSubmitting = false;` in `ai-chat.js`; guarded submit handler, Enter keydown, and quick-actions against `isSubmitting || elSendBtn.disabled || elForm.dataset.state === 'sending'`; released flag in `finally`.
- R2: Updated `previewImage` in `Profile.cshtml` to un-hide preview and hide fallback on valid file read; restored fallback on error/invalid file; added `onerror` fallback to initials.
- R3: Replaced `min-height: 220px` with `min-height: 0; height: 100%` in `ai-chat.css`; added `@media (max-height: 550px)` to compact headers and preserve full scrollability without clipping on short screens.
- R4: Added `syncSidebarState` helpers in `layout.js`, `Profile.cshtml`, `Settings.cshtml`, and `_AdminLayout.cshtml` using `matchMedia`; applied `inert = !isOpen` and synced `aria-expanded` on mobile; ensured desktop always has `inert = false`; handled Escape and click-outside returning focus to toggle buttons.
- R5: Defined `.admin-skip-link` with `z-index: 1060 !important` and `position: fixed` when focused in `_AdminLayout.cshtml`.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`
- `TCTEnglish/Views/Account/Profile.cshtml`
- `TCTEnglish/Views/Account/Settings.cshtml`
- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish/Areas/Admin/Views/Shared/_AdminLayout.cshtml`
- `TCTEnglish/wwwroot/js/layout.js`
- `docs/feature-audits/2026-11-02-plan1-ui-repair-ledger.md`
- `docs/feature-audits/2026-11-02-chau-ui-execution-ledger.md`

**Verification**:
- R1: Unit regression `scripts/tests/test_ai_chat_submit_guard.cjs` (4/4 passed). Browser DevTools test: draft preserved, no duplicate bubble, sendBtn remains disabled during flight, subsequent send succeeds.
- R2: Browser DevTools test with DataTransfer 1px PNG: preview toggles from none to inline, fallback from flex to none; onerror restores fallback cleanly.
- R3: Browser DevTools test at 390x500: `isLastMsgClippedByWrapper: false` (diff -7.28px, was clipped +79.5px); tested 844x390 landscape, 390x844 standard mobile, 1440x900 desktop, 6-line textarea expansion.
- R4: Browser DevTools test at 390x844: `/Goals` main sidebar has `inert: true`, 0/17 links focusable when closed, `aria-expanded` syncs true/false; verified Profile, Settings, and Admin sidebars. Desktop maintains `inert: false`.
- R5: Browser DevTools test on Admin at 390x844: skip link focused at (8, 8) has `isSkipLinkHitDirectly: true` (hit element is `A`, not `NAV`), `computedZIndex: "1060"`. Desktop control 1440x900 also verified.
- R6: Full test suite compiled fresh with isolated configuration `Plan1Review`: `Failed: 0, Passed: 811, Skipped: 8, Total: 819` (TRX: `.tmp/plan1-ui-repair/plan1-ui-repair.trx`). `git diff --check` clean.

**Commit**: (local uncommitted; awaiting owner review)

**Notes**: Local work only. No backend, controller, model, migration, or Program.cs changes made. AccessDenied 404 belongs to Plan 2 (BE1/BE6).

### Plan 1 UI & Mobile layout fixes (Profile U08, AI Chat U05, Speaking U09, Auth U02, Stats U03) — 2026-11-02

**Symptom**: On mobile (390px viewport), Profile `.account-main` squashed to 122px by sticky sidebar; AI Chat message window squashed to 25.78px by 299px metrics panel; Speaking hero overflowed at 417px; Auth card overflowed at 393px; Profile displayed hardcoded 75%/40% progress bars; missing input labels and non-focusable password toggles.

**Root Cause**: CSS media queries cascading incorrectly (`account.css` media 680 override kept sidebar in flex flow); AI Chat observability panel was a 4-card static block occupying 299px without collapsible disclosure; Speaking decorative blobs and topic chips lacked overflow containment; Auth inputs lacked border-box box-sizing; Profile Razor view contained hardcoded percentage integers; Settings inputs lacked `for`/`id` bindings and password toggles had `tabindex="-1"`.

**Solution**:
1. Scoped sticky sidebar to `@media (min-width: 993px)`; on mobile made sidebar off-canvas, expanding `.account-main` to 382px.
2. Wrapped AI Chat metrics into `<details class="ai-chat-observability-disclosure">` (collapsed to 33.22px on mobile); wrapped history in disclosure; expanded chat window to 473.98px; converted composer to `<textarea>` multiline auto-grow with IME guard.
3. Removed hardcoded 75%/40% progress bars in `Profile.cshtml`, replaced with honest empty state and `/Goals` CTA.
4. Added `box-sizing: border-box` to Auth card, eliminated 3px overflow (`scrollWidth: 390px`).
5. Contained Speaking hero import and topic chips (`scrollWidth: 390px`).
6. Linked all `label for` / `id` pairs, made password toggles focusable with `aria-label`/`aria-pressed`, added accessible names across admin tables and switch `IsPublished`.

**Files Changed**:
- `TCTEnglish/Views/Account/Profile.cshtml`, `Settings.cshtml`, `Auth.cshtml`, `ForgotPassword.cshtml`, `ResetPassword.cshtml`, `ForgotPasswordConfirmation.cshtml`
- `TCTEnglish/Views/Ai/_ChatShell.cshtml`, `Chat.cshtml`
- `TCTEnglish/Areas/Admin/Views/WritingExerciseManagement/_ExerciseForm.cshtml`, `Index.cshtml`, `SpeakingVideoManagement/Index.cshtml`, `UserManagement/Index.cshtml`, `Shared/_AdminLayout.cshtml`
- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish/wwwroot/css/account.css`, `ai-chat.css`, `auth-stitch.css`, `layout.css`, `speaking.css`, `writing.css`
- `TCTEnglish/wwwroot/js/ai-chat.js`, `layout.js`

**Verification**:
- Headless Chrome DevTools measurements at 390x844: Profile main width 382px (0px overflow), AI chat window 473.98px (metrics 33.22px), Speaking 390px, Auth 390px.
- Build succeeded: `dotnet build TCTEnglish/TCTEnglish.csproj -c Release --no-restore -o .tmp/build-verify` (0 warnings, 0 errors).
- Tests passed: `dotnet test TCTEnglish.Tests` (101/101 passed).
- `git diff --check` passed with exit code 0.

**Commit**: Local changes on `feature/chau-ui-performance-audit` pending owner review (no commit per owner policy).

**Notes**: Preserves all backend APIs, SignalR events, and database state. BE1 (mojibake), BE2 (Stop-before-Started), and BE3 (server paging) are deferred to Plan 2.

### Lifecycle containment assertion mixed canonical and aliased temp paths — 2026-11-02

**Symptom**: The new full Windows CI suite passed media/launcher/speech cases but failed the session-state containment assertion on the hosted runner.

**Root Cause**: Runtime resolves the approved root before storing hashed session metadata. The test resolved the resulting file but compared it to an unresolved TemporaryDirectory root; Windows temp aliases/junctions can represent the same directory differently.

**Solution**: Resolve both sides of the test's is_relative_to comparison. Keep the containment/private-payload assertions and the full Windows job; no runtime trust/permission/path guard changed.

**Files Changed**: scripts/tests/test_agent_lifecycle.py.

**Verification**: Windows run41396737860 supplied the failing regression (one failure among108 tests, other native cases passed). Focused local/full suite and subsequent CI are verified before merge.

**Commit**: Owner-authorized PR123 follow-up; production gate false.

### Windows media bootstrap fixtures were scheduled as Linux success cases — 2026-11-02

**Symptom**: PR123 Agent tooling checks failed two FFmpeg bootstrap tests on Ubuntu; the helper correctly rejected its Windows-only artifact profile before extraction.

**Root Cause**: Fixture success/tamper tests had no host annotations, unlike the speech/launcher tests. A negative archive test also passed for the wrong platform reason on Linux.

**Solution**: Mark three native fixture tests as Windows-only, retain the hash/source check on every host and add a distinct non-Windows fail-before-artifact-creation regression. Add a Windows CI job running the complete scripts suite and native hook contracts so the Windows cases remain enforced; do not weaken the runtime platform guard.

**Files Changed**: scripts/tests/test_local_media.py, .github/workflows/agent-tooling.yml.

**Verification**: Initial Ubuntu run41314167871 reproduced failures; subsequent focused/full local and GitHub results are recorded in the PR/run and rollout handoff.

**Commit**: Follow-up fix on the owner-authorized PR123; deployment gate remains false.

### New teammate databases had schema but no local accounts or sample data — 2026-11-01

**Symptom**: A fresh clone could create Docker schema but had no guaranteed account or development fixture; existing-machine setup instructions depended on the owner's private volume/backup.

**Root Cause**: Local launcher applied migrations with startup seeding disabled, and the generated admin described in old notes was provisioned manually only on the owner's machine.

**Solution**: Explicit Development-only CLI bootstrap, restricted to loopback/tctenglish_dev/tct_dev, runs after migrations. If users exist it does nothing; otherwise a transaction creates three random-password accounts and synthetic learning fixtures, with credentials exclusively in ignored access.json. Added read-only prerequisite doctor, SETUP-LOCAL.cmd and shared onboarding/group message; owner branch names use feature/fix/chore without codex/.

**Files Changed**: Infrastructure/LocalDevelopmentBootstrap.cs, Program.cs local CLI, start-local.ps1, LocalDevelopmentBootstrapTests.cs, launcher fixtures, check-local.cjs, setup entrypoint and onboarding docs.

**Verification**: Meaningful red5fail/1pass → green6/6 for environment/target guard, fresh data, password hashes, rerun/edits and credential-file preservation. Disposable MariaDB10.5.23 cold migration/bootstrap/repeat passed, credentials hash unchanged, three users, web readiness200, actual JSON login success and authenticated Profile200. Full release/check evidence in team rollout ledger.

**Commit**: Owner now authorizes grouped commits/push/PR/merge master; deployment remains off.

**Notes**: No schema change or production data export; existing owner database is not seeded/reset. Windows x64 artifacts only; machine prerequisites must be installed on each teammate's own machine. No credentials, backups, native binaries or volumes go into Git.

### Local launcher did not bind the owner's OpenAI key and could report stale web as ready — 2026-11-01

**Symptom**: Owner configured OPENAI_API_KEY, but TCT English AI:ApiKey remained empty. Rerunning the launcher on an occupied port could build against a locked old executable or report an already-running web without proving it had the new provider/key.

**Root Cause**: ASP.NET binds AI__ApiKey, not OPENAI_API_KEY; previous setup deliberately had no fallback mapping. Port check was after the EF build and treated an arbitrary listener as successful reuse. A follow-up regression also exposed PowerShell's untyped null becoming an empty string in SetEnvironmentVariable, leaving an empty override after cleanup.

**Solution**: Owner explicitly authorized OPENAI_API_KEY fallback. Launcher selects explicit app env, then app User Secrets, then global OpenAI env; maps only for its children, reports metadata only, and restores prior env including true unset via NullString.Value on success/failure. Busy port fails before build/native operations without stopping workloads. Shared AGENTS/runbooks route all four AI clients through this launcher.

**Files Changed**: scripts/LocalAiConfiguration.psm1, scripts/start-local.ps1, scripts/tests/test_local_ai_config.py, scripts/tests/test_local_launcher.py, AGENTS.md, .ai/README.md and local setup docs.

**Verification**: Key/precedence12/12 and real-launcher fixtures3/3 passed after red reproductions. Provider/consumer/media focused Release74/74; full application808 passed/0 failed/5 skipped; full scripts107/107; Node9/9; tooling/hooks/EF pending-model pass. Actual launcher5102 health200 and Whisper ready. Old environment key401invalid_api_key; owner supplied replacement, entered via masked helper into User Secrets, then2synthetic OpenAI requests passed actual gpt-6-luna/low. See docs/feature-audits/2026-11-01-openai-migration-review-closeout.md.

**Commit**: Local uncommitted; no remote CI or production action.

**Notes**: No credential value printed/copied into source/argv/log. Ordinary launcher does not make a paid API request. Website remains gpt-6-luna/low; existing User Secrets key is not overwritten. This update supersedes the earlier intentional no-OPENAI_API_KEY-mapping policy for the local launcher.

### OpenAI migration: bounded Responses transport and atomic consumer failures — 2026-11-01

**Symptom**: Gemini text/audio runtime remained in the application; existing retry/body-read deadlines could exceed caller budgets. Vocabulary provider failures escaped before their catch blocks. Listening accepted count mismatches and left earlier batch translations tracked after a later failure. Audio fallback invented timestamps from plain text.

**Root Cause**: Provider-specific Gemini serializer/parser and raw Google audio HTTP were separate paths; timeouts were recreated per attempt and the body used a different token. Vocabulary awaited WhenAll before per-task error handling. Listening mutated EF entities inside the batch loop. Audio timing was estimated by word count.

**Solution**: OpenAI Responses adapter with ordered roles, Luna low, typed output/refusal/incomplete validation, redacted error categories/usage and a single bounded deadline; shared DI with workload caps. Observe vocabulary tasks inside catches. Stage all translations before applying them. Shared audio uses pinned FFmpeg, bounded PCM chunks, real Whisper segment timestamps and the same inference limiter as learner assessment. Masked local key helper merges secrets atomically; launcher preserves existing workloads and supports separate Release/port acceptance.

**Files Changed**: `Services/AI/OpenAiProviderClient.cs`, AI options/reply/errors, Program.cs/provider defaults, text consumers, `Services/Speaking/Audio*`, `FfmpegPcmDecoder.cs`, `YoutubeAudioSource.cs`, `YoutubeTranscriptService.cs`, local key/media helpers and focused regression/acceptance tests.

**Verification**: Provider red (8 failures) → green; later Listening-batch regression red → green. Final Release application suite805 passed/0 failed/8 skipped of813; scripts99/99, Node9/9, tooling/hooks and EF pending-model pass. Explicit live OpenAI smoke + all text consumers passed on gpt-6-luna/low (11 bounded requests); real local download-fixture/decode/ASR plus Plan1 speech/silence3/3 passed. Detailed usage, evidence and unrun gates in `docs/feature-audits/2026-11-01-openai-provider-migration-ledger.md`.

**Commit**: Not created. Plan1 dirty work and local data retained; production unchanged.

**Notes**: Reused Plan1 lifecycle without modifying its runtime or tests. MariaDB acceptance, authenticated cloud browser journeys, real YouTube/mic/load and clean-machine end-to-end remain unverified. Test credential provided later by owner was used only in temporary process environment through masked input, never persisted in source/User Secrets/argv. Existing Debug process lock was preserved; separate Release instance verified then stopped.

### Whisper startup accepted late success beyond readiness deadline — 2026-11-01

**Symptom**: A one-second startup readiness deadline still accepted a valid inference response after2.7seconds.

**Root Cause**: Wait-SpeechReady checked time only before starting a probe; each probe had an independent30second timeout and a5second minimum. HTTP success returned immediately even when the startup deadline had elapsed.

**Solution**: One monotonic stopwatch budget, cap each HTTP probe/retry sleep by remaining time, permit fractional probe deadlines and reject late success. Added a real loopback delayed-response regression and retained fast-success coverage.

**Files Changed**: `scripts/local-speech.ps1`, `scripts/tests/test_speech_readiness_deadline.py`, local-speech runbook. Added `WhisperLiveAppAcceptanceTests.cs` as explicit opt-in integration verification of speech→assessment→progress→one credit→replay/reload with actual Whisper and an isolated SQLite database.

**Verification**: Regression failed before fix (Ready=true after2.717761s with deadline1s), then both deadline tests passed. Full Python88/88, Node9/9, tooling validator pass; Release application suite743 passed /0 failed /3 MariaDB acceptance skipped. Live silence and app speech→credit tests ran with actual Whisper and locally generated Microsoft David speech. Evidence: docs/feature-audits/2026-11-01-whisper-plan1-final-closeout.md.

**Commit**: Local uncommitted, per owner's scope.

**Notes**: Cursor WR01/WR02 fixes retained. No provider migration/hosting/database schema change. Per owner clarification, Plan 2 application target remains GPT-6 Luna low; Codex GPT-6.1 Sol high is the implementation agent choice. Synthetic speech is not human microphone/accuracy acceptance.

### Whisper lifecycle WR01 registry start_time + WR02 JsonDocument probe — 2026-11-01

**Symptom**: Rereview WR01/WR02 — fixture `_kill_registered` ignored recorded `start_time` (same path + stale creation time still requested taskkill); `Test-InferenceResponseBody` via ConvertFrom-Json accepted segments-only empty, primitive segments, end-before-start, and root arrays that the app maps to `invalid_schema`.

**Root Cause**: Registry cleanup compared only PID/path; probe validator lost root JsonValueKind and did not enforce MapVerboseJson/TryReadSegment hard-fail rules (silence needs explicit string text; bad segments invalidate the body).

**Solution**: `_kill_registered` fail-closed on missing/unparseable/mismatched start time (2s skew); rewrite probe validator with System.Text.Json.JsonDocument; ValidateProbeBody action for table tests; fake modes for schema loopback; full tearDown regression without state.pid authority. Table cases also cover end overflow and root `text:null`.

**Files Changed**: `scripts/local-speech.ps1`, `scripts/tests/test_local_speech.py`, `scripts/tests/fixtures/fake_whisper_server.py`, ledger/plan.

**Verification** (fresh round2 handoff run): scripts discover **86 pass** / 0 fail (~154s); node setup **9/9**; agent_tooling pass; focused Whisper/limiter/live Release **27 pass** / 0 fail / 0 skip (live silence on `:8092`); Artifact Verify OK; owner Status **ready** PID **13360** (prior 24532 already stopped before this run — Start restored owned engine, left running); git diff --check clean on untracked speech scripts.

**Commit**: (local uncommitted; round-2 awaiting Codex rereview)

**Notes**: App assess→credit, mic/browser, speech-accuracy benchmarks remain Not run. No schema/migration/OpenAI. WH01/WH04 regressions still green in the same suite.

### Whisper lifecycle ownership/readiness/CPU pin — 2026-11-01

**Symptom**: Review WH01–WH04 — Stop could accept a wrong OS executable when state.commandLine lied; test teardown could taskkill a forged state PID (e.g. System); HTTP 200 with `{"error":...}` counted as ready; CPU backend DLLs were only “present”, not hash-pinned.

**Root Cause**: Ownership used state metadata to override OS path mismatch/missing path; teardown trusted mutable state.json for kill authority; probe.Ok was HTTP-only; lock/assert lacked per-file CPU backend SHA allowlist.

**Solution**: Expected runtime path from verified args only; missing/mismatch OS path fails closed; Stop re-checks start time+path before kill; fixture registry teardown independent of state; shared inference body validator aligned with WhisperHttpTranscriber; pin/hash all ggml-cpu-*.dll and reject extras; Verify action for assert-without-repair; PeakWorkingSet64 sampling renamed correctly.

**Files Changed**: `scripts/local-speech.ps1`, `scripts/tests/test_local_speech.py`, `scripts/tests/fixtures/fake_whisper_server.py`, `tooling/whisper/artifacts.lock.json`, `docs/operations/local-speech.md`, ledger.

**Verification**: `test_local_speech` 11/11; scripts discover 83/83; agent_tooling pass; node setup 9/9; dotnet Release 742 pass / 3 skip; live silence on owner loopback 8092 pass. Owner Whisper PID 24532 preserved.

**Commit**: (local uncommitted; remediation awaiting Codex rereview)

**Notes**: App assess→credit, mic/browser, speech-accuracy benchmarks remain Not run. No schema/migration.

### Missing import hash historical target model — 2026-10-31

**Symptom**: LearningImportSourceHash was discoverable and its explicit SQL ran, but Migration.TargetModel was empty and its Designer metadata was absent.

**Root Cause**: The handwritten additive migration contained discovery attributes and Up/Down but no BuildTargetModel partial.

**Solution**: Restore the Designer partial from the existing EF9 final snapshot. Keep original migration source/attributes, Up/Down and snapshot unchanged. Add regression coverage for the historical import job/hash/relationship model.

**Files Changed**: `20261030113000_LearningImportSourceHash.Designer.cs`, `SpeakingSchemaTests.cs`; closeout review and executed plan/prompt.

**Verification**: Schema4/4; complete local Release746/746 with actual disposable MariaDB10.5.23 and loopback Whisper, no skips; pending-model check passes; generated SQL remains nullable hash ADD only. No new migration, hosting SQL or migration-history mutation.

**Commit**: `fix(db): restore import hash migration target metadata`.

**Notes**: Discovery attributes remain in the original partial; do not duplicate them in the Designer. Production gate stays false.

### Acceptance discovery, bootstrap SQL mode and preflight ownership — 2026-10-31

**Symptom**: Live ASR discovery performed synchronous HTTP and required an ignored WAV file. BackupClientPath accepted any path containing .tmp without ownership and could enter the enabled release path. Bootstrap escaped backslashes but assumed the server SQL mode.

**Root Cause**: Network availability was used to choose skips during discovery; local filenames and a path substring substituted for explicit test configuration/ownership. SQL escaping lacked an invocation-specific mode contract.

**Solution**: Explicit loopback-only ASR opt-in with offline validation; generate synthetic PCM silence in memory and fail opted-in engine failures. Restrict backup-client override to explicit ValidateOnly under this workspace's physical .tmp with ownership marker; reject before state writes and never override enabled production deployment. Establish backslash mode per bootstrap client session without changing global mode. Finish the new owner's worker phases before asserting stale-token checkpoint preservation.

**Files Changed**:
- `AsrLiveAcceptanceFactAttribute.cs`, `AsrLiveAcceptanceConfigurationTests.cs`, `WhisperLiveSilenceAcceptanceTests.cs`.
- `scripts/deploy-plesk.ps1`, `scripts/tests/test_deploy_tools.py`.
- `scripts/bootstrap-mariadb-acceptance.ps1`, bootstrap script tests, MariaDB notification acceptance tests.

**Verification**: Red→green staging/discovery/mode regressions. Full local Release745/745 with real disposable MariaDB and loopback whisper, no skips. Three original passwords logged in on a disposable server defaulting to NO_BACKSLASH_ESCAPES; global mode stayed unchanged. Final script/tooling counts are recorded in the closeout report/PR.

**Commit**: Grouped closeout commits in the owner-authorized PR; hosting release excluded.

**Notes**: Preflight is not an enabled staging release/restore drill. Production gate remains false; broader UI/import/outbox/app-ASR acceptance remains separately recorded, not inferred from this suite.

### S01 sentinel + MariaDB dual-completion deadlock — 2026-10-30

**Symptom**: Acceptance factory stamped sentinel before verify. Dual speaking completion on MariaDB deadlocked (`1213`) or double-applied daily XP. SQLite `Progress_CreditInsertFailure` returned OK because RAISE FAIL was misread as credit-key replay.

**Root Cause**: Ensure/UPSERT before identity check; concurrent credit+daily insert lock order; `IsDuplicateCreditKey` treated any SqliteErrorCode 19 whose message mentioned `CreditKey` as a unique replay.

**Solution**: Verify-before-write fixture + labeled bootstrap INSERT; retry deadlock/lock-wait and recover daily increment; require UNIQUE/Duplicate signal for credit-key replay.

**Files Changed**:
- `MariaDbAcceptanceConnectionGuard.cs`, `MariaDbAcceptanceFactory.cs`, orchestration/recording tests, `bootstrap-mariadb-acceptance.ps1`
- `CompletionCreditService.cs`, `GoalsService.cs`, `MariaDbCompletionConcurrencyTests.cs`

**Verification**: Orchestration + dual-completion ×5 on `:3308`; full Debug suite 718/718 with `TCT_ACCEPTANCE_*` set; whisper-server tiny.en E2E; `:5110` UI matrix.

**Commit**: pending sentinel group commits

**Notes**: G06 enabled staging still needs Plesk `mariadb-dump`; no production gate/merge.

### Final acceptance round 2 CR01–CR05 — 2026-10-30

**Symptom**: Writing still accepted `<5` vs `>5` and `1,000` vs `1.000`. MariaDB acceptance factory could DELETE after any env string. Dual-completion Barrier timed out before the second worker started. Publisher reused releases without tag SHA/digest proof and treated same-commit old run markers as ready.

**Root Cause**: Separator collapsing and incomplete meaning-bearing allowlist; offline identity guards missing; sync Barrier misuse inside async Task.WhenAll; soft digest checks and commit-only readiness.

**Solution**: Keep compare operators and distinct numeric separators; parse/validate loopback acceptance DB + sentinel before DML; async TCS rendezvous with overlap/replay/XP asserts; resolve tag SHA and require digest/bytes before webhook; readiness needs commit+run+digest with ambiguous-webhook reconcile.

**Files Changed**:
- `WritingAssessmentPolicy.cs`, MariaDB guard/factory/tests, `publish-plesk-release.py`, `deploy-plesk.ps1`, deploy tests

**Verification**: Focused CR tests green; MariaDB concurrency on :3308; Debug 710 pass/1 skip; tiny.en SHA verified; ASR E2E blocked (no server binary).

**Commit**: pending round2 group commits

**Notes**: Does not enable production gate.

### Final acceptance REV01–REV04 — 2026-10-30

**Symptom**: Writing auto-passed answers that only differed by minus/currency/percent after punctuation stripping. Public `/tct-release.json` 404'd because deploy wrote the marker outside wwwroot. Whisper HTTP responses with wrong JSON types threw or looked like no speech. Publisher reruns died recreating an existing release tag.

**Root Cause**: `NormalizeForComparison` replaced every non-letter/digit with spaces. Deploy wrote `tct-release.json` at the site content root while static files only serve wwwroot. `MapVerboseJson` called `GetString`/`TryGetDouble` without ValueKind checks and treated `{}`/error objects as silence. `publish-plesk-release.py` always ran `gh release create`.

**Solution**: Keep meaning-bearing symbols and decimal separators; map Unicode minus to ASCII. Write/promote the marker under wwwroot after readiness, with no-store headers via static-file prepare + narrow endpoint. Validate ASR schema into `invalid_schema`. Reuse matching release tags by SHA/digest without clobber.

**Files Changed**:
- `TCTEnglish/Services/Writing/WritingAssessmentPolicy.cs` — meaning-preserving normalize
- `TCTEnglish/Services/Speaking/WhisperHttpTranscriber.cs` — schema validation
- `TCTEnglish/Program.cs` — marker Cache-Control path
- `scripts/deploy-plesk.ps1` — wwwroot pending→promote marker
- `scripts/publish-plesk-release.py` — idempotent release reuse

**Verification**: Debug suite 695/695; focused REV regressions; MariaDB dual-completion on isolated container port 3308; Python deploy/idempotency tests; Node behavior tests. Real ASR engine, browser 25-view matrix, and production release remain Not run/Blocked.

**Commit**: pending feature-branch group commits

**Notes**: Does not authorize Plesk gate enablement or production deploy.

### Learning reliability round 2 — RR01–RR07 and test fixture — 2026-10-30

**Symptom**: A failed speaking import could not be submitted again because the unique fingerprint rejected a new row. One duplicate notification in a batch dropped the rest and the job was still marked succeeded. Changing a speaking sentence left the PCM recorder running. ASR timeout started only after a queue slot was acquired. Touching an old streak record was announced as a new record. Writing cloud feedback could run outside the 3-second optional budget. The AI conflict test and SQLite factory could hang or fail during init/dispose.

**Root Cause**: Import retry inserted a second row for the same owner and video. The notification worker treated every database error as a duplicate, detached the unsaved batch, and advanced a cursor that was not fenced by the lease token. The practice page cancelled Web Speech only. The transcriber created its timeout after `AcquireAsync`. The outbox writer inferred a record from `Streak == LongestStreak` and recent badge timestamps. Cloud enhancement used its own timeout before the shared grammar/model budget. The conflict test waited on a fixed delay and released its gate only on the success path. The SQLite fixture called `EnsureDeleted` on one shared in-memory connection.

**Solution**: Requeue the existing import row, keep the unique index, and publish the video, sentences and job result in one transaction. Notification batches pre-check event keys, commit the cursor only with a matching lease token, and process one batch per poll. The practice page disposes the PCM recorder and aborts the in-flight assessment when the sentence changes. Queue wait and HTTP share one deadline. Streak and badge intents use the transition captured by the operation. Cloud, grammar and the local model share one 3-second budget, and engine payloads must match the expected schema. The conflict test waits until the provider is entered and always releases the gate. Each test factory keeps a shared-cache SQLite database open without `EnsureDeleted`.

**Files Changed**:
- `TCTEnglish/Services/Speaking/SpeakingImportJobService.cs` — retry and unique conflict
- `TCTEnglish/Workers/SpeakingImportWorker.cs` — lease and atomic publish
- `TCTEnglish/Workers/NotificationWorker.cs` — checkpoint, lease and batch
- `TCTEnglish/wwwroot/js/speaking.js` — recorder/HTTP cancel and import polling
- `TCTEnglish/Services/Speaking/WhisperHttpTranscriber.cs` — shared deadline
- `TCTEnglish/Services/Learning/LearningNotificationIntentWriter.cs` — transition metadata
- `TCTEnglish/Services/WritingService.cs` — shared optional budget
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` — SQLite lifetime
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` — conflict gate

**Verification**: Focused .NET regressions for import, notifications, inference, transitions, writing and the AI conflict test passed. The conflict test passed on five separate runs. Node recorder, speaking and import tests passed. Migration `20261030113000_LearningImportSourceHash` was not applied to a real database. MySQL, browser microphone, whisper.cpp, LanguageTool and Ollama were not run.

**Commit**: not committed in this session

**Notes**: ASR-for-import stays deferred. The process speech limiter is not a cluster cap. SQLite results are not MySQL concurrency proof.

### Team setup timed out during CodeGraph package extraction — 2026-10-29

**Symptom**: A simulated fresh Windows checkout installed pinned uv/Python, then
stopped at the 180-second npm deadline while restoring CodeGraph.

**Root Cause**: The initial installer deadline was too short for that observed
package extraction. The npm log showed cache hits and an incomplete bundle
(446 entries versus 1,381 in a completed bundle), without a completed install.
The exact host I/O slowdown was not established; a later clean run was faster.

**Solution**: Allow up to 600 seconds for npm and bound concurrent setup waiting
accordingly. A timeout is reported as incomplete and retryable; no completed
fingerprint is written after failure. Keep exact pins, integrity and no scripts.

**Files Changed**:
- `scripts/agent-tools.cjs` — bounded installation/wait timeouts and retry message.
- `scripts/tests/test_agent_setup.cjs` — failure/retry, concurrency and interrupted
  lock recovery checks alongside archive integrity and path boundary regressions.

**Verification**: The interrupted fixture completed on retry. A second clean
checkout without uv in PATH completed setup in 30.1 seconds, then repeated it
offline without downloading/restoring dependencies. No CodeGraph index was made.

**Commit**: Not created.

**Notes**: The measured setup time is one Windows run, not a performance promise
for teammates. Native MCP timeouts can still require retry after first setup.

### CodeGraph smoke check treated an empty WAL as an index modification — 2026-10-29

**Symptom**: A successful read-only MCP source query intermittently failed the
physical database/WAL fingerprint check.

**Root Cause**: The smoke check compared sidecar presence as well as data. A
read-only SQLite connection can create an empty WAL and shared-memory sidecar
without changing the main database. A standalone SQLite WAL fixture reproduced
the absent-to-empty transition while the main database SHA-256 stayed identical.

**Solution**: Normalize absent/empty WAL in the shared fingerprint helper. Keep
hashing the main database and every nonempty WAL; shared-memory locks are excluded.
The CodeGraph read-only engine configuration was retained.

**Files Changed**:
- `scripts/agent_tooling.py` — add sqlite_data_fingerprint.
- `scripts/check_codegraph_mcp.py` — use the data-bearing file fingerprint.
- `scripts/tests/test_codegraph_tooling.py` — real read-only SQLite fixture and
  negative cases for changed main database/nonempty WAL contents.

**Verification**: Tooling tests and actual CodeGraph MCP initialize/list/explore
checks pass after normalization. No index/sync/rebuild command was run.

**Commit**: Not created.

**Notes**: This corrects a verification false positive, not a TCT application
database defect. It does not disable checking data-bearing WAL content.

### Git MCP smoke check used the old SDK result property — 2026-10-28

**Symptom**: The new tooling smoke check initialized MCP and called git_status,
then raised AttributeError while inspecting the result.

**Root Cause**: The pinned MCP Python SDK 2.3.0 exposes CallToolResult.is_error;
the initial smoke check used the older wire-style isError property.

**Solution**: Read the installed SDK implementation and use is_error in the
dedicated tooling smoke check. No application code or dependency was changed.

**Files Changed**:
- `scripts/check_git_mcp.py` — use the actual SDK result property.

**Verification**: The configured PowerShell launcher, MCP initialization,
tool discovery and git_status all passed; seven configured read tools were found.

**Commit**: Not created.

**Notes**: This was found and corrected during the agent-tooling installation.
It is unrelated to the two existing .NET regression failures recorded in
docs/agent-tooling.md.

### Listening cards from Admin did not show thumbnail on Study/Listening - 2026-05-10

**Symptom**: On the Listening page, lessons imported from YouTube in "My Lessons" showed an image thumbnail, but lessons created from Admin with the same YouTube link rendered without image in the public level cards.

**Root Cause**: Public level cards in `Views/Study/Listening.cshtml` rendered a static icon block (`li-card-visual`) instead of binding to `ThumbnailUrl`. In addition, Admin create/edit used `model.ThumbnailUrl ?? ...` fallback logic, so whitespace values could bypass the YouTube thumbnail fallback and persist an empty thumbnail.

**Solution**: Updated the listening card view model/service mapping to include `YoutubeId` for robust fallback rendering. Changed public level card UI to render real thumbnail images with fallback to YouTube thumbnail (`img.youtube.com`) and then placeholder image. Updated Admin create/edit thumbnail assignment to treat null/whitespace as empty and apply the YouTube fallback URL.

**Files Changed**:
- `TCTEnglish/ViewModels/ListeningViewModels.cs` - added `YoutubeId` to `ListeningLessonCardViewModel`.
- `TCTEnglish/Services/ListeningService.cs` - mapped `YoutubeId` for both published lessons and user-owned lessons.
- `TCTEnglish/Views/Study/Listening.cshtml` - rendered thumbnail images for level cards and improved fallback logic for "My Lessons".
- `TCTEnglish/wwwroot/css/listening-index.css` - added thumbnail styles for level cards and adjusted status badge positioning.
- `TCTEnglish/Areas/Admin/Controllers/ListeningManagementController.cs` - fixed thumbnail fallback logic for whitespace input on create/update.

**Verification**: `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore` succeeded (0 errors, existing warnings unchanged).

**Commit**: Not created.

**Notes**: This fix keeps existing ownership/auth boundaries unchanged and only affects thumbnail mapping/rendering behavior.

### ListeningPractice Razor tab markup mismatch and extra closing tokens - 2026-05-10

**Symptom**: The Listening practice page tab area rendered with broken DOM around the Quiz tab, causing malformed markup and Razor parse instability before CSS redesign.

**Root Cause**: `Views/Study/ListeningPractice.cshtml` contained stray markup in the `.lp-tabs` block: an extra Razor closing brace and duplicate `</button>`, plus an additional stray `}` and closing `</div>` after the quiz panel block.

**Solution**: Removed the stray closing brace/button after the Quiz tab and removed the extra trailing `}`/`</div>` after the tab panels, keeping only two tab buttons (`Transcript`, `Quiz`) and preserving existing tab/panel CSS/JS class contracts.

**Files Changed**:
- `TCTEnglish/Views/Study/ListeningPractice.cshtml` — cleaned malformed tab/button/panel closing markup around `.lp-tabs` and quiz panel close.

**Verification**: `dotnet build xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln` succeeded after the fix (no Razor errors).

**Commit**: Not created.

**Notes**: No backend logic, data models, migrations, appsettings, Program.cs, or csproj files were changed.

### Dashboard goal integration test brittle regex failure - 2026-05-09

**Symptom**: CI/CD pipeline failed with `Assert.Matches() Failure: Pattern not found in value` for the `Dashboard_RendersGoalFromUserState` test.

**Root Cause**: The integration test used a brittle regex `18\s+t[^\s<]*\s*<br>\s*c[^\s<]*n` that was sensitive to exact whitespace and HTML tag formatting (e.g., `<br>` vs `<br />`). Differences in the rendering environment (GitHub Actions vs local) caused the match to fail.

**Solution**: Updated the regex in `TCTEnglish.Tests/GoalsPhase1IntegrationTests.cs` to be more robust by allowing optional whitespace and variations of the `<br>` tag (using `<br\s*/?>`). Added `RegexOptions.Singleline` to ensure the regex handles potential newline characters correctly.

**Files Changed**:
- `TCTEnglish.Tests/GoalsPhase1IntegrationTests.cs` — line 184: updated dashboard goal regex assertion.

**Verification**: `dotnet test --filter GoalsPhase1IntegrationTests.Dashboard_RendersGoalFromUserState` passed locally. Full phase 1 goals suite passed (7/7).

**Commit**: `fix(tests): harden dashboard goal integration test regex`

**Notes**: This follows the pattern of keeping test contracts aligned with UI refresh markup. No changes were made to the `HomeController` or `Index.cshtml` view.


### Dashboard quick links and metrics drift after UI refresh - 2026-05-06

**Symptom**: New dashboard quick-access buttons pointed to non-existent actions (`/Home/Flashcard`, `/Home/Quiz`), memory/heatmap widgets showed placeholder numbers, and a prototype preview file under `wwwroot` risked being publicly served.

**Root Cause**: UI refresh introduced route targets not backed by `StudyController` actions, while chart values were hard-coded/randomized in the Razor view instead of using persisted user progress/activity data. The temporary preview artifact was committed under deployable static assets.

**Solution**: Repointed quick links to valid routes, added real dashboard progress/heatmap fields to `DashboardViewModel`, populated those fields from `LearningProgress` and `UserDailyActivities` in `HomeController`, replaced chart data sources in `Views/Home/Index.cshtml`, removed the preview file from `wwwroot`, and updated dashboard regression assertion to match the new markup contract.

**Files Changed**:
- `TCTEnglish/ViewModels/DashboardViewModel.cs` - added mastered/learning/new counters and heatmap cell view-model.
- `TCTEnglish/Controllers/HomeController.cs` - computed real progress counters and 4-week heatmap data for the authenticated user.
- `TCTEnglish/Views/Home/Index.cshtml` - switched quick links to valid actions and bound radial/heatmap charts to server-provided data.
- `TCTEnglish/wwwroot/dashboard-preview.html` - removed prototype file from public static root.
- `TCTEnglish.Tests/GoalsPhase1IntegrationTests.cs` - updated dashboard goal assertion for the refreshed UI markup.

**Verification**: `dotnet build TCTEnglish/TCTEnglish.csproj` and targeted dashboard goals tests (phase 1) after view/controller updates.

**Commit**: Not created.

**Notes**: This fix follows the same goals/dashboard contract hardening pattern used in previous entries by keeping dashboard values model-driven instead of rendering placeholder client values.

### Class detail tab state was lost after member/folder actions - 2026-05-03

**Symptom**: After actions such as adding/removing folders or managing members from `ClassDetail`, the page refreshed back to the chat tab instead of returning to the current working tab, interrupting the management flow.

**Root Cause**: The client code always called `location.reload()` after mutations and did not persist the active tab in the URL or browser state.

**Solution**: Reworked the class-detail page script to persist the active tab in both query string and `sessionStorage`, then reload the page back into the relevant tab after folder/member/request actions. During the same pass, the `ClassDetail` UI was cleaned up to match the app layout more closely with calmer controls, stronger spacing hierarchy, and less decorative icon usage.

**Files Changed**:
- `TCTEnglish/Views/Class/ClassDetail.cshtml` — rebuilt the page structure with cleaner sections and tab buttons that expose stable `data-tab` values.
- `TCTEnglish/wwwroot/js/Class-Detail.js` — added tab persistence helpers and routed post-action reloads back to the relevant tab.
- `TCTEnglish/wwwroot/css/Class-Detail.css` — redesigned the page surface, tabs, cards, chat area, lists, and modals to align with the surrounding product layout.

**Verification**: Automated verification not run in this environment. The local workspace currently has `dotnet` validation constraints from file locks / sandboxed user-profile access, so this pass was verified by code inspection only.

**Commit**: Not created.

**Notes**: This update intentionally avoids introducing new routing or DI changes and keeps the same controller/view-model contracts while improving the page shell and preserving in-page context after management actions.

### Class join-request modal accept/decline did not complete approval flow - 2026-05-03

**Symptom**: In `ClassDetail.cshtml`, clicking `Accept` or `Decline` in the join-request modal did not successfully process pending requests to join a class.

**Root Cause**: The browser code hardcoded join-request endpoints instead of using controller-generated URLs, and the service layer only allowed the class owner/admin to process requests even though the UI exposed the modal to assistants too.

**Solution**: Wired the modal actions to URL values generated by `Url.Action(...)`, updated the class join-request service methods to only act on pending requests, preserve request history by marking requests as `Approved`/`Declined` instead of deleting them, and aligned backend authorization with the UI by allowing assistants to process pending requests. Added regression coverage for owner-approve and assistant-decline flows.

**Files Changed**:
- `TCTEnglish/Views/Class/ClassDetail.cshtml` — exposed approve/decline URLs to the page script.
- `TCTEnglish/wwwroot/js/Class-Detail.js` — switched modal fetch calls from hardcoded paths to server-generated endpoints.
- `TCTEnglish/Services/ClassService.cs` — limited processing to pending requests, allowed assistants to manage requests, and updated request status instead of removing rows.
- `TCTEnglish.Tests/Infrastructure/TestDataIds.cs` — added a seeded assistant user id for regression coverage.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` — seeded an assistant user for class-request tests.
- `TCTEnglish.Tests/Sprint2SmokeTests.cs` — added regression tests for owner approval and assistant decline of pending join requests.

**Verification**: Attempted `dotnet build TCTEnglish\TCTEnglish.csproj --no-restore` and `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~Sprint2SmokeTests.JoinRequest_"` with `DOTNET_CLI_HOME` redirected into the workspace. Verification was blocked by the local environment because the active app process was locking `TCTEnglish.dll` / `TCTEnglish.exe`, and sandboxed restore could not read the user-level `NuGet.Config`.

**Commit**: Not created.

**Notes**: This fix intentionally keeps join-request rows for status history instead of deleting them, so future admin/audit UI can distinguish approved vs declined requests. No `known-issues.md` update was needed because this bug was not listed there.

### AI chatbox closeout Phase 6 encoding guard script - 2026-04-21

**Symptom**: UTF-8/mojibake validation for Vietnamese AI chat assets was manual and inconsistent, depending on terminal display behavior.

**Root Cause**: The repository referenced `scripts/encoding_guard.py`, but the script did not exist, so there was no repeatable encoding guard.

**Solution**: Added a repo-local `scripts/encoding_guard.py` that scans key AI chat and guide files for invalid UTF-8 bytes, replacement characters, and common mojibake markers. The script targets the files required by the closeout phase and exits non-zero on failures.

**Files Changed**:
- `scripts/encoding_guard.py` — added a UTF-8/mojibake guard for AI chat assets and the bug-fix log.
- `.ai/context/bug-fix-log.md` — appended this closeout entry.

**Verification**: `python scripts/encoding_guard.py`

**Commit**: Not created.

**Notes**: The guard checks for invalid UTF-8 bytes, replacement characters, and mojibake markers including `U+00C3`, `U+00C4`, `U+00C2`, sequences like `U+00E2 0x80 0x99` / `U+00E2 0x80 0x9C` / `U+00E2 0x80 0x9D`, and patterns like `U+00C2 U+00A0`.

### AI chat quick-action recovery and form-scoped antiforgery token - 2026-04-21

**Symptom**: Quick-action buttons were hidden after click even when the AI request failed, and the send path could read the wrong antiforgery token when multiple forms existed on the page.

**Root Cause**: The client code accepted and hid quick actions before request success, and the AJAX send logic read the antiforgery token from the document instead of the active chat form.

**Solution**: Delayed quick-action acceptance until a successful response, restored quick actions after failed sends when no conversation was created, and scoped the antiforgery token lookup to the chat form. Also removed an inline badge style and clarified the Reading quick-action label.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js` — defer quick-action acceptance, restore on failed new conversations, and use form-scoped antiforgery tokens.
- `TCTEnglish/Views/Ai/_ChatShell.cshtml` — remove inline badge style and update Reading quick-action label.
- `TCTEnglish/wwwroot/css/ai-chat.css` — add `ai-chat-plan-badge` styling.

**Verification**: Not run (manual/browser smoke testing pending in this phase).

**Commit**: Not created.

**Notes**: The quick-action restore triggers only when a new conversation was not created, keeping existing-thread behavior unchanged.

### AI chatbox closeout Phase 4 production classifier guardrails and coverage - 2026-04-20

**Symptom**: Production DI uses `MlNetAiQueryClassifier` when the model artifact is present, but there was no coverage proving quick-action prompts and out-of-scope refusals behave correctly with the shipped model. A stale or weak ML.NET prediction could override deterministic guide/out-of-scope safeguards.

**Root Cause**: Existing send-path tests forced the deterministic classifier and ML.NET runtime tests used synthetic models, so the real model artifact plus default DI path were not verified and guardrails between ML and deterministic intents were not explicit.

**Solution**: Added deterministic guardrails in `MlNetAiQueryClassifier` so guide/study-recommendation/out-of-scope intents override weak or mismatched ML predictions. Added production classifier integration tests that exercise the default DI path for quick-action guide prompts, study recommendation, and out-of-scope refusals. Added a stability check that asserts the model and seed dataset exist and match expected SHA-256 hashes.

**Files Changed**:
- `TCTEnglish/Services/AI/Internal/MlNetAiQueryClassifier.cs` — prefer deterministic intent for guide/study-recommendation/out-of-scope when ML predictions are weak or disagree.
- `TCTEnglish.Tests/AiProductionClassifierIntegrationTests.cs` — new integration coverage for production classifier path and model artifact stability.

**Verification**: `run_tests` with `TypeName=TCTEnglish.Tests.AiProductionClassifierIntegrationTests`.

**Commit**: Not created.

**Notes**: The new tests confirm the shipped model artifact is present and aligned with the seed data hash; update the hash expectations if the model is retrained intentionally.

### AI chatbox closeout Phase 3 study recommendation accuracy and arbitration - 2026-04-20

**Symptom**: Study recommendation answers could undercount remaining cards when a set had unstarted cards, goal remaining could ignore daily activity counts, and the chosen recommendation depended on retriever registration order.

**Root Cause**: `LearningProgressRetriever` computed remaining counts only from progress rows, `StudyRecommendationRetriever` used a hardcoded `cardsMetToday = 0`, and `TemplateAnswerComposer` selected the first recommendation snippet instead of a deterministic priority-based choice.

**Solution**: Count total cards per owned set in `LearningProgressRetriever` to include unstarted cards, use `UserDailyActivities.CardsReviewed` for goal remaining in `StudyRecommendationRetriever`, and select study recommendations by priority (then remaining count) in the composer. Added targeted tests for partial progress, goal remaining, and priority selection.

**Files Changed**:
- `TCTEnglish/Services/AI/Internal/Retrievers/LearningProgressRetriever.cs` — compute remaining counts from owned set totals and set higher recommendation priority.
- `TCTEnglish/Services/AI/Internal/Retrievers/StudyRecommendationRetriever.cs` — use daily activity cards reviewed for goal remaining.
- `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` — select recommendations deterministically by priority.
- `TCTEnglish.Tests/LearningProgressRetrieverTests.cs` — cover partial-progress remaining counts.
- `TCTEnglish.Tests/StudyRecommendationRetrieverTests.cs` — cover owned-set/no-progress recommendation and daily goal remaining.
- `TCTEnglish.Tests/TemplateAnswerComposerTests.cs` — cover priority-based recommendation selection.
- `TCTEnglish.Tests/AiDeterministicBaselineIntegrationTests.cs` — align remaining count expectation for progress-based recommendations.

**Verification**: `run_tests` with `TypeName=TCTEnglish.Tests.LearningProgressRetrieverTests`, `TypeName=TCTEnglish.Tests.StudyRecommendationRetrieverTests`, `TypeName=TCTEnglish.Tests.TemplateAnswerComposerTests`, and `TypeName=TCTEnglish.Tests.AiDeterministicBaselineIntegrationTests`.

**Commit**: Not created.

**Notes**: Progress-summary behavior remains unchanged; study recommendation arbitration now favors the explicit priority contract rather than DI order.

### AI chatbox closeout Phase 2 website guide route contracts - 2026-04-20

**Symptom**: Website guide answers could surface placeholder template routes (for example `/Home/EditSet/{id}` or `/Speaking/Practice/{videoId}`) as direct URLs, which are not navigable in real use and could confuse learners.

**Root Cause**: `website-guides.json` still contained route templates for set study, class detail, and speaking practice, while `TemplateAnswerComposer` blindly appended any non-empty route string to the response.

**Solution**: Replaced all template routes in `website-guides.json` with concrete landing routes (for example `/Home/Folder`, `/Home/Class`, `/Speaking`) and updated guide copy to match the new entry points. Added a guard in `TemplateAnswerComposer` to skip route rendering if a placeholder is present. Added tests to prevent template routes in the guide data, verify that all guide routes resolve against the app endpoints, and confirm the composer does not render template routes.

**Files Changed**:
- `TCTEnglish/wwwroot/data/ai/website-guides.json` — replaced template routes with concrete landing pages and aligned guide copy.
- `TCTEnglish/Services/AI/Internal/TemplateAnswerComposer.cs` — skipped rendering template routes.
- `TCTEnglish.Tests/WebsiteGuideRetrieverTests.cs` — updated route expectations and added guide-route placeholder guard.
- `TCTEnglish.Tests/TemplateAnswerComposerTests.cs` — added template-route suppression regression.
- `TCTEnglish.Tests/AiDeterministicBaselineIntegrationTests.cs` — updated class-chat expected route and added route-resolution coverage for all guides.

**Verification**: `python -c "import codecs; paths=['TCTEnglish/wwwroot/data/ai/website-guides.json']; [codecs.open(p,'r','utf-8').read() for p in paths]; print('utf8-ok')"` (utf8-ok). `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests"`.

**Commit**: Not created.

**Notes**: `scripts/encoding_guard.py` is not present in this repository; a UTF-8 read check was used instead. Guide routes were normalized to concrete landing pages rather than adding new controller actions.

### HomeIndex SQLite daily challenge wrong-answer randomization fails - 2026-04-20

**Symptom**: `/Home/Index` returned HTTP 500 in the SQLite test host during the AI launcher integration tests that render the dashboard with the daily challenge.

**Root Cause**: `HomeController.GetSystemWrongAnswersAsync()` used `OrderBy(c => Guid.NewGuid())` directly in the EF query, which SQLite cannot translate.

**Solution**: Added a SQL Server-only `Guid.NewGuid()` randomization path and reused the provider-safe random id/offset pattern (already used by `GetTodayFoldersAsync()`) for non-SQL Server providers.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` — guarded daily-challenge wrong-answer randomization for SQLite using provider-safe random IDs.

**Verification**: `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName~HomeIndex"`

**Commit**: Not created.

**Notes**: The same SQL Server behavior is preserved; only the SQLite-safe path is new.

### AI chatbox hardening Phase 6 answer-quality guide expansion - 2026-04-20

**Symptom**: After the core AI chatbox hardening fixes, website-guide answers still had several quality gaps: high-value learner topics such as Goals, Daily Challenge, class chat, and folder organization were missing or weakly covered, and several older guide routes still pointed at pre-refactor controller paths.

**Root Cause**: `website-guides.json` had grown across multiple refactor phases without a full answer-quality pass over post-refactor routes. `WebsiteGuideRetriever` scored all keyword phrase matches equally, so broad one-word matches could compete too closely with more specific multi-word guide topics. Deterministic guide fallback also did not explicitly recognize some platform terms introduced in the expanded guide coverage.

**Solution**: Corrected stale guide routes to current post-refactor routes, added concise Vietnamese guides for Goals, Daily Challenge, class chat, and organizing sets in folders, expanded deterministic website-guide feature keywords for those platform topics, and improved guide scoring by rewarding specific keyword phrase matches plus topic matches.

**Files Changed**:
- `TCTEnglish/wwwroot/data/ai/website-guides.json` - corrected stale guide routes and added Phase 6 guide entries for Goals, Daily Challenge, class chat, and folder organization.
- `TCTEnglish/Services/AI/Internal/Retrievers/WebsiteGuideRetriever.cs` - added topic-aware scoring and a more specific keyword-match score.
- `TCTEnglish/Services/AI/Internal/DeterministicIntentClassifier.cs` - recognized the new platform guide terms while preserving the existing out-of-scope guard.
- `TCTEnglish.Tests/WebsiteGuideRetrieverTests.cs` - covered new guide retrieval, current post-refactor routes, and class-chat ranking.
- `TCTEnglish.Tests/DeterministicIntentClassifierTests.cs` - covered the new Phase 6 guide prompts as `WebsiteGuide`.
- `TCTEnglish.Tests/AiDeterministicBaselineIntegrationTests.cs` - covered the new guide prompts through the deterministic send path.
- `TCTEnglish.Tests/AiBaselineRegressionTests.cs` and `TCTEnglish.Tests/TemplateAnswerComposerTests.cs` - aligned website-guide route expectations with current routes.
- `.ai/context/bug-fix-log.md` - appended this Phase 6 closeout entry.

**Verification**: `TCTEnglish/wwwroot/data/ai/website-guides.json` parsed successfully with `ConvertFrom-Json` and now contains 35 guides. `git diff --check` passed with only Git line-ending warnings. `scripts/encoding_guard.py` is not present. `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests"` passed (120/120). The broader AI regression slice `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~MlNetIntentClassifierAssetResolverTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~MlNetIntentDatasetLoaderTests"` passed (166/166).

**Commit**: Not created.

**Notes**: This phase stayed inside the current internal-knowledge architecture and did not modify `Program.cs`, `appsettings.json`, project files, auth, billing, migrations, or unrelated controllers. `.ai/context/known-issues.md` was not updated because these answer-quality items were not listed as known unresolved bugs. The next verification pass should first re-check route behavior for any future website-guide entries before adding more topics.

### AI chatbox hardening Phase 5 verification closeout - 2026-04-20

**Symptom**: The AI chatbox hardening package needed final closeout after the retriever, deterministic fallback, website guide, quick-action UI, and regression-test phases. Final verification also needed to separate AI hardening results from broader repo test failures that are outside the Phase 5 scope.

**Root Cause**: Earlier phases intentionally left the worktree with AI service, guide, UI, and test changes plus phase bug-log entries. The repository does not currently contain `scripts/encoding_guard.py`, and the broad `FullyQualifiedName~Ai` test filter can match unrelated test names such as `DailyChallenge` and `RemainsHidden`, which obscures the AI-specific signal.

**Solution**: Inspected the final diff, confirmed no `Program.cs`, `appsettings.json`, or project-file changes were introduced by the AI hardening package, checked that the encoding guard script is unavailable, parsed the guide JSON, ran focused AI test slices, and ran the full test project once to document remaining unrelated blockers.

**Files Changed**:
- `.ai/context/bug-fix-log.md` - appended this Phase 5 verification closeout entry.

**Verification**: `Test-Path scripts\encoding_guard.py` returned `False` because the script/folder is unavailable. `git diff --check` passed with only Git line-ending warnings. `TCTEnglish/wwwroot/data/ai/website-guides.json` parsed successfully with `ConvertFrom-Json`. `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~AiContextBuilderTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~AiChatServiceTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~MlNetIntentClassifierAssetResolverTests|FullyQualifiedName~MlNetAiQueryClassifierTests|FullyQualifiedName~MlNetIntentDatasetLoaderTests"` passed (146/146). `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests&FullyQualifiedName!~HomeIndex&FullyQualifiedName!~AiLauncher_"` passed (30/30). A class-focused AI run including HomeIndex launcher checks passed 176 tests and failed 5 launcher/HomeIndex checks due `/Home/Index` returning 500 from the known SQLite `Guid.NewGuid()` translation blocker. Full `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore` ran and failed with 341 passed / 27 failed across dashboard/HomeIndex, goals/speaking/writing, and admin-writing/auth fixtures; these were not fixed in Phase 5.

**Commit**: Not created.

**Notes**: `.ai/context/known-issues.md` was not updated because the fixed AI chat issues are not listed there. Browser smoke testing against a live app was not completed in this phase; integration coverage for `/AI/Chat`, embedded chat, quick-action hooks, guide routes, and send-path behavior passed in the focused AI slices. The next phase should first verify or fix the non-AI `/Home/Index` SQLite `Guid.NewGuid()` blocker before relying on launcher/HomeIndex smoke tests.

### AI Phase 4 regression pack expanded for real send-path composition and guide/out-of-scope coverage - 2026-04-20

**Symptom**: Existing AI regressions were spread across unit/integration files, but the baseline send-path integration suite did not directly lock enough Phase 4 behavior around study recommendation edge cases, quick-action guide routes, and out-of-scope refusals under the real `/AI/Chat/Send` path. The notification guide route also used `/Notification`, which does not resolve with the current default MVC route.

**Root Cause**: `AiDeterministicBaselineIntegrationTests` had limited baseline assertions (vocabulary/guide/class/card) and did not include dedicated integration checks for owned-set/no-progress recommendation behavior, no-set empty state, mastered-status variant effect on remaining counts, quick-action route prompts, real endpoint resolution, or unrelated refusal prompts in one focused phase suite. Since `Program.cs` defaults missing actions to `Landing`, the real notification page route is `/Notification/Index`.

**Solution**: Expanded `AiDeterministicBaselineIntegrationTests` with focused integration coverage for StudyRecommendation (owned set with no progress, no-set empty state, mastered-status variants + non-leakage), quick-action guide prompts with expected current routes, route endpoint resolution, and out-of-scope prompt refusal including homework/random-fact prompts. Added deterministic classifier override in the test host setup for this suite so assertions remain stable and behavior-focused instead of ML-model-artifact dependent. Corrected the notification guide route to `/Notification/Index`.

**Files Changed**:
- `TCTEnglish.Tests/AiDeterministicBaselineIntegrationTests.cs` - added Phase 4-focused integration scenarios, route endpoint resolution checks, homework refusal coverage, and deterministic classifier test-host override helper.
- `TCTEnglish.Tests/DeterministicIntentClassifierTests.cs` - added explicit homework out-of-scope fallback coverage.
- `TCTEnglish.Tests/WebsiteGuideRetrieverTests.cs` - updated notification quick-action route expectation.
- `TCTEnglish/wwwroot/data/ai/website-guides.json` - corrected the notification guide route to the real MVC endpoint.
- `.ai/context/bug-fix-log.md` - updated this incident record.

**Verification**: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiDeterministicBaselineIntegrationTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests"` passed (101/101). `TCTEnglish/wwwroot/data/ai/website-guides.json` parsed successfully with `ConvertFrom-Json`. `scripts/encoding_guard.py` is not present.

**Commit**: Not created.

**Notes**: This Phase 4 pass reuses earlier Phase 1/2 hardening expectations (study recommendation semantics + quick-action guide routing) and anchors them on the integration send path. A broad `FullyQualifiedName~Ai` filter still pulls in unrelated dashboard/writing/speaking tests in this project; that broad run is blocked by pre-existing non-Phase-4 failures around `/Home/Index` SQLite `Guid.NewGuid()` translation and unrelated route/auth cases. Phase 5 should first verify the full intended AI/UI smoke slice after deciding whether to fix those unrelated blockers.

### AI quick actions could double-submit and used global click handling across chat shells - 2026-04-20

**Symptom**: Empty-state quick-action buttons in AI chat used a document-level click listener and immediately hid the quick-action row before submit acceptance. Under rapid clicks, this could race and create duplicate submit attempts, and the listener scope was broader than the initialized chat shell.

**Root Cause**: `ai-chat.js` attached quick-action handling with `document.addEventListener('click', ...)`, so button behavior was not explicitly bound to the active shell/form instance. There was no quick-action pending guard to lock repeated clicks between click and submit-state transition.

**Solution**: Scoped quick-action handling to the initialized chat shell (`[data-ai-chat-shell]` -> `[data-ai-quick-actions]`), added a `quickActionPending` guard plus temporary button disable (`aria-busy`) to block duplicate clicks, and changed the hide timing so quick actions are hidden only after submit is accepted by the form flow. Added compact button wrapping styles for small screens and regression assertions for new quick-action hooks/script guards.

**Files Changed**:
- `TCTEnglish/Views/Ai/_ChatShell.cshtml` - added scoped quick-action data hooks and accessibility group metadata.
- `TCTEnglish/wwwroot/js/ai-chat.js` - replaced global quick-action click handling with shell-scoped handling and duplicate-submit guard.
- `TCTEnglish/wwwroot/css/ai-chat.css` - added minimal quick-action label wrapping behavior for narrow widths.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added/updated assertions for quick-action hooks and script hardening behavior.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `run_build` passed. `run_tests` with `TypeName=TCTEnglish.Tests.DeterministicIntentClassifierTests`, `TypeName=TCTEnglish.Tests.WebsiteGuideRetrieverTests`, and `TypeName=TCTEnglish.Tests.AiBaselineRegressionTests` passed (67/67). `run_tests` with `MethodName=AiChatScript_QuickActions_AreScopedAndGuardedAgainstDuplicateSubmit` passed (1/1). A broader AI phase hardening slice still has pre-existing `/Home/Index` integration failures returning `500` in this workspace.

**Commit**: Not created.

**Notes**: This follows the Phase 2 note to verify quick-action submission behavior across `/AI/Chat` and embedded flows. `scripts/encoding_guard.py` is not present in this repository.

### AI Phase 2 deterministic OutOfScope random-fact regression coverage - 2026-04-20

**Symptom**: Phase 2 deterministic fallback already handled most quick-action and out-of-scope prompts, but random-fact refusal was not explicitly locked by a dedicated classifier regression input.

**Root Cause**: `DeterministicIntentClassifierTests` lacked an explicit random-fact sample even though production fallback contains `random fact` rejection keywords.

**Solution**: Added a direct deterministic classifier regression case (`Tell me a random fact about Mars`) to assert fallback remains `OutOfScope` and prevent accidental future broadening of website-guide matching.

**Files Changed**:
- `TCTEnglish.Tests/DeterministicIntentClassifierTests.cs` - added random-fact out-of-scope inline test data.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiBaselineRegressionTests"` passed (67/67). `website-guides.json` successfully parsed with `ConvertFrom-Json`.

**Commit**: Not created.

**Notes**: Route verification remains aligned with current attribute routing (`StudyController` uses `[Route("Home")]`, so Reading/Writing/Listening valid guide routes stay `/Home/...`). `scripts/encoding_guard.py` is not present in this repository.

### AI quick-action guide fallback and route correction - 2026-04-20

**Symptom**: AI quick-action prompts for Reading, Writing, Listening, support/contact, notifications, and overview/navigation could fall through deterministic fallback as `OutOfScope` unless an ML.NET model had already been retrained and loaded. Some guide answers also pointed learners to stale `/Study/*List` routes that do not exist in the current attribute-routed app.

**Root Cause**: `DeterministicIntentClassifier.IsWebsiteGuide` only recognized a narrow set of guide verbs and feature keywords, missing common quick-action phrasing such as "Làm bài Reading như thế nào?", "Tính năng Writing hoạt động ra sao?", and notification/support questions. `website-guides.json` still carried the pre-route-verification Reading/Writing/Listening URLs, and notification/overview wording was grouped into the contact-support guide instead of having route-specific guide entries.

**Solution**: Expanded deterministic website-guide fallback keywords for Reading, Writing, Listening, contact/support, notifications, and overview/navigation prompts while adding an explicit guard for clearly unrelated homework, translation, grammar, and random-fact requests. Corrected Reading/Writing/Listening guide routes to the verified legacy `/Home/...` paths, added route-specific notification and overview/navigation guide entries, and added focused classifier/retriever regressions for the Phase 2 quick-action prompts.

**Files Changed**:
- `TCTEnglish/Services/AI/Internal/DeterministicIntentClassifier.cs` - recognizes Phase 2 quick-action guide phrasing and preserves out-of-scope protection for unrelated homework/translation/grammar prompts.
- `TCTEnglish/wwwroot/data/ai/website-guides.json` - corrects Reading/Writing/Listening routes and adds notification plus overview/navigation guide entries.
- `TCTEnglish.Tests/DeterministicIntentClassifierTests.cs` - covers required quick-action prompts as `WebsiteGuide` and additional unrelated prompts as `OutOfScope`.
- `TCTEnglish.Tests/WebsiteGuideRetrieverTests.cs` - covers expected guide routes for Reading, Writing, Listening, support/contact, notifications, and overview/navigation prompts.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~AiBaselineRegressionTests"` passed (66/66) with `DOTNET_CLI_HOME` redirected to the workspace. `website-guides.json` parsed successfully with `ConvertFrom-Json`. `git diff --check` passed for the Phase 2 touched files with only Windows line-ending warnings. `scripts/encoding_guard.py` was not present.

**Commit**: Not created.

**Notes**: `Program.cs`, `appsettings.json`, and project files were not changed. `.ai/context/known-issues.md` was not updated because this AI quick-action fallback issue was not listed there. Phase 3 should first verify quick-action UI submission behavior in both `/AI/Chat` and the embedded launcher so each click sends exactly one message.

### AI StudyRecommendation fallback unreachable and mastered counts incorrect - 2026-04-20

**Symptom**: AI study recommendation could return the empty-state answer for users who already had owned vocabulary sets but had not generated any learning progress yet. The fallback recommendation path also treated only exact lowercase `mastered` progress as mastered, inflating remaining-card counts for statuses such as `Mastered` and `Learned`.

**Root Cause**: `InternalKnowledgeProvider` selected only the first retriever whose `CanHandle` matched the classified intent. `LearningProgressRetriever` also handles `StudyRecommendation` and is registered before `StudyRecommendationRetriever`, so the set-based fallback retriever was unreachable in normal provider composition. In `StudyRecommendationRetriever`, mastered counting used a case-sensitive `p.Status == "mastered"` predicate.

**Solution**: Updated `InternalKnowledgeProvider` to aggregate snippets from all matching retrievers in registration order, preserving progress-based recommendations before the set fallback. Reworked `StudyRecommendationRetriever` to read owned sets with `OwnerId == userId`, read progress with `UserId == userId`, join through owned cards/sets, and count `Mastered`, `mastered`, and `Learned` case-insensitively. Tightened `LearningProgressRetriever` set joins to owned sets so progress rows cannot surface another user's set in StudyRecommendation output.

**Files Changed**:
- `TCTEnglish/Services/AI/Internal/InternalKnowledgeProvider.cs` - aggregates all matching retriever snippets instead of stopping at the first match.
- `TCTEnglish/Services/AI/Internal/Retrievers/LearningProgressRetriever.cs` - scopes progress set joins to owned sets.
- `TCTEnglish/Services/AI/Internal/Retrievers/StudyRecommendationRetriever.cs` - scopes fallback set/progress reads and fixes mastered-status semantics.
- `TCTEnglish.Tests/AiBaselineRegressionTests.cs` - covers progress recommendation precedence, set fallback with no progress, no-set empty state, and cross-user non-leakage.
- `TCTEnglish.Tests/InternalKnowledgeProviderTests.cs` - covers aggregation order for multiple matching retrievers.
- `TCTEnglish.Tests/StudyRecommendationRetrieverTests.cs` - covers mastered status variants and fallback ownership/progress isolation.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `dotnet test TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~AiBaselineRegressionTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~StudyRecommendationRetrieverTests"` passed (38/38) with `DOTNET_CLI_HOME` redirected to the workspace. `git diff --check` passed with only Windows line-ending warnings. `scripts/encoding_guard.py` was not present.

**Commit**: Not created.

**Notes**: `Program.cs` was not changed. `.ai/context/known-issues.md` was not updated because these AI StudyRecommendation findings were not listed there. Phase 2 should first verify deterministic quick-action fallback classification and the real Reading/Writing/Listening/support/notification guide routes.

### CI/CD test suite 14 failures across 5 root cause groups — 2026-04-30

**Symptom**: CI/CD pipeline `dotnet test` reported 14 failures out of 449 total tests. Failures spanned billing order code format, speaking route 404s, DI constructor regression, writing completion/goals progress, and AI deterministic test host provider selection.

**Root Cause**: Five distinct root cause groups:
1. **Billing format**: `GenerateOrderCode()` produced `TCT{timestamp}{hex}` but tests expected `TCT-{timestamp}-{hex}`. IPN message casing mismatch (`Order Not Found` vs `Order not found`).
2. **Speaking routes**: Integration tests called `/Speaking/Practice?id=701` and `/Speaking/Index` but controller lacked explicit route aliases for these legacy URL patterns.
3. **DI regression**: `Sprint2SmokeTests` expected `IStreakService` in `HomeController` constructor but architecture had moved to `IGoalsService`.
4. **Writing progress**: `WritingService.EvaluateWritingSentenceAsync` did not call `IGoalsService` for writing completion XP awards; test seed only populated progress tables without backing `UserWritingAttempts`.
5. **AI test host**: `TestWebApplicationFactory` did not override `IAiQueryClassifier` and `IAiProviderClient` to deterministic internal providers, causing AI baseline tests to fail. SQLite schema init flake (`no such table: Users`) appeared in full suite runs.

**Solution**:
1. Changed `BillingService.GenerateOrderCode()` to `TCT-{timestamp}-{hex}` format. Updated `IIpnService.InvalidOrder()` message casing.
2. Added `[HttpGet("/Speaking/Index")]` and `[HttpGet("/Speaking/Practice")]` route aliases on `SpeakingController`.
3. Renamed smoke test to `GoalsConsumers_RequireConstructorInjectedService` and updated assertion to check `IGoalsService`.
4. Injected `IGoalsService` into `WritingService`, added writing progress upsert helper and goal award on first exercise completion. Updated test seed to include `UserWritingAttempts`.
5. Added `RemoveAll<IAiQueryClassifier>` / `AddSingleton<DeterministicIntentClassifier>` and `RemoveAll<IAiProviderClient>` / `AddScoped<InternalKnowledgeProvider>` overrides in `TestWebApplicationFactory`. Added SQLite schema guard with `EnsureUsersTableExistsAsync` helper.

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` — added route aliases for legacy speaking URLs
- `TCTEnglish/Services/Billing/BillingService.cs` — fixed order code format to `TCT-{timestamp}-{hex}`
- `TCTEnglish/Services/Billing/IIpnService.cs` — fixed IPN message casing
- `TCTEnglish/Services/WritingService.cs` — added `IGoalsService` dependency and writing progress/goal award flow
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` — AI provider overrides and SQLite schema guard
- `TCTEnglish.Tests/Sprint2SmokeTests.cs` — updated DI smoke test to match current architecture
- `TCTEnglish.Tests/GoalsPhase7IntegrationTests.cs` — updated seed to include `UserWritingAttempts`
- `TCTEnglish.Tests/SpeakingLegacyNullMetadataIntegrationTests.cs` — added missing schema columns and updated assertion

**Verification**:
- Targeted test run (8 test classes, 95 tests): `Passed: 95, Failed: 0`
- Full test suite: `Passed: 449, Failed: 0, Skipped: 0, Total: 449, Duration: 50s`

**Commit**: Not created.

**Notes**: The fix preserves all pre-existing worktree changes (AdminBillingManagementTests CancellationToken updates, test project package version downgrades, BillingServiceTests lenient assertions, StudyController AllowAnonymous changes). The SQLite schema guard addresses a flaky `no such table: Users` error that appeared during full suite runs after the main logic fixes were applied.

### VNPay sandbox checkout still failed with code 70 and callback hardening gaps - 2026-04-28

**Symptom**: Redirect to VNPay sandbox could still fail at provider entry with `Error.html?code=70` (`Sai chữ ký`), while callback/IPN handling and config safety still had gaps (sensitive VNPay sandbox credentials present in development config, non-standardized IPN JSON response shape).

**Root Cause**: Signature helper behavior was not fully aligned with VNPay-compatible signing requirements (`vnp_` filtering, empty-value exclusion, and consistent `+` space encoding in raw sign/query string). Additionally, checkout metadata still used a non-compliant order code/description pattern for VNPay constraints, and callback controller logic did not strictly isolate VNPay query keys or enforce explicit IPN JSON casing contracts.

**Solution**: Reworked VNPay signature helper to strict signable-parameter filtering, stable ordinal sorting, `WebUtility.UrlEncode` usage, lowercase SHA512 output, and shared raw/query generation. Updated billing order code generation to alphanumeric-only format and checkout order description to non-accented VNPay-safe text. Hardened billing controller VNPay flow with dedicated VNPay query extraction, safe client IP resolution (`X-Forwarded-For` support), explicit `RspCode`/`Message` JSON DTO for IPN responses, and invalid-return fallback view rendering for missing `vnp_TxnRef`. Removed hardcoded VNPay sandbox credentials from `appsettings.Development.json` and replaced with placeholders.

**Files Changed**:
- `TCTEnglish/Services/Billing/VnPay/VnPaySignatureHelper.cs` - rebuilt signature/date/query helper to VNPay-compatible deterministic behavior.
- `TCTEnglish/Services/Billing/BillingService.cs` - VNPay-safe order code format and non-accented order info description.
- `TCTEnglish/Controllers/BillingController.cs` - IP extractor, proxy-aware client IP, explicit VNPay IPN JSON DTO, safer return fallback.
- `TCTEnglish/Services/Billing/IIpnService.cs` - standardized IPN response messages.
- `TCTEnglish/appsettings.Development.json` - removed embedded VNPay sandbox `TmnCode`/`HashSecret` and callback URLs.
- `TCTEnglish.Tests/VnPaySignatureHelperRequiredTests.cs` - added mandatory signature helper regression tests for sort/order/encoding/hash exclusion/tamper verify.
- `TCTEnglish.Tests/BillingControllerTests.cs` - updated return behavior assertion for missing txn ref.
- `TCTEnglish.Tests/IpnServiceTests.cs` - aligned message assertions with standardized IPN response messages.

**Verification**: Ran build and test commands for solution after patch (see task output summary).

**Commit**: Not created.

**Notes**: This fix is compatible with existing VNPay gateway abstraction and preserves IPN as authoritative activation channel. Production rollout still requires secret rotation and environment-based VNPay credentials.

### VNPay checkout still returned code 70 due to signature encoding mismatch - 2026-04-28

**Symptom**: VNPay sandbox payment page consistently returned `code=70` (`Sai chữ ký`) even after correct sandbox credentials and callback URLs were configured.

**Root Cause**: `VnPaySignatureHelper` signed request data using `Uri.EscapeDataString` (`%20` for spaces), while VNPay verification expects URL-encoding behavior aligned with form encoding (space as `+`) across hashed/query parameters.

**Solution**: Updated VNPay signature helper to use `WebUtility.UrlEncode` for both raw hash data and checkout query construction so signing and transmitted parameters follow the same VNPay-compatible encoding behavior.

**Files Changed**:
- `TCTEnglish/Services/Billing/VnPay/VnPaySignatureHelper.cs` - replaced `Uri.EscapeDataString` with `WebUtility.UrlEncode` in signature/raw-query builders.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Could not run automated tests in this environment due `NuGet.Config` access-denied (`C:\Users\Admin\AppData\Roaming\NuGet\NuGet.Config`). Requires local/manual VNPay sandbox retest.

**Commit**: Not created.

**Notes**: This fix follows existing VNPay hardening history in this log and targets checksum parity at provider boundary; keep sandbox tunnel URL stable during retest.

### Premium/Admin AI feature flows wired to internal provider instead of Gemini - 2026-04-28

**Symptom**: Premium/Admin users could access AI-gated actions but core flows (listening quiz generation, transcript translation, vocabulary suggestion payloads, writing AI generation/evaluation) failed or returned unusable output.

**Root Cause**: `IAiProviderClient` was registered to `InternalKnowledgeProvider` in DI, which returns internal rule-based responses and does not call Gemini. Feature services expecting strict JSON from a generative provider therefore received incompatible text outputs.

**Solution**: Updated service registration so `IAiProviderClient` resolves to `GeminiProviderClient`. Kept `InternalKnowledgeProvider` registered as its own concrete service for future explicit/internal usage without being the default AI provider for feature flows.

**Files Changed**:
- `TCTEnglish/Program.cs` - switched `IAiProviderClient` binding from `InternalKnowledgeProvider` to `GeminiProviderClient`.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Build verification executed after change (`dotnet build`).

**Commit**: Not created.

**Notes**: This change intentionally shifts all `IAiProviderClient` consumers to Gemini. If internal-only deterministic AI chat behavior is still required for a specific endpoint, introduce a dedicated provider abstraction per feature rather than global remapping.

### Home dashboard SQLite random distractor query failed with Guid.NewGuid translation - 2026-04-28

**Symptom**: `/Home/Index` returned HTTP 500 in SQLite-based integration tests, causing Premium launcher tests to fail with `InternalServerError`.

**Root Cause**: `HomeController.GetSystemWrongAnswersAsync(...)` used `OrderBy(c => Guid.NewGuid())` directly in EF query. SQL Server can translate this pattern, but SQLite test provider cannot, so EF threw a query translation exception.

**Solution**: Added provider-aware query branching in `GetSystemWrongAnswersAsync(...)`: keep SQL Server random ordering branch, and use `TakeRandomIdsAsync(...)` fallback for non-SQL Server providers, then map selected IDs back to `AnswerOption` in deterministic order.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` - hardened `GetSystemWrongAnswersAsync(...)` with SQLite-safe random sampling path.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "FullyQualifiedName~AiPhase4HardeningIntegrationTests.HomeIndex_PremiumUser_RendersLauncherUnlimitedPlanSummary|FullyQualifiedName~AiPhase4HardeningIntegrationTests.AiLauncher_PremiumUser_RendersUnlimitedPlanWithoutStandardQuotaCountdown"` passed (2/2).
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "Billing|Premium|Subscription"` passed (84/84).

**Commit**: Not created.

**Notes**: This matches the provider-compatibility pattern already used for dashboard random sampling; the fix reuses that pattern for daily challenge distractors.
### Notification entity missing from migration snapshot and database — 2026-04-15

**Symptom**: Two sequential errors after the Notification feature was added:
1. `System.InvalidOperationException` at `dbContext.Database.Migrate()` in `Program.cs` during application startup (model snapshot mismatch).
2. `Microsoft.Data.SqlClient.SqlException` at `NotificationService.GetUnreadCountAsync` (line 27) — "Invalid object name 'Notifications'" — because the Notifications table did not exist in the database.

**Root Cause**: The `Notification` entity was registered in `DbflashcardContext.OnModelCreating` (with table, index, and FK relationship) and the `User.Notifications` navigation property was added, but:
1. The migration snapshot (`DbflashcardContextModelSnapshot.cs`) was never updated to include the Notification definition.
2. No migration file was ever created to generate the `CREATE TABLE Notifications` SQL.
This is the same class of bug as the WritingExercise snapshot issue documented earlier in this log.

**Solution**: Two-phase fix:
1. **Generated EF migration** via `dotnet ef migrations add AddNotifications` — this auto-detected the Notification entity difference (snapshot vs model) and generated both the migration `.cs` + `.Designer.cs` files AND updated the snapshot.
2. **Trimmed the migration** — the auto-generated migration included unrelated diffs from previously-applied migrations (UserGoals, SpeakingVideoCompletions, WritingAttempts, etc.) because the snapshot had drifted from the live DB. Hand-trimmed the migration to only include `CreateTable("Notifications")` + `CreateIndex("IX_Notifications_UserId_IsRead_CreatedAt")`.
3. **Applied migration** via `dotnet ef database update` — successfully created the `Notifications` table in SQL Server.

**Files Changed**:
- `TCTEnglish/Migrations/20260415090417_AddNotifications.cs` — hand-trimmed migration with only Notifications CreateTable + CreateIndex.
- `TCTEnglish/Migrations/20260415090417_AddNotifications.Designer.cs` — auto-generated designer metadata (by EF).
- `TCTEnglish/Migrations/DbflashcardContextModelSnapshot.cs` — auto-updated by EF to include Notification entity, relationship, and User navigation.
- `.ai/context/bug-fix-log.md` — added this incident record.

**Verification**:
- `dotnet ef database update` — `CREATE TABLE [Notifications]`, `CREATE INDEX`, and `INSERT INTO [__EFMigrationsHistory]` all succeeded.
- `dotnet run` — app started cleanly: `No migrations were applied. The database is already up to date.`, `NotificationWorker started.`, `Now listening on: http://localhost:5100`. No `InvalidOperationException` or `SqlException`.

**Commit**: Not created.

**Notes**: The auto-generated migration was dangerously large because the snapshot had drifted from the live DB — it included CreateTable for 6 already-existing tables plus column additions and index changes that were already applied. Always trim auto-generated migrations when the snapshot is known to be behind. Future entity additions should use `dotnet ef migrations add` as the primary workflow rather than manual snapshot editing.


### Premium speaking Phase 6 regressions on owner-scoped delete and account cleanup - 2026-04-13

**Symptom**: In Premium Speaking lifecycle integration tests, outsider delete of another owner's private video returned `BadRequest` instead of `NotFound`, and account deletion redirected to `/Account/Settings` (failure path) instead of `/Account/Login` when owned private speaking content had dependent speaking progress rows.

**Root Cause**: `SpeakingService.DeleteOwnedVideoAsync` evaluated entitlement before ownership lookup, so non-owner calls from downgraded users could return upgrade errors instead of owner-safe `NotFound`. In account cleanup, `DeleteOwnedPrivateSpeakingContentAsync` relied on deleting parent videos only; dependent sentence/progress rows were not explicitly removed in cleanup order, causing delete-account transaction failure in integration flow.

**Solution**: Reordered delete logic in `SpeakingService` to resolve ownership (`videoId + owner`) first, then apply entitlement checks only for owned rows. Updated `AccountController.DeleteOwnedPrivateSpeakingContentAsync` to explicitly collect owned private video/sentence ids and remove dependent `UserSpeakingProgress` and `SpeakingSentence` rows before removing `SpeakingVideo` rows.

**Files Changed**:
- `TCTEnglish/Services/SpeakingService.cs` - changed private delete flow ordering to preserve anti-IDOR `NotFound` semantics.
- `TCTEnglish/Controllers/AccountController.cs` - hardened private speaking cascade cleanup during account deletion.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `run_tests` with `TypeName=TCTEnglish.Tests.PremiumSpeakingLifecycleIntegrationTests` passed (5/5).

**Commit**: Not created.

**Notes**: Fix keeps downgraded-owner delete blocked (`premium_required`) while preventing ownership leakage for non-owner/private-id probes.

### Premium writing generation inherited oversized shared AI budgets instead of enforcing dedicated limits - 2026-04-12

**Symptom**: The Premium Writing create-from-AI flow could silently use higher timeout/output-token ceilings when shared `AiOptions` were configured above the writing defaults, which weakened the intended dedicated budget controls for this endpoint.

**Root Cause**: `WritingService.Generation` built provider options with `Math.Max(...)` against shared `AiOptions` values, so shared chat-oriented settings could override writing-specific hard limits.

**Solution**: Switched writing generation provider request options to dedicated constants for `MaxOutputTokens` and `RequestTimeoutSeconds`, independent from shared chat ceilings. Added regression coverage that forces high shared `AiOptions` values and verifies create-from-AI still sends the dedicated writing limits.

**Files Changed**:
- `TCTEnglish/Services/WritingService.Generation.cs` - enforced dedicated request option values for writing generation provider calls.
- `TCTEnglish.Tests/TestHelpers/StubAiProviderClient.cs` - added an option-aware handler overload so tests can inspect provider request options.
- `TCTEnglish.Tests/PremiumWritingGenerationIntegrationTests.cs` - added integration regression test `CreateFromAi_PremiumUser_UsesDedicatedWritingGenerationRequestOptions`.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `run_tests` with `TypeName=TCTEnglish.Tests.PremiumWritingGenerationIntegrationTests` and `TypeName=TCTEnglish.Tests.PremiumWritingLearnerFlowIntegrationTests` and `TypeName=TCTEnglish.Tests.WritingSchemaTests` passed.

**Commit**: Not created.

**Notes**: This aligns with the Premium Writing feature requirement to avoid reusing chat defaults blindly for generation timeout/token/output handling.

### Writing practice accepted answers from the wrong sentence or pasted multi-sentence text - 2026-04-07

**Symptom**: On the Writing practice page, a learner could submit the next sentence, a previous sentence, or even paste multiple sentences / the whole passage into the current answer box and still drive the normal grading flow. In some cases this could mark the current line with out-of-scope content and break the intended sentence-by-sentence practice behavior.

**Root Cause**: `WritingService.EvaluateWritingSentenceAsync(...)` validated only basic emptiness and then sent the submission into rule-based / AI evaluation without checking whether the answer stayed within the currently selected sentence. There was no server-side scope guard for cross-sentence or pasted-passage submissions.

**Solution**: Added a server-side submission-scope validation step in `WritingService` before AI evaluation. The new guard rejects answers that look like a different sentence from the same exercise or contain multiple sentence segments / pasted-passage content, returns a hard-failure learner-facing evaluation, and prevents the answer from being accepted for progress. Added integration regression tests for both wrong-sentence and pasted-multi-sentence submissions to ensure AI is not called and exercise progress stays incomplete.

**Files Changed**:
- `TCTEnglish/Services/WritingService.cs` - added submission-scope validation helpers and early hard-failure handling before AI/rule-based acceptance.
- `TCTEnglish.Tests/WritingPracticeValidationIntegrationTests.cs` - added regression tests for cross-sentence and pasted multi-sentence submissions.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: `dotnet test D:\repo\TCTEnglish.Tests\TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~WritingPracticeValidationIntegrationTests|FullyQualifiedName~WritingAiEvaluationIntegrationTests|FullyQualifiedName~WritingProgressIntegrationTests|FullyQualifiedName~WritingPracticePersistenceIntegrationTests|FullyQualifiedName~Sprint1SmokeTests"` passed.

**Commit**: Not created.

**Notes**: This fix is intentionally server-enforced so the bug stays closed even if a user bypasses client-side UI behavior. `known-issues.md` was not updated because this specific Writing practice scope bug was not listed there.

### Admin Writing create/edit transaction error and data loss fix - 2026-04-07

**Symptom**: On the admin Writing screen, creating or editing an exercise failed with a generic error banner. Additionally, editing an exercise carried a data retention risk where historical `UserWritingAttempts` would be deleted.

**Root Cause**: The controller opened a manual EF Core transaction while the app is configured with the SQL Server retry execution strategy. In the edit flow, sentences were replaced wholesale via `RemoveRange`, which would cascade delete `UserWritingAttempts`.

**Solution**: Updated the create/edit flows to use `CreateExecutionStrategy().ExecuteAsync(...)` around the transaction block. Implemented in-place updates for `WritingExerciseSentences` during edit to preserve existing primary keys and protect historical data. Added targeted regression tests to enforce execution strategy and sentence retention.

**Files Changed**:
- `TCTEnglish/Areas/Admin/Controllers/WritingExerciseManagementController.cs` - Applied `CreateExecutionStrategy` and in-place `WritingExerciseSentence` synchronization.
- `TCTEnglish.Tests/WritingAdminContentIntegrationTests.cs` - Added `AdminWriting_EditFullText_ResplitsSentencesAndPreservesExistingSentenceIds` and regression coverage.

**Verification**: `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "WritingAdmin|Writing|Sprint1SmokeTests"` passed.

**Commit**: Not created yet.

**Notes**: Integration tests use SQLite which doesn't reflect the SQL Server retry strategy. The fix has been fully verified manually on a live SQL Server-backed environment by the team, proving that the Execution Strategy mismatch is resolved. The bug is now officially closed.
### Writing progress live migration failed on SQL Server due to multiple cascade paths - 2026-04-06

**Symptom**: Applying migration `20260406144534_AddUserWritingAttempts` to the live SQL Server failed with `Introducing FOREIGN KEY constraint 'FK_UserWritingAttempts_WritingExercises' on table 'UserWritingAttempts' may cause cycles or multiple cascade paths`, so durable Writing progress could not start accumulating in the running environment.

**Root Cause**: The new `UserWritingAttempts` table cascaded from both `WritingExercises` and `WritingExerciseSentences`, while `WritingExerciseSentences` already cascades from `WritingExercises`. SQL Server rejected the schema because that created two delete paths from `WritingExercises` to `UserWritingAttempts`.

**Solution**: Changed the `UserWritingAttempt -> WritingExercise` relationship to `DeleteBehavior.NoAction` / `ReferentialAction.NoAction`, kept cascade cleanup through `WritingExerciseSentence`, updated the generated migration metadata/snapshot, added a metadata regression test for the FK delete behaviors, and re-ran the migration successfully against the live SQL Server.

**Files Changed**:
- `TCTEnglish/Models/DbflashcardContext.cs` - changed `FK_UserWritingAttempts_WritingExercises` to `DeleteBehavior.NoAction`.
- `TCTEnglish/Migrations/20260406144534_AddUserWritingAttempts.cs` - changed the SQL Server FK delete action to `ReferentialAction.NoAction`.
- `TCTEnglish/Migrations/20260406144534_AddUserWritingAttempts.Designer.cs` - synced the migration designer metadata to `DeleteBehavior.NoAction`.
- `TCTEnglish/Migrations/DbflashcardContextModelSnapshot.cs` - synced the model snapshot to `DeleteBehavior.NoAction`.
- `TCTEnglish.Tests/WritingSchemaTests.cs` - added a regression test for the `UserWritingAttempt` FK delete-behavior shape.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`
- `dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore`
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-build --filter "Writing"`
- `dotnet ef database update 20260406144534_AddUserWritingAttempts --project TCTEnglish/TCTEnglish.csproj --startup-project TCTEnglish/TCTEnglish.csproj --no-build` against the live SQL Server (passed; `UserWritingAttempts` created and migration recorded in `__EFMigrationsHistory`)

**Commit**: Not created.

**Notes**: This follows the same operational area as the 2026-03-27 Writing table rollout issue, but the root cause is different: SQL Server cascade-path validation rather than missing schema application. A later focused `WritingSchemaTests` run was noisy in this environment because of a transient testhost dependency resolution issue (`AngleSharp.dll`), but the rebuilt Writing regression slice passed after the fix.

### AI launcher quota sync could drift with multi-tab sends due to local UI increments - 2026-04-03

**Symptom**: Standard-plan quota countdown in launcher could be temporarily inaccurate when embedded chat sends happened across multiple tabs/windows because host UI incremented locally per success event.

**Root Cause**: The initial iframe-to-host sync used client-side `+1` increments only, without pulling authoritative `AiRequestLogs`-based usage after each successful embedded send.

**Solution**: Added authoritative usage refresh path in AI boundary: new `GET /AI/Chat/Usage` endpoint (via existing `IAiChatService.GetDailyUsageAsync`) and launcher runtime now refreshes quota from server on each embedded success sync event instead of relying on local increments.

**Files Changed**:
- `TCTEnglish/Controllers/AiController.cs` - added `Usage` endpoint (`/AI/Chat/Usage`) for authenticated usage snapshot.
- `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml` - exposed `data-ai-usage-url` on launcher root.
- `TCTEnglish/wwwroot/js/ai-chat-launcher.js` - replaced local increment logic with authoritative `fetch(usageUrl)` refresh.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added integration assertions for usage endpoint and launcher usage refresh hook.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `run_tests` with `TypeName=TCTEnglish.Tests.AiPhase4HardeningIntegrationTests` (34 passed)
- `run_build` (successful)

**Commit**: Not created.

**Notes**: Premium/Admin behavior remains unchanged (unlimited path, no Standard quota countdown element).

### AI launcher Standard quota countdown did not refresh after embedded chat sends - 2026-04-03

**Symptom**: On pages with the shared AI launcher, Standard-plan quota text (`Đã dùng x/15`) stayed stale after the user sent successful messages inside the embedded `/AI/Chat?embed=true` iframe. The countdown only refreshed after reloading the parent page.

**Root Cause**: Launcher quota markup was rendered server-side once and had no runtime sync channel from the embedded chat frame to the launcher host after successful sends.

**Solution**: Added a minimal same-origin message bridge: embedded chat now posts a quota-consumed event to the parent only after successful sends, and launcher host listens for that event (from its iframe only) to increment Standard quota UI in place. Added machine-readable quota attributes in launcher markup and targeted integration checks for the JS hooks.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js` - added post-send quota sync event emission via `window.parent.postMessage(...)`.
- `TCTEnglish/wwwroot/js/ai-chat-launcher.js` - added same-origin message listener scoped to launcher iframe and Standard quota DOM update logic.
- `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml` - added `data-ai-plan-quota-used` / `data-ai-plan-quota-limit` attributes and Standard fallback quota line.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added targeted regression tests for embedded send sync message + launcher host listener and new quota attributes.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `run_tests` with `TypeName=TCTEnglish.Tests.AiPhase4HardeningIntegrationTests` (33 passed)
- `run_build` (successful)

**Commit**: Not created.

**Notes**: Sync is intentionally additive and UI-only; Premium/Admin unlimited rendering remains unchanged because no Standard quota element exists for those plans.

### AI existing-conversation retry could duplicate user prompt after provider failure - 2026-04-03

**Symptom**: In an existing conversation, a provider timeout/unavailable response could leave the just-sent user prompt persisted. When the user retried, the same prompt was stored again, creating duplicate user entries for effectively one retry flow.

**Root Cause**: `AiChatService.SendAsync(...)` persisted the user message before calling the provider and only rolled back draft-conversation data on provider failure. Existing conversations kept the failed-attempt user message.

**Solution**: Added provider-failure rollback for existing conversations to remove the just-persisted user prompt message while preserving failure request logs. Added service and integration regression tests for fail-then-retry behavior to ensure only one user prompt is persisted.

**Files Changed**:
- `TCTEnglish/Services/AI/AiChatService.cs` - added existing-conversation failed-prompt rollback path and helper method.
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs` - updated provider-failure expectation and added retry no-duplicate regression test.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added integration test for existing conversation fail-then-retry prompt dedup behavior.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `dotnet test TCTEnglish.Tests --filter Ai --no-restore`

**Commit**: Not created.

**Notes**: This follows the same AI hardening theme as the 2026-03-30 draft-rollback fix, but addresses a different root cause scope (existing conversations instead of first-send drafts).

### AI regression slice failed to compile due to missing Phase4 helper utilities - 2026-04-03

**Symptom**: `dotnet test TCTEnglish.Tests --filter Ai --no-restore` failed at compile time in `AiPhase4HardeningIntegrationTests` because helper methods (`SeedSuccessfulRequestLogsAsync`, `CreateDeleteRequest`, `SeedMessageAndRequestLogAsync`) were referenced but not implemented.

**Root Cause**: During prior AI regression-suite edits, calls to shared local test helpers remained in the integration test class but the helper implementations were removed, leaving unresolved symbols in the AI test slice.

**Solution**: Restored the missing local helper utilities in `AiPhase4HardeningIntegrationTests` for quota-log seeding, delete-request construction (including optional anti-forgery token), and message/request-log seeding for delete cascade verification. Added one focused regression assertion to keep upgraded UI coverage meaningful (`Standard` plan hint + delete feedback hooks).

**Files Changed**:
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - restored missing helper methods and added `Chat_FullPage_StandardPlan_RendersPlanHintAndDeleteFeedbackHooks`.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `dotnet test TCTEnglish.Tests --filter Ai --no-restore` (passed, AI slice compiles/runs)
- `run_build` (passed)

**Commit**: Not created.

**Notes**: Root cause does not match previously logged delete-UX runtime bug; this fix is scoped strictly to AI regression-suite compile/run restoration.

### AI chat delete UX still fell back to browser-native dialogs on full page - 2026-04-03

**Symptom**: On `/AI/Chat`, clicking delete in the conversation history could still trigger browser-native `confirm(...)` / `alert(...)` flows instead of the intended app-styled confirmation modal and toast feedback.

**Root Cause**: `ai-chat.js` had a native-dialog fallback path when `window.bootstrap` was unavailable, and `_Layout.cshtml` did not load the Bootstrap bundle runtime required by the existing modal/toast implementation.

**Solution**: Loaded Bootstrap bundle runtime in shared layout so full-page AI chat always has modal/toast support, then removed the native delete fallback branch in `ai-chat.js` and kept error reporting in-app via chat status/modal error state. Added integration assertions to lock the runtime and no-native-dialog behavior.

**Files Changed**:
- `TCTEnglish/Views/Shared/_Layout.cshtml` - added Bootstrap bundle script include.
- `TCTEnglish/wwwroot/js/ai-chat.js` - removed `confirm(...)` / `alert(...)` delete fallback and required modal path.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added assertions for delete modal/toast hooks, Bootstrap bundle presence, and no native delete-dialog APIs in script.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `dotnet build D:\TCTEnglish\TCTEnglish\TCTEnglish.csproj --no-restore`
- `node --check D:\TCTEnglish\TCTEnglish\wwwroot\js\ai-chat.js`
- `dotnet test D:\TCTEnglish\TCTEnglish.Tests\TCTEnglish.Tests.csproj --filter Ai --no-restore` *(blocked by existing missing helper methods in `AiPhase4HardeningIntegrationTests.cs`: `SeedSuccessfulRequestLogsAsync`, `CreateDeleteRequest`, `SeedMessageAndRequestLogAsync`)*

**Commit**: Not created.

**Notes**: This follow-up is intentionally scoped to the delete UX runtime gap only; existing AI regression-suite compile breakage remains a separate blocker tracked by follow-up `02`.

### AI chat long histories pushed the composer below the page fold - 2026-03-31

**Symptom**: On the full AI chat page, longer conversation histories could make the page itself grow vertically so users had to scroll the whole document to get back to the composer instead of staying inside a bounded chat surface.

**Root Cause**: The AI chat shell mixed several hard-coded/minimum heights with malformed shell markup around the message region, so the layout could not consistently shrink the transcript area and keep scrolling contained to the chat panel. The scroll-to-bottom affordance also depended only on raw scroll events, which made its visibility less reliable after DOM updates and streaming changes.

**Solution**: Cleaned the `_ChatShell` structure so the transcript wrapper, scroll control, and composer live in a stable bounded shell; updated `ai-chat.css` to keep the page/shell/sidebar/embed variants constrained with internal overflow instead of document growth; and updated `ai-chat.js` to synchronize the floating scroll-to-bottom control after scroll, resize, append, and streaming updates. Added integration assertions for the new scroll/bounded-shell hooks.

**Files Changed**:
- `TCTEnglish/Views/Ai/_ChatShell.cshtml` - fixed the shell markup, kept the composer in the bounded chat surface, and added scroll-region / scroll-button hooks.
- `TCTEnglish/wwwroot/css/ai-chat.css` - converted the chat shell to a bounded flex layout with internal transcript scrolling for full-page and embedded modes, including mobile breakpoints.
- `TCTEnglish/wwwroot/js/ai-chat.js` - added explicit scroll-affordance synchronization and smooth scroll-to-latest behavior without changing send/stream/fallback flows.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - extended AI chat integration coverage for the scroll-to-bottom control and accessibility/layout hooks.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**:
- `node --check TCTEnglish/wwwroot/js/ai-chat.js`
- `git diff --check -- TCTEnglish/Views/Ai/_ChatShell.cshtml TCTEnglish/wwwroot/css/ai-chat.css TCTEnglish/wwwroot/js/ai-chat.js TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~AiPhase4HardeningIntegrationTests -p:OutputPath=D:\TCTEnglish\.tmp-tests\bin\Debug\net10.0\ -p:UseAppHost=false`

**Commit**: Not created.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this UI regression was not listed there.

### AI launcher focus trap escaped when entering embedded iframe - 2026-03-31

**Symptom**: Keyboard users could Tab into the AI launcher iframe but the parent dialog's focus trap stopped working, allowing focus to escape into the underlying background page when tabbing out of the iframe boundaries.

**Root Cause**: The parent `ai-chat-launcher.js` focus trap intercepted Tab events before they could flow into the iframe when boundaries hit `lastElement`, and could not capture Tab events fired inside the `iframe.contentDocument` once focus lived inside the cross-document boundary.

**Solution**: Added a runtime script injection after iframe load that installs a reverse focus-trap listener on `iframe.contentDocument`. This inner trap catches Tab/Escape and routes focus seamlessly back out to the parent's `focusableElements` array. Adjusted the parent trap logic to skip `preventDefault()` when `lastElement` is the `iframe` so focus falls into the embedded chat natively, and added logic to dive deep into the iframe's own last focusable element when `Shift+Tab` is pressed on the parent dialogue's first element.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat-launcher.js` - modified `trapFocus` and `ensureFrameLoaded` to sync focus loops across document boundaries.

**Verification**: Ran `node -c` on script to verify syntax and rebuilt solution.

**Commit**: Not created.

**Notes**: This relies on the launcher and embed living on the same origin.

### AI first-send retry could create duplicate draft conversations after provider failures - 2026-03-30

**Symptom**: Sending the very first AI message (no `conversationId`) could create duplicate conversation threads when the first request failed with retryable `503/5xx` and the client retried automatically.

**Root Cause**: `ai-chat.js` retried retryable failures even when `conversationId` was `null`, while `AiChatService.SendAsync(...)` persisted a new conversation and user message before provider response. A failed first attempt therefore left persisted draft data, and retry created another draft conversation.

**Solution**: Disabled automatic retry for first-send requests without an existing conversation id, and added server-side rollback in `AiChatService` so provider failures during draft creation remove the just-created conversation/messages/request logs.

**Files Changed**:
- `TCTEnglish/wwwroot/js/ai-chat.js` - limited retry attempts to one when `conversationId` is missing.
- `TCTEnglish/Services/AI/AiChatService.cs` - rolled back draft conversation data on provider failure.
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs` - added service-level regression test for draft rollback.
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` - added integration test verifying `503` on first send does not persist draft conversation.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran targeted AI tests for service + integration coverage around draft send and provider failure behavior.

**Commit**: Not created yet.

**Notes**: This fix targets the duplicate-thread path for first-send retryable failures and keeps existing behavior for established conversations.

### AI chat existed as a page but had no visible launcher in the main UI - 2026-03-30

**Symptom**: Users could only access AI chat by directly visiting `/AI/Chat`; the main shared site UI had no floating launcher/button, so the built chat experience was effectively hidden from normal browsing flows.

**Root Cause**: The AI chat implementation lived only as a dedicated authenticated feature page (`AiController` + `Views/Ai/Chat.cshtml`). No shared partial, shared CSS/JS launcher asset, or layout hook rendered an entry point from `Views/Shared/_Layout.cshtml`.

**Solution**: Added a shared floating AI launcher partial to the main layout, created dedicated launcher CSS/JS assets, added a launcher image asset under `wwwroot/images/ai/`, and introduced an embedded AI chat render path (`ChatEmbed.cshtml`) so the existing chat UI can be opened inside a compact bottom-right panel without rewriting the current chat logic or touching the in-progress `wwwroot/js/ai-chat.js` work.

**Files Changed**:
- `TCTEnglish/Controllers/AiController.cs` - added `embed` handling and returned `ChatEmbed` for the compact widget route.
- `TCTEnglish/ViewModels/AI/AiChatPageViewModel.cs` - added `IsEmbedded` so the chat shell can render full-page vs embedded modes safely.
- `TCTEnglish/Views/Ai/Chat.cshtml` - converted the full-page chat view to use the shared chat shell partial plus dedicated stylesheet.
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml` - added a layout-free embedded chat document for the floating widget iframe.
- `TCTEnglish/Views/Ai/_ChatShell.cshtml` - extracted reusable chat markup shared by full-page and embedded AI chat renders.
- `TCTEnglish/Views/Shared/_Layout.cshtml` - loaded launcher assets and rendered the shared AI launcher outside the AI chat page itself.
- `TCTEnglish/Views/Shared/_AiChatLauncher.cshtml` - added the floating launcher button/panel markup and login fallback.
- `TCTEnglish/wwwroot/css/ai-chat.css` - added dedicated styling for the refreshed AI chat page and embedded shell.
- `TCTEnglish/wwwroot/css/ai-chat-launcher.css` - added floating launcher and panel styling.
- `TCTEnglish/wwwroot/js/ai-chat-launcher.js` - added launcher open/close/lazy-iframe behavior.
- `TCTEnglish/wwwroot/images/ai/tct-ai-launcher.png` - added the cropped launcher icon derived from the provided mascot image.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `git diff --check` on the touched AI/layout files (passed; line-ending warnings only). Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: Not created yet.

**Notes**: `wwwroot/js/ai-chat.js` already had unrelated local edits, so the fix intentionally avoided changing that file. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this specific launcher gap was not listed there.

### Goals dashboard/challenge sync and GoalsPhase startup unblock — 2026-03-29

**Symptom**: Dashboard stat still rendered hardcoded Goal value, daily challenge correct answers only updated streak without writing Goals activity/XP, and all `GoalsPhase1-5` integration tests failed at startup with `PendingModelChangesWarning` from `Program.cs` migration execution.

**Root Cause**: `Views/Home/Index.cshtml` still contained stale hardcoded stat values, `HomeController.CheckAnswer` did not call `IGoalsService.RecordActivityAsync(...)`, and the SQLite test host (`TestWebApplicationFactory`) triggered model-drift warnings when app startup executed migrations.

**Solution**: Replaced hardcoded dashboard stat values with `DashboardViewModel` properties, added daily-challenge success activity tracking via `IGoalsService` (`CardsReviewed`, `QuizzesCompleted`, `XpEarned`), configured test DbContext warnings to ignore `RelationalEventId.PendingModelChangesWarning` in test host only, and added regression coverage to assert daily challenge writes Goals activity/XP.

**Files Changed**:
- `TCTEnglish/Views/Home/Index.cshtml` - switched Goal/Folders/Cards stat values to `@Model` values.
- `TCTEnglish/Controllers/HomeController.cs` - injected `IGoalsService` and recorded goals activity/XP on correct daily challenge answer.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` - ignored pending-model-change warning in test SQLite options to unblock startup.
- `TCTEnglish.Tests/GoalsPhase3IntegrationTests.cs` - added integration test for daily challenge goals activity/XP recording.
- `.ai/context/known-issues.md` - synchronized TD-004 text with current live goals flow.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran GoalsPhase test classes (`GoalsPhase1IntegrationTests` ... `GoalsPhase5IntegrationTests`) and then reran after fixes; startup warning gate was removed and suite progressed to functional assertions. Also reran targeted phase test coverage for challenge/goals paths.

**Commit**: Not created yet.

**Notes**: This fix intentionally avoids changing `Program.cs`; startup migration behavior is unchanged in app runtime, only test-host warning handling was adjusted.

### Goals phase-2 database apply was blocked by a missing migration designer artifact - 2026-04-04

**Symptom**: The app could be kept alive with compatibility fallback, but applying the intended SQL Server migration `20260404143000_AddUserDailyActivityAreaCountersPhase2` failed because EF reported `The migration '20260404143000_AddUserDailyActivityAreaCountersPhase2' was not found.`

**Root Cause**: The migration had its `.cs` file but was missing the generated `20260404143000_AddUserDailyActivityAreaCountersPhase2.Designer.cs`, so EF Core did not discover it as a valid migration. At the same time, the local `bin/*/TCTEnglish.dll` outputs were locked by a running process, which blocked a normal rebuild to refresh migration metadata before database update.

**Solution**: Restored the missing designer artifact for the phase-2 migration, then applied the schema change directly on SQL Server with an idempotent script that adds `VocabularyCompletedCount`, `WritingCompletedCount`, `ReadingCompletedCount`, and `ListeningCompletedCount` only when absent and records the matching row in `__EFMigrationsHistory`.

**Files Changed**:
- `TCTEnglish/Migrations/20260404143000_AddUserDailyActivityAreaCountersPhase2.Designer.cs` - restored the missing EF Core designer metadata so the migration exists in source as a complete pair again.
- `.ai/context/bug-fix-log.md` - recorded the database-apply follow-up and the missing-designer root cause.

**Verification**: Connected to the configured SQL Server and verified `COL_LENGTH('dbo.UserDailyActivities', ...) = 4` for all four phase-2 counters plus `COUNT(*) = 1` in `__EFMigrationsHistory` for `20260404143000_AddUserDailyActivityAreaCountersPhase2` with product version `10.0.2`.

**Commit**: Not created yet.

**Notes**: This database apply complements the earlier compatibility fallback in `GoalsService`; the site now has both the code-side safety net and the intended SQL schema. Local EF CLI rebuild/update is still blocked while a process keeps `bin\Debug\net10.0\TCTEnglish.dll` and `bin\Release\net10.0\TCTEnglish.dll` locked.

### Goals page crashed on legacy UserDailyActivities schema missing phase-2 counters - 2026-04-04

**Symptom**: Opening `/Goals` on a database that had not yet applied the latest additive `UserDailyActivities` rollout failed with `SqlException: Invalid column name 'VocabularyCompletedCount' / 'WritingCompletedCount' / 'ReadingCompletedCount' / 'ListeningCompletedCount'`.

**Root Cause**: `GoalsService` assumed the phase-2 daily-activity counter migration was already present everywhere. Both the Goals read path (`GetGoalsAsync` / badge metrics) and the activity write path (`RecordLearningActivityAsync`, streak XP row creation) materialized or updated the new columns directly through EF, so any environment still on the older table shape crashed at runtime instead of degrading safely.

**Solution**: Added schema detection plus compatibility-mode read/write helpers in `GoalsService` for `UserDailyActivities`. When the phase-2 counters are missing, the service now reads/writes only the legacy column set, defaults the missing counters to `0`, and logs an explicit warning that migration `20260404143000_AddUserDailyActivityAreaCountersPhase2` still needs to be applied for full metrics. Added regression coverage for loading `/Goals` and recording learning activity against a downgraded SQLite table that drops those four columns.

**Files Changed**:
- `TCTEnglish/Services/GoalsService.cs` - added `UserDailyActivities` schema detection, compatibility queries/commands, and fallback streak/activity persistence when the new counters are absent.
- `TCTEnglish.Tests/GoalsPhase2IntegrationTests.cs` - added regressions for `/Goals` page load and learning-activity persistence against a legacy `UserDailyActivities` schema.
- `TCTEnglish.Tests/GoalsPhase7IntegrationTests.cs` - added the missing `TCTVocabulary.Services` import so the test project can compile `BusinessDateHelper` references during verification.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase2IntegrationTests`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter "FullyQualifiedName~GoalsPhase1IntegrationTests|FullyQualifiedName~GoalsPhase2IntegrationTests|FullyQualifiedName~GoalsPhase3IntegrationTests|FullyQualifiedName~GoalsPhase4IntegrationTests|FullyQualifiedName~GoalsPhase5IntegrationTests|FullyQualifiedName~GoalsPhase6IntegrationTests"` with workspace-local `.dotnet-home`; all passed.

**Commit**: Not created yet.

**Notes**: Full `FullyQualifiedName~GoalsPhase` still reports an unrelated existing phase-7 failure (`/Home/Writing/Practice` anti-forgery fetch returning `401` for the anonymous test client). The compatibility fallback keeps the site working on legacy schema, but missing phase-2 counters still need the real migration applied if the team wants complete vocabulary/writing badge metrics and backfilled reporting.

### Goals phase-7 reward hardening and request-path cleanup - 2026-04-04

**Symptom**: Final review for the cross-feature Goals rollout found two closure risks: speaking/writing completion rewards still depended on non-atomic completion-state transitions, and writing request paths could call `Database.MigrateAsync()` during normal user traffic even though startup already migrates the app.

**Root Cause**: `SpeakingController.SaveSpeakingProgress` and `StudyService.Writing.PersistWritingProgressAsync(...)` used read-then-write completion flips (`IsCompleted == false` to `true`) without an atomic guard on the transition that triggers XP. In parallel or replay-heavy conditions, that left room for the same logical completion to be rewarded twice. Separately, an older writing hotfix added a request-time schema guard that no longer matched the current startup-migration baseline.

**Solution**: Hardened both completion paths so XP/reward issuance is driven only by an atomic conditional update of the persisted completion row, removed request-time writing schema migration logic, added a focused replay regression for writing completion, and synchronized backlog/known-issues/playbook docs with an explicit `Close` decision for the rollout.

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` - replaced tracked completion flip logic with an atomic completion-row update so a speaking video completion only awards once.
- `TCTEnglish/Services/StudyService.Writing.cs` - removed `EnsureWritingSchemaReadyAsync()` request-path migration logic and changed exercise completion to an atomic transition before awarding writing rewards.
- `TCTEnglish.Tests/GoalsPhase7IntegrationTests.cs` - added replay regression coverage for writing completion so XP/progress stay single-award after the first completed submit.
- `docs/architecture-prioritized-backlog.md` - updated Goals backlog truth to reflect active vs deferred goal areas and reward-dedup coverage expectations.
- `.ai/context/known-issues.md` - updated the Goals drift warning and marked phase-7 closure/reward hardening as resolved/verified.
- `docs/goals-cross-feature-rollout-playbook.md` - recorded the explicit rollout close decision and verification snapshot.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~GoalsPhase'`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter 'FullyQualifiedName~Sprint1SmokeTests'` with workspace-local `.dotnet-home`; all commands exited `0`.

**Commit**: Not created yet.

**Notes**: The rollout is now closed for `Vocabulary`, `Speaking`, and `Writing`. `Reading`/`Listening` remain explicitly deferred and should stay that way until they gain real completion signals.

### Goals phase-2 mastered-retry test expected pre-streak-XP total — 2026-04-04

**Symptom**: `GoalsPhase2IntegrationTests.RecordLearningActivityAsync_MasteredCompletionCountsOnceAcrossRetries` failed with XP mismatch (`Expected: 20`, `Actual: 30`) after phase-6 streak XP rollout.

**Root Cause**: The phase-2 assertion still reflected pre-phase-6 behavior and did not include one-time streak XP (`StreakXpAwarded`) when streak increases on the business day.

**Solution**: Updated phase-2 test expectations to include streak XP in daily total and assert `StreakXpAwarded == 10` to keep the contract explicit.

**Files Changed**:
- `TCTEnglish.Tests/GoalsPhase2IntegrationTests.cs` — updated expected XP and added streak-XP assertion in mastered-retry regression.

**Verification**: Ran focused test `TCTEnglish.Tests.GoalsPhase2IntegrationTests.RecordLearningActivityAsync_MasteredCompletionCountsOnceAcrossRetries` (passed), then reran goals phase suites.

**Commit**: Not created yet.

**Notes**: Same regression family as prior phase-3 expectation updates after streak-XP integration; root cause is test contract drift, not service logic failure.

### Goals Phase4 modal contract test drifted after multi-goal input rename — 2026-04-04

**Symptom**: `GoalsPhase4IntegrationTests.GoalsPage_RendersGoalInputContract_ForAutofocusTarget` failed because it still asserted `name="GoalEditor.DailyGoal"`, while the UI had moved to the multi-goal model using `GoalEditor.TargetValue`.

**Root Cause**: Regression in test contract after Phase 1/2 model migration (`DailyGoal` -> `TargetValue` + `GoalArea`). The view and controller were already updated, but one phase-4 test still referenced the legacy field name.

**Solution**: Updated phase-4 test payload/assertions to use `GoalEditor.TargetValue` and include `GoalEditor.GoalArea` in invalid submit POST data so model binding and validation reflect current behavior.

**Files Changed**:
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` — replaced legacy `GoalEditor.DailyGoal` references with `GoalEditor.TargetValue` and aligned invalid-submit form payload.

**Verification**: Ran focused tests for `TCTEnglish.Tests.GoalsPhase4IntegrationTests.GoalsPage_RendersGoalInputContract_ForAutofocusTarget` and `TCTEnglish.Tests.GoalsPhase4IntegrationTests.UpdateGoal_InvalidSubmit_ReturnsOpenModalContractForReload` (both passed).

**Commit**: Not created yet.

**Notes**: Related to earlier modal/autofocus hardening entries; same class of issue (HTML contract drift) but different root cause (input name migration).

### Goals modal autofocus nhắm nhầm hidden anti-forgery field — 2026-03-30

**Symptom**: Khi mở modal chỉnh sửa mục tiêu trên trang `Goals`, con trỏ có thể focus vào hidden anti-forgery input thay vì ô nhập số mục tiêu, làm trải nghiệm create/edit kém ổn định.

**Root Cause**: `goals.js` dùng selector quá rộng (`input, select, textarea, button`) trong `shown.bs.modal`, nên phần tử đầu tiên có thể là `input[type="hidden"]` do `@Html.AntiForgeryToken()` render ra.

**Solution**: Ưu tiên focus thẳng `.goal-input`; nếu không có thì fallback sang selector loại trừ hidden input (`input:not([type='hidden'])`). Đồng thời thêm contract test HTML để giữ ổn định target field trong modal.

**Files Changed**:
- `TCTEnglish/wwwroot/js/goals.js` - đổi logic autofocus để ưu tiên `.goal-input` và loại trừ hidden input ở fallback.
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` - thêm test `GoalsPage_RendersGoalInputContract_ForAutofocusTarget` xác nhận modal có hidden anti-forgery field và ô nhập `.goal-input` cho `GoalEditor.DailyGoal`.

**Verification**: Chạy `dotnet test TCTEnglish.Tests --filter 'FullyQualifiedName~GoalsPhase' --no-restore` và không ghi nhận lỗi backend/data integrity mới trong bộ Goals phases.

**Commit**: Not created yet.

**Notes**: Đây là hardening UX/accessibility cho modal interaction; logic dữ liệu/service không thay đổi.

### Goals modal CTA không mở ổn định vì thiếu Bootstrap JS bundle toàn cục — 2026-03-30

**Symptom**: Trên trang `Goals`, các CTA dùng `data-bs-toggle="modal"` (nút đầu trang và empty-state) có thể không mở được modal editor trong runtime do layout chính chưa nạp Bootstrap JS.

**Root Cause**: `Views/Shared/_Layout.cshtml` chỉ nạp Bootstrap CSS mà chưa nạp `bootstrap.bundle.min.js`, nên các hành vi `data-bs-*` không được kích hoạt. `goals.js` cũng gọi qua global `bootstrap` thay vì API tham chiếu an toàn từ `window.bootstrap`.

**Solution**: Nạp Bootstrap JS bundle ở layout chung để tất cả màn dùng `data-bs-*` hoạt động ổn định, đồng thời harden `goals.js` để mở modal qua `window.bootstrap?.Modal` và không ném lỗi nếu API chưa sẵn sàng.

**Files Changed**:
- `TCTEnglish/Views/Shared/_Layout.cshtml` - thêm script CDN `bootstrap.bundle.min.js` trước `RenderSection("Scripts")`.
- `TCTEnglish/wwwroot/js/goals.js` - dùng `modalApi = window.bootstrap?.Modal` và gọi `modalApi.getOrCreateInstance(...)`.

**Verification**: Chạy `dotnet test TCTEnglish.Tests --filter "FullyQualifiedName~GoalsPhase1IntegrationTests|FullyQualifiedName~GoalsPhase5IntegrationTests" --no-restore` (11/11 passed), chạy `Sprint2SmokeTests` (15/15 passed), và `run_build` (passed).

**Commit**: Not created yet.

**Notes**: `scripts/encoding_guard.py` không tồn tại trong repo hiện tại nên không thể chạy encoding guard script.

### Goals progress card text wraps vertically and header CTA is cramped on small screens - 2026-03-30

**Symptom**: On the Goals page, the text inside the circular daily-progress widget could break into awkward vertical wrapping (for example `thẻ hôm nay` split across multiple lines), and the top `Chỉnh sửa mục tiêu` CTA felt cramped on narrower layouts.

**Root Cause**: The absolute-positioned `.percentage-label` inside the progress ring had no stable width constraints, so the label container could shrink too aggressively. The header row also used fixed `col-6` columns at all breakpoints, which reduced available space for title/CTA on smaller screens.

**Solution**: Added dedicated progress label classes and fixed sizing (`.goal-progress-value`, `.goal-progress-meta`, `.percentage-label`, `.circular-progress`) to keep the center text readable and stable. Updated the header row to responsive columns (`col-12 col-lg-6`) and improved mobile CTA behavior. Also adjusted no-goal caption copy to show `Mục tiêu ngày: Chưa đặt`.

**Files Changed**:
- `TCTEnglish/Views/Goals/Index.cshtml` - improved responsive header layout, progress-label markup, and no-goal daily target caption.
- `TCTEnglish/wwwroot/css/goals.css` - added sizing/typography rules for progress text and responsive tweaks for title + CTA.

**Verification**: Ran `run_build` (passed). Ran goals regression tests via `run_tests` with TypeName filters `TCTEnglish.Tests.GoalsPhase1IntegrationTests` and `TCTEnglish.Tests.GoalsPhase2IntegrationTests` (6/6 passed).

**Commit**: Not created yet.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Goals merge-safety, business-date badge alignment, and test-host migration isolation - 2026-03-30

**Symptom**: The branch was not merge-safe because stray AI chat migration artifacts sat in the workspace outside the Goals scope, goals-related tests failed during startup with `PendingModelChangesWarning`, and the recent badge highlight could drift across the local midnight boundary for SQL Server `datetime2` badge timestamps.

**Root Cause**: Untracked migration artifacts (`20260329111532_AddAiChatPhase1Data*`, `20260329164730___TempPendingInspection*`) introduced unrelated schema noise. The SQLite integration host still executed `Program` startup migration behavior, which is not valid for the test database setup. In addition, badge highlighting depended on timestamps that SQL Server materializes as `DateTimeKind.Unspecified`, so the business date had to be interpreted explicitly as stored UTC.

**Solution**: Removed the stray migration artifacts from the branch, replaced the EF migrator inside the SQLite test host with a test-only no-op implementation so startup no longer attempts the SQL Server migration chain, kept streak/badge sequencing inside `GoalsService`, and added regression coverage for SQL Server-style `datetime2` badge timestamps near the business-date boundary.

**Files Changed**:
- `TCTEnglish/Services/GoalsService.cs` - treated daily activity rows as stored business dates directly and kept badge recent-unlock evaluation on the UTC-aware helper path.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs` - replaced the runtime migrator in the SQLite host with a no-op test implementation.
- `TCTEnglish.Tests/Infrastructure/NoOpMigrator.cs` - added a test-only EF Core migrator that makes startup `Database.Migrate()` a no-op.
- `TCTEnglish.Tests/GoalsPhase4IntegrationTests.cs` - added a regression test for SQL Server `datetime2` badge timestamps near local midnight.
- `.ai/context/known-issues.md` - moved the goals business-date issue out of unresolved technical debt.
- `TCTEnglish/Migrations/20260329111532_AddAiChatPhase1Data.cs` - removed empty stray migration artifact.
- `TCTEnglish/Migrations/20260329111532_AddAiChatPhase1Data.Designer.cs` - removed empty stray migration artifact.
- `TCTEnglish/Migrations/20260329164730___TempPendingInspection.cs` - removed stray AI chat migration artifact.
- `TCTEnglish/Migrations/20260329164730___TempPendingInspection.Designer.cs` - removed stray AI chat migration snapshot artifact.

**Verification**: Ran `dotnet ef migrations has-pending-model-changes --project TCTEnglish/TCTEnglish.csproj --startup-project TCTEnglish/TCTEnglish.csproj --context DbflashcardContext --no-build`, then ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase4IntegrationTests` and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~GoalsPhase5IntegrationTests`.

**Commit**: Not created yet.

**Notes**: This replaces the temporary controller-level badge refresh workaround with service-owned orchestration already present in the workspace, and it keeps `Program.cs` untouched by isolating the migration behavior inside the test host only.

### Streak badge unlock bị trễ một lượt học vì thứ tự cập nhật streak và badge — 2026-03-30

**Symptom**: Khi người dùng đạt mốc streak (ví dụ 3 ngày liên tiếp) trong request học hiện tại, badge streak có thể chưa mở khóa ngay, chỉ xuất hiện ở lượt học sau.

**Root Cause**: `LearningApiController.Record` gọi `IGoalsService.RecordActivityAsync(...)` (có refresh badge) trước khi gọi `IStreakService.UpdateStreakAsync(...)`, nên logic badge đọc `User.Streak` cũ trong cùng request.

**Solution**: Thêm API refresh badge tường minh trong goals service và gọi lại ngay sau khi cập nhật streak để đồng bộ trạng thái badge theo streak mới trong cùng request.

**Files Changed**:
- `TCTEnglish/Services/IGoalsService.cs` — thêm `RefreshBadgesAsync(int userId)`.
- `TCTEnglish/Services/GoalsService.cs` — triển khai `RefreshBadgesAsync(...)` dùng lại `RefreshUserBadgesAsync(...)`.
- `TCTEnglish/Controllers/LearningApiController.cs` — gọi `await _goalsService.RefreshBadgesAsync(currentUserId);` sau `UpdateStreakAsync(...)`.
- `TCTEnglish.Tests/GoalsPhase5IntegrationTests.cs` — thêm regression test `LearningRecord_ReachesThreeDayStreak_UnlocksStreakBadgeImmediately`.

**Verification**: `run_build` thành công. Chạy `GoalsPhase5IntegrationTests` bị chặn bởi lỗi nền workspace `PendingModelChangesWarning` (liên quan migration local chưa đồng bộ), không do thay đổi fix này.

**Commit**: Not created yet.

**Notes**: Fix này giữ nguyên boundary hiện tại (`LearningApiController` + `IGoalsService`), không thêm logic sang `HomeController`.

### Goals core rollout and challenge-token hardening restored after cleanup — 2026-03-28

**Symptom**: During commit cleanup, related updates around goals rollout documentation and daily-challenge token validation were dropped from working changes even though they were still required.

**Root Cause**: The workspace was intentionally trimmed to a narrow `goals core` commit scope, which discarded additional local updates that were planned for a follow-up commit.

**Solution**: Restored the dropped updates by reapplying token-based daily challenge validation flow (`HomeController` + dashboard views), startup migration block in `Program.cs` per team decision, XP reward toast in vocabulary study view, and documentation/context updates in known-issues and bug-fix log.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` - restored signed challenge token generation/validation and secure `CheckAnswer` flow.
- `TCTEnglish/ViewModels/DailyChallengeViewModel.cs` - restored `ChallengeToken` property.
- `TCTEnglish/Views/Home/Index.cshtml` and `TCTEnglish/Views/Home/_DailyChallenge.cshtml` - restored token-aware answer submission.
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - restored XP reward toast display.
- `TCTEnglish/Program.cs` - restored startup `Database.Migrate()` block.
- `.ai/context/known-issues.md` and `.ai/context/bug-fix-log.md` - restored context records.

**Verification**: Build/check verification should be run after reapplying all restored files.

**Commit**: Not created yet.

**Notes**: This entry documents restoration of intentionally removed local changes that were later confirmed as still required.

### Writing UI still showed mojibake Vietnamese text that looked like font corruption — 2026-03-28

**Symptom**: Learners still saw broken Vietnamese fragments (for example `TrÃ¢n trá»ng`, `ChÃºc may máº¯n`) in writing-related UI paths, which appeared as a font/display issue on the interface.

**Root Cause**: Legacy mojibake values could still flow from persisted writing sentence content into practice/hint rendering. Closing-line detection also contained explicit mojibake matching, confirming mixed-encoding data was still being tolerated but not normalized for display.

**Solution**: Added display normalization for known mojibake Vietnamese phrases in `StudyService.Writing` and applied it on writing sentence payload/hint lookup paths before returning view/API data. Also simplified closing-line detection to run on normalized text only.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - added `NormalizeVietnameseDisplayText(...)`, normalized sentence text in practice/hint pipelines, and updated closing-line detection to use normalized text.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression assertions to ensure writing page and writing practice JSON payload do not contain known mojibake fragments.

**Verification**: Ran `dotnet build` (passed). Ran `Sprint1SmokeTests` via filter `TypeName=TCTEnglish.Tests.Sprint1SmokeTests` (27/27 passed).

**Commit**: Not created - workspace already contains unrelated local changes.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this specific writing mojibake issue is not currently listed there.

### Writing smoke tests drifted from localized UI copy and AI evaluator rejected code-fenced JSON — 2026-03-28

**Symptom**: Writing smoke tests failed after recent writing updates because assertions still expected older English UI text (`0/14 completed`, `Closing hint`) while the live writing UI/hint copy had moved to Vietnamese. In addition, AI writing evaluation had a latent parsing risk: if the model returned JSON wrapped in markdown code fences, deserialization failed and silently forced rule-based fallback.

**Root Cause**: Test fixtures were not updated alongside localization changes in writing views/service responses. The AI evaluation parser expected raw JSON only and did not normalize fenced markdown payloads.

**Solution**: Updated writing smoke-test assertions to match the current localized writing output and added AI response normalization in `StudyService.Writing` to strip markdown code fences before JSON deserialization.

**Files Changed**:
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - aligned writing assertions with current Vietnamese writing copy.
- `TCTEnglish/Services/StudyService.Writing.cs` - added `NormalizeAiJsonPayload(...)` and applied it before deserializing AI evaluation output.

**Verification**: Ran `dotnet build` (passed). Ran `Sprint1SmokeTests` via test explorer filter `TypeName=TCTEnglish.Tests.Sprint1SmokeTests` (27/27 passed). Ran `git diff --check -- TCTEnglish/Services/StudyService.Writing.cs TCTEnglish.Tests/Sprint1SmokeTests.cs .ai/context/bug-fix-log.md` (passed; line-ending warning only).

**Commit**: Not created - workspace already contains unrelated local changes.

**Notes**: This follows the same writing hardening trend as previous entries by reducing fallback-only behavior when AI responses are structurally valid but wrapped in markdown. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice CTA sat below the fold and failure messaging overemphasized AI fallback - 2026-03-28

**Symptom**: On the `Writing Practice` page at `1280x720`, users landed on the exercise without seeing the textarea CTA immediately because the compose area sat too low. On small mobile widths, the shared header felt cramped because the search box copy was too long for the available space. When a writing sentence failed, the warning banner led with `AI feedback is unavailable...`, which made the screen feel broken instead of clearly telling the learner the sentence still needed revision.

**Root Cause**: The practice page still spent too much vertical space on pre-compose chrome and the workspace column needed a denser desktop treatment. The shared header only tightened spacing a little on mobile and did not shorten placeholder copy responsively. The writing client rendered rule-based evaluation copy directly from the server payload, so fallback-mode responses surfaced the AI-unavailable note inside the main failure banner instead of separating the fallback note from the learner-facing retry state.

**Solution**: Tightened the writing practice layout, widened the workspace column, reduced compose spacing for shorter desktop viewports, and kept the compose CTA visually prioritized. Improved the shared mobile header by adding a compact search placeholder plus tighter responsive spacing. Added a dedicated fallback note below the feedback banner and normalized rule-based evaluation copy in the client so the main banner now says the sentence needs another try while the AI-fallback explanation appears separately.

**Files Changed**:
- `TCTEnglish/Views/Shared/_Layout.cshtml` - added a stable search input id plus default/compact placeholder metadata for responsive header behavior.
- `TCTEnglish/wwwroot/js/layout.js` - swapped the search placeholder to `Search` on narrow screens.
- `TCTEnglish/wwwroot/css/layout.css` - tightened mobile header spacing, resized action buttons, and gave the search box more usable room.
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - added a dedicated feedback source note under the banner.
- `TCTEnglish/wwwroot/js/writing.js` - split rule-based fallback copy from retry/acceptance copy and rendered a separate fallback note when AI is unavailable.
- `TCTEnglish/wwwroot/css/writing.css` - widened the workspace column, tightened compose spacing, and styled the new fallback note.
- `TCTEnglish/Services/StudyService.Writing.cs` - aligned the non-AI retry title with the learner-facing `Try this sentence again` wording.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Shared/_Layout.cshtml TCTEnglish/wwwroot/js/layout.js TCTEnglish/wwwroot/css/layout.css TCTEnglish/Views/Study/WritingPractice.cshtml TCTEnglish/wwwroot/js/writing.js TCTEnglish/wwwroot/css/writing.css TCTEnglish/Services/StudyService.Writing.cs` (passed, with line-ending warnings only). Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors). Re-verified in a real browser with Playwright at `1280x720` and `390x844`, including a failed submit case that now shows `Try this sentence again` plus a separate fallback note.

**Commit**: Not created - workspace already contained unrelated local changes.

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run. `known-issues.md` was not updated because this specific UI/UX regression was not listed there.

### Writing Exercises header still looked broken after partial reordering - 2026-03-28

**Symptom**: Even after earlier header tweaks, the `Writing Exercises` page still looked visually broken because the back button and breadcrumb did not form a balanced top row and the title/filters row still felt misaligned.

**Root Cause**: The previous iteration grouped the back button with the title column, but the breadcrumb stayed in the controls column above the filters, which kept the whole header visually uneven.

**Solution**: Reworked the header into two explicit rows: a top bar with `Back` on the left and the breadcrumb on the right, followed by a second row with `Available Exercises` on the left and the filters on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - split the page header into a top bar and a separate title/filter row.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - added top-bar styling and rebalanced the title/filter row alignment.
- `.ai/context/bug-fix-log.md` - added this final header-layout correction record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): split header into topbar and controls`

**Notes**: This keeps the breadcrumb close to the filters while also preventing the back button from creating a visually empty standalone block. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises header split the back button onto a broken standalone row - 2026-03-28

**Symptom**: The `Writing Exercises` page header looked visually broken because the back button sat on its own row above the title, while the breadcrumb and filters were grouped separately on the right.

**Root Cause**: `WritingExercises.cshtml` rendered the back button in a standalone toolbar block before the main header layout instead of grouping it with the page title column.

**Solution**: Moved the back button into the left title column so the header now forms two balanced columns: `back button + title` on the left and `breadcrumb + filters` on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - merged the back button into the title column.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - removed the standalone toolbar styling and added a title-block layout for the left header column.
- `.ai/context/bug-fix-log.md` - added this layout correction record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): rebalance header layout`

**Notes**: This keeps the breadcrumb above the filters as requested, while removing the extra empty-looking top row. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises breadcrumb sat too far away from the filters - 2026-03-28

**Symptom**: On the `Writing Exercises` page, the `Writing > Beginner > Emails` breadcrumb appeared detached from the filter controls instead of sitting above them where users expected the context label.

**Root Cause**: `WritingExercises.cshtml` rendered the breadcrumb as a separate block above the page toolbar instead of grouping it with the right-side filter controls.

**Solution**: Moved the breadcrumb into the head controls column and placed it directly above the filters, then adjusted the layout CSS so the heading stays on the left while the breadcrumb and filters stack together on the right.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - moved the breadcrumb into the filter controls block above the form.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - updated head alignment and added styling for the new controls column layout.
- `.ai/context/bug-fix-log.md` - added this layout fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): place breadcrumb above filters`

**Notes**: On narrower screens the controls column now expands full-width and left-aligns so the breadcrumb still stays close to the filters. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing Exercises page had no clear way back to level selection - 2026-03-28

**Symptom**: On the `Writing Exercises` page, users could see the breadcrumb but did not have an obvious dedicated button to return to the Writing screen and reselect level or content type.

**Root Cause**: `WritingExercises.cshtml` only rendered the breadcrumb plus filters and exercise cards, so the back-navigation affordance was too subtle for the page flow.

**Solution**: Added an explicit `Back to level and format` action above the exercise heading and styled it to match the current Writing blue theme.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingExercises.cshtml` - added a dedicated back button that returns to `Study/Writing` with the current level.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - added matching styles for the new back button.
- `.ai/context/bug-fix-log.md` - added this UX fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingExercises.cshtml TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-exercises): add explicit back navigation`

**Notes**: The breadcrumb remains in place, but the main flow now has a much more visible return action. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing level selection reloaded the whole index page - 2026-03-28

**Symptom**: On the Writing index page, every level selection (`Beginner`, `Intermediate`, `Advanced`) triggered a full page reload before the user could continue to choose a content type.

**Root Cause**: The level cards in `Writing.cshtml` were plain navigation links back to the same `StudyController.Writing` action with a different `level` query string, so the level switch always round-tripped to the server.

**Solution**: Kept the links as a no-JavaScript fallback, but added client-side level switching for the Writing index page. The new script updates the selected card state, refreshes the selected-level helper copy, rewrites the content-type links to the chosen level, and syncs the URL with `history.replaceState(...)` without reloading the page.

**Files Changed**:
- `TCTEnglish/Views/Study/Writing.cshtml` - added data hooks for level cards and content links, plus the page-specific Writing index script reference.
- `TCTEnglish/wwwroot/js/writing-index.js` - added client-side level-selection behavior so changing levels no longer reloads the page.
- `.ai/context/bug-fix-log.md` - added this UX fix record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/Writing.cshtml TCTEnglish/wwwroot/js/writing-index.js` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-index): switch levels without page reload`

**Notes**: The server route still works as a fallback if JavaScript is unavailable, so direct links/bookmarks with `?level=...` remain valid. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing pages still missed the app's real brand blue - 2026-03-28

**Symptom**: Even after the lighter UI pass, the Writing screens still did not match the actual TCT site branding because the user wanted the blue logo/header color, not the temporary green palette. The `Writing` index screen also remained on an older dark theme.

**Root Cause**: The previous visual update optimized layout density and removed the dark feel, but it used a green accent family that did not match the app's canonical header/logo color (`#4255ff`). `writing-index.css` was also still on its own older dark/yellow treatment.

**Solution**: Re-themed all Writing learner pages (`Writing`, `Writing Exercises`, `Writing Practice`) to the app's brand-blue palette based on the shared header/logo color, replacing the remaining dark/yellow and green variants with blue surfaces, blue buttons, blue chips, blue progress styling, and blue status treatments.

**Files Changed**:
- `TCTEnglish/wwwroot/css/writing-index.css` - moved the Writing index page from the old dark/yellow theme to the shared blue brand palette.
- `TCTEnglish/wwwroot/css/writing.css` - switched the Writing Practice page from green accents to the app's blue brand palette.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - switched the Writing Exercises list and status badges to the same blue brand palette.
- `.ai/context/bug-fix-log.md` - added this branding follow-up record.

**Verification**: Ran `git diff --check -- TCTEnglish/wwwroot/css/writing-index.css TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): align writing screens with brand blue`

**Notes**: A hard refresh in the browser may be needed if cached CSS still shows the earlier palette. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice still used dark/yellow styling and cramped status chips - 2026-03-28

**Symptom**: After the first UI cleanup, the writing practice page still looked too dark compared with the main site theme, progress and accent surfaces still felt heavy, the inline highlighted text chip looked cramped, and the `New` status badge on the exercise list still appeared too tight and off-theme.

**Root Cause**: The first pass compacted the layout but kept a dark practice palette derived from the writing sub-theme. `writing.css` still centered its design around dark panels and yellow accents, and `writing-exercises.css` did not define a dedicated `new` badge style that matched the site's green direction.

**Solution**: Reworked the writing practice and exercise-list styling around a light green app-aligned palette, removed the dark page background treatment, converted yellow accents to green, reduced heading and panel typography further, improved inline sentence chip padding, and added a dedicated green `new` status badge style for exercise cards.

**Files Changed**:
- `TCTEnglish/wwwroot/css/writing.css` - switched the practice page from dark/yellow to light green surfaces, tightened text sizing, softened the progress area, and improved inline sentence chip padding.
- `TCTEnglish/wwwroot/css/writing-exercises.css` - changed the exercise list to the same light green palette and added refined `new`, `in-progress`, and `completed` badge styling.
- `.ai/context/bug-fix-log.md` - added this follow-up incident record.

**Verification**: Ran `git diff --check -- TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/css/writing-exercises.css` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): align practice and badges with green site theme`

**Notes**: Browser QA is still recommended for final visual tuning because this pass was validated through code/build checks only. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing practice layout was oversized, theme-drifted, and missed sentence state styling - 2026-03-28

**Symptom**: The writing practice page felt too large and vertically long, several top metadata chips and icons were hard to read against the page background, and completed or retry sentence states did not render consistently after submit.

**Root Cause**: `WritingPractice.cshtml` rendered the practice workspace without the shared writing-theme background used by the writing index screen, `writing.css` used larger spacing and a tall stacked feedback layout, and `writing.js` toggled `is-completed` and `is-retry` while the CSS only styled `is-submitted` and `is-editing`.

**Solution**: Re-themed the practice page to reuse the navy/gold writing palette, tightened panel spacing and vertical rhythm, made the source passage and feedback area more compact with internal scrolling and a denser feedback grid, added themed icons in the feedback cards, and aligned the JS/CSS sentence-state classes so completed, editing, and retry states render correctly.

**Files Changed**:
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - wrapped the page in a themed shell and updated the feedback cards to use compact, color-aligned icon labels.
- `TCTEnglish/wwwroot/css/writing.css` - rebuilt the practice-page styling around the shared writing palette, reduced oversized spacing, added compact scrollable panels, and styled the actual sentence-state classes used by the script.
- `TCTEnglish/wwwroot/js/writing.js` - aligned sentence state toggles with the CSS so completed, editing, and retry states show correctly in the passage.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `git diff --check -- TCTEnglish/Views/Study/WritingPractice.cshtml TCTEnglish/wwwroot/css/writing.css TCTEnglish/wwwroot/js/writing.js` (passed) and `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` with workspace-local `DOTNET_CLI_HOME` and `HOME` (passed, 0 warnings / 0 errors).

**Commit**: `fix(writing-ui): compact and retheme practice workspace`

**Notes**: Full browser QA is still recommended to fine-tune feel on real content lengths. `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be run.

### Writing closing-line hints failed on UTF-8 Vietnamese phrases - 2026-03-27

**Symptom**: Some writing-practice closing sentences did not return `Closing hint` even when the Vietnamese source line clearly contained a closing phrase like `Trân trọng` or `Chúc may mắn`.

**Root Cause**: `StudyService.Writing.IsClosingLine(...)` matched only mojibake string fragments (`TrÃ¢n trá»ng`, `ChÃºc may máº¯n`) and missed the proper UTF-8 Vietnamese forms.

**Solution**: Expanded closing-phrase detection to support both proper UTF-8 Vietnamese strings and legacy mojibake variants, then added smoke-test coverage that discovers a seeded closing sentence and verifies the hint endpoint returns `Closing hint`.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - updated closing-phrase detection logic in `IsClosingLine(...)`.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression assertion for closing-line hint title on seeded writing data.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `TCTEnglish.Tests.Sprint1SmokeTests.WritingJsonEndpoints_ReturnServiceBackedPayloads` (passed) and workspace build (successful).

**Commit**: `fix(writing): restore closing-hint detection for utf8 vietnamese`

**Notes**: `scripts/encoding_guard.py` is not present in this repository, so no encoding guard script could be executed.

### Writing practice leaked reference answers and skipped server-side evaluation - 2026-03-28

**Symptom**: The writing practice page embedded `EnglishMeaning` directly into the page payload, the browser compared answers locally, and submit moved to the next sentence without any dedicated backend evaluation or graded feedback.

**Root Cause**: The first writing slice had DB-backed reference answers and read-only writing endpoints, but no POST evaluation endpoint. `WritingPractice.cshtml` serialized `EnglishMeaning` into the client bootstrap payload, and `writing.js` treated submit as a local state transition instead of a server review.

**Solution**: Moved sentence evaluation into `StudyController` + `IStudyService`/`StudyService.Writing`, added `POST /Home/Writing/Practice/Evaluate` with anti-forgery, removed `EnglishMeaning` from practice page/client payloads, converted hint responses to non-answer hint text, and wired the frontend to submit each sentence to the server. The new service path performs a fast rule-based check first, then optionally calls OpenAI for meaning/grammar/naturalness/word-choice feedback when an API key is configured, with safe rule-based fallback when AI is unavailable.

**Files Changed**:
- `TCTEnglish/Controllers/StudyController.cs` - added the server-side writing evaluation endpoint while keeping the work inside the `StudyController` boundary.
- `TCTEnglish/Services/IStudyService.cs` - added the writing evaluation contract.
- `TCTEnglish/Services/StudyService.cs` - extended constructor dependencies for configuration, HTTP, and logging needed by AI-backed evaluation.
- `TCTEnglish/Services/StudyService.Writing.cs` - removed reference answers from practice DTOs, replaced hint output with safe hint text, and implemented rule-based + optional AI evaluation flow.
- `TCTEnglish/ViewModels/WritingIndexViewModel.cs` - added evaluation request/response models and removed `EnglishMeaning` from practice sentence view models sent to the client.
- `TCTEnglish/Views/Study/WritingPractice.cshtml` - removed `englishMeaning` from the bootstrapped JSON, added anti-forgery markup, and updated the feedback copy for server-driven evaluation.
- `TCTEnglish/wwwroot/js/writing.js` - changed submit flow to call the new backend endpoint and only complete/auto-advance a sentence when the server returns pass.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added regression coverage for hidden answers, hint shape, antiforgery on evaluate, and evaluate JSON responses.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore -p:OutputPath=D:\TCTEnglish\.tmp\build\TCTEnglish\ -p:UseAppHost=false` and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~Sprint1SmokeTests -p:OutputPath=D:\TCTEnglish\.tmp-tests\bin\Debug\net10.0\`. The focused smoke suite passed `27/27`. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(writing): evaluate practice sentences on the server`

**Notes**: When no OpenAI API key is configured, the feature falls back to a rule-based evaluation so the learner loop still works without leaking the teacher reference answer. The AI prompt still uses `EnglishMeaning` server-side only.

### Writing exercises crash with `Invalid object name 'WritingExercises'` - 2026-03-27

**Symptom**: Opening `/Home/Writing/Exercises` threw SQL Server exception `Invalid object name 'WritingExercises'` from `StudyService.LoadWritingExerciseCardsAsync`.

**Root Cause**: The writing feature shipped with new EF tables/migration, but runtime environments could execute writing queries before pending SQL Server migrations were applied.

**Solution**: Added a one-time SQL Server schema-initialization guard in `StudyService.Writing` that runs `Database.MigrateAsync()` before writing-table queries (`WritingExercises`, `WritingExerciseSentences`) execute.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Writing.cs` - added `EnsureWritingSchemaReadyAsync()` (with `SemaphoreSlim` gate) and invoked it before all writing DB query entry points.
- `.ai/context/bug-fix-log.md` - added this incident record.

**Verification**: Built the solution and ran focused study smoke tests after the change; writing-service code compiles and no new compilation errors were introduced.

**Commit**: `fix(writing): apply pending migrations before writing queries`

**Notes**: This fix targets SQL Server only (`Database.IsSqlServer()`), so SQLite test setup (`EnsureCreated`) remains unaffected.

### Class admin mutation consistency, legacy auth route fallback, and coverage hardening - 2026-03-20

**Symptom**: After the controller/service split, Admin users could view private class detail but still could not kick members or remove shared folders through the legacy `/Home/*` class actions. `ClassChatHub` also still duplicated class-access checks locally, and the leftover `HomeController.Login/Register` actions pointed at missing Home views instead of the real account endpoints.

**Root Cause**: Authorization rules had drifted between the class detail ViewModel/UI, `ClassService` mutation methods, and the SignalR hub. At the same time, the refactor left behind legacy `HomeController` auth actions that no longer matched the moved view structure. Several of these seams were also missing direct regression tests.

**Solution**: Extended class mutation authorization so admin users can remove shared folders and kick class members consistently with the existing `CanManageClass` boundary, centralized SignalR class-access checks through `IClassService.CanAccessClassAsync(...)`, redirected legacy `/Home/Login` and `/Home/Register` to `AccountController`, and added focused regression coverage for admin class mutations, secondary study modes, save/unsave folder flows, and legacy auth-route redirects.

**Files Changed**:
- `TCTEnglish/Services/IClassService.cs` - expanded class mutation signatures to carry the admin authorization context explicitly.
- `TCTEnglish/Services/ClassService.cs` - aligned `CanRemove`, `RemoveFolderFromClassAsync`, and `KickMemberAsync` with admin manage-class permissions.
- `TCTEnglish/Controllers/ClassController.cs` - passed `IsAdminUser()` into the class mutation service calls.
- `TCTEnglish/Hubs/ClassChatHub.cs` - replaced the duplicated hub-local class access check with `IClassService.CanAccessClassAsync(...)`.
- `TCTEnglish/Views/Class/ClassDetail.cshtml` - exposed the Kick action whenever `CanManageClass` is true instead of owner-only.
- `TCTEnglish/Controllers/HomeController.cs` - redirected legacy Home auth endpoints to `AccountController` so old URLs remain safe.
- `TCTEnglish.Tests/Sprint2SmokeTests.cs` - added admin regression coverage for kicking members and removing shared folders.
- `TCTEnglish.Tests/CriticalFlowSqliteIntegrationTests.cs` - added coverage for secondary study-mode auth, legacy Home auth redirects, and save/unsave folder round-trips.
- `TCTEnglish.Tests/Sprint4SmokeTests.cs` - added a structural assertion that `ClassChatHub` requires constructor-injected `IClassService`.

**Verification**: Ran `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore` with workspace-local `DOTNET_CLI_HOME`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`, and `DOTNET_NOLOGO`; the suite passed `67/67`. The only remaining output was the existing `NU1900` warning because the environment could not reach the NuGet vulnerability feed.

**Commit**: `fix(class-auth): align admin mutations and legacy route fallbacks`

**Notes**: This continues the same authorization-alignment pattern as the 2026-03-19 SignalR/class-detail fix, but extends it to mutation flows and adds broader regression guards so HTTP and SignalR access rules stay in sync.

### Post-refactor documentation drift and EOF hygiene - 2026-03-20

**Symptom**: `git diff --check` still failed because `TCTEnglish/Views/Vocabulary/Study.cshtml` had a blank line at EOF, and the main architecture/context docs still described the pre-refactor structure (for example "no test project", `ChatController` living in `HomeController`, and legacy `ViewModel/` paths).

**Root Cause**: Documentation and hygiene drift accumulated after the controller/service/view-model refactor landed in multiple steps. The codebase moved faster than the supporting markdown/context files.

**Solution**: Removed the EOF blank line in `Study.cshtml`, rewrote the architecture backlog to match the current controller/service/test structure, created a fresh implementation plan for the remaining work, and updated `known-issues.md` plus this log so future agents see the post-refactor paths and priorities first.

**Files Changed**:
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - removed the extra trailing blank line so `git diff --check` no longer reports the EOF error.
- `.ai/context/known-issues.md` - replaced outdated unresolved items with the remaining confirmed issues and debt after the refactor.
- `.ai/context/bug-fix-log.md` - added a refactor-sync entry so older historical notes are read with the current controller/view/view-model layout in mind.
- `docs/post-refactor-followup-plan.md` - added the current post-refactor implementation plan.
- `docs/architecture-prioritized-backlog.md` - rewrote the English backlog around the current codebase state.
- `docs/architecture-prioritized-backlog.vi.md` - rewrote the Vietnamese backlog around the current codebase state.

**Verification**: Re-ran `git diff --check` after the hygiene fix and then validated the codebase snapshot against the live controllers, services, view models, views, DI registrations, and test project under `TCTEnglish.Tests`. Full build/test verification was run after the documentation sync to make sure the repo state still matched the updated docs.

**Commit**: `chore(docs): sync post-refactor backlog and repo hygiene`

**Notes**: Older entries from 2026-03-19 intentionally describe the pre-normalization layout from that moment in history. In the current codebase, the equivalent feature files now live under `Controllers/ClassController.cs`, `Controllers/FolderController.cs`, `Views/Class/`, `Views/Folder/`, and `ViewModels/`.

### Admin speaking video playlist Vietnamese messages mojibake - 2026-03-20

**Symptom**: The admin speaking video edit flow showed broken Vietnamese text in the playlist validation error and the success toast after saving, with visibly garbled multi-byte fragments instead of readable UI copy.

**Root Cause**: Two user-facing strings in `SpeakingVideoManagementController.cs` had been saved with mixed Windows-1252/UTF-8-decoded text. The broader investigation also confirmed that several docs/views only looked broken in the terminal, not on disk, because their UTF-8 bytes were still valid.

**Solution**: Replaced the two corrupted literals with clean Vietnamese UTF-8 strings and re-scanned the repo with a byte-level UTF-8 + repairability check so only genuinely broken text was edited.

**Files Changed**:
- `TCTEnglish/Areas/Admin/Controllers/SpeakingVideoManagementController.cs` - restored the playlist validation message and update success toast to proper Vietnamese UTF-8.

**Verification**: Ran a repo-wide Python scan over tracked/untracked text files and confirmed `INVALID_UTF8_COUNT=0` plus `REPAIRABLE_HIT_COUNT=0` after the fix. Also ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore` with workspace-local `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE`; build succeeded with 9 pre-existing nullable warnings only. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(admin-speaking): normalize Vietnamese encoding in playlist messages`

**Notes**: `AGENTS.md`, `.agent/workflows/bug-investigation.md`, `.ai/context/bug-fix-log.md`, `.ai/context/known-issues.md`, `README.md`, `TCTEnglish/Views/Vocabulary/Study.cshtml`, `TCTEnglish/Views/Study/WriteMode.cshtml`, `TCTEnglish/Models/JsonVocabularySeeder.cs`, and `TCTEnglish/Views/Speaking/Practice.cshtml` were reviewed because terminal output looked suspicious, but they were already valid UTF-8 and were not auto-edited.

### Vocabulary study view Vietnamese text mojibake in comments and UI - 2026-03-20

**Symptom**: `Views/Vocabulary/Study.cshtml` showed broken Vietnamese text in comments, settings labels, study feedback messages, shuffle toasts, and the completion overlay. The mojibake also leaked into visible UI copy, making parts of the vocabulary study page hard to read.

**Root Cause**: The study view contained mixed-encoding text after recent edits. Some literals had been decoded through a Windows-1252/Latin-1 path instead of staying in UTF-8, so a single `.cshtml` file ended up with both valid Vietnamese strings and mojibake sequences.

**Solution**: Normalized the corrupted literals in `Study.cshtml` back to UTF-8 while preserving the current ViewModel-based refactor and anti-forgery updates already present in the working tree. Re-checked the entire `Views/Vocabulary/` folder with a targeted mojibake detection pass to confirm no repairable lines remained.

**Files Changed**:
- `TCTEnglish/Views/Vocabulary/Study.cshtml` - restored Vietnamese comments and UI strings in the settings modal, study actions, result messages, shuffle messages, and completion overlay without reverting unrelated in-progress refactor changes.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore` with `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` pointed at the workspace-local `.dotnet-home`; build succeeded with pre-existing warnings only. Also ran a workspace-local Python scan over `TCTEnglish/Views/Vocabulary/*.cshtml` and confirmed `repairable_lines=0`. `scripts/encoding_guard.py` was not present, so no encoding guard script could be run.

**Commit**: `fix(vocabulary-study): normalize Vietnamese text encoding in study view`

**Notes**: This was an encoding-normalization bug in the view layer, not a controller or EF issue. `known-issues.md` was not updated because this exact bug was not listed there.

### Dashboard random sampling broke SQLite tests after controller split - 2026-03-20

**Symptom**: `/Home/Index` returned HTTP 500 in the SQLite integration test environment, and the full suite failed on the authenticated dashboard smoke tests. The dashboard challenge endpoint also pointed to a partial that did not exist after the view move.

**Root Cause**: `HomeController` used `OrderBy(_ => Guid.NewGuid())` inside EF queries for dashboard folder suggestions and daily challenge distractors. That translation works on SQL Server but not on the SQLite provider used by the test host. At the same time, `Views/Home/Index.cshtml` still contained stale links from the pre-split controller layout and `GetDailyChallenge()` referenced `_DailyChallenge` without a matching partial file in the moved view structure.

**Solution**: Kept the SQL Server random-order branch, then added a provider-safe fallback that samples ordered ids in memory for non-SQL Server providers. Restored the missing `_DailyChallenge` partial, switched the dashboard view to render through that partial, and updated the remaining dashboard links so the moved class/folder/study pages still resolve through their new controllers.

**Files Changed**:
- `TCTEnglish/Controllers/HomeController.cs` - added provider-aware random sampling helpers for dashboard folders and daily challenge wrong answers.
- `TCTEnglish/Views/Home/Index.cshtml` - updated stale controller links and rendered the daily challenge through the restored partial.
- `TCTEnglish/Views/Home/_DailyChallenge.cshtml` - added the partial returned by `GetDailyChallenge()`.
- `TCTEnglish.Tests/Sprint1SmokeTests.cs` - added coverage for the daily challenge partial endpoint.

**Verification**: Ran `dotnet build TCTEnglish/TCTEnglish.csproj --no-restore`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~Sprint1SmokeTests`, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore --filter FullyQualifiedName~CriticalFlowSqliteIntegrationTests`, and `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --no-restore` with `DOTNET_CLI_HOME`, `HOME`, and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` pointed at the workspace-local `.dotnet-home`; all commands passed, including the full 59-test suite.

**Commit**: `fix(home-dashboard): use provider-safe random sampling and restore challenge partial`

**Notes**: This is a provider-compatibility bug rather than a SQL Server production defect. `known-issues.md` was not updated because this specific issue was not listed there.

### Split folder/set controllers loaded owner resources before scope check - 2026-03-20

**Symptom**: Remaining Sprint 1 folder/set mutations in the split controllers could load a `Folder` or `Set` by id first and only then compare `UserId` or `OwnerId`, leaving an anti-IDOR gap in the data access pattern.

**Root Cause**: `FolderController.DeleteFolder`, `FolderController.UpdateFolderName`, `SetController.DeleteSet`, `SetController.RemoveSetFromFolder`, `SetController.EditSet` (GET/POST), and the adjacent `CreateSet` folder lookup used post-load ownership checks instead of embedding the owner/admin scope in the EF query.

**Solution**: Mutable folder/set actions now use a safer combined `GetFolder()`/`GetSet()` that always applies the current user and admin scope, and the remaining slice of folder/set queries in the study services were updated to use explicit user or admin filtering where relevant.

**Files Changed**:
- `TCTEnglish/Services/StudyService.Folder.cs` - replaced legacy `GetFolder` uses in update/delete actions with safe-get pattern.
- `TCTEnglish/Services/StudyService.Set.cs` - replaced legacy `GetSet` uses in update/delete/actions with safe-get pattern.
- `TCTEnglish/Controllers/FolderController.cs` - reverted unintentional test-only deletions of scope-related code.
- `TCTEnglish/Controllers/SetController.cs` - maintained admin authorization context in the update and delete actions.
- `TCTEnglish.Tests/CriticalFlowSqliteIntegrationTests.cs` - added more coverage for upload/answer flows against deleted or retracted content.
- `TCTEnglish.Tests/IntegrationSetup/Sqlite/NewSetUpload.cs` - validated that new set uploads fail when the parent folder is deleted.

**Verification**: Ran relevant integration and critical-flow tests with `--filter` targeting the changed files; all passed. Also verified that no new compilation or migration errors were introduced.

**Commit**: `fix(folder-set): apply user/admin scope in folder/set mutations`

**Notes**: This follows the same alignment pattern as previous admin-class mutations by ensuring all legacy action routes now resolve to the correct controller/service with the proper authorization context.
### MoMo callback/IPN signature verification used sorted dynamic params instead of fixed recipe - 2026-04-28

**Symptom**: Real MoMo callback/IPN payloads could fail signature verification, causing valid payments to not activate Premium (or mock confirmation to drift from real payload shape).

**Root Cause**: `MoMoSignatureHelper` built callback/IPN raw signature by sorting all inbound parameters and excluding only `signature/sign`. This did not enforce the official fixed-field recipe and did not require `accessKey` in the signed payload.

**Solution**: Reworked MoMo callback/IPN signing and verification to a fixed-field recipe with explicit `accessKey` injection, required-field validation, and unknown-field exclusion from hash data. Updated gateway and mock confirm flow to use the new signature API and include realistic callback fields.

**Files Changed**:
- `TCTEnglish/Services/Billing/MoMo/MoMoSignatureHelper.cs` - replaced dynamic sorted payload hashing with fixed callback field order, required-field checks, access-key validation, and new verify signature contract.
- `TCTEnglish/Services/Billing/MoMo/MoMoGateway.cs` - switched return/IPN verification calls to pass effective access key + secret key.
- `TCTEnglish/Controllers/BillingController.cs` - updated MoMo mock confirm payload and signature generation to the fixed callback recipe.
- `TCTEnglish.Tests/MoMoSignatureHelperTests.cs` - updated existing tests and added regression coverage for missing required fields and extra unknown fields.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "MoMo"` passed (16/16).

**Commit**: Not created.

**Notes**: This fix keeps Premium activation on IPN flow only; return URL behavior remains display-oriented and does not grant premium directly.
### VNPay signature raw-data hashing did not URL-encode values before HMAC - 2026-04-28

**Symptom**: VNPay signatures could mismatch when `vnp_OrderInfo` or other fields contained Vietnamese text, spaces, URLs, or reserved characters (`:`, `/`, `?`, `&`, `=`).

**Root Cause**: `VnPaySignatureHelper.BuildRawSignatureData(...)` concatenated raw key/value pairs directly for signing, while payment URL parameters were URL-encoded in query output. This encoding mismatch can break checksum verification for non-trivial characters.

**Solution**: Updated VNPay raw signature builder to URL-encode both keys and values before HMAC generation, keeping sorted key order and secure-hash field exclusion. Added regression tests for Vietnamese/special-character payloads and verification round-trip.

**Files Changed**:
- `TCTEnglish/Services/Billing/VnPay/VnPaySignatureHelper.cs` - URL-encoded key/value in raw signature data builder.
- `TCTEnglish.Tests/VnPayGatewayTests.cs` - added encoding-focused tests for raw signature data and verify behavior with Vietnamese/URL characters.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "VnPay|Ipn"` passed (49/49).

**Commit**: Not created.

**Notes**: Signature behavior is now consistent between checkout URL creation and callback/IPN verification paths.
### VNPay provider readiness allowed missing IPN URL and non-HTTPS callback URLs - 2026-04-28

**Symptom**: Provider health/readiness could report VNPay as effectively usable even when `IpnUrl` was missing or callback URLs were not strict HTTPS, increasing risk of successful checkout without server-to-server activation path.

**Root Cause**: `VnPayOptions.GetConfigurationErrors()` required `ReturnUrl` but did not require/validate `IpnUrl`. `PaymentProviderHealthService` also treated VNPay return/IPN URL presence too loosely.

**Solution**: Hardened VNPay configuration validation to require absolute HTTPS URLs for both `ReturnUrl` and `IpnUrl`, and aligned health dashboard checks/missing-field diagnostics with the same rules. Added regression tests for missing `IpnUrl` in both health and checkout fail-fast behavior.

**Files Changed**:
- `TCTEnglish/Services/Billing/VnPay/VnPayOptions.cs` - added strict HTTPS validation for `ReturnUrl` and `IpnUrl`, plus reusable HTTPS URL validator.
- `TCTEnglish/Services/Billing/PaymentProviderHealthService.cs` - aligned VNPay health flags/missing fields with strict HTTPS + required `IpnUrl`.
- `TCTEnglish.Tests/PaymentProviderHealthServiceTests.cs` - added VNPay missing-ipn and fully-configured health regression tests.
- `TCTEnglish.Tests/VnPayGatewayTests.cs` - added fail-fast checkout regression when `IpnUrl` is missing.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "PaymentProviderHealth|VnPay"` passed (39/39).

**Commit**: Not created.

**Notes**: This change intentionally blocks VNPay checkout in misconfigured environments before redirecting users to provider pages.
### Billing background workers existed but were not registered in startup DI - 2026-04-28

**Symptom**: Pending payment cleanup and premium-expiry automation could be silently inactive at runtime even though worker classes existed.

**Root Cause**: `PendingPaymentCleanupWorker` and `PremiumExpiryWorker` were implemented with config flags, but `Program.cs` did not register them as hosted services. Also, default appsettings lacked explicit `Billing` worker toggles.

**Solution**: Registered both workers in startup DI and added explicit `Billing` config flags in appsettings (default and development) so behavior is controlled and visible.

**Files Changed**:
- `TCTEnglish/Program.cs` - registered `PendingPaymentCleanupWorker` and `PremiumExpiryWorker` with `AddHostedService`.
- `TCTEnglish/appsettings.json` - added `Billing:PendingPaymentCleanupWorkerEnabled` and `Billing:PremiumExpiryWorkerEnabled` defaults.
- `TCTEnglish/appsettings.Development.json` - added worker enable toggles for local/dev environment.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "Worker|Pending|Expiry|Subscription"` passed (25/25).

**Commit**: Not created.

**Notes**: Worker classes still self-check config and no-op when disabled, so startup registration is safe for environments that explicitly turn flags off.
### VNPay reconciliation used no-op QueryDR client and could not recover missing IPN cases - 2026-04-28

**Symptom**: Reconciliation worker could not query VNPay transaction status, so stale pending orders were only heuristically marked manual_review/expired and could miss provider-confirmed paid states when IPN was delayed/lost.

**Root Cause**: `Program.cs` registered `IVnPayQueryClient` as `NoOpVnPayQueryClient`, and there was no HTTP QueryDR implementation wired into reconciliation flow.

**Solution**: Implemented `VnPayQueryClient` with real QueryDR HTTP POST integration and HMAC-SHA512 request/response checksum handling per VNPay docs, extended VNPay options for QueryDR configuration, updated reconciliation query-client contract to pass transaction-created timestamp and optional provider transaction number, and switched DI registration from NoOp to HTTP client implementation.

**Files Changed**:
- `TCTEnglish/Services/Billing/VnPay/VnPayQueryClient.cs` - new QueryDR HTTP client with signed request + signed response verification.
- `TCTEnglish/Services/Billing/PaymentReconciliationService.cs` - updated query-client contract usage and interface method signature.
- `TCTEnglish/Services/Billing/VnPay/VnPayOptions.cs` - added `QueryDrUrl`, `QueryIpAddress`, `QueryDrCommand` options.
- `TCTEnglish/Program.cs` - switched DI registration to `AddHttpClient<IVnPayQueryClient, VnPayQueryClient>()`.
- `TCTEnglish/appsettings.Development.json` - added VNPay QueryDR config placeholders/defaults.
- `TCTEnglish.Tests/VnPayQueryClientTests.cs` - added unit tests for disabled config and valid signed QueryDR success response.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "Reconciliation|VnPayQuery|QueryDR"` passed (2/2).

**Commit**: Not created.

**Notes**: QueryDR client degrades safely (returns null) when VNPay QueryDR config is not enabled/complete or provider response signature is invalid.
### Admin manual-review orders could be marked but not resolved in UI/workflow - 2026-04-28

**Symptom**: Admin billing flow could mark orders as `manual_review` but lacked explicit resolve actions to confirm paid or reject with full audit trail, forcing awkward/manual follow-up.

**Root Cause**: `BillingManagementController` and admin details page only implemented `MarkManualReview`; there were no dedicated resolve endpoints/actions for the manual-review state.

**Solution**: Added admin resolve actions for manual-review orders: confirm paid (with subscription activation) and reject (mark failed), each with required reason, payment event append, and immutable `PaymentAdminAction` audit entry. Also expanded admin order status filters and added details-page forms for both resolve paths.

**Files Changed**:
- `TCTEnglish/Areas/Admin/Controllers/BillingManagementController.cs` - added `ResolveManualReviewConfirmPaid` and `ResolveManualReviewReject`, plus manual-review/partially-refunded order status filters.
- `TCTEnglish/Areas/Admin/ViewModels/Billing/BillingManagementViewModels.cs` - added `AdminResolveManualReviewRequest` model.
- `TCTEnglish/Areas/Admin/Views/BillingManagement/Details.cshtml` - added manual-review resolve forms (confirm paid / reject) with anti-forgery.
- `TCTEnglish.Tests/AdminBillingManagementTests.cs` - added controller tests for confirm-paid and reject resolve flows.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "FullyQualifiedName~AdminBillingManagementTests"` passed (8/8).
- Broader filter `Admin|ManualReview|BillingManagement` triggered unrelated pre-existing `WritingAdminContentIntegrationTests` 401 failures.

**Commit**: Not created.

**Notes**: Resolve actions are intentionally constrained to orders currently in `manual_review` state to preserve state-machine safety.
### Refund action ambiguity in admin UI/doc could imply unsupported automation - 2026-04-28

**Symptom**: Billing operations could be misunderstood as having in-app automated refund execution, while provider refund integration is not implemented.

**Root Cause**: Admin billing surfaces and configuration docs lacked explicit, prominent messaging that refund must be handled externally via provider portals.

**Solution**: Added explicit operational notice in admin billing order details page and documented refund support status in payment configuration guide (Option A: disable/clarify, no fake refund action).

**Files Changed**:
- `TCTEnglish/Areas/Admin/Views/BillingManagement/Details.cshtml` - added clear alert that automatic refund in admin is not supported and must be handled at provider side.
- `docs/payment-configuration.md` - added refund support status and operational SOP notes.

**Verification**:
- `dotnet test xay_dung_website_hoc_tu_vung_tieng_anh_TCT.sln -c Release --filter "FullyQualifiedName~AdminBillingManagementTests"` passed (8/8).

**Commit**: Not created.

**Notes**: This intentionally keeps refund execution out of UI until provider refund APIs are implemented end-to-end with audit and tests.
### Payment status normalization/mapping could coerce unknown states to pending and hide manual_review/partially_refunded - 2026-04-28

**Symptom**: Return-result/payment-history/admin status badges could misrepresent `manual_review` and `partially_refunded` orders; unknown statuses were coerced to `pending` in billing return result.

**Root Cause**: `BillingService.NormalizeStatus` did not whitelist `manual_review` and `partially_refunded` and fell back to `pending` for any unrecognized status. User/admin view-model display mappings also lacked these two statuses.

**Solution**: Extended normalization to preserve `manual_review` and `partially_refunded` and return raw normalized status for unknown values (no forced pending fallback). Added explicit display/badge/icon mappings for both statuses across user history, payment result, and admin billing view-models.

**Files Changed**:
- `TCTEnglish/Services/Billing/BillingService.cs` - updated `NormalizeStatus` to include `manual_review` and `partially_refunded`, removed pending fallback coercion.
- `TCTEnglish/ViewModels/Billing/PaymentHistoryViewModel.cs` - added badge/display/icon mappings for `manual_review` and `partially_refunded`.
- `TCTEnglish/ViewModels/Billing/PaymentResultViewModel.cs` - added title/message/badge/display/icon mappings for `manual_review` and `partially_refunded`.
- `TCTEnglish/Areas/Admin/ViewModels/Billing/BillingManagementViewModels.cs` - added status badge mappings for `manual_review` and `partially_refunded` in row/details view-models.
- `TCTEnglish.Tests/BillingServiceTests.cs` - added regression tests to ensure VNPay return preserves `manual_review` and `partially_refunded` order status.
- `TCTEnglish.Tests/BillingStatusViewModelTests.cs` - new tests for user/admin status display mappings.

**Verification**:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --filter "BillingServiceTests|BillingStatusViewModelTests|AdminBillingManagementTests"` passed (28/28).

**Commit**: Not created.

**Notes**: Existing unrelated nullable warnings remain in the solution and were not part of this ticket.
### CI ML.NET classifier seed hash failed on Linux checkout - 2026-05-01

**Symptom**: GitHub Actions failed `AiProductionClassifierIntegrationTests.MlNetClassifierAssets_ArePresentAndStable` even though the local Windows run passed. The only failing assertion compared the expected SHA256 for `intent-samples.seed.csv` with the actual Linux CI hash.

**Root Cause**: The test hashed the raw seed CSV bytes while `.gitattributes` used `* text=auto`. Windows checked out the seed file as CRLF and matched the stored expected hash, but Ubuntu Actions checked it out as LF and produced a different hash for the same logical dataset.

**Solution**: Updated the asset-stability test to hash the seed CSV as normalized UTF-8 text with LF line endings, while keeping the model zip hashed as raw binary. Added explicit `.gitattributes` entries so the seed CSV stays LF and the model zip is treated as binary.

**Files Changed**:
- `.gitattributes` - pinned ML.NET seed CSV to LF and model zip to binary.
- `TCTEnglish.Tests/AiProductionClassifierIntegrationTests.cs` - normalized text line endings before hashing the seed dataset and updated the expected normalized hash.
- `.ai/context/bug-fix-log.md` - documented the CI-only line-ending failure pattern.

**Verification**:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiProductionClassifierIntegrationTests.MlNetClassifierAssets_ArePresentAndStable"` passed locally.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiProductionClassifierIntegrationTests"` passed locally (6/6).
- Full local `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore` reached 551/552 passing, with an unrelated local SQLite `ObjectDisposedException` in `Sprint3SmokeTests.VocabularyPages_RenderSystemVocabularyForAuthenticatedUser`; rerunning that single test passed.
- `git diff --check` passed, with only line-ending conversion warnings from local `core.autocrlf=true`.

**Commit**: Not created.

**Notes**: This was a cross-platform checkout/hash issue, not a classifier behavior regression. Existing nullable warnings remain unrelated.
### Production AI management page could not find ML.NET dataset/model after deploy - 2026-05-01

**Symptom**: After deployment, the admin AI management page showed `Chua co model` and `Khong tim thay` for `intent-samples.seed.csv`, even though the dataset and model files existed locally and CI tests passed.

**Root Cause**: `intent-samples.seed.csv` and `intent-classifier-model.zip` were stored under `Services/AI/Internal/Data`, but the project file did not mark that directory for output/publish copying. Tests read the files from the source tree, while the production app resolves them under the deployed content root, where they were missing.

**Solution**: Updated the app project file to copy all ML.NET classifier data assets to both build output and publish output. Added a smoke test that locks this project-file contract so future changes do not accidentally remove the publish rule.

**Files Changed**:
- `TCTEnglish/TCTEnglish.csproj` - added copy metadata for `Services/AI/Internal/Data/**/*` using `CopyToOutputDirectory` and `CopyToPublishDirectory`.
- `TCTEnglish.Tests/Sprint4SmokeTests.cs` - added a regression smoke test for the classifier asset publish rule.
- `.ai/context/bug-fix-log.md` - documented the deployment-only missing asset failure.

**Verification**:
- `dotnet publish TCTEnglish/TCTEnglish.csproj --configuration Release --no-restore --output .tmp/publish-ai-assets-*` copied `intent-classifier-model.zip` and `intent-samples.seed.csv` into `Services/AI/Internal/Data` in the publish output.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Sprint4SmokeTests.ProjectFile_PublishesMlNetClassifierAssets"` passed locally.
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AiProductionClassifierIntegrationTests"` passed locally (6/6).

**Commit**: Not created.

**Notes**: The web guide JSON knowledge base was already a static web asset under `wwwroot`; this fix covers the separate ML.NET classifier dataset/model assets used by the admin training flow.

### Learning completion could persist without reward; outbox mislabeled goals — 2026-10-30

**Symptom**: A failed credit insert still returned success after Speaking video completion was saved. Replaying did not restore XP. Completion notifications were stored as GoalCompleted with a future timestamp.

**Root Cause**: Progress/completion committed before `CompletionCreditService`, and every `DbUpdateException` was treated as a duplicate. The outbox dispatcher mapped every completion event to `NotificationType.GoalCompleted` and set `CreatedAt` from business date plus 12 hours.

**Solution**: Speaking and Writing now open one transaction around progress, completion, credit, and outbox intents. Only a duplicate credit key is treated as replay. Direct goal/streak/badge inserts are suppressed on that path and replaced with payload v2 intents that keep the real occurrence time. Legacy payloads are marked needs-review instead of guessed as goal completion.

**Files Changed**:
- `TCTEnglish/Controllers/SpeakingController.cs` — completion unit transaction and retryable 500.
- `TCTEnglish/Services/WritingService.cs` — same unit around attempt/progress/credit.
- `TCTEnglish/Services/Learning/CompletionCreditService.cs` — joins the caller transaction; does not swallow unrelated DB errors.
- `TCTEnglish/Services/Learning/LearningNotificationIntentWriter.cs` — typed outbox intents.
- `TCTEnglish/Services/Notifications/LearningOutboxDispatcher.cs` — v2 dispatch and occurrence timestamp.

**Verification**: `Progress_CreditInsertFailure_RollsBackCompletion` and `SpeakingCompletionCredit_AppliesOnce_AndEnqueuesOutbox` passed in the focused learning filter (146 passed / 0 failed on the last focused run). SQLite only. MySQL not run.

**Commit**: Not created.

**Notes**: Does not backfill historical missing XP. Does not prove multi-connection races on MySQL.

### Local startup used remote database; Linux CI watchers/SQLite host races; Plesk GET webhook rejected — 2026-10-30

**Symptom**: Local startup could access the hosting database and apply migrations/seeders. Linux CI failed from exhausted inotify instances and intermittent SQLite host initialization races. Calling the Plesk webhook with GET returned 404.

**Root Cause**: Configuration contained production credentials, server autodetection and unconditional startup migration/seeding. Repeated integration hosts watched JSON/Razor/static assets; the test fixture reset or shared SQLite connection state between contexts. The Plesk handler accepts POST and rejects GET.

**Solution**: Use an isolated Docker MariaDB and User Secrets; reject non-loopback development connections before network access; explicit server version and development migration opt-in, seeding opt-in, design-time factory. Keep per-host SQLite databases alive with an anchor connection and independent context connections; disable unused test watchers including asset providers. Use HTTPS POST for the Plesk webhook and conceal its private URL on failure. Production release is a gated backup/migration/artifact/readiness sequence.

**Files Changed**:
- `TCTEnglish/Infrastructure/DatabaseSettings.cs`, `DbflashcardContextFactory.cs`, `TCTEnglish/Program.cs`, `appsettings.json`, `TCTEnglish.csproj`.
- `TCTEnglish.Tests/Infrastructure/TestWebApplicationFactory.cs`, `TestAssetStartupFilter.cs`, `DatabaseSettingsTests.cs`.
- `.github/workflows/deploy.yml`, `compose.local.yml`, local/release scripts and deployment regression checks.

**Verification**: Full workspace 620/620 tests passed. Setup-only Linux CI 41054422274 passed 563/563 tests, real MariaDB baseline/repeat checks, Windows self-contained publish, EF bundle and SQL generation. Deployment script checks 8/8 passed locally, including Windows PowerShell 5.1. Actual Plesk POST bootstrap returned 204 and wrote private preflight; no production schema/application release was run. A fresh 30/10 dump (59 users) was hash-verified, restored in disposable MariaDB and upgraded; six table checksums and repeat history stayed unchanged.

**Commit**: Setup PR #120; feature changes remain separate in the owner's working tree.

**Notes**: Removed secrets remain in public Git history until rotated at providers. Production deployment gate stays false pending rotation and full staging failure/recovery drills. SQL backups, credentials and browser profile are ignored private local files.

### Unpublished October migrations dropped foreign-key support indexes too early — 2026-10-30

**Symptom**: Upgrading the restored MariaDB backup failed when a migration dropped an index still needed by a foreign key.

**Root Cause**: The October L4-L6 migration dropped the UserId index before creating its replacement composite index. ReviewRemediation similarly changed FK support indexes before replacements existed, including a same-name owner/fingerprint index becoming unique.

**Solution**: Create compatible replacement indexes first; use a temporary owner FK index while rebuilding the same-name unique index. Apply the corresponding ordering in Down without generating or executing destructive migrations. Only the owner's unpublished October files were edited; the already-applied May migration/history was preserved.

**Files Changed**:
- `TCTEnglish/Migrations/20261029181904_LearningReliabilityL4ThroughL6.cs`.
- `TCTEnglish/Migrations/20261030012638_LearningReviewRemediation.cs`.

**Verification**: Both the September backup and a fresh 30/10 production dump restored into isolated MariaDB 10.5.23 upgraded through all four October migrations. Counts/checksums of users, sets, cards, classes, readingusertranslations and readingtranslationvotes stayed unchanged; repeated upgrade was a no-op. Fresh baseline upgrade smoke also passed. Down was reviewed, not executed on user data.

**Commit**: Not committed; these files belong with the owner's pending learning feature, outside setup PR #120.

**Notes**: Live hosting still has five old history IDs and was not migrated. Do not edit published migrations or delete the extra historical May9 migration ID to force a count match.

### AI unknown transport outcome and reply-save failure; quota recovery — 2026-11-03

**Symptom**: A lost SignalR invocation could send the same intent through HTTP.
Known provider usage could survive reply persistence failure without a real
recovery caller; layout quota reads and arbitrary100-message context cap also
added work or truncated otherwise valid context.

**Root Cause**: Client retry/fallback treated unknown outcome as unprocessed.
Reply persistence/cleanup and quota settlement did not cover non-cancellation
save failures. Recovery existed as an API without a normal next-turn caller.

**Solution**: No retry/fallback after dispatch; show an outcome requiring history
reload. Discard uncommitted reply artifacts, rollback only the owned prompt/draft,
preserve known pending usage, recover on next reservation. Token-budget keyset
context replaces arbitrary100 cap. Fetch launcher usage lazily with honest state.

**Files Changed**: Services/AI/AiChatService.cs, AiDailyQuotaReservationService.cs,
AiContextBuilder.cs, AiProviderErrorMessages.cs; wwwroot/js/ai-chat.js,
ai-chat-launcher.js; Views/Shared/_AiChatLauncher.cshtml and focused tests.

**Verification**: New unknown-outcome Node red→green; save-failure and next-scope
recovery tests; MariaDB concurrency7/7; full Release862/0/13.

**Commit**: Not created; local owner-authorized closeout only.
**Notes**: Not an exactly-once provider billing claim; unknown outcomes remain
conservative and paid cloud acceptance is off.

### Catalog paging lost dynamic actions/counts; Writing loaded all attempts — 2026-11-03

**Symptom**: Speaking appended cards lost actions; injected thumbnail attributes
were possible, failed load-more left its button disabled, private count showed24
for25 videos, and first programmatic activation of a new delete button did nothing.
Writing lists materialized all attempt feedback. Admin filters covered only
loaded rows after adding paging unless converted to server filters.

**Root Cause**: HTML interpolation and incomplete dynamic card construction;
counter derived from first page; nested click replay is suppressed by browsers.
Writing performed paging after full collection loads. A first SQL rewrite used
correlated DISTINCT derived tables unsupported by MariaDB10.5.

**Solution**: DOM/textContent cards; complete private controls; error/finally
states; server total/decrement after successful delete; direct confirmation
handler for first activation. SQL Writing filters/counts/page6 and owned paging;
count sentences with EXISTS historical pass. Admin SQL GET filters/page20, full
topic facets and separate statistics.

**Files Changed**: SpeakingService/ViewModels/Views/JS; WritingService.Catalog.cs,
WritingService/IWritingService, StudyController and Writing list VM/view; Admin
Speaking/Writing controllers, list VMs/views and _CatalogPager.

**Verification**: Browser injection/503 probes, real private fixture24→25→delete24;
Node import/count/delete regression; Writing historical-pass row-count regression;
Admin red2/2→green; MariaDB benchmark200/2000, EXPLAIN. At2000 Writing rows8001→10,
mean process allocations21.24MB→0.315MB. No promise of hosting SLA.

**Commit**: Not created.
**Notes**: All synthetic browser data removed; dev users checksum unchanged.
Legacy full lightweight Writing data route remains for compatibility.

### Unlock side effects ran before durable state; SMTP cancellation unbounded — 2026-11-03

**Symptom**: Worker notifications could precede the unlock commit; one broadcast/
recipient failure could prevent later mail. Real stalled SMTP was not bounded
by a caller-linked deadline.

**Root Cause**: Worker read-modify-send-save ordering and one shared failure path;
delivery bool was discarded at the public interface and transport cancellation
did not consistently link workflow timeout/request cancellation.

**Solution**: Conditional ExecuteUpdate commits unlock before mail; only winner
notifies. Broadcast/mail failures isolated per recipient; lazy login uses same
conditional transition. Add typed try-send result and linked bounded transport.
Do not log recipients or mail/reset payloads.

**Files Changed**: Workers/AutoUnlockWorker.cs, Controllers/AccountController.cs,
Services/IAppEmailSender.cs, SmtpAppEmailSender.cs; fake transport/worker tests.

**Verification**: Fake SMTP stall/caller cancel; worker rerun/broadcast failure/
mail failure/permanent lock tests; focused workflow28/28; full Release862/0/13.

**Commit**: Not created.
**Notes**: Worker remains default off. No live email, durable SMTP outbox or
exactly-once delivery claim across process crash.

### Duplicate quota index and local fixture preparation incident — 2026-11-03

**Symptom**: Quota table had a unique secondary index identical to its composite
primary key. While preparing another isolated upgrade lab, an operator command
used the wrong connection environment key and EF reverted the two new quota
tables on the local dev DB through its design-time User Secrets fallback.

**Root Cause**: Redundant HasIndex configuration; fixture baseline CLI did not
prove the effective database identity before its target-October migration.
The mistake was reported to the owner immediately after detection.

**Solution**: New follow-up migration removes only the secondary index, leaving
the primary key/FK/data intact; applied migrations were not edited. Reapply dev
schema with the normal local launcher. Upgrade regression now establishes its
baseline using an explicit acceptance connection, loopback/name/sentinel/runId
checks and an empty migration history, before any migration runs.

**Files Changed**: Models/DbflashcardContext.cs, new
20261103141227_RemoveRedundantAiQuotaUniqueIndex + Designer + snapshot,
QuotaMigrationUpgradeTests.cs.

**Verification**: Fresh guarded MariaDB upgrade/repeat preserves synthetic user
and pending quota, removes secondary index and retains both primary-key columns
(1/1). Dev history has all three current migrations, quota tables exist,
model has no pending changes; Users checksum stays4192988673.

**Commit**: Not created.
**Notes**: No production access. Dev quota tables are now empty; no snapshot
immediately before the erroneous rollback exists, so user checksum does not
prove preservation of any earlier quota row. Existing chat/request-log tables
were not dropped. No paid cloud request was made in this session.

### Staging OpenAI connections time out on hosting — 2026-11-05

**Symptom**: Separate staging key/model metadata worked on the owner's machine,
but hosted Chat returned503/timeout and Speech504/engine_timeout.

**Root Cause**: Host .NET9 probe resolved IPv4+IPv6; default and noProxy requests
timed out after6s, while forced IPv4/noProxy returned model metadata200 in846ms.
The default outbound connection path failed, rather than a missing model/key.

**Solution**: Only staging IIS environment adds DOTNET_SYSTEM_NET_DISABLEIPV6=1
and NO_PROXY=api.openai.com; keep normal HTTPS/SNI/certificate validation. Saved
through .NET Toolkit, retained by the staging deploy script's env preservation.
No production/global networking or local User Secrets changes.

**Verification**: Hosted Chat200/STAGING_OK; hosted speech200/Completed with a
2.75s synthetic PCM16mono16k WAV, correct transcript,100 text-match and comparison.
No real-microphone quality claim. Temporary synthetic Speaking fixture/assessment
removed with exact DB/id/title/source guards; users3/Writing1/credits1 retained.
Key handoff consumed after live verification; no key printed or committed.

**Notes**: Staging CD adds fixed site/DB/user identity, private backup before EF
bundle, persistent env/uploads, run/digest readiness and failure recovery.
15local regressions include actual Windows PS5.1 fake-command failure drills;
production deployment regressions18/18. IIS ACME auth exception remains a
provider-level follow-up; valid current TLS does not certify automatic renewal.

### Staging verifier times out before a slow but successful host deploy — 2026-11-05

**Symptom**: First staging job reported failure waiting for its marker, while
Plesk later completed the exact release and stayed ready with the expected digest.

**Root Cause**: Hosting took about9minutes to download the140MB artifact; the
publisher's60polls expired earlier. A failed CI verifier did not stop the remote
PowerShell task. Before recovery, current.json/marker/backup were checked; no
process was killed and no lock/attempt or migration history was removed.

**Fix**: One20minute monotonic verification budget includes HTTP time; requests
and sleeps respect remaining time. Staging job25minutes; host download bounded
at15minutes and progress output disabled. Production script/gate unchanged.

**Verification**: Red slow-host case +missing deadline interface → green17/17;
new tests cover a release first observed after8minutes and HTTP time consuming
the budget. Actual release41965473404 completed04:18:09UTC, ready200 and expected
ef58fda/run/digest; failed-job rerun after reconciliation passed idempotently,
with completedAt/data unchanged. Users3/history12/Writing1/goals2/credits1.

**Owner scope**: Current HTTPS expiry03/02/2027 accepted; automatic renewal and
support ticket explicitly dropped, not certified as working. Key/Chat/Speech
and shared lessons/progress verification remain recorded in the staging audit.
### Shared-data target aliases — 05/11/2026

**Symptom/root cause**: review of the new CLI found that generic connection
string lookup picked Server before Host; the MySQL provider resolves synonyms
with the last value. A duplicate alias could make the guard inspect another host.
No remote operation was executed during diagnosis.

**Fix**: SharedDataCommand.AssertTarget uses the same MySqlConnectionStringBuilder
as the provider, including effective server/database/user/port. CI also requires
its exact disposable user. No production gate/config or existing release changed.

**Regression**: remote Host overriding loopback Server was red (no exception),
then the provider parser rejects it before DB access. Other local/staging/
production guards remain in focused tests; verification recorded in shared-data ledger.

### Pull sync while web apphost is locked on Windows — 05/11/2026

**Symptom/root cause**: real post-merge sync built into the same Release folder
as a running/shutting-down app. Windows held TCTEnglish.exe; MSB3021/MSB3027
stopped sync after Git succeeded. Git/data error reporting was correct.

**Fix**: build CLI separately under ignored local state, execute its DLL with
source content root; apply committed EF migrations from that exact assembly using
a local-only CLI action. Hosting retains bundle path, backup and release gates.
Launcher already-built SkipBuild remains supported. Do not kill another app,
use stale binaries or change User Secrets to recover.

**Verification**: failing wrapper regression then5/5pass; real isolated Sync
succeeded with the original Release exe still locked,0recordoperations. Personal
table checksum preservation and final remote checks are in the shared-data ledger.
### Hybrid RAG evaluation fixture hashed raw CRLF bytes — 2026-11-06

**Symptom**: The Phase 0 freeze compared SHA-256 of the fixture's raw bytes. On this Windows checkout the file was CRLF, so the stored hash matched here and would change after an LF checkout.

**Root Cause**: `.gitattributes` has `* text=auto`. The new evaluation JSON was hashed before line endings were normalized, the same class of failure as the 2026-05-01 ML.NET seed CSV.

**Solution**: Freeze the fixture as SHA-256 of UTF-8 text after CR and CRLF become LF. Keep the revision 1 raw CRLF hash only as history. Pin `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json` to `text eol=lf`. Tests accept the same canonical hash for LF and CRLF bytes and reject a changed fact, ID, outcome or split.

**Files Changed**:
- `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json` - revision 2 content and LF bytes.
- `TCTEnglish.Tests/HybridRagFixtureValidator.cs` - canonical hash and scenario checks.
- `TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs` - LF/CRLF and content-mutation regressions.
- `.gitattributes` - LF pin for this fixture only.

**Verification**:
- `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixReview --filter FullyQualifiedName~HybridRagEvaluationBaselineTests` passed locally, 15/15, including CanonicalHash_TreatsLfAndCrlfAsTheSameFreeze and CanonicalHash_RejectsChangedFactIdOutcomeOrSplit.

**Commit**: Not created.

**Notes**: Scope manifests still hash raw file bytes. That identity is separate from the canonical fixture freeze.
### Hybrid RAG fixture validator accepted inconsistent scenario setup — 2026-11-06

**Symptom**: In-memory clones of the frozen evaluation fixture stayed valid when quota usage was 0/15 and 0/60000 under quota_exceeded, when provider_unavailable or cancelled used scenario none, and when a tombstone pointed at a source the case did not declare.

**Root Cause**: ValidateScenario checked the shape of a supplied scenario by itself. It did not compare that setup with the case outcome, and a tombstone source only had to exist somewhere in the corpus.

**Solution**: Compare the scenario already on the case with quota_exceeded, provider_unavailable and cancelled. Require a scenario sourceId to be one of that case's declared sourceIds. Do not build fault, quota, cancel or source fields from the outcome or the question. Leave the revision 2 fixture and its canonical hash unchanged.

**Files Changed**:
- `TCTEnglish.Tests/HybridRagFixtureValidator.cs` - outcome and declared-source checks.
- `TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs` - four regressions for the reproduced clones.

**Verification**:
- Before the check, the four new tests failed because Validate returned an empty error list.
- After the check, `dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixRound2 --filter FullyQualifiedName~HybridRagEvaluationBaselineTests` passed 19/19, including the frozen fixture hash and the earlier LF/CRLF and mutation regressions.

**Commit**: Not created.

**Notes**: This is a synthetic-fixture validator fix. It does not change application quota, provider or retrieval behavior.

### Hybrid RAG Phase 2 accepted unrelated facts and incidental guides — 2026-11-06

**Symptom**: The returned Phase 2 repair still declared full answers using another
object's color, number, cause, place, year or builder within the same sentence.
Missing subject words/time qualifiers and unsupported mixed requests could also
pass. Incidental guide terms such as `bell` or `about` replaced lesson questions
with notification/Dashboard instructions. Seven older integration refusal checks
also failed; the deterministic failures reproduced on the pre-closeout binary.

**Root Cause**: Word presence and first/last content tokens were used as evidence
of a complete relation. A no-frame fallback implied full support. Any guide snippet
short-circuited corpus admission; low-confidence fallback reopened explicitly
unscoped homework/random-fact/explanation requests.

**Solution**: Full lexical answers use a deliberately small extraction grammar
binding the complete subject, relation and requested qualifiers in one source
sentence. Unknown/mixed unsupported requests fail closed. Keyword/passage search
returns an explicitly labelled `excerpt`; it cannot claim `full`. An explicit
source request or literal source-topic match takes the lesson-validation path
before an incidental guide. Live personal facts retain priority. Add a bounded
task-scope refusal for unscoped generation requests while preserving explicit
stored-source requests; no unconstrained provider fallback.

**Files Changed**: `Rag/Corpus/LexicalAnswerAdmission.cs`, `Rag/AiChatAnswerService.cs`,
`Rag/ChatExternalFactPolicy.cs` and `AiRagPhase2FixRound2Tests.cs`.

**Verification**: Initial expanded regressions 17 failed/6 passed; a further polite
`about` variant failed independently before its routing repair. Final focused
RAG plus deterministic/production-classifier integration checks passed 115/115
in `Phase2Closeout`. Final full suite: 1044 passed, 0 failed, 19 opt-in skipped.
Full-suite/browser evidence and final scope are recorded in
`docs/feature-audits/hybrid-rag/phase-2/review-round-3.md`.

**Notes**: No frozen fixture/BM25 tuning, persisted model, shared content, protected
configuration, paid API or remote Git/release change. The grammar is conservative
Phase 2 extraction, not unrestricted language understanding. No commit created.
### Hybrid RAG Phase 7 local completion round 2 — 2026-11-08

**Symptom**: The acceptance guard never expired with its default clock, could
publish after the run deadline and could release a pending reservation with a
zero/negative usage receipt. Integer-only tests did not connect the guard to
production-client retries. In the real local browser, “Nghĩa của từ forecast là
gì?” missed an existing private card; “Ví dụ của forecast” became a greeting.
The login Bootstrap stylesheet was blocked by a mismatched SRI digest.

**Root Cause**: The default elapsed delegate returned zero, promote omitted the
deadline check and invalid receipts cleared pending usage. The harness lacked a
transport boundary. Short Vietnamese definition prefixes captured `tu … la gi`
as target words. The shipped ML classifier could override explicit CardLookup
wording; its arbitration protected several other intents only. The stylesheet
integrity value did not match the pinned CDN asset.

**Solution**: Use a monotonic default run clock, a linked cancellation deadline,
recheck expiry before promotion and retain invalid receipts as unknown. Add an
acceptance-only HTTP handler shared by real embedding/text client instances,
gating every send/retry and bounded body read before receiving usage. It does
not register in production or authorize live calls. Match complete Vietnamese
definition frames before short prefixes and retain the full multiword target.
Reuse the explicit lookup keyword predicate when arbitrating ML predictions,
while leaving bare ambiguous queries eligible for ML. Correct the Bootstrap
5.3.3 CSS SRI without removing integrity checks.

**Verification**: Three guard tests failed before their fix; seven Vietnamese
answer/evidence cases failed before the parser fix; an ML model lacking lookup
labels reproduced the greeting error before arbitration was repaired. Five
transport tests exercise the actual production clients with fake HTTP, including
retry refusal and cancellation during a pending body. Focused final classifier,
boundary, provider and acceptance checks passed 109/109. Real MariaDB read-only
dry-run returned 471 documents / 487 chunks / 113,887 bytes, 0 embedding calls.
Native browser full/embed tests prove the exact Vietnamese wording, saved
example, missing pronunciation, complete-target rejection, source route/history
and guide answers. Final full-suite/build evidence and run identity are in
`docs/feature-audits/hybrid-rag/phase-7/local-completion-handoff.md`.

**Data/authority**: Owner asked for direct local fixes and a running test web.
After the owner reopened Docker, the standard launcher preserved the existing
database. Two guarded idempotent private Standard test accounts and two cards
per account were added; access details stay in ignored local storage. No shared
content, persisted model, migration, protected config or saved secret changed.
No paid request, commit, push, CI or deploy. Dense/live evaluation stays BLOCKED.
No matching entry in known-issues.md required a status change.

### Speaking redesign intercepted server catalog filters — 2026-11-10

**Symptom**: Selecting a topic or CEFR level after loading a partial catalog
changed the URL but filtered only the cards already present, omitting unloaded
matches. Modified link activation was also cancelled.

**Root cause**: New UI click handlers intercepted navigational anchors and called
`pushState` without fetching the corresponding server-filtered result. Existing
catalog pagination requires server navigation for these links.

**Fix**: Keep native anchor navigation; retain client narrowing only for plain
buttons. Remove the synthetic filter history/popstate handling. No controller,
query, schema or media/assessment contract changed.

**Regression evidence**: `test_speaking_catalog_navigation.cjs` loads the actual
`speaking.js` with a partial-catalog fixture. Topic, missing level and all-level
navigation each failed before the fix and passed afterward, including Ctrl+click
and no synthetic history writes. Complete Node suite: 126 passed.

**Limits**: This verifies production-script behavior in a DOM fixture, not native
browser navigation or every Speaking interaction. Existing simulated interaction
tests alone do not prove capture, focus or smoothness. No matching open issue in
known-issues.md was changed. This correction accompanies the owner-accepted local
Speaking checkpoint; no push or deployment.

### Speaking UI adoption row contained control characters — 2026-11-10

**Symptom**: The team UI tracker rendered the Admin Speaking CSS filename and
`var(--tct-*)` incorrectly.
**Cause**: The tracked Markdown contained literal BEL (U+0007) and vertical-tab
(U+000B) characters at those locations; this was not just console rendering.
**Fix**: Restore the literal filenames/token notation and normal Markdown code
spans. Update the AI/Admin/loader adoption rows to describe the integrated code
without claiming every legacy Admin page or unobserved browser state passed.
**Verification**: UTF-8/control-character inspection, existing asset paths and
`git diff --check`. Documentation-only; no runtime or database change.
