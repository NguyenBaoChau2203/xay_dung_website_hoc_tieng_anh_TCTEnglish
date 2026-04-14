# Phase 06 - Independent Review and Hardening

**Assigned model:** Claude Opus  
**Platform:** Antigravity  
**Phase type:** Independent review gate with limited hardening allowed

## Mission

Perform an independent review of the implementation from P03 through P05B and decide whether the project is truly ready for final closure.

## Area Hint

`TCTEnglish/Services/AI`, `TCTEnglish/Controllers/AiController.cs`, `TCTEnglish.Tests/*AI*`, all files changed in P03 through P05B

## Read First

Follow the common read order in `docs/implementation_plan.md`, then read only:

- `.agent/skills/security-audit/SKILL.md`
- `.agent/workflows/code-review-flow.md`

Also read the completed P03 through P05B logs in `docs/implementation_plan.md`.

## Review Priorities

1. Security:
   - anti-IDOR
   - anti-CSRF
   - correct ownership checks
   - no data leakage through AI responses
2. Correctness:
   - in-scope queries
   - out-of-scope behavior
   - no-data behavior
   - null-handling
3. Architecture:
   - no regression into `HomeController`
   - no wrong view placement
   - AI code stays in the service boundary
4. Performance:
   - `.AsNoTracking()` on display/read queries
   - no obvious N+1 in retrievers
5. Product scope:
   - no fallback to general English tutoring
   - no hidden Gemini default path

## In Scope

1. Produce review findings ordered by severity.
2. If a finding is small and local, you may fix it in the same phase.
3. If a finding is broad or risky, record it and mark the phase not ready for closure.
4. Update `docs/implementation_plan.md` status board and P06 execution log.

## Out of Scope

1. New features unrelated to review findings.
2. Broad refactors that should belong to earlier phases.

## Verification

Re-run the most relevant checks for any fixes made during this phase.

## Exit Gate

Do not mark this phase complete unless:

1. No critical/high blockers remain open.
2. Any residual medium/low risks are explicitly recorded.
3. The next phase has a clean yes/no answer on whether closure is possible.
4. `docs/implementation_plan.md` is updated.

## End-of-Phase Rule

Stop after P06. Do not start P07 in the same run.
