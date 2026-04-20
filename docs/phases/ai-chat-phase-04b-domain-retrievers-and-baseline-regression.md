# Phase 04B - Domain Retrievers and Baseline Regression

**Assigned model:** GPT-5.3 Codex  
**Platform:** Visual Studio  
**Phase type:** Focused backend expansion and regression coverage

## Mission

Expand the deterministic baseline with the remaining priority retrievers and add baseline regression coverage.

## Area Hint

`TCTEnglish/Services/AI/Internal`, `TCTEnglish.Tests/*AI*`, vocabulary/class/speaking data queries, baseline regression coverage

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/vocabulary-feature/SKILL.md`
- `.agent/skills/speaking-feature/SKILL.md` if speaking retrieval is touched
- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/code-review-flow.md`

Also read the completed P00-P04A logs in `docs/implementation_plan.md`.

## In Scope

1. Implement the remaining priority baseline retrievers:
   - `UserVocabularyRetriever`
   - `ClassRetriever`
   - optionally `LearningProgressRetriever` if the baseline is already stable
2. Integrate these retrievers without breaking the P04A path.
3. Add/update baseline regression tests for:
   - vocabulary query
   - class query
   - out-of-scope refusal
   - no-data behavior
4. Update `docs/implementation_plan.md` status board and P04B execution log.

## Out of Scope

1. ML.NET work.
2. `.csproj` changes.
3. Unrelated UI work.

## Exit Gate

Do not mark this phase complete unless the core in-scope scenarios are covered and stable and `docs/implementation_plan.md` clearly states what remains for P05A.

## End-of-Phase Rule

Stop after P04B. Do not start P05A in the same run.

