# Phase 01A - Intent Inventory and Dataset Seed

**Assigned model:** Claude Sonnet  
**Platform:** Antigravity  
**Phase type:** Focused content/data drafting

## Mission

Prepare the intent inventory and ML.NET seed dataset material only.

## Area Hint

`docs/*`, vocabulary/class/speaking domain language, future AI internal data assets

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/vocabulary-feature/SKILL.md`
- `.agent/skills/speaking-feature/SKILL.md`
- `.agent/workflows/new-feature-flow.md`
- `.ai/context/domain-glossary.md` if terminology clarification is needed

## In Scope

1. Freeze the intent label list:
   - `MyVocabulary`
   - `MyProgress`
   - `CardLookup`
   - `SpeakingSuggestion`
   - `ClassInfo`
   - `WebsiteGuide`
   - `StudyRecommendation`
   - `OutOfScope`
2. Create or update a seed dataset file for later ML.NET work.
3. Ensure each intent has varied Vietnamese phrasing.
4. Include strong `OutOfScope` coverage.
5. Update `docs/implementation_plan.md` status board and P01A execution log.

## Preferred Output Files

1. `TCTEnglish/Services/AI/Internal/Data/intent-samples.seed.csv`
2. If the final folder is not ready yet, create the draft under `docs/` and note the intended final path.

## Asset Quality Rules

1. Keep all examples grounded in the real website domains and entities.
2. Do not include general English tutoring prompts as supported behavior.
3. Include multiple Vietnamese phrasings per intent.
4. Include realistic noisy variants, but do not invent features the site does not have.

## Out of Scope

1. Website guide content.
2. Answer-style writing.
3. Service/controller/runtime code changes.
4. `Program.cs` or `.csproj` changes.

## Exit Gate

Do not mark this phase complete unless dataset intent coverage is ready for later ML.NET work and `docs/implementation_plan.md` has been updated.

## End-of-Phase Rule

Stop after P01A. Do not start P01B in the same run.

