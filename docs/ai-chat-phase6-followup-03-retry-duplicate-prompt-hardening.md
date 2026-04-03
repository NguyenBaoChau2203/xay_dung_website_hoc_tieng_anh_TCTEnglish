# AI Chat Phase 6 Follow-Up 03

> Must-fix follow-up created from the independent review on 2026-04-03.
> Execute this file only. Do not silently pick up other follow-up files.

## Goal

Remove the risk that one user action can create duplicate user prompts in an
existing AI conversation after transient provider failures.

Current review finding:

- `TCTEnglish/wwwroot/js/ai-chat.js` retries `503` / `5xx` responses in
  `sendWithRetry(...)`.
- `TCTEnglish/Services/AI/AiChatService.cs` persists the user message before
  the provider call for existing conversations.
- Draft conversations are rolled back on provider failure, but existing
  conversations are not.

Resulting risk:

- a transient provider failure can persist the first user prompt
- the automatic retry can send the same prompt again
- the conversation can end up with duplicated user messages and a second AI
  attempt for one click

This is a correctness issue and should be fixed before closure.

## Scope

In scope:

- AI send retry behavior for existing conversations
- Idempotency/correctness of persisted user messages around provider failure
- Targeted tests for the chosen fix

Out of scope:

- New provider/model work
- Broad observability redesign
- New chat features unrelated to duplicate-send hardening

## Primary Files

- `TCTEnglish/wwwroot/js/ai-chat.js`
- `TCTEnglish/Services/AI/AiChatService.cs`
- `TCTEnglish/Controllers/AiController.cs`
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`

## Implementation Tasks

1. Choose a single clear fix strategy before coding:
   - remove client auto-retry for AI send
   - or introduce a safe idempotency mechanism that prevents duplicate prompt
     persistence for one user action
2. Keep the UX reasonable:
   - users should still get a clear retry path
   - failures should still surface a friendly message
3. Preserve existing protections:
   - quota logic
   - anti-forgery
   - ownership checks
   - provider failure logging
4. Add targeted tests for the chosen strategy:
   - provider failure on existing conversation does not duplicate the user
     prompt for one action
   - retry/fallback behavior remains understandable

## Verification

Run targeted verification:

- `dotnet test TCTEnglish.Tests --filter Ai --no-restore`

Manual verification checklist:

- existing conversation send fails once due to provider/service unavailability
- one user action does not create duplicate prompts
- user can still intentionally retry after the failure

## Definition Of Done

- one user action cannot silently create duplicated prompts in an existing
  conversation
- the chosen retry strategy is explicit and test-covered
- no regression to quota, delete, or chat rendering behavior

## Suggested Agent Prompt

`Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-03-retry-duplicate-prompt-hardening.md. Execute only this follow-up. Fix the AI chat duplicate-prompt risk around retry/provider failure for existing conversations, run targeted AI tests, and report any remaining tradeoffs.`
