# Phase 02 - Architecture Lock and Handoff

**Assigned model:** Claude Opus  
**Platform:** Antigravity  
**Phase type:** Architecture review, interface freeze, coding split lock

## Mission

Review the proposal plus outputs from P00, P01A, and P01B, then freeze the implementation shape so coding phases do not drift.

## Area Hint

`TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`, `TCTEnglish/Services/AI/AiStreamingService.cs`, `TCTEnglish.Tests/*AI*`, `docs/*`

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/workflows/new-feature-flow.md`
- `.agent/workflows/code-review-flow.md`
- `.agent/skills/security-audit/SKILL.md`

Also read the completed P00, P01A, and P01B logs in `docs/implementation_plan.md`.

## In Scope

1. Decide and document the concrete implementation approach for the internal AI runtime:
   - keep `IAiProviderClient` and swap the implementation, or
   - route `AiChatService` through a new internal orchestrator service
2. Freeze the core interfaces and likely method signatures for:
   - query classifier
   - retriever layer
   - answer composer
   - internal provider/orchestrator
3. Split file ownership cleanly across P03, P04A, P04B, P05A, and P05B.
4. Define the minimum test set each coding phase must leave behind.
5. Define the exact gate for when ML.NET is allowed to begin.
6. Update `docs/implementation_plan.md` status board and P02 execution log.

## Preferred Output

Prefer updating `docs/implementation_plan.md` plus optionally creating:

- `docs/ai-chat-internal-technical-spec.md`

If you create that spec file, keep it concise and implementation-focused.

## Architecture Rules

1. Preserve current chat UI and conversation persistence contracts if possible.
2. Avoid unnecessary controller changes.
3. Keep `AiStreamingService` stable unless a concrete blocker requires change.
4. Keep security and ownership rules visible in the design.
5. Keep `.csproj` additions approval-gated.

## Out of Scope

1. Do not implement production code yet.
2. Do not start the ML.NET package phase.
3. Do not add new views or new feature screens.

## Verification

1. Cross-check the design against real repo boundaries.
2. Ensure the coding split is concrete enough for separate models with separate context.
3. Ensure P03, P04A, and P04B do not accidentally share the same write-heavy files unless unavoidable.

## Exit Gate

Do not mark this phase complete unless:

1. The implementation strategy is frozen.
2. File ownership per later phase is clear.
3. Test ownership per later phase is clear.
4. ML.NET approval dependency is explicitly called out.
5. `docs/implementation_plan.md` has been updated.

## End-of-Phase Rule

Stop after P02. Do not start P03 in the same run.
