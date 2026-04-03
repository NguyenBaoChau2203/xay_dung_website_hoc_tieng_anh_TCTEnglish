# AI Chat Phase 6 Follow-Up 01

> Must-fix follow-up created from the independent review on 2026-04-03.
> Execute this file only. Do not silently pick up other follow-up files.

## Goal

Make the AI chat delete confirmation and success/error feedback actually run as
app-styled UI on the full `/AI/Chat` page.

Current review finding:

- `TCTEnglish/wwwroot/js/ai-chat.js` still falls back to native
  `confirm(...)` / `alert(...)` when `window.bootstrap` is unavailable.
- `TCTEnglish/Views/Shared/_Layout.cshtml` does not currently load a Bootstrap
  bundle for the full page, so the browser-native path remains live.

This is a release blocker for the Phase 5 UX promise because the feature is not
actually complete in the shipped runtime.

## Scope

In scope:

- Full-page AI chat delete confirmation and feedback
- Runtime dependency wiring needed for the existing modal/toast UI to work
- Removal of browser-native delete prompt/error fallback from AI chat
- Targeted tests for the delete UX path if practical

Out of scope:

- New delete features
- Bulk delete
- Per-message delete
- Unrelated layout refactors

## Primary Files

- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/Views/Ai/Chat.cshtml`
- `TCTEnglish/Views/Shared/_Layout.cshtml`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

## Implementation Tasks

1. Decide the runtime fix path up front:
   - either ensure the existing Bootstrap modal/toast runtime is available on
     `/AI/Chat`
   - or replace the dependency with a small app-local confirmation/feedback
     component that does not depend on `window.bootstrap`
2. Keep the final behavior app-styled:
   - no `confirm(...)`
   - no `alert(...)`
   - success feedback stays in-app
   - delete errors stay in-app
3. Preserve current security behavior:
   - anti-forgery token still required
   - owner-only delete still returns `404` for outsiders
4. Verify the active-conversation case still behaves safely:
   - delete active conversation redirects or resets to a clean draft state
5. Remove dead fallback code if the app-styled path is guaranteed.

## Verification

Run targeted verification:

- `dotnet test TCTEnglish.Tests --filter Ai --no-restore`

Manual verification checklist:

- deleting a conversation does not show a browser-native prompt
- full-page `/AI/Chat` shows the intended confirmation UI
- delete success feedback appears in-app
- delete failure feedback appears in-app
- deleting the active conversation returns to a safe empty/new-chat state

## Definition Of Done

- the full AI chat page no longer relies on browser-native delete dialogs
- the shipped runtime actually supports the chosen confirmation/feedback UI
- no regression to delete behavior, anti-forgery, or ownership protection

## Suggested Agent Prompt

`Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-01-delete-ux-runtime-gap.md. Execute only this follow-up. Fix the full-page AI chat delete confirmation/runtime gap so the app-styled confirmation and feedback actually work, run targeted AI tests, and report remaining risks.`
