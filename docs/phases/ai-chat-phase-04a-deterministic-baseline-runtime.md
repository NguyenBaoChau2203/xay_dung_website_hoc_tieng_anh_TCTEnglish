# Phase 04A - Deterministic Baseline Runtime

**Assigned model:** GPT-5.4  
**Platform:** Codex App  
**Phase type:** Core integration with minimal feature slice

## Mission

Turn the shell from P03 into a working deterministic internal baseline with the smallest useful answer path.

## Area Hint

`TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`, `TCTEnglish.Tests/*AI*`, baseline classifier/composer wiring

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/code-review-flow.md`

Also read the completed P00-P03 logs in `docs/implementation_plan.md`.

## In Scope

1. Implement the deterministic baseline classifier if P03 only created placeholders.
2. Implement the answer composer.
3. Implement only the smallest baseline retrievers needed to prove the path works:
   - `WebsiteGuideRetriever`
   - one minimal user-data retriever chosen by the locked architecture
4. Wire the internal runtime so `/AI/Chat/Send` returns meaningful website-grounded answers.
5. Ensure out-of-scope refusal works.
6. Add/update the minimum baseline tests.
7. Update `docs/implementation_plan.md` status board and P04A execution log.

## Out of Scope

1. Full retriever set.
2. ML.NET work.
3. Broad domain expansion.

## Exit Gate

Do not mark this phase complete unless the internal baseline path works end-to-end and `docs/implementation_plan.md` clearly states what remains for P04B.

## End-of-Phase Rule

Stop after P04A. Do not start P04B in the same run.

