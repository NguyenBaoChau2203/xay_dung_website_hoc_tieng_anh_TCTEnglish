# Phase 7 repair round 1 — nonpaid verification and executable API envelope

## Outcome, authority and input

Repair HR-P7-R1-F01/F02/F03 and complete available nonpaid preflight before asking
for a paid budget. This is an implementation plan to execute only on owner launch,
as one agent. Preserve current AGENTS.md and earlier approvals. No paid text,
embedding or speech, Git/CI/release, owner data reset or protected configuration
change is granted by this file. Do not start or approve a later phase.

Submitted Phase7 scope:155entries,
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.
Phase6 approved153:
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Branch `feature/hybrid-rag-chatbot`; HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Read phase7/review-round-1.md and
archive/verify the submitted manifest before editing. The review did not change
its source hashes. New guidance here belongs in the next cumulative manifest.

## Ordered work and dependencies

### 1. Environment and baseline (B01)

- [ ] Verify actual checkout,155file hashes, submitted TRX and current workload
      ownership. Reuse CodeGraph first; do not index/rebuild it or create agents.
- [ ] Verify Docker Engine and local MariaDB10.5.23 are available. At review the
      Docker Linux pipe was unavailable. Never reset/reseed the owner's database.
      Use `scripts/start-local.ps1 -NoBrowser` with a verified free port and fake
      AI endpoint/key scoped to child processes when serving the web.
- [ ] Run the real `IVectorIndexService.DryRunAsync` / existing admin embed-dry-run
      flow against the serving MariaDB and the current snapshot. Record status,
      snapshot/checksum, chunks/hits/misses/byte upper/request estimate and zero
      provider calls. Do not call BuildAsync with a live transport at this stage.
      If drift exists, preserve both versions and rebase the envelope explicitly;
      a lexical rebuild is a separate deliberate local action, not a paid embed.

### 2. Stabilize meaningful provider tests (F03, P2)

Files: `TCTEnglish.Tests/OpenAiProviderClientTests.cs`; a narrowly scoped test
helper if needed; `TCTEnglish/Services/AI/OpenAiProviderClient.cs` only if a minimal
clock/delay seam is necessary to verify actual production control flow.

- [ ] Reproduce the submitted failures from their TRX and source. Distinguish timer
      scheduling delay from sending after the caller token was already cancelled.
- [ ] Use explicit stage coordination or a controlled clock to observe first
      response/backoff/body entry before cancelling/advancing the deadline.
- [ ] Cover first503 + backoff + next response-body wait under one original
      deadline. The current one-attempt SlowContent test does not exercise retry.
      Add a meaningful red proof for resetting the deadline across retry.
- [ ] Preserve default production timing, total timeout, error classification,
      cancellation precedence, retry policy and usage accounting. Keep constructor
      callers compatible. Prefer existing dependencies; do not change Program.cs,
      appsettings or csproj. A necessary test seam must not weaken request admission.
- [ ] Do not widen the2.5s assertion, drop call-count assertions, skip/quarantine,
      retry tests until green or disable parallelization globally. Run provider
      tests with other suites and then full app verification once after fixes.

### 3. Define executable evaluation suites (F02, P1)

Files: phase7 `demo-script.md`, a new `evaluation-protocol.md`, acceptance runner/
test helpers only where required. Keep `Fixtures/AI/hybrid-rag-evaluation.json`
revision2/canonical hash/80-40 split unchanged. Preserve plan quality gates.

- [ ] Separate frozen synthetic evaluation (120cases/51sources) from actual
      local-corpus rehearsal (currently471documents/487chunks). Publish the data
      adapter/seed procedure, mapping from labeled source IDs to runtime source/
      chunk hashes, actor ownership/membership/role and fixed business-date policy.
- [ ] Seed only a disposable fixture environment, never the owner's database.
      Preserve Standard/Premium/Admin distinctions and legitimate daily limits;
      do not replace actor-lan-standard with Admin and claim the same access case.
- [ ] Specify development and held-out denominators, answerable cases, refusal
      cases, label coverage and unavailable-case reporting before results exist.
      Record prior held-out exposure; do not tune against it and claim blindness.
- [ ] Mark12case A/B runs as partial smoke/rehearsal only. Missing dense index
      returns lexical_degraded before query embedding; do not invent embedding
      or selector requests when deterministic branches answer. Require20online
      turns for the final live gate if approved budget permits; otherwise final
      acceptance remains BLOCKED, with the unmet criterion listed.
- [ ] Plan actual-corpus lexical/dense/hybrid comparison with genuine versioned
      corpus/query embeddings and labeled relevance. Include query batches and
      representative online turns in the eventual envelope. No random vectors or
      label-derived canned winners as semantic proof; no120paid generations by default.

### 4. Rebuild the budget envelope with a real run guard (F01, P1)

Files: phase7 `dry-run-envelope.md`; acceptance-runner budget/recording helpers and
regressions as needed. Do not change production embedding caps12POST/40000tokens
or override saved configuration. Reuse the existing job pause/resume behavior.

- [ ] Preserve the verified nominal cold-index calculation, but label its
      assumptions:113887unique byte upper,3nominal jobs/10first-attempt POSTs.
      Batch1=36276; unknown first attempt leaves3724in a40000job, so retry36276
      is denied. Recompute all subsequent batches/jobs when usage or retries differ.
- [ ] Distinguish unique text bytes, reserved bound per attempt, known usage,
      unknown held amount and billable actual usage. Count retries in both request
      and token admission. Never clear unknown usage merely to finish an index.
- [ ] Choose and document one executable policy: retry-safe bounded batching or
      stop/partial on insufficient retry headroom. If existing behavior cannot
      guarantee a complete build under the proposed cap, state that explicitly;
      do not silently raise caps or promise exactly3jobs after errors.
- [ ] Implement/verify a run-scoped guard shared across embedding jobs, query
      batches, online text calls and retries. Admit before each HTTP send, retain
      unknown usage, persist safe run counters and stop at token/POST/time caps.
      An acceptance-harness/recording HttpClient wrapper can do this without
      changing protected application wiring. Use fake HTTP for verification.
- [ ] Fake regressions: lost usage then retry; batch36276/job40000 denial; resume
      after partial; aggregate across multiple jobs/turns; duplicate sends/retries
      counted; cap cancellation and no index promotion on incomplete input. Keep
      model/dimension/source-current checks and request IDs without payload/keys.
- [ ] Record concrete models, source/index hashes, batch membership, query texts
      or safe hashes, actors/available daily quota, online turns/output limits,
      total/max attempts, maximum duration, stop conditions, reuse assumptions
      and remaining coverage. Daily60000/Standard15 remain enforced.
- [ ] 169087 is nominal 113887+55200, not an all-retry guarantee.282974 is an
      illustrative twice-unique-input counterexample, not an approved/requested
      budget or a guarantee of the current batch policy. Do not turn either into
      spending authority. Present the corrected concrete envelope for review.

### 5. Nonpaid rehearsal and verification

- [ ] Rehearse local cold launch/readiness/speech metadata and full/embed source
      navigation with synthetic data and recording loopback/fake transports.
      Include network loss/recovery, unavailable/corrupt index and provider rejection.
      Preserve owner listeners and accounts. Record real versus injected evidence.
- [ ] Confirm startup/rehearsal made zero external text/embedding/speech requests.
      Reuse saved keys only when a later explicit live grant covers the exact run;
      do not read/print/rewrite keys for nonpaid checks.
- [ ] Run relevant budget/grounding/access/cancel/provider regressions, existing
      Node14chat guards and app build. Then one full app suite on final source;
      preserve all failures/skips. Isolated rerun success does not close F03.
- [ ] No persisted-model changes are expected. If introduced, stop scope expansion
      and apply tct-ef-migration/provider assessment; do not invent schema authority.
- [ ] Append actual bug-fix evidence to the bug log after fixes; update a matching
      known issue if present. Inspect diff, whitespace, report links and secret hygiene.

## Deliverables and exit

- Updated evaluation protocol/demo/envelope with assumptions and denominators.
- Maintained deterministic regressions and any minimal reviewed test/runner seams.
- Verified nonpaid browser/dry-run evidence, exact test/build counts and remaining
  environment blockers, under phase7 audits; ignored raw evidence stays private.
- `fix-round-1-handoff.md` and a fresh cumulative manifest including new guidance
  and all actual changes. Use the reviewed155-entry input as predecessor and
  preserve its manifest, rather than incorrectly pointing to Phase6 as the repair
  snapshot. Review result files remain excluded to avoid recursive hashes.

Return local repair AWAITING_REVIEW only when its nonpaid acceptance is evidenced.
Required live acceptance remains BLOCKED until a separate concrete budget grant
and real results. Never APPROVED, call paid APIs, commit/push/CI/deploy or treat
selection of an outdated A/B envelope as permission for a revised higher cap.
