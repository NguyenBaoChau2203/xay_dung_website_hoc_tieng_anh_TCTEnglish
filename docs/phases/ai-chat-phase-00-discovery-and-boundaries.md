# Phase 00 - Discovery and Boundary Validation

**Assigned model:** Gemini 3.1 Pro High  
**Platform:** Antigravity  
**Phase type:** Read-heavy, documentation-first, no production feature work yet

## Mission

Validate the approved proposal against the live repo and produce an exact implementation map for later phases.

## Area Hint

`docs/*`, `TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`, `TCTEnglish/Services/AI/AiStreamingService.cs`, `TCTEnglish.Tests/*AI*`

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/workflows/new-feature-flow.md`
- `.agent/skills/security-audit/SKILL.md`

## In Scope

1. Inspect current AI architecture and confirm the real touch points:
   - `AiController`
   - `AiChatService`
   - `IAiProviderClient`
   - `GeminiProviderClient`
   - `AiStreamingService`
   - relevant AI tests
2. Confirm which files can stay mostly unchanged and which files must be refactored.
3. Identify whether `Program.cs` DI changes will be required later.
4. Identify whether `.csproj` changes will be required later for ML.NET.
5. Confirm the correct feature boundaries and note any boundary traps.
6. Update `docs/implementation_plan.md` status board and P00 execution log.

## Out of Scope

1. Do not implement the feature.
2. Do not modify runtime code unless a tiny documentation-only correction is necessary.
3. Do not start dataset generation or interface design work from later phases.

## Deliverables

1. Exact file touch list for P03, P04A, P04B, P05A, and P05B.
2. Short blocker/risk list for later phases.
3. Confirmation of whether AI streaming should remain untouched.
4. A clean handoff note in `docs/implementation_plan.md`.

## Verification

1. Check actual repo files, not just the proposal.
2. Verify test coverage shape in `TCTEnglish.Tests`.
3. Verify whether any existing local changes already touch AI files.

## Exit Gate

Do not mark this phase complete unless:

1. The touch list is concrete enough that P01A, P01B, and P02 know what they are preparing for.
2. Program/DI risk is explicitly recorded.
3. ML.NET package risk is explicitly recorded.
4. Streaming impact is recorded.
5. `docs/implementation_plan.md` has been updated.

## End-of-Phase Rule

Stop after P00. Do not start P01A in the same run.
