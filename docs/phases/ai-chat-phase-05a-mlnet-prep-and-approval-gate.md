# Phase 05A - ML.NET Prep and Approval Gate

**Assigned model:** GPT-5.3 Codex  
**Platform:** Visual Studio  
**Phase type:** Small preflight phase to avoid context overflow and unsafe package edits

## Mission

Prepare the ML.NET work, confirm package/model needs, and stop early if approval is required.

## Area Hint

`TCTEnglish/Services/AI/Internal`, `TCTEnglish/TCTEnglish.csproj`, `TCTEnglish.Tests/TCTEnglish.Tests.csproj`, ML.NET dataset/model loading, approval gating

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/new-feature-flow.md`

Also read the completed P00-P04B logs in `docs/implementation_plan.md`.

## In Scope

1. Check whether `Microsoft.ML` is already present.
2. Confirm the exact packages, model artifact path, and dataset path needed.
3. Decide the smallest viable runtime integration shape.
4. If approval is missing for `.csproj` edits, stop and record the blocker clearly.
5. If approval already exists and no package changes are needed, prepare the exact implementation checklist for P05B.
6. Update `docs/implementation_plan.md` status board and P05A execution log.

## Out of Scope

1. Full ML.NET runtime implementation.
2. Broad non-ML refactors.

## Exit Gate

Do not mark this phase complete unless the ML.NET package/dependency situation is explicit and the next step for P05B is unambiguous.

## End-of-Phase Rule

Stop after P05A. Do not start P05B in the same run.

