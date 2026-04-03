# AI Chat Phase 6 Follow-Up 02

> Must-fix follow-up created from the independent review on 2026-04-03.
> Execute this file only. Do not silently pick up other follow-up files.

## Goal

Restore a compilable, runnable AI regression test slice for the upgraded AI
chat feature.

Current review finding:

- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs` references helper
  methods that do not exist:
  - `SeedSuccessfulRequestLogsAsync(...)`
  - `CreateDeleteRequest(...)`
  - `SeedMessageAndRequestLogAsync(...)`
- `dotnet test TCTEnglish.Tests --filter Ai --no-restore` currently fails at
  compile time instead of executing tests.

This is a release blocker because the review phase cannot verify the feature
through automated tests in its current state.

## Scope

In scope:

- Fix the AI test project so the AI-focused test slice compiles and runs
- Restore missing helpers or rewrite affected tests cleanly
- Keep AI test coverage aligned with the upgraded feature set

Out of scope:

- Unrelated test cleanup outside the AI area
- Large refactors of the test infrastructure unless truly necessary

## Primary Files

- `TCTEnglish.Tests/AiPhase4HardeningIntegrationTests.cs`
- `TCTEnglish.Tests/AiPhase2ServiceTests.cs`
- `TCTEnglish.Tests/AiPhase1DataAndContextTests.cs`
- `TCTEnglish.Tests/Infrastructure/*`

## Implementation Tasks

1. Make the AI test slice compile cleanly:
   - either restore the missing helper methods
   - or inline/replace them with clearer local test utilities
2. Keep the upgraded feature coverage intact:
   - quota limit path
   - delete outsider `404`
   - delete owner success
   - anti-forgery requirement on delete
   - embed/full page rendering hooks already under test
3. Add missing tests discovered by review where practical:
   - Standard-plan hint rendering
   - delete confirmation markup or runtime hooks
   - delete success/error feedback hooks if they remain server-rendered
4. Do not silently weaken assertions just to make the suite pass.

## Verification

Run:

- `dotnet test TCTEnglish.Tests --filter Ai --no-restore`

If the AI slice passes, optionally run:

- `dotnet test TCTEnglish.Tests --filter \"Ai|HomeIndex_RendersAiLauncherWithDialogAccessibilitySemantics\" --no-restore`

## Definition Of Done

- the AI-focused test slice compiles
- the AI-focused test slice runs
- the tests still meaningfully cover the upgraded AI chat feature

## Suggested Agent Prompt

`Read AGENTS.md, docs/ai-chat-upgrade-execution-plan.md, and docs/ai-chat-phase6-followup-02-ai-regression-suite-repair.md. Execute only this follow-up. Repair the AI regression tests so the AI test slice compiles and runs, keep or improve meaningful coverage for the upgraded AI chat feature, and report any remaining gaps.`
