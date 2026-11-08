# Phase 5 repair round 2 — complete envelope validation and bounded response projection

## Outcome and authority

Close the remaining cases of HR-P5-R1-F02 and HR-P5-R1-F05, plus the manifest
reference defect HR-P5-R2-F06 in
[review round 2](../../../feature-audits/hybrid-rag/phase-5/review-round-2.md).
Preserve the verified F01/F03/F04 repairs and every existing citation/history/
quota regression. Phase 6–7 remain LOCKED.

This plan is prepared in REVIEW_ONLY. Execute IMPLEMENT_LOCAL only on owner
launch of this repair. One agent investigates, implements, reviews and verifies.
The review alone grants no application fixes or next-phase work.

Input identity: branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 140-entry Phase 5 manifest,
fingerprint `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`.
Its predecessor fingerprint is reviewed Phase 5 137-entry
`01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
Do not confuse that predecessor with the incorrectly named Phase 4 path in the
current manifest. Verify current file hashes before implementation.

Preserve all dirty work, workloads and data. No paid API, credential write,
commit, push, PR, remote CI, deployment, dev DB reset, shared-content edit or new
protected config/project/model-provider setting change is authorized. Carry only
the existing narrow chat DI grant if needed; these fixes should not need new DI.
No new persisted mapping or migration is expected.

Use AGENTS.md, the active review/state, tct-engineering/tct-superpowers/API/security
procedures and symptom-relevant bug memory. CodeGraph first for indexed source;
bounded reads for omitted gaps. Do not spawn agents, index/rebuild or alter trust.
Apply tct-ef-migration if a persisted-model change becomes necessary; do not widen
scope or rewrite an applied migration to solve these runtime validation defects.

## Confirmed evidence

| Finding | Source | Observed behavior |
| --- | --- | --- |
| F02 / P1, residual | GroundingCitationService.TryRead, lines 584–611 | Version 1 answered envelope without sources is accepted as an empty list; ProviderContext retains original private marker |
| F05 / P2, residual | AiChatService.SendAsync post-commit projection, line 410 | Cooperative waiting projection receives CancellationToken.None; send waits until an external barrier opens, after assistant/settlement already committed |
| F06 / P3, new | phase-5/scope-manifest.json predecessorManifest | Path points to Phase 4 but predecessor fingerprint identifies reviewed Phase 5 137-entry snapshot |

Ignored optional diagnostic evidence on this machine:
`.tmp/hybrid-rag-review/phase-5/round-2/AiRagPhase5ReviewRound2ProbeTests.cs` and
`review-probes.trx`. Three cases are intentionally red. Do not depend on ignored
files being present on another checkout. The seven-second observation establishes
that the current call has no application deadline; it is not a new product SLA.

## Ordered checklist

### 1. Verify entry and preserve completed work

- [x] Inspect actual branch, HEAD and dirty tree. Verify all 140 hashes and the
      exact fingerprint before editing. Preserve the input snapshot identity;
      classify unexpected drift rather than overwriting it.
- [x] Record owner launch, repair scope and Phase 6–7 locks in review state.
- [x] Confirm current parser/capture contract, post-commit acceptance ordering,
      source projection I/O and conversation lease lifetime from source.

### 2. Finish F02 without breaking explicit empty or legacy envelopes

- [x] Add maintained failing regressions for the exact missing-sources examples
      in the review, through direct projection and a real SendAsync/context recorder.
      Include initial history/older-page safe display behavior as applicable.
- [x] Require the current envelope's mandatory structure. A missing sources
      property is unverifiable metadata, not a verified empty source list. Keep
      property-name handling consistent with the serializer's existing case policy.
- [x] Route missing/null/non-array sources and other invalid required structure
      through the safe unverified policy. ProviderContext must never replay the
      original body. Preserve narrow parser exception handling and cancellation.
- [x] Keep true legacy NULL metadata compatible and test an explicit valid
      `sources: []` envelope for an existing source-free outcome. Do not solve
      this by hiding every legacy answer, rejecting all empty arrays, fabricating
      source identities or rewriting stored rows.
- [x] Preserve broken/future/oversized/deep/null-member protections, permission
      rechecks, safe routes, chronological paging and the F01 token-window repair.

### 3. Finish F05 with a real finite deadline

- [x] Document the chosen finite projection bound and why it fits a response
      after durable acceptance. Use a local implementation boundary; do not add
      protected configuration solely to create a timeout knob.
- [x] Add a regression where Display projection is actually waiting and honors
      the supplied token, rather than immediately throwing OperationCanceledException.
      The send must return the safe fallback when its deadline expires without
      an external barrier release. Always release barriers during test cleanup.
- [x] Give accepted-turn projection an appropriate independent, bounded token
      and propagate it through relevant database/file operations. Do not rely on
      a caller token that may already be aborted or a per-command DB timeout to
      bound the whole projection.
- [x] Keep exception, timeout and cancellation outcomes fail closed: no raw source
      body/title/link when authorization cannot complete. Preserve conversation/
      request identity, one saved assistant with its metadata, known usage and
      exactly one settled question. Do not release, roll back or resettle acceptance.
- [x] Do not race away unobserved work that continues using a disposed scoped
      DbContext. Keep cancellation/resource cleanup correct and preserve active
      stream/conversation guard behavior.
- [x] Verify HTTP/service reply and streaming completion under timeout and normal
      operation, followed by a successful authorized reload. No automatic retry
      or new assistant turn may be introduced by the fallback.

### 4. Correct F06 in the next manifest

- [x] Keep history identities factual. The next manifest predecessorFingerprint
      must identify the current reviewed 140-entry snapshot (`6f674441...`).
- [x] A predecessor path, if retained, must identify that same real snapshot.
      If no immutable predecessor manifest is available, use an explicitly named
      predecessor review/reference plus the fingerprint, or remove the misleading
      optional path. Do not fabricate a 137-entry manifest from current hashes,
      point a historical reference at a mutable self path, or silently claim
      the Phase 4 path identifies the Phase 5 predecessor.

### 5. Verify final source and return for review

- [x] Run new meaningful cases red before their fixes and green afterward.
      Retain the existing 10 repair regressions and 8 citation tests unchanged
      unless a documented behavioral correction truly requires an assertion change.
- [x] Build and run focused Phase 4/5, quota, cancel and provider-consumer checks.
      Run the final full app suite; list exact counts/failures/skips. Run affected
      Node history/submit tests when needed; use recording fakes, no paid calls.
- [x] Confirm no persisted mapping/schema drift. Run repo-local EF pending-model
      check. Keep the submitted migration, Designer and snapshot aligned.
- [x] Reuse valid provider evidence for unchanged schema only when its source
      identity and preserved rows are verifiable; otherwise run a guarded disposable
      MariaDB 10.5.23 upgrade/repeat/legacy-row check on final binaries. Record real
      database/container/port/version and checksums, never credential values.
- [x] Review complete diff, append actual fixes to bug memory and update a matching
      known issue if present. Check links and `git diff --check`. Do not label a
      catch-only test as proof of a real deadline.
- [x] Write `docs/feature-audits/hybrid-rag/phase-5/fix-round-2-handoff.md` in
      Vietnamese with evidence, limits and any remaining blocker.
- [x] Refresh the cumulative Phase 5 scope manifest, including repaired source/
      tests and the new controlling plan/prompt. Exclude results/state/self from
      its digest. Recompute every hash and ordinal fingerprint from final bytes.
- [x] Stop at Phase 5 AWAITING_REVIEW, Phase 6–7 LOCKED. Do not self-approve,
      open the next phase or perform ungranted Git/release stages.

## Acceptance

Missing required envelope structure cannot replay the original body into either
real context collection. Legacy NULL and explicitly valid empty-array outcomes
remain compatible. A cooperative stalled post-commit projection ends through a
documented finite deadline, returns safe content and preserves one accepted turn
and settlement. The manifest predecessor reference identifies the same snapshot
as its predecessor fingerprint. Existing F01/F03/F04 behavior stays green.

Report actual local results in Vietnamese. Skipped or unavailable checks are not
passes; identify concrete blockers without inventing completion evidence.
