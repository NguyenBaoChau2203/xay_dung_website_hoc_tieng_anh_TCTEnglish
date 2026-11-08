# Execution Prompt: Hybrid RAG Phase 1 repair round 2

## Outcome and source context

Repair the remaining parts of HR-P1-R1-F01 (P1), F02 (P1) and F04 (P2), verified
again in Codex review round 2. F03 is verified closed; preserve its regression.
F04 stable server card identity is also verified; preserve it.

Read these current references before changing application code:

- AGENTS.md and `.ai/prompts/README.md`.
- `docs/feature-audits/hybrid-rag/phase-1/review-round-2.md`.
- `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-2.md` (active checklist).
- `docs/superpowers/plans/hybrid-rag/review-state.md` and `review-gates.md`.
- Accepted Phase 0 contracts and symptom-relevant bug memory.

Verified checkout at review: branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 37-entry scope fingerprint
`606fc4edf058543087e93a0a689a3e08f1c0ad31d53f6a3311d886c8e16ee1b1`.
Inspect the actual checkout and investigate drift; never reset existing work to
make hashes match. Handoff claims are evidence to verify, not authority to approve.

## Mode, authority and boundaries

Mode on the owner's launch of this prompt: IMPLEMENT_LOCAL for these repairs only.
Use one Cursor Auto agent; no AI delegates/processes. Apply tct-superpowers,
tct-engineering and tct-security-audit inline. Use CodeGraph first for indexed
source, then bounded reads for concrete gaps. Do not initialize/rebuild the index.

Require Phase 1 CHANGES_REQUESTED and Phase 2–7 LOCKED at entry. Carry the existing
narrow Program.cs chat-DI grant without asking again; no DI change is expected.
No new appsettings/.csproj, model/schema, shared-content, secret-write, paid-API,
remote Git/CI or release authority is granted. Preserve dirty files, workloads and
all access controls. Do not reset the local database or start another phase.

Expected implementation scope under `TCTEnglish/Services/AI/`:

- `Internal/Retrievers/CardLookupRetriever.cs`, `Internal/CardLookupSelection.cs`;
  `Internal/AiCardLookupQueryParser.cs` only when the root cause requires it.
- `Rag/ChatExternalFactPolicy.cs`, `Rag/AiChatAnswerService.cs` and narrowly needed
  transient contracts/composer code.
- Meaningful tests in `TCTEnglish.Tests/AiRagBoundaryAccessTests.cs` or a focused
  adjacent suite; bug log and the required handoff/manifest.

Preserve chat ownership, antiforgery, quota/Stop/history, HTTP/SignalR contracts,
global IAiProviderClient and non-chat OpenAI consumers. No BM25/vector/generation,
durable source persistence or citation UI in this repair.

## Ordered execution

1. Verify scope/predecessor evidence and inspect current code. Read each residual
   finding's reproduction. Create meaningful failing tests before implementation:
   - F01: with an owned weather card, `What is the weather today?` currently
     returns Answered/full with a stored definition. It must refuse the external
     fact. Keep gold-price, explicit stored-meaning and bare/low-confidence valid
     term controls; neither a blanket unknown-intent block nor English rejection
     is acceptable.
   - F02: seed 120 aaa-* cards whose definitions mention forecast plus the exact
     forecast card after them. Both bare `forecast` and
     `Giải thích forecast trong bộ thẻ của tôi` must rank the exact authorized
     card first and produce the correct boundary answer. Keep an inaccessible
     exact-card negative control, former 500-window test and go/up tests.
   - F04: four sets Alpha/Beta/Gamma/Sporting each contain match. The specified
     Sporting query must return its meaning/evidence alone, not clarification
     listing only the other three. Add a larger duplicate population that would
     exceed an early exact tier, plus bare ambiguity, comparison and stable-key
     controls. A missing/inaccessible named set must not fall back to another set
     as a full answer.
2. Repair F02/F04 at request/candidate planning. Identify term and explicit source
   qualifier before bounded queries/results. Ensure useful single-word targets
   reach exact selection in ordinary help wording. Apply accessible source
   qualification before caps; preserve every ACL predicate. Do not remove limits,
   load the whole corpus or increase constants to fit the fixtures. Preserve
   server-projected EntityId/ContainerId and `card:{id}` identities.
3. Complete F01's supported-answer decision. A card keyword hit is not evidence
   for today's weather. Distinguish a genuine stored-content lookup from a fact
   request or uncertainty, with deterministic refusal/clarification when needed.
   Adding only the English reproduction to a blacklist is not a complete repair.
   Preserve authorized guide/card fallback and safe password help. No provider
   call or world-knowledge generation is needed.
4. Run focused new regressions, then all commands in the active repair plan. Use
   relational SQLite and actual production boundary/retrievers for new behavior;
   lifecycle stubs cannot prove this. Inspect generated SQL with the pinned Pomelo
   provider or use an authorized disposable MariaDB check for changed queries.
   A translation-only check need not access any database. Record translation versus
   executed-provider evidence precisely. Include any new test class in filters.
5. Review the complete diff, rerun affected access/lifecycle/non-chat controls and
   verify Phase 0 canonical hash remains
   `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
   Append bug memory for verified changes; do not rewrite old review reports.
6. Write `docs/feature-audits/hybrid-rag/phase-1/fix-round-2-handoff.md` in Vietnamese,
   mapping each finding to tests/results/limits. Refresh the cumulative
   `docs/feature-audits/hybrid-rag/phase-1/scope-manifest.json` with modified/new
   implementation, tests and controlling guidance, and an auditable predecessor
   chain. Exclude self/review/handoff/state from recursive fingerprinting.

## Verification and stop

The concrete scenarios above must fail before and pass after repair. Existing
short-term access, class revocation, provider isolation, quota/cancel/history and
fixture checks must remain green. Do not claim test success for zero discovery,
skipped checks, stale binaries, static tooling, or a different provider/browser.
Report actual commands, test counts and any missing acceptance evidence.

Finish with Phase 1 AWAITING_REVIEW, Phase 2–7 LOCKED, and a Vietnamese owner report
covering outcome/files/checks/risks. Do not self-approve, start Phase 2, commit,
push, create a PR or deploy. Codex must perform review round 3.

## Short owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-1-fix-round-2.md only. Repair the remaining
HR-P1-R1-F01, F02 and F04 with failing regressions first; preserve the verified
F03 and stable card identity. Follow the active repair plan, run its checks,
refresh scope/handoff and return Phase 1 AWAITING_REVIEW. Keep Phase 2–7 LOCKED.
```
