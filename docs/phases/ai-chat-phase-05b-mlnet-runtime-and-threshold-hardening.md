# Phase 05B - ML.NET Runtime and Threshold Hardening

**Assigned model:** GPT-5.3 Codex  
**Platform:** Visual Studio  
**Phase type:** Focused ML.NET implementation

## Mission

Implement the ML.NET-backed classifier and threshold hardening once P05A has cleared the package/model gate.

## Area Hint

`TCTEnglish/Services/AI/Internal`, `TCTEnglish/Program.cs`, `TCTEnglish/TCTEnglish.csproj`, `TCTEnglish.Tests/TCTEnglish.Tests.csproj`, ML.NET runtime integration and threshold hardening

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/feature-scaffold/SKILL.md`
- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/new-feature-flow.md`

Also read the completed P00-P05A logs in `docs/implementation_plan.md`.

## In Scope

1. Add the minimum ML.NET package(s) if approved and still required.
2. Implement the ML.NET classifier and prediction result shape.
3. Integrate dataset/model loading.
4. Apply the approved confidence threshold fallback to `OutOfScope`.
5. Do not train at app startup.
6. Keep the runtime fallback behavior clear if the model file is missing.
7. Update relevant tests.
8. Update `docs/implementation_plan.md` status board and P05B execution log.

## Out of Scope

1. General English generation.
2. Training at runtime/app startup.
3. Expanding retrievers beyond approved scope.

## Exit Gate

Do not mark this phase complete unless ML.NET is integrated and verified, or the phase is explicitly marked blocked in `docs/implementation_plan.md`.

## End-of-Phase Rule

Stop after P05B. Do not start P06 in the same run.

