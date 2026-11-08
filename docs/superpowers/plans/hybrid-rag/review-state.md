# Hybrid RAG phase state

Initialized 2026-11-06. This file records coordination, not test results or Git authority.
Phase 0 was launched by the owner on 2026-11-06. Codex review round 3 verified
repair of every Phase 0 finding, including HR-P0-R2-F01, and approved the recorded
local snapshot. The owner launched Phase 1 on 2026-11-06. Codex review round 1
found four reproducible answer-routing/card-retrieval/identity defects. The owner
launched repair round 1 on 2026-11-06. Codex review round 2 verified the returned
37-entry snapshot, closed F03 and the card-identity part of F04, but reproduced
remaining F01/F02/F04 behavior on SQLite. The owner launched repair round 2 on
2026-11-06. Codex review round 3 verified the 39-entry returned snapshot and
closed F02 while preserving F03/card identity. F01/F04 remain open and the new
source-qualification regression HR-P1-R3-F05 was reproduced on SQLite.
The owner launched repair round 3 on 2026-11-06. Codex review round 4 verified
the 42-entry snapshot and closed F05, preserving F02/F03 and card identity.
F01/F04 remain open: keyword-only support admits missing facts, and source-label
framing deletion can substitute a prefix set. The owner launched repair round 4
on 2026-11-06. Codex review round 5 verified the 44-entry snapshot and closed
F04 while preserving F02/F03/F05 and card identity. Only HR-P1-R1-F01 remains open:
reference IDs can satisfy the free-text value heuristic, and polite fact requests
can bypass its recognized frame. The owner launched repair round 5 on
2026-11-06. During review round 6 the owner explicitly authorized Codex to finish
the remaining F01 repair and verification locally. Codex implemented complete
lookup-target/field selection, verified all Phase 1 checks and approved the
48-entry final snapshot. The owner subsequently returned Phase 2 AWAITING_REVIEW.
Codex review round 1 verified its 60-entry snapshot and reproduced four defects:
Dry-run loses its operation value, lexical evidence drops the matching sentence,
unsupported lexical hits become full answers and long sentences bypass chunk caps.
Phase 2 was CHANGES_REQUESTED. The owner launched repair round 1 on 2026-11-06
after fingerprint ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a
matched 60/60. The repair returned Phase 2 AWAITING_REVIEW with cumulative
fingerprint 67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a.
Codex review round 2 verified all 64 entries and closed the F01 operation defect
and F04 chunk-limit defect. F02 remains open when framing words beat the subject
span; F03 remains open when unrelated relation cues or default Reference admit
unsupported full answers. Rendered logged-in admin acceptance is still pending.
Phase 2 was CHANGES_REQUESTED. The owner launched repair round 2 on 2026-11-06
after fingerprint 67ec08a66cc20aa6afc25b88750d184e11c1f9e949dbe92ceb9a7d8a817ed80a
matched 64/64. The repair returned Phase 2 AWAITING_REVIEW. F02 and F03 are
ready for Codex review; F01 and F04 stay closed. Cursor did not self-approve.
Phases 3–7 remain LOCKED.
No paid API or remote Git/release authority is implied.

The owner then explicitly asked Codex to identify the current phase and finish
its outstanding work locally. Codex closeout/review round 3 verified the returned
67-entry scope, reproduced residual F03 subject/relation/qualifier and incidental
guide failures, repaired them, and reran focused plus full-suite verification.
The final 70-entry scope is APPROVED: 115 focused checks passed; the final full
suite passed 1044 tests with 0 failures and 19 unrelated opt-in skips. Rendered
admin dry-run/build, keyboard, duplicate-submit and narrow layout checks passed.
F01/F02/F03/F04 are closed. Phase 2 is APPROVED, Phase 3 READY, Phases 4–7 LOCKED.
This owner grant permits this local implementation/review closeout, not a launch
of Phase 3 or any paid API/remote Git/release action.

The owner launched Phase 3 on 2026-11-06. Before editing, Cursor recomputed the
Phase 2 approval: 70/70 digest files matched and the ordinal fingerprint was
`62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf`. The local
implementation returned Phase 3 AWAITING_REVIEW. The returned scope is 81
entries, fingerprint
`dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`, recorded in
the phase 3 manifest. Cursor did not approve it. Phases 4–7 remain LOCKED.
No paid embedding call and no remote Git/release action was performed.

Codex review round 1 verified the 81-entry returned snapshot and reproduced four
P1 defects with isolated synthetic probes: transport retries bypass maxRequests;
token admission uses an estimate and drops known overage usage; admission locks
are scoped per request; and later batches still send sources unpublished during
an earlier batch. Existing focused/compatibility suites passed 119+84 checks.
Phase 3 is CHANGES_REQUESTED with HR-P3-R1-F01–F04 open. Review application source
and the returned manifest were preserved; the repair plan/prompt require owner
launch. Phases 4–7 remain LOCKED. No paid API or remote action occurred in review.

The owner launched repair round 1 on 2026-11-06. Before editing, the returned
81-entry fingerprint `dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`
matched 81/81. The repair returned Phase 3 AWAITING_REVIEW with cumulative
fingerprint `408779fa6c5d9139fa40d8021693c4b76fa6a3b860bbf1478804b368cec9e720`
(86 entries). Cursor did not approve it and does not claim Codex has reviewed
the repairs. Phases 4–7 remain LOCKED.

The owner then explicitly authorized Codex to fix remaining Phase 3 defects locally
so Phase 4 can proceed. Codex review/closeout round 2 verified the returned 86/86
files, preserved F01/F03/F04 repairs, reproduced remaining F02 receipt/unknown-cost/
I/O/preflight accounting defects and the new cold-corpus progress defect F05,
and repaired them inline. Nine added regressions failed before their respective
repairs and pass now. Final focused checks: 135/135; final full suite: 1067 passed,
19 opt-in skipped, 0 failed. Tooling and preserved-runtime admin keyboard dry-run
checks passed, with the runtime/binary distinction documented in the review.
On 2026-11-07 Codex approved the final 89-entry snapshot, fingerprint
`66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`. Phase 3 is APPROVED, Phase 4 READY, Phases 5–7 LOCKED.
This grant does not launch Phase 4 or authorize paid API/remote Git/release work.

The owner launched Phase 4 on 2026-11-07. Before editing, Cursor recomputed the
Phase 3 approval: 89/89 digest files matched and the ordinal fingerprint was
`66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb`. Local
implementation returned AWAITING_REVIEW. The returned scope was 109 entries,
fingerprint `78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`.
Cursor did not approve it. Phases 5–7 remain
LOCKED. No paid API or remote Git/release action was performed.

Codex review round 1 on 2026-11-07 verified the returned Phase 4 scope, 109/109
hashes and fingerprint 78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5.
Eight synthetic observations exposed five findings: dropped per-attempt/invalid-
response usage (F01), missing final-input/unknown-generation budget (F02), chunk-
versus-sentence/provenance mismatch (F03), source revocation during selection (F04),
and paid work continuing after lease recovery (F05). Full suite passed 1078 tests
with 20 opt-in skips, but does not cover these observed defects. The focused run
had one promotion failure that passed isolated and in the later full suite.
Independent pending-model metadata check passed; MariaDB upgrade/concurrency
reverification could not run because the Docker Linux engine was unavailable.
The owner launched repair round 1 on 2026-11-07. Before editing, Cursor recomputed the
returned Phase 4 scope: 109/109 files matched and the ordinal fingerprint was
`78d7bd633f430347d7bcde8de870225890fd15bce7881b5ee7af6f34cebcb6c5`. The repair
returned Phase 4 AWAITING_REVIEW. The cumulative scope is 113 entries, fingerprint
`b3406e3068e06442a6319e88a3e0f6e606ce9c18a9e997c0c13f983a3330693f`, recorded in the
phase 4 manifest. F01–F05 are addressed in that local snapshot and are pending
Codex review. Cursor did not approve them. Phases 5–7 remain LOCKED. No paid API
or remote Git/release action was performed.

Owner grant 2026-11-06, recorded at Phase 1 entry and reusable by later individually
approved phases: narrow `TCTEnglish/Program.cs` dependency-injection registrations
for chat-specific RAG services only. The global `IAiProviderClient` registration
and behavior used by non-chat features stay unchanged. This grant does not allow
`appsettings.json`, `.csproj`, model-setting, paid-API, commit, push, PR or
deployment changes.

The owner explicitly authorized Codex to review and finish remaining Phase 4 work
locally on 2026-11-07 so Phase 5 can proceed. Review/closeout round 2 verified the
returned 113/113 entries, repaired residual cancellation/receipt/attempt accounting,
per-send ownership/current-source guards, snapshot and transient provenance gaps,
and reproduced/repaired Windows lexical/vector atomic-promotion sharing conflicts
(HR-P4-R2-F06). Thirteen new regression cases have red/green evidence. Final focused
checks passed 191/191; final full suite passed 1100 with 22 opt-in skips and 0 failures,
including the original promotion tests. MariaDB lifecycle passed 3/3; independent
10.5.23 baseline upgrade/14-migration repeat preserved user 900001 checksum, and
repo-local EF has no pending model changes. No new persisted mapping/migration.
Codex approves the final 117-entry scope, fingerprint `1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`.
Phase 4 is APPROVED, Phase 5 READY, Phases 6–7 LOCKED. This local grant does not
launch Phase 5 or grant paid API, remote Git/CI, deployment or protected configuration
authority. See the round-2 review for exact evidence and fake/live limits.

The owner launched Phase 5 on 2026-11-07. Before editing, Cursor recomputed the
approved Phase 4 scope: 117/117 digest files matched and the ordinal fingerprint was
`1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61`. No paid API or
remote Git/release action was performed.

Phase 5 local implementation is AWAITING_REVIEW. Submitted scope is 137 entries,
fingerprint `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
Handoff: [phase-5/handoff.md](../../../feature-audits/hybrid-rag/phase-5/handoff.md).
Phases 6–7 remain LOCKED. This record is not APPROVED and does not start Phase 6.

Codex review round 1 on 2026-11-07 verified the returned 137/137 files and fingerprint
`01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
Seven meaningful synthetic probes confirmed five findings: budget-exhausted history
returns before reauthorization (F01), invalid/future metadata replays original bodies
in ProviderContext (F02), null source entries crash parsing (F03), document-only
dedup loses a contributing sentence and misses its later edit (F04), and response
projection can throw after an assistant is committed (F05). The submitted suite
passed 1108 with 22 opt-in skips and 0 failures; the added reviewer probes are red
evidence, not repairs. Independent MariaDB 10.5.23 upgrade through 15 migrations,
repeat execution, preserved user/legacy-message/conversation checks and repo-local
EF pending-model check passed. No implementation/test/config/migration bytes in
the submitted 137-entry manifest were changed. Review artifacts and state only.
Phase 5 is CHANGES_REQUESTED with HR-P5-R1-F01–F05 open. Phase 6–7 remain LOCKED.
The repair plan/prompt require owner launch. No paid API, commit or remote action.

The owner launched Phase 5 repair round 1 on 2026-11-07. Before editing, Cursor recomputed
the reviewed Phase 5 scope: 137/137 digest files matched and the ordinal fingerprint was
`01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`. Repair policy:
legacy null metadata stays readable; present but unverifiable metadata keeps a safe
display body and is replaced by a neutral notice in provider context; every cited
sentence is retained for freshness and a v1 envelope without a matching full-document
hash is not treated as current when live sentences are missing from the envelope;
every history exit projects before budgeting; a projection fault after commit returns
a safe result for the saved turn. The repair returned Phase 5 AWAITING_REVIEW.
Submitted repair scope is 140 entries, fingerprint
`6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`, with
predecessor fingerprint `01b58fe297e2e3a2453061894d77122b738511b2507bcf5b71ead834cf865165`.
Handoff: `docs/feature-audits/hybrid-rag/phase-5/fix-round-1-handoff.md`.
Phases 6–7 remain LOCKED. This record is not APPROVED and does not start Phase 6.

Codex review round 2 on 2026-11-07 verified 140/140 returned files and fingerprint
`6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`.
F01, F03 and F04 are verified closed for their reported defects. F02 remains open
for envelopes missing the mandatory sources array: they still replay the body in
ProviderContext. F05 remains open for post-commit projection with no independent
deadline; a cooperative waiting dependency receives CancellationToken.None.
Three new red probes confirm these residual cases. New P3 HR-P5-R2-F06 records
the stale predecessorManifest path, which points to Phase 4 while its fingerprint
identifies the reviewed Phase 5 predecessor. Focused tests passed 70 with 2 opt-in
skips; full submitted suite passed 1118 with 22 skips and 0 failures. Fresh disposable
MariaDB 10.5.23 applied 15 migrations, preserved user/legacy-message/conversation,
repeated stably and has no pending model changes. Review source/test/config/schema
bytes and input manifest are preserved. Phase 5 is CHANGES_REQUESTED; Phase 6–7
remain LOCKED. Repair round 2 requires owner launch; no paid API, Git or release
authority is added.

The owner launched Phase 5 repair round 2 on 2026-11-07. Before editing, Cursor recomputed
the reviewed 140-entry scope: 140/140 digest files matched and the ordinal fingerprint was
`6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`. The predecessor
fingerprint remains the reviewed 137-entry Phase 5 snapshot; `predecessorManifest` still
points at the Phase 4 file and is the F06 defect to correct in the next manifest.
F01/F03/F04 stay as verified. Residual F02 requires a present sources array. Residual F05
gives accepted-turn display projection its own finite deadline. The repair returned
Phase 5 AWAITING_REVIEW. Submitted scope is 143 entries, fingerprint
`33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`, with
predecessor fingerprint `6f67444183b954dc4729d2b050a225ba98aae1cb17fb55cfc8e92c9cca141683`
and predecessor review `docs/feature-audits/hybrid-rag/phase-5/review-round-2.md`.
The Phase 4 manifest path is no longer the predecessor reference.
Handoff: `docs/feature-audits/hybrid-rag/phase-5/fix-round-2-handoff.md`.
Phases 6–7 remain LOCKED. This record is not APPROVED and does not start Phase 6.

Codex review round 3 on 2026-11-07 verified the returned 143/143 file hashes and
fingerprint `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
Residual F02 now rejects missing/ambiguous sources arrays without breaking legacy
NULL or explicit empty arrays. F05 has an independent five-second cooperative
projection deadline, safe fallback and no abandoned scoped work; service/stream
deadline regressions and the three unchanged reviewer probes pass. F06 now uses
the correct predecessor review/fingerprint. F01/F03/F04 remain verified closed.
Final focused checks passed 83 with 2 opt-in skips (80 submitted + 3 independent
probes); full submitted suite passed 1128 with 22 skips and 0 failures. Node 7/7.
Persisted model/migration/DI hashes match the reviewed 140-entry snapshot. Reused
independently verified MariaDB 10.5.23 upgrade evidence plus final-binary repeat,
15-entry history, preserved user/legacy-message/conversation and pending-model
checks all pass. No source/config/migration changes were made during this review.
Codex APPROVES the exact 143-entry snapshot above: Phase 5 APPROVED, Phase 6 READY,
Phase 7 LOCKED. F01–F05 and HR-P5-R2-F06 are closed. This approval does not launch
Phase 6 or add paid API, commit, push, CI or deployment authority.

The owner launched Phase 6 on 2026-11-07. Before editing, Cursor recomputed the
approved Phase 5 scope: 143/143 digest files matched and the ordinal fingerprint was
`33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`. Files outside
that digest match the recorded baseline and excluded handoff/review set, including
the pre-existing `.gitattributes` line for the evaluation fixture. Phase 6 is
IN_PROGRESS. Phase 7 stays LOCKED. No paid API, commit, push, or deployment.

Phase 6 implementation stopped on 2026-11-07 as BLOCKED. Submitted scope is 151
entries, fingerprint `0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`,
predecessor `33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
Handoff: [phase-6 handoff](../../../feature-audits/hybrid-rag/phase-6/handoff.md).
Browser report: [phase-6 browser report](../../../feature-audits/hybrid-rag/phase-6/browser-report.md).
The required browser journey for account switch, role downgrade and membership
removal with a warm cache was not run. The SQLite test
`WarmCache_DoesNotLeakAcrossAccountDowngradeOrRevoke` passed and does not replace
that browser row. Keyboard Tab reached the local source link; the painted outline
was not confirmed. This record is not APPROVED and does not unlock Phase 7.
No commit, push, CI, or deployment.

The owner subsequently authorized Codex to complete Phase6 locally, explicitly
forbidding APPROVED and unlocking/starting Phase7. The returned151/151 hashes
matched `0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`
before edits. Actual cookie/browser/MariaDB journeys now prove account A→B,
Premium→Standard and class-member removal with singleton cache hits within TTL;
SQLite is supplementary. Real paging/legacy/source deletion/full/embed/SignalR,
focus/narrow/text200%, missing/corrupt index and identified UI fault injection
complete the recorded local matrix. Late Stop and retry/event ownership were red
first, then repaired with Node regressions; Razor source whitespace was repaired
and checked visually. App1136 passed/22 opt-in skipped/0 failed; focused224/224;
Node14/14, final build and EF pending/repeat checks pass. Own fixture processes
and container were stopped; owner DB/secrets/corpus/workloads remain intact.
The new153-entry identity is
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
The input manifest is preserved as phase6/scope-manifest-implementation.json.
Phase6 is AWAITING_REVIEW; Phase7 remains LOCKED. This completion record is
not APPROVED and adds no paid API, Git, CI or release authority. See
[local completion handoff](../../../feature-audits/hybrid-rag/phase-6/local-completion-handoff.md)
and [browser closeout](../../../feature-audits/hybrid-rag/phase-6/browser-closeout.md).

The latest owner request on2026-11-07 asks to finish Phase6 so they can proceed to
Phase7. Codex completed the approval review of the unchanged153-entry snapshot;
this supersedes the preceding hold on APPROVED/gate changes for Phase6 closure.
Source hashes/fingerprint still match. Fresh focused224/224, Node14/14 and build
pass; actual browser/cache artifacts and the same-snapshot full1136/0/22 TRX were
verified. No new actionable finding remains in Phase6. Review:
[Phase6 round1 — approved](../../../feature-audits/hybrid-rag/phase-6/review-round-1.md).
Phase6 APPROVED; Phase7 READY, not launched. A concrete paid-API budget still needs
owner approval before any spend; this review grants no Git, CI or deployment stage.

The owner launched Phase 7 on 2026-11-07 by sending the approved Phase 6 fingerprint.
Before any Phase 7 edit, Cursor recomputed that scope: 153/153 raw-byte hashes matched
and the ordinal fingerprint was
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Branch `feature/hybrid-rag-chatbot`, HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
Phase 7 is IN_PROGRESS. This launch does not grant a paid text/embedding budget,
commit, push, CI or deployment.

Phase 7 stopped BLOCKED on 2026-11-07 before any paid call. The Phase 6
fingerprint still matches 153/153 after the state edit. Fixture revision 2 and
the promoted 487-chunk lexical snapshot were measured. The corpus safe upper is
113,887 bytes, above the 100,000 planning ceiling, and no vector cache exists.
Full suite on the Phase7Acceptance binary: 1135 passed, 22 opt-in skipped, 1
failed, total 1158. The failure is the wall-clock bound in
`Deadline_CoversBodyRead_AndDoesNotResetForRetry` (2.8705s versus 2.5s) after
`ErrorCodeTimeout` matched; eight isolated reruns passed. The earlier focused
run failed only `CallerCancellation_DuringBackoff_Propagates` (2 calls versus 1)
and twelve isolated reruns passed. Node 14/14 and the app build passed. Envelope:
[phase-7 dry-run](../../../feature-audits/hybrid-rag/phase-7/dry-run-envelope.md).
Handoff: [phase-7 handoff](../../../feature-audits/hybrid-rag/phase-7/handoff.md).
Phase 7 scope fingerprint
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.
No paid grant, Git, CI or deployment.

Codex reviewed the submitted Phase7 snapshot on 2026-11-08. Phase6 153/153 and
Phase7 155/155 hashes/fingerprints still match. Review round1 requests changes to
the retry/aggregate envelope (F01), evaluation actor/data/date/source protocol
(F02), and scheduler-sensitive provider tests (F03). B01 still needs actual
MariaDB embed-dry-run and nonpaid browser rehearsal; the Docker Linux engine pipe
was unavailable during review. The49 focused provider/embedding-budget tests
passed, but that does not clear the submitted full/focused failures. No product,
test, configuration or migration was edited; no paid grant was selected. See
[Phase7 review round1](../../../feature-audits/hybrid-rag/phase-7/review-round-1.md),
[repair plan](phase-7-fix-round-1.md) and the execution prompt linked by the review.
Phase7 stays BLOCKED for live acceptance with local changes requested. Neither
the old80POST/169087 proposal nor the282974 counterexample is spending authority.

The owner launched the Phase 7 repair on 2026-11-08. Before edits, the submitted
155-entry snapshot recomputed to
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817` and was archived
at `docs/feature-audits/hybrid-rag/phase-7/scope-manifest-input.json`. F01–F03 are
repaired locally: the A/B envelope is withdrawn, a lost 36,276-byte batch stays
unknown and cannot be retried inside the 40,000-byte job, the evaluation protocol
locks Lan Standard on 2026-11-06, and the provider deadline tests use one
coordinated clock inside the original 0.8–2.5s window. Full suite on the
Phase7FixRound1 binary: 1142 passed, 22 opt-in skipped, 0 failed, total 1164.
Node 14/14. App build 0 warnings, 0 errors. The Docker Linux engine pipe is still
missing and port 3307 is closed, so MariaDB dry-run and browser rehearsal did not
run. No paid call. Repair scope is 163 entries, fingerprint
`41386db1939220cc3d9f7e3f31bf5e00ac48425a81a6896572a92101c7bd9a35`.
The nonpaid repair is AWAITING_REVIEW. Live acceptance remains BLOCKED. This
record is not APPROVED and grants no spending, Git, CI or deployment. See
[fix round 1 handoff](../../../feature-audits/hybrid-rag/phase-7/fix-round-1-handoff.md).

The owner subsequently authorized direct local repairs and a running local test
web on 2026-11-08. The 163-entry input was verified before edits and archived at
phase-7/scope-manifest-fix-round-1.json. Local completion round2 repairs the default
run deadline/publication/invalid-receipt gaps, connects the guard to actual
production-client fake HTTP sends/retries, repairs complete Vietnamese lookup
framing and shipped-ML lookup arbitration, and fixes login CSS integrity. Owner
reopened Docker; actual read-only MariaDB dry-run matches 471 documents / 487
chunks / 113887 bytes, pending model false. Browser full/embed/source/history and
representative local questions now have runtime evidence. Final suite1161 passed /
22 opt-in skipped / 0 failed, focused109/109, Node14/14; standard Release launcher
build succeeds. Web stays running on localhost:5199 using existing User Secrets;
the browser is on a fresh private Standard manual account with no chat sent.
The new172-entry fingerprint is
`76a51a99633d0ea1574de7d0e63b7f7564e3a6b479df6c36a61ff16478a36806`.
Local completion is AWAITING_REVIEW. B01 local MariaDB/browser preflight has
evidence; dense/live acceptance remains BLOCKED for lack of a concrete paid grant
and required benchmark measurements. This is not APPROVED and grants no Git, CI
or deployment authority. See
[local completion handoff](../../../feature-audits/hybrid-rag/phase-7/local-completion-handoff.md).

| Phase | State | Handoff | Latest review | Approved scope fingerprint | Open findings |
| --- | --- | --- | --- | --- | --- |
| 0 | APPROVED | [Fix round 2](../../../feature-audits/hybrid-rag/phase-0/fix-round-2-handoff.md) | [Round 3 — approved](../../../feature-audits/hybrid-rag/phase-0/review-round-3.md) | 1c33f6700cde76fc80558f823e2fb5699cbeae8c6fba89aeaab29bd25a2268cb | None — all verified closed |
| 1 | APPROVED | [Codex fix round 6](../../../feature-audits/hybrid-rag/phase-1/fix-round-6-handoff.md) | [Round 6 — approved](../../../feature-audits/hybrid-rag/phase-1/review-round-6.md) | 764d370dc1b2482cd1b78954a8959dfd4706b16b5156ed00659a9301b729e7a9 | None — F01/F02/F03/F04/HR-P1-R3-F05 verified closed |
| 2 | APPROVED | [Codex closeout](../../../feature-audits/hybrid-rag/phase-2/review-round-3.md) | [Round 3 — approved](../../../feature-audits/hybrid-rag/phase-2/review-round-3.md) | 62a864787561df4164da82030b24b590564fdce804d097a49b66c4ddc5a4dbdf | None — F01/F02/F03/F04 verified closed |
| 3 | APPROVED | [Codex closeout](../../../feature-audits/hybrid-rag/phase-3/review-round-2.md) | [Round 2 — approved](../../../feature-audits/hybrid-rag/phase-3/review-round-2.md) | 66bae722fbbb0b4d5d3724dcd363b1c58eda91202880e93dd00746c147b89fcb | None — F01/F02/F03/F04/F05 verified closed |
| 4 | APPROVED | [Codex closeout](../../../feature-audits/hybrid-rag/phase-4/review-round-2.md) | [Round 2 — approved](../../../feature-audits/hybrid-rag/phase-4/review-round-2.md) | 1073d5074db16b161e04364e06178644a4ccd5f90b65c8337605e41ef381af61 | None — F01–F05 and HR-P4-R2-F06 verified closed |
| 5 | APPROVED | [Fix round 2](../../../feature-audits/hybrid-rag/phase-5/fix-round-2-handoff.md) | [Round 3 — approved](../../../feature-audits/hybrid-rag/phase-5/review-round-3.md) | 33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4 | None — F01–F05 and HR-P5-R2-F06 verified closed |
| 6 | APPROVED | [Local completion](../../../feature-audits/hybrid-rag/phase-6/local-completion-handoff.md) | [Round1 — approved](../../../feature-audits/hybrid-rag/phase-6/review-round-1.md) | 68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c | None — browser blocker and HR-P6-C-F01/F02/F03 verified closed |
| 7 | AWAITING_REVIEW | [Local completion round2](../../../feature-audits/hybrid-rag/phase-7/local-completion-handoff.md) | [Round1 — changes requested](../../../feature-audits/hybrid-rag/phase-7/review-round-1.md) | None | Local repairs/preflight submitted at172-entry identity. B01 MariaDB/browser now has evidence. Dense/live benchmark remains BLOCKED; no paid grant. |

Read [review-gates.md](review-gates.md) before changing state. Cursor may update
only its active phase to IN_PROGRESS, AWAITING_REVIEW or BLOCKED with evidence.
The owner-requested Codex review records CHANGES_REQUESTED or APPROVED and alone
makes the next phase READY. A future phase becoming READY still needs owner launch.

Record future narrow protected-file/live-API grants here with date and exact scope
only after the owner actually grants them. None are added by these prepared prompts.
Never store credential values. No commit, push, PR, merge or deployment grant exists.
