# Phase 01B - Website Knowledge Assets and Answer Style

**Assigned model:** Claude Sonnet  
**Platform:** Antigravity  
**Phase type:** Focused content asset creation

## Mission

Prepare website guide content and canonical answer-style assets for the internal assistant.

## Area Hint

`docs/*`, `TCTEnglish/wwwroot/data`, website feature guides, answer-style assets

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/vocabulary-feature/SKILL.md`
- `.agent/skills/speaking-feature/SKILL.md`
- `.agent/workflows/new-feature-flow.md`

Also read the completed P01A log in `docs/implementation_plan.md`.

## In Scope

1. Create or update website guide knowledge content that stays strictly inside TCT English features.
2. Draft a small answer-style reference for:
   - greeting
   - in-scope answer format
   - no-data-found
   - out-of-scope refusal
3. Keep wording short, deterministic, and website-grounded.
4. Update `docs/implementation_plan.md` status board and P01B execution log.

## Preferred Output Files

1. `TCTEnglish/wwwroot/data/ai/website-guides.json`
2. `docs/ai-chat-answer-style-reference.md`

If the final folder is not ready yet, create the draft under `docs/` and note the intended final path.

## Asset Quality Rules

1. Stay inside current product scope and real site features.
2. Do not mention unsupported flows.
3. Refusal answers must not drift into general English tutoring.
4. Keep answer-style guidance compatible with template-based composition.

## Out of Scope

1. Intent dataset writing.
2. Service/controller/runtime code changes.
3. `Program.cs` or `.csproj` changes.

## Exit Gate

Do not mark this phase complete unless guide coverage and answer-style assets are ready for P02/P04A and `docs/implementation_plan.md` has been updated.

## End-of-Phase Rule

Stop after P01B. Do not start P02 in the same run.

