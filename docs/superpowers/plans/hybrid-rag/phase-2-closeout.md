# Phase 2 local closeout — 2026-11-06

Mode: IMPLEMENT_LOCAL. The owner asked Codex to identify the current phase and
finish its outstanding work before the owner starts the next phase. Work inline
as one agent, preserve the dirty checkout and existing processes. Apply AGENTS.md
and the existing phase gate. No Phase 3 implementation, paid API or remote stage.
Reuse the recorded narrow chat-RAG DI grant only if necessary; no other protected
configuration, persisted model, shared content or credentials need modification.

## Outcome and evidence

Phase 0/1 are approved. Phase 2 repair round 2 is awaiting review with cumulative
67-entry fingerprint `ab0b31c22cad9b6bebddf802433684663e995f7de236c6caea56d6bad2c97b51`.
F02 (subject sentence selection) and F03 (unsupported full lexical answers) need
independent verification; F01 (admin operation) and F04 (chunk limits) stay fixed.
The latest handoff records rendered-admin checks, which must be assessed against
the actual current code/runtime. Earlier results are evidence, not fresh passes.

## Ordered checklist

- [x] Verify the returned manifest, review source/tests and classify any drift.
- [x] Reproduce residual F03 relation/subject/qualifier and unknown-request gaps
      with meaningful regression tests before changing application behavior.
- [x] Use a deliberately bounded extraction contract for full lexical answers:
      match the subject, relation and requested qualifiers in the same source.
      Unknown fact requests fail closed. Label keyword-search results as excerpts
      without claiming a full answer. Preserve exact source IDs/hash/text.
- [x] Recheck F02 through retrieval and the production answer boundary; preserve
      F01/F04, access revalidation, current-user facts and non-chat provider paths.
- [x] Build in a fresh local configuration; run focused regressions, then the
      broader suite once. Record real skip counts and development baseline only.
- [x] Verify actual admin dry-run/build feedback, keyboard operation, responsive
      layout and unchanged snapshot for dry-run; preserve existing listeners.
- [x] Review final diff, append verified bug memory, refresh the cumulative scope
      manifest and write a Vietnamese closeout/review report. Mark APPROVED only
      if every mandatory Phase 2 acceptance is evidenced; then Phase 3 READY and
      Phases 4–7 LOCKED. Do not launch Phase 3.

## Acceptance and delivery

No full answer from another subject's number/color/cause/time/agent; unmatched
fact phrasing and unsupported mixed facets return insufficient evidence with no
unused citations. Supported time/location/count/cause/agent controls still work.
Correct subject-bearing source sentences, capped v2 spans, safe snapshots and
admin auth/antiforgery remain verified. Preserve the frozen evaluation fixture
and do not score held-out cases or tune BM25 here. No schema/content edit means
no migration/shared package. Deliver the final files/checks/limits in Vietnamese,
distinguishing local verification from unperformed CI/production work.

## Completion evidence

Completed locally: focused checks 115/115; final full suite 1044 passed, 0 failed,
19 opt-in skipped; Node 7/7; tooling validator exit 0 and Python 134 passed/1 skipped.
Real admin keyboard/double-submit/dry-run/build/responsive checks are documented
in the [review report](../../../feature-audits/hybrid-rag/phase-2/review-round-3.md).
Phase 2 is APPROVED and Phase 3 READY. Future phases were not executed.
