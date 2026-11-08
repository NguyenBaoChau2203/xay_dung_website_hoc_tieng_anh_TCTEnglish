# Cursor Auto — Phase 1 repair round 1 only

## Outcome, entry and authority

Repair HR-P1-R1-F01 through F04 using
`docs/superpowers/plans/hybrid-rag/phase-1-fix-round-1.md` and the actual review at
`docs/feature-audits/hybrid-rag/phase-1/review-round-1.md`.
Mode on explicit owner repair launch: IMPLEMENT_LOCAL for this repair scope only.
Require Phase 1 CHANGES_REQUESTED; Phase 0 remains APPROVED and Phase 2–7 LOCKED.

Read AGENTS.md, prompt authoring rules, review gates/state, accepted Phase 0
contracts and active engineering/security/domain skills. Work as one Cursor Auto
agent. Verify actual branch/HEAD/dirty work; reviewed branch is feature/hybrid-rag-
chatbot, HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69, returned fingerprint
781f92c523d57554a0b96acfd9c704986e8c7ac4e74cf47c9fcd39a2d9ef483f.

Retain the recorded narrow chat-DI grant without asking again. No new protected
config/project/model, paid API, real content/data, remote Git/CI or release work.
Preserve existing workload and prior uncommitted files. Do not start Phase 2.

## Ordered repairs

1. Inspect current CodeGraph source and relevant bug memory. Add meaningful failing
   regressions for all four findings, following the plan's positive controls.
2. F02/F03: correct bounded card candidate selection so exact/appropriate phrase
   matches are prioritized before caps, and legitimate short terms such as go/up
   remain searchable. Keep SQL-translatable access predicates, stable ordering and
   bounded I/O. Do not replace the query with an unbounded full-table load.
3. F01: prevent explicit external requests such as the gold-price question from
   becoming full card answers after a keyword hit. Preserve legitimate low-
   confidence internal term lookup and requests for the card's stored meaning.
4. F04: carry real card/set identity into transient evidence, use distinct stable
   source keys, and make outcome/text correctly request clarification for an
   ambiguous bare term in multiple sets. A specified accessible source must resolve.
   Keep all current access/revocation checks and non-chat provider consumers.
5. Run the exact applicable verification in the plan, including real-boundary tests,
   affected retrieval/composer, quota/cancel/history, HTTP/SignalR and non-chat paths.
   Use an isolated output configuration; no paid or production operations.
6. Review the full diff, record verified fixes in bug memory and create the actual
   fix-round-1 handoff with updated cumulative scope manifest and predecessor check.
   Do not describe current limitations as repaired without matching evidence.

## Acceptance and stop

Every finding must meet the plan's behavior criteria. Existing green tests do not
replace the new regressions. No BM25/vector search, paid generation, durable
citations, UI redesign or unrelated refactor belongs in this repair.

Report in Vietnamese with each ID, failing-before/passing-after, files, commands,
counts, limits and local-only status. Set Phase 1 AWAITING_REVIEW, keep Phase 2–7
LOCKED and stop. Do not self-approve or start another phase.

## Owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-1-fix-round-1.md only. Repair HR-P1-R1-F01 through F04, preserve the approved Phase 0 baseline and existing narrow authority, run the required regressions, and stop at AWAITING_REVIEW with Phase 2–7 LOCKED. Do not implement Phase 2.
```
