# AI Chat Upgrade Execution Plan

> Execution-ready implementation plan for the AI chat upgrade request.
> This document is designed so a future coding agent can be told to read this
> file and execute exactly one phase at a time.

## Target Outcome

Deliver the following user-visible upgrades to the AI chat feature:

1. Move the `AI dang tra loi...` state into the chat log as an in-chat assistant
   response instead of showing it below the composer.
2. Add small avatars next to user and assistant messages to make the chat UI
   feel more lively.
3. Enforce role-based daily usage:
   - `Standard`: maximum 15 AI questions per day
   - `Premium`: unlimited
   - `Admin`: treat as unlimited for operational safety
4. Allow users to delete individual conversations from the left history column.
5. Add a visible quota hint for limited-plan users in the AI chat header/meta
   area so users understand they are on a constrained plan.
6. Replace browser-native delete confirmation with an app-styled confirmation
   and follow-up notification that matches the product UI.

## Scope Boundaries

In scope:

- `AI/Chat` full page
- `AI/Chat?embed=true` embedded launcher chat
- Backend enforcement for daily question quota
- Conversation deletion from the left history list
- Frontend chat-shell refresh required to support avatars and in-chat typing
- Limited-plan quota hint in the AI chat header/meta area
- App-styled delete confirmation and delete success/error feedback

Out of scope:

- Payment or subscription purchase flow
- Role-management screens or admin promotion workflow
- Per-message deletion inside a conversation
- Provider/model swaps
- Major AI observability redesign
- `Program.cs`, `appsettings.json`, `.csproj`, or migration work unless the user
  explicitly asks for it later

## Current Code Map

Primary implementation files:

- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Services/AI/AiConversationService.cs`
- `TCTEnglish/Services/AI/IAiConversationService.cs`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish/Models/AiConversation.cs`
- `TCTEnglish/Models/AiMessage.cs`
- `TCTEnglish/Models/AiRequestLog.cs`

Relevant existing assets and helpers:

- AI avatar asset already exists at `TCTEnglish/wwwroot/images/ai/tct-ai-launcher.png`
- User avatar claim is already issued as `AvatarUrl` in `AccountController`
- Existing AI tests already exist in:
  - `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
  - `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

## Agent Execution Rules

Every agent assigned to a phase should follow these rules:

1. Read `AGENTS.md` and this file before coding.
2. Execute only the requested phase. Do not silently start a later phase.
3. Preserve security rules:
   - async/await only
   - anti-forgery on mutations
   - ownership checks on conversation reads/deletes
   - return `NotFound()` for ownership violations
4. If a phase depends on output from an earlier phase and that output is not
   present, stop and report the missing prerequisite instead of improvising a
   large refactor.
5. Prefer small focused changes and targeted tests over broad rewrites.

## Recommended Phase Order

Execution order:

1. Phase 1: Role-based daily question quota
2. Phase 2: Conversation deletion from history
3. Phase 3: Chat UI refresh with avatars and in-chat typing
4. Phase 4: Regression pass, polish, and final verification
5. Phase 5: Post-implementation UX follow-ups
6. Phase 6: Independent review and closure assessment

Parallelization guidance:

- Phase 1 and Phase 2 can happen on separate branches if needed, but both touch
  AI chat backend/controller code, so sequential delivery is lower risk.
- Phase 3 should preferably wait until Phase 2 is merged because it will also
  touch the history/chat-shell DOM and JS behavior.
- Phase 4 should happen only after the earlier phases are merged together.
- Phase 5 should happen only after the baseline feature set from Phases 1-4 is
  present, because it is a UX refinement pass on top of completed behavior.
- Phase 6 should always be last, and should be handled by an agent in explicit
  review mode rather than feature-building mode.

## Phase 1 - Role-Based Daily Question Quota

### Goal

Enforce the new product rule on the server:

- `Standard` users can ask at most 15 AI questions per day
- `Premium` and `Admin` users are unlimited

The backend must enforce this even if the frontend is bypassed.

### Likely Files

- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish/Models/User.cs`
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

### Implementation Tasks

1. Define the counting rule clearly in code comments or test names:
   - Count one successful AI request as one consumed daily question.
   - Failed provider requests should not consume quota.
   - Use `Roles.Normalize(user.Role)` so legacy role text does not bypass rules.
2. Add a role lookup in `AiChatService` using a read-only query:
   - Load the current user role with `AsNoTracking()`.
   - Prefer a private helper such as `GetNormalizedUserRoleAsync(...)`.
3. Add quota enforcement before persisting a new rejected user message:
   - Check quota as early as possible in `SendAsync(...)`, after input validation
     but before the service writes a new user message.
   - This avoids leaving partial chat records for blocked requests.
4. Use existing observability tables instead of adding schema:
   - Count successful `AiRequestLogs` for the user within the current day window.
   - Keep the first implementation schema-free; do not add a migration.
5. Add a stable AI limit error code such as `daily_question_limit_exceeded`.
6. Return a clear Vietnamese user-facing message from the controller when the
   quota is reached.
7. Keep existing per-minute throttling and token-budget logic in place.
   The new quota is additive, not a replacement.

### Testing Work

Add or update tests for:

- `Standard` user below limit can still send
- `Standard` user at 15 successful requests is blocked on the next request
- `Premium` user bypasses the 15/day cap
- `Admin` user bypasses the 15/day cap
- failed AI provider calls do not consume the question quota
- controller returns `429` with the expected friendly message

### Definition Of Done

- Server rejects the 16th daily question for `Standard`
- `Premium` and `Admin` remain unrestricted
- no rejected request creates a stray user message
- automated tests cover the new quota paths

### Prepare For Phase 2

Before closing Phase 1:

- keep the error payload shape additive and stable
- avoid coupling quota logic to the chat history DOM
- leave `AiController` clean enough to accept one more mutation endpoint in the
  next phase

## Phase 2 - Delete Conversations From Left History

### Goal

Allow a user to delete one conversation at a time from the left history column.
Only the owner may delete that conversation.

### Likely Files

- `TCTEnglish/Services/AI/IAiConversationService.cs`
- `TCTEnglish/Services/AI/AiConversationService.cs`
- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`
- `TCTEnglish.Tests/AiPhase1DataAndContextTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

### Implementation Tasks

1. Add a service-level delete method:
   - Example shape: `DeleteConversationAsync(int userId, Guid conversationId, CancellationToken ct)`
   - Query tracked conversation by both `Id` and `UserId`
   - If not found, treat as ownership failure and let controller return `NotFound()`
2. Delete only the conversation root record and rely on existing cascade rules:
   - `AiMessages` already cascade from `AiConversation`
   - `AiRequestLogs` already cascade from `AiConversation`
   - verify behavior in tests instead of manually deleting every child row unless
     the current EF setup forces it
3. Add a mutation endpoint to `AiController`:
   - `POST` action
   - `[ValidateAntiForgeryToken]`
   - payload contains `conversationId`
   - return JSON success result on success
4. Update the left history UI in `Views/Ai/Chat.cshtml`:
   - each history item gets a delete affordance
   - delete button must not navigate into the conversation when clicked
   - preserve keyboard accessibility
5. Update `ai-chat.js` to handle delete behavior:
   - prompt for confirmation before delete
   - send anti-forgery token with the request
   - remove the deleted history item from the DOM on success
   - if the deleted conversation is the active one, redirect to `/AI/Chat` or
     reset the page to a fresh draft state
6. Keep ownership handling strict:
   - deleting another user's conversation must not leak whether it exists
   - return `NotFound()` rather than `Forbid()`

### Testing Work

Add or update tests for:

- deleting your own conversation succeeds
- deleting another user's conversation returns `404`
- deleting a conversation removes its messages and request logs
- deleting the active conversation returns the page to a safe empty/new-chat state
- anti-forgery is still required

### Definition Of Done

- a delete control exists beside each conversation in the left sidebar
- users can delete only their own conversations
- child rows are removed with the conversation
- the active-conversation case is handled cleanly

### Prepare For Phase 3

Before closing Phase 2:

- keep stable DOM hooks such as `data-conversation-id` and a dedicated delete
  selector like `data-ai-history-delete`
- avoid mixing avatar layout work into the sidebar delete change
- make sure the sidebar markup is still easy to enhance visually in the next phase

## Phase 3 - Chat UI Refresh With Avatars And In-Chat Typing

### Goal

Refresh the AI chat UI so it matches the requested visual behavior:

- assistant typing state appears inside the chat log
- user messages show a small user avatar
- assistant messages show a small AI avatar

This should work in both the full chat page and the embedded launcher chat
because both reuse `_ChatShell.cshtml`.

### Likely Files

- `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`

### Implementation Tasks

1. Decide avatar sources up front:
   - user avatar: `AvatarUrl` claim if available
   - user fallback: initials or a styled placeholder circle if no avatar exists
   - assistant avatar: `/images/ai/tct-ai-launcher.png`
2. Pass avatar metadata into the chat shell in a frontend-friendly way:
   - add data attributes on the chat root or composer region
   - avoid unnecessary backend refactors if claims already provide what is needed
3. Update server-rendered message markup in `_ChatShell.cshtml`:
   - render avatar slot plus bubble content for existing messages
   - preserve `assistant`, `user`, and `system` distinctions
4. Replace the current standalone `typing` line under the composer:
   - the waiting state should render as an assistant message row inside the chat
   - prefer animated dots or a lightweight placeholder bubble
5. Update `ai-chat.js` so appended messages match the new HTML structure:
   - `appendMessage(...)` must create avatar + bubble layout
   - streaming mode must reuse or replace the assistant placeholder instead of
     showing status below the input
   - AJAX fallback for new conversations should also use the in-chat placeholder
6. Keep error/status messaging separate:
   - `chatStatus` can remain for warnings/errors
   - only the "AI is responding" state moves into the chat log
7. Update `ai-chat.css`:
   - avatar sizing, spacing, alignment, and responsive behavior
   - user and assistant row layout
   - embedded mode and full-page mode should both remain usable on mobile

### Testing Work

Add or update coverage where practical, and manually verify:

- existing history messages render with avatars
- newly appended user messages render with avatars
- assistant replies use the AI avatar
- typing indicator appears inside the chat window, not below the composer
- embedded chat still works after the DOM/CSS change
- active scroll-to-bottom behavior still works

### Definition Of Done

- chat rows visibly show a small avatar on each side
- assistant waiting state appears inside the conversation flow
- both full-page and embedded chat remain functional
- no layout break on typical desktop and mobile widths

### Prepare For Phase 4

Before closing Phase 3:

- remove any dead CSS or JS references to the old standalone typing UI
- keep selectors and helper functions readable so regression tests can target them
- note any remaining UX edge cases for the final regression pass

## Phase 4 - Regression Pass, Polish, And Final Verification

### Goal

Combine the earlier work, close gaps, and produce a stable handoff-ready result.

### Likely Files

- any touched AI chat files from earlier phases
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- additional AI test files if new focused coverage is needed

### Implementation Tasks

1. Run targeted automated tests for the touched AI areas.
2. Add missing tests discovered during integration:
   - quota edge cases
   - delete active conversation
   - delete another user's conversation
   - embedded chat rendering after UI refactor
3. Do a manual verification pass covering:
   - `Standard` can send up to 15 questions per day
   - `Standard` is blocked on the next question
   - `Premium` remains unlimited
   - delete inactive history item
   - delete active history item
   - avatar fallback when the user has no `AvatarUrl`
   - assistant avatar appears correctly
   - typing placeholder appears in-chat
   - full page and embed remain responsive
4. Remove dead code introduced by earlier iterations:
   - stale DOM ids
   - unused CSS classes
   - orphan helper functions
5. Produce a concise handoff note in the final response:
   - changed files
   - tests run
   - remaining risks

### Definition Of Done

- the three requested capabilities are working together:
  - quota
  - conversation delete
  - avatar + in-chat typing UI
- test coverage protects the main regression paths
- no known broken full-page or embedded flow remains

### Prepare For Release Or Next Iteration

If more work is requested later, likely follow-ups are:

- show remaining daily questions to `Standard` users
- move daily quota from UTC day boundary to a business/local timezone abstraction
- add rename conversation or bulk delete actions
- add richer assistant typing animation or timestamp polish

## Phase 5 - Post-Implementation UX Follow-Ups

### Goal

Improve the user-facing experience after the first four phases are already
working:

- limited-plan users should see a clear hint that their AI usage is capped
- delete conversation should use product-styled confirmation and feedback rather
  than a browser-native `confirm(...)` dialog

### Likely Files

- `TCTEnglish/Views/Ai/_ChatShell.cshtml`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/Views/Ai/ChatEmbed.cshtml`
- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/wwwroot/css/ai-chat.css`
- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish/ViewModels/AI/AiChatPageViewModel.cs`
- targeted AI tests under `TCTEnglish.Tests/`

### Implementation Tasks

1. Add a visible limited-plan hint in the AI chat header/meta area:
   - place it in or near the top AI header region that stays visible and feels
     intentional
   - only show it for constrained plans such as `Standard`
   - the copy should communicate the real active rule in the application
     (`daily` if the backend still uses daily quota, `weekly` only if the live
     implementation has truly moved to weekly quota)
   - prefer a compact info chip, helper line, or info icon with tooltip/popover
     rather than a heavy warning banner
2. If useful and low-risk, expose lightweight quota context to the view:
   - plan label
   - quota window label
   - remaining allowance or max allowance
   - keep this additive; do not rewrite quota enforcement just to show UI text
3. Replace browser-native delete confirmation:
   - remove `window.confirm(...)` or equivalent native browser prompt
   - replace it with a styled app confirmation using an existing Bootstrap modal
     pattern or a small custom confirmation component
   - the confirmation should clearly state which action will happen
4. Add post-delete feedback:
   - success feedback after a conversation is deleted
   - clear error feedback if deletion fails
   - feedback should match the rest of the AI chat UI instead of using browser
     dialogs
5. Verify both full-page and embedded chat behavior:
   - full page definitely needs the quota hint and delete confirmation flow
   - embedded chat should stay visually consistent, even if the left history list
     is only present on the full page

### Testing Work

Add or update tests where practical, and manually verify:

- `Standard` users see the limited-plan hint
- unrestricted users do not see misleading limited-plan copy
- delete no longer triggers a browser-native `localhost says` prompt
- delete confirmation uses app-styled UI
- success/error feedback appears after delete
- no regression in actual delete behavior or anti-forgery protection

### Definition Of Done

- limited-plan users have a visible, understandable quota hint
- delete confirmation no longer uses a browser-native prompt
- delete success/error state is communicated in-app
- the added UX elements do not clutter or break the AI chat header

### Prepare For Phase 6

Before closing Phase 5:

- stabilize the final UI copy so a review agent can assess it cleanly
- leave screenshots or precise selectors easy to inspect during review
- keep the change narrowly focused on UX refinement, not fresh feature scope

## Phase 6 - Independent Review And Closure Assessment

### Goal

Have a separate AI agent review the combined output of all earlier phases and
answer these questions:

- Is the work actually complete against the original request and later UX
  follow-ups?
- Is the feature safe enough to close?
- What risks, regressions, or polish gaps still remain?

### Likely Files

- all AI chat files touched in Phases 1-5
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- any new AI tests added during implementation
- `docs/ai-chat-upgrade-execution-plan.md`

### Review Tasks

1. Re-read the execution plan and compare implemented behavior against every
   phase outcome.
2. Inspect the final AI chat backend and frontend in code-review mode:
   - quota enforcement
   - conversation deletion
   - avatar and typing UI
   - limited-plan hint
   - delete confirmation/feedback UX
3. Check boundary and security compliance:
   - feature stays inside AI chat boundaries
   - no `HomeController` drift
   - anti-forgery is still enforced on mutations
   - ownership checks still return `NotFound()` on violations
4. Run targeted verification where available:
   - relevant AI tests
   - manual spot checks if the environment allows it
5. Produce a closure report with explicit verdicts:
   - implemented and acceptable
   - implemented but still has must-fix issues
   - implemented with only low-priority polish items
   - not ready to close
6. If the review agent is asked only to review, it should not silently rewrite
   large parts of the feature. It should report findings first.

### Expected Review Output

The review agent should explicitly report:

- what is complete
- what is incomplete
- whether the task can be closed now
- what risks remain
- what improvements are optional vs must-fix

### Definition Of Done

- a clear closure recommendation exists
- remaining risks are identified with enough detail to act on
- the team can decide whether to close the work item or schedule follow-up fixes

### Prepare For Closure

If the review verdict is positive:

- the feature can be marked ready to close
- any remaining items should be documented as follow-up polish, not blockers

If the review verdict is negative:

- convert each must-fix finding into a separate follow-up task before closure

## Phase 6 Follow-Up Files

These follow-up files were created from the independent review on 2026-04-03.
They are intended to be executed one at a time by a future AI coding agent.

Current closure status:

- Do not close the AI chat upgrade yet.
- Closure remains blocked until follow-up files `01` through `03` are resolved
  and re-verified.

Must-fix follow-up files:

1. `docs/ai-chat-phase6-followup-01-delete-ux-runtime-gap.md`
   - fixes the gap where the full `/AI/Chat` page still falls back to
     browser-native delete dialogs because the app-styled runtime is not
     actually available in the shipped page
2. `docs/ai-chat-phase6-followup-02-ai-regression-suite-repair.md`
   - fixes the broken AI regression suite so the upgraded feature can be
     verified by automated tests again
3. `docs/ai-chat-phase6-followup-03-retry-duplicate-prompt-hardening.md`
   - fixes the risk that one user action can create duplicate prompts after a
     transient provider failure in an existing conversation

Non-blocking follow-up notes from review:

- consider returning a stable AI limit `errorCode` payload from the controller
  in addition to the user-facing message
- add explicit automated assertions for the Standard-plan hint and delete UX
  hooks once the AI regression suite is repaired
- the daily quota still uses UTC day boundaries; treat that as a later product
  follow-up unless business rules now require a local/business timezone

## Suggested Agent Prompts

Use prompts like these when delegating:

- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 1. Do not start any later phase. Implement the code, run targeted tests, and report blockers if Phase 1 cannot be completed cleanly.`
- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 2. Assume Phase 1 is already merged.`
- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 3. Assume Phases 1-2 are already merged.`
- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 4. Assume Phases 1-3 are already merged and focus on verification/polish.`
- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 5. Assume Phases 1-4 are already complete, and focus on the quota hint plus delete confirmation/notification UX follow-ups only.`
- `Read docs/ai-chat-upgrade-execution-plan.md and execute only Phase 6 in review mode. Assume Phases 1-5 are complete. Do not start feature work unless the review explicitly requires a must-fix follow-up to be scheduled.`
- `Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-01-delete-ux-runtime-gap.md. Execute only this follow-up and do not start any other follow-up file.`
- `Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-02-ai-regression-suite-repair.md. Execute only this follow-up and do not start any other follow-up file.`
- `Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-03-retry-duplicate-prompt-hardening.md. Execute only this follow-up and do not start any other follow-up file.`

## Final Notes

- Keep the implementation aligned with current repository rules from `AGENTS.md`.
- Do not add new feature work to unrelated controllers.
- Prefer additive changes over broad rewrites.
- If a phase reveals a hidden dependency that materially changes scope, stop and
  document the dependency before continuing.
