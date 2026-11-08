# Phase 7 — measured evaluation, bounded live acceptance and demo freeze

Entry: Phase 6 APPROVED, latest scope verified, Phase 7 READY and owner launch.
Mode IMPLEMENT_LOCAL for local verification, demo artifacts and approved in-scope
repairs only through the review loop. Skills: tct-superpowers, engineering, security
and UI review as needed. Tentative dates 22–24 Nov; demo 25 Nov 2026.

## Outcome and mandatory distinction

Prove real Hybrid RAG works on the intended local machine, corpus and account;
measure supported answers/refusals, retrieval quality and actual latency; rehearse
network degradation and freeze reviewed versions. Offline/fake tests alone cannot
approve genuine semantic/model or live-account acceptance in this final phase.

No production/staging deployment, DB copy/reset or public release is in scope.
Preserve current model/settings and saved-key store; no key values in artifacts.

## A. Evaluation protocol

- [ ] Revalidate the frozen 120-case fixture and 80/40 split, source coverage matrix
      and reviewed thresholds. Report cases excluded as unavailable, with reasons;
      do not silently remove failures, count refusals as answers or change denominators.
- [ ] Run the full deterministic access/grounding/lifecycle/index/history/browser
      regression suite and actual build. Run the broader application test suite once
      after focused checks pass, documenting inherited failures/skips separately.
- [ ] Evaluate development and held-out source queries using genuine versioned
      embedding artifacts for the actual corpus/query texts, not random vectors or
      handpicked canned winners. Batch query embeddings if appropriate; retain
      provenance and disclose precomputed versus online measurements.
- [ ] Compare lexical-only/dense-only/hybrid on identical eligible sources and
      labeled relevance. Report Recall@5 with exact relevant-source definition and
      denominator, per-topic results and specific misses. Keep quality and answer
      coverage separate from correct refusal behavior.
- [ ] Evaluate factual support and task correctness with expected fact/source labels
      plus human-readable review, not citation-count or exact-string equality alone.
      Do not rely solely on an LLM grading its own answer. Distinguish an extractively
      grounded but irrelevant answer from a correct supported answer.
- [ ] Do not tune on held-out results. If exposed results drive design changes,
      document that exposure and require a reviewed new unseen supplement for a
      fresh claim; do not rebrand the tuned set as blind. Fixes stay in the active
      Phase 7 repair round and rerun impacted earlier-phase regressions.

## B. Live budget and execution

- [ ] Prepare a dry-run request/token/cost envelope first: actual source batch sizes,
      embedding reuse, held-out query batch requests, online turn count, output caps,
      retry policy and max duration. Present it to the owner for explicit approval
      unless an existing recorded grant already covers the exact run. No calls yet.
- [ ] A starting proposal is no more than 60 total text/embedding POSTs and 100,000
      aggregate input/output tokens, with retries counted and no speech requests.
      These are planning ceilings, not an existing grant or promised currency cost.
      Reduce scope if the real corpus/input budget exceeds them; do not run all
      120 cases through paid generation automatically.
- [ ] After the concrete grant, confirm live access with fail-fast bounded calls,
      then real corpus/query embeddings and at least 20 representative online turns
      if the approved envelope permits. Include the initial smoke turns in that
      total. Stop at any cap or fatal account/configuration failure; no retry storm.
- [ ] Reuse saved credentials without printing, rewriting or sending them to chat.
      Record safe model/provider request IDs and actual usage; no secret values or
      private account/payment rows in reports. Separate fake and live observations.
- [ ] Required live checks without authority/access remain BLOCKED. They cannot
      become passed merely because offline regression or metadata health succeeds.

## C. Quality and performance gates

| Measure | Proposed acceptance, agreed before final evaluation |
| --- | --- |
| Recall@5 on answerable source questions | At least 90%, actual counts and per-topic misses reported |
| Correct supported answer on in-scope answerable cases | At least 95%; no credit for unjustified refusal or irrelevant excerpt |
| Evaluated access/injection/safety cases | Zero observed unauthorized disclosure or unsupported factual addition |
| Labeled hard-negative demo questions | Correct refusal/clarification for every case |
| Warm local retrieval p95 | At most 300 ms, explicitly separate from network embedding time |
| Online end-to-end p95 with working network/provider | At most 8 s, report sample count and all stage timings |

These are test-set gates, not universal guarantees. Include hardware, corpus/vector
count and RAM, source/index/model versions, cold/warm/cache conditions and timestamp.
For small samples p95 is an observed estimate, not a production SLA. A miss requires
an explicit reviewed repair/scope decision, not a silently relaxed target.

## D. Rehearsal and artifacts

- [ ] Create a 15–20-question synthetic demo script covering feature help, personal
      facts, lesson/card explanations, paraphrases, follow-ups, mixed questions,
      insufficient evidence, out-of-scope, injection and another user's data.
      Include expected supporting source/facts and fallback for each journey.
- [ ] Rehearse cold launch with `scripts/start-local.ps1 -NoBrowser`, a free port if
      needed, `/health/ready` and `scripts/speech-status.ps1`. Measure startup and
      confirm no paid text/embedding/speech work happens just from startup.
- [ ] Rehearse network loss/recovery, expired/corrupt index and provider rejection.
      Degraded lexical/template answers must be labeled; do not present canned or
      cached output as a fresh live answer. Personal facts remain authoritative/live.
- [ ] Verify full/embed source navigation and synthetic Standard/Premium/Admin
      access on the actual demo machine; preserve existing DB/content/workloads.
- [ ] Write `docs/operations/hybrid-rag-local-demo.md` with verified commands/URLs,
      exact accepted versions, rebuild/status steps, known limitations, fallback and
      recovery. Do not invent commands before they exist. Freeze source/corpus/index
      fingerprints and capture the final reviewed baseline.
- [ ] Produce evaluation/latency/live-usage reports, final coverage limits and
      handoff/scope under `docs/feature-audits/hybrid-rag/phase-7/`.

## Final review gate

Cursor finishes AWAITING_REVIEW. Codex reviews required real evidence and any fixes;
final APPROVED means local demo acceptance on the recorded setup only. There is no
Phase 8 to start and no implicit commit/push/merge/deployment. Keep unresolved
checks BLOCKED and report exactly what the demo can/cannot support.
