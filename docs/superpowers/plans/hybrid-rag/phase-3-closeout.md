# Phase 3 local closeout — 2026-11-06

Mode: IMPLEMENT_LOCAL, explicitly requested by the owner after repair round 1.
Review and finish remaining Phase 3 defects inline as one agent. Preserve the
dirty checkout, running workloads and the existing narrow RAG DI grant. No paid
API, secret/config/project-file change, remote Git/release or Phase 4 execution.

Returned scope: 86 matching entries, fingerprint
`408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`.
The existing plan/review gates and Phase 3 acceptance remain applicable.

- [x] Verify manifest and inspect the returned attempt ledger, transport, shared
      admission, source rechecks and meaningful regression assertions.
- [x] Reproduce residual F02 failures before editing: valid usage accompanying
      invalid vectors, cancellation after send, and unknown-spend retry under a
      tight token allowance. Preserve all known usage and reserve uncertain cost.
- [x] Reproduce cold-corpus admission above one job's safe byte ceiling. Support
      bounded progress/cache reuse without raising caps, bypassing source checks,
      claiming an incomplete index ready or issuing paid review calls.
- [x] Repair the smallest ledger/transport/batching boundaries. Keep normal chat
      lexical, maintain deadline/cancel/error/model contracts and process-wide
      source-root admission. Never share a scoped DbContext across concurrent work.
- [x] Build in an unused configuration; run focused tests, compatibility and the
      final broader suite. Distinguish opt-in skips and synthetic math from live
      semantic evidence. Recheck actual keyboard dry-run evidence and safe paths.
- [x] Review final diff, append verified bug memory, refresh cumulative manifest,
      write the Vietnamese round-2 review/closeout, and update the active gate.
      Approve Phase 3 and mark only Phase 4 READY when required checks are proved;
      do not implement Phase 4.

Primary files: `EmbeddingOperationBudget.cs`, `IOpenAiEmbeddingClient.cs`, `OpenAiEmbeddingClient.cs`,
`Rag/Corpus/VectorIndexService.cs` under `TCTEnglish/Services/AI`, plus focused
`TCTEnglish.Tests/AiRagPhase3CloseoutTests.cs`. A ViewModel/persisted/schema/shared
content change is not expected. Conservative UTF-8 bounds stay labelled bounds,
not tokenizer measurements. Unknown cost cannot become an unused reservation.

Additional reproduced boundary: a production-client configuration failure before
HTTP must report zero attempts. Move single-call adapter accounting into the
default interface method; do not infer sends from exceptions in the index service.

Completed 2026-11-07. Nine regressions were demonstrated failing before the relevant
repair and pass now. Focused 135/135; full suite 1067 passed, 19 opt-in skipped,
0 failed. Tooling validator exit 0; Python 134 passed/1 platform skip. Final evidence
and gate: `docs/feature-audits/hybrid-rag/phase-3/review-round-2.md`.
