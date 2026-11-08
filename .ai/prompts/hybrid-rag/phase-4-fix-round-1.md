# Cursor — Phase 4 repair round 1 only

## Outcome and verified entry

Repair HR-P4-R1-F01–F05 from
`docs/feature-audits/hybrid-rag/phase-4/review-round-1.md` by executing
`docs/superpowers/plans/hybrid-rag/phase-4-fix-round-1.md` in order. The goal is
correct sentence grounding/provenance, a bounded final provider payload, cumulative
per-attempt usage, current-source validation and durable stage ownership.

Reviewed branch: `feature/hybrid-rag-chatbot`; HEAD:
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Phase 4 returned manifest: **109 entries**,
fingerprint `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
Its Phase 3 prerequisite was approved at
`66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`.
Verify the current returned Phase 4 entries before editing. Do not require old
Phase 3 hashes to remain identical after authorized Phase 4 shared-file changes.

## Mode, authority and scope

On this owner's repair launch, mode is IMPLEMENT_LOCAL for this repair only.
Follow AGENTS.md, prompt standards and review gates/state. Use one agent inline;
preserve dirty work and existing workloads. Phase 4 must be CHANGES_REQUESTED;
Phases 5–7 stay LOCKED. Do not start durable citations, new chat UI or another phase.

The existing narrow Program.cs grant covers chat DI only. Keep non-chat clients,
models, transports and contracts stable; shared text transport changes must be
chat-opt-in. No appsettings/.csproj/model/secret change, paid API call, commit,
push, PR, remote CI, merge or deployment is authorized. Use local recording fakes
and guarded disposable MariaDB; never reset the dev DB or change migration history.

Use tct-superpowers/tct-engineering/security and tct-ef-migration where applicable.
Use CodeGraph first if indexed, with the pinned CLI when MCP is absent. Do not
rebuild the index or launch another AI agent. Read only active bug memory/context.

## Ordered work

1. Verify branch/HEAD/scope and reproduce each finding with meaningful maintained
   failing regressions before implementation. Scratch results under
   `.tmp/hybrid-rag-review/phase-4/round-1/` are diagnostic data, not a substitute
   for tests or permission. The plan describes enough fixtures to recreate them.
2. F03: materialize original individual sentences from real chunks and preserve
   stable IDs, sentence/document hashes, source identity, visibility, route and
   versions in transient evidence. Reject completed empty/unknown/unrelated
   selections. Keep prior facet/qualifier safeguards; do not relax grammar to
   accept whole multi-sentence chunks or add Phase 5 citation storage.
3. F05: make stage acquisition/renewal failure observable and durable. A terminal,
   expired/recovered or missing owner cannot silently start another send/stage.
   Gate sends/retries and reply acceptance on ownership; define bounded late-
   receipt handling and idempotent recovery across contexts/processes.
4. F02: assemble and bound the exact final selection context/schema/envelope and
   output before dispatch. Reconcile embedding/generation/retries with the same
   request/daily reservation. Shrink/refuse before HTTP; chars/4 and max output
   alone cannot bound generation input or unknown total spend.
5. F01: connect actual per-HTTP-attempt accounting to query embedding and scoped
   chat generation. Retain both known receipts and earlier uncertain sends through
   retry, invalid vectors/output, incomplete/refusal/cancel/timeout and cleanup
   faults. Record independent valid usage before answer/shape validation. Preserve
   zero-send preflight distinction; do not infer sends from exceptions.
6. F04: recheck source access/publication/hash/snapshot before external selection
   and after provider await, before acceptance/commit. On revocation/change keep
   paid usage and return a safe typed outcome without a stale assistant success.
7. Assess schema impact. Ship any new additive migration plus Designer/snapshot
   now; do not edit applied migrations or postpone dependent schema to Phase 5.
8. Run the plan's focused RAG/provider/cancel/quota/consumer regressions, build,
   final full suite, pending-model check and disposable MariaDB 10.5.23 upgrade/
   repeat/data/ownership tests. Use a free build output; preserve owner processes.

## Acceptance and delivery

Every finding needs failing-before/passing-after evidence with actual requests,
assistant/log artifacts and settled counters where relevant:

- Lost first embedding response plus successful retry retains uncertainty plus
  known 7. Invalid vector preserves known 7. Incomplete generation preserves its
  independent known total 105 instead of downgrading it to unknown 64.
- The exact selector payload fits the admitted input/schema/output allowance;
  CJK/Vietnamese history and context-cap cases are shrunk/refused before overspend.
- A real multi-sentence chunk supports the correct follow-up with original
  sentence/provenance. Completed empty selection cannot become catalog success.
- Unpublish/hash/private/snapshot changes at selector barriers cannot become a
  stale accepted reply. A current authorized source still works.
- A live lease survives another conversation's reserve; an expired/recovered
  owner sends no new generation request and creates no success artifact.
  Recovery/late cleanup stays bounded and idempotent on SQLite and MariaDB.

Docker was unavailable during independent review. Do not count handoff claims or
skipped facts as your MariaDB run. If mandatory provider evidence cannot be obtained,
report the precise blocker and keep the gate closed. Preserve the original
promotion regression: its first review focused failure passed in isolation and
the later full suite; investigate recurrence rather than weakening assertions.

Review the complete final diff. Append bug memory for verified fixes. Write
`docs/feature-audits/hybrid-rag/phase-4/fix-round-1-handoff.md` in Vietnamese and
refresh/recompute the cumulative scope manifest including repair guidance/tests.
Report exact checks, skips, schema/data evidence and fake-versus-live limits.
Set Phase 4 AWAITING_REVIEW, or BLOCKED for unavailable mandatory evidence, and
stop. Keep Phases 5–7 LOCKED. Do not self-approve or claim Codex reviewed the fixes.
