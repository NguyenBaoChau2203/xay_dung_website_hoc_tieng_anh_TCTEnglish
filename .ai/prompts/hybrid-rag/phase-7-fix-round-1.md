# Phase 7 repair round 1 — fix local tests and budget protocol, no paid calls

## Outcome and context

Execute `docs/superpowers/plans/hybrid-rag/phase-7-fix-round-1.md` after owner
launch. Read current AGENTS.md, the Phase7 review-round-1.md and submitted handoff.
Resolve F01 retry/aggregate budgeting, F02 executable evaluation context and F03
scheduler-dependent provider tests. Complete available nonpaid preflight/rehearsal
and return a corrected concrete API envelope for review before any spending.

Input scope155:
`4948d507087a980d32de797f8d6d29a9f309d707ddbec106f2bd37b3497e4817`.
Approved Phase6 scope153:
`68116f3d7a5839865c381ff016399a4688f74f741e6dc9a3f1b454b9754f8e8c`.
Verify actual branch/HEAD and raw hashes. Expected branch
`feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`. Preserve the submitted input manifest
and existing work; CodeGraph first for indexed source. One agent, no delegation.

## Mode and authority

IMPLEMENT_LOCAL for this owner's launched nonpaid repair only. No text, embedding
or speech API calls, even smoke calls. No commit/push/PR/CI/merge/deploy. No changes
to Program.cs, appsettings, csproj, production/staging gates, saved keys or owner
data. Use disposable synthetic fixtures for actor/role/date tests and fake HTTP
for all transport/budget regressions. Do not self-approve Phase7.

## Ordered execution

1. Verify input identity and inspect submitted failures. Docker Engine was
   unavailable at review: establish availability and preserve workloads before
   the required launcher/read-only DB dry-run. Report an unavailable prerequisite
   honestly and continue independent local repairs; never count file-only status
   as a live MariaDB corpus match.
2. Fix provider tests through explicit stage coordination/controlled time. Prove
   cancellation during backoff prevents the next request and one original deadline
   covers first attempt, retry/backoff and response-body wait. Do not loosen time
   windows, remove call-count checks, skip tests or disable parallelization globally.
   Keep production behavior unchanged; only a minimal compatible clock/delay seam
   is allowed if required, without protected wiring/dependency changes.
3. Define synthetic versus real-corpus evaluation, actor/role/business date, source
   mapping and fixed denominators. Preserve revision2/120cases/80-40 split and all
   quality gates.12case smoke is not final acceptance; missing dense index is
   lexical_degraded and may make zero query embedding calls. Do not force paid
   stages or use Admin as the frozen Standard actor. Keep quotas and owner DB safe.
4. Recompute the envelope using actual dry-run and the existing per-job ledger.
   Nominal113887bytes/3jobs/10POSTs is not a retry guarantee: first batch36276 plus
   unknown usage leaves only3724in job40000. Count every attempt and unknown hold,
   choose a feasible retry/partial policy, and verify one run-scoped POST/token/time
   guard across all stages/jobs/turns before each HTTP send using fake transports.
   Include evaluation query batches and required online-turn coverage. Neither
  169087 nor282974 in the review grants spending authority.
5. Run the nonpaid cold-start/full/embed/network/index/provider-rejection rehearsal
   when environment permits, with fake child-process AI settings, readiness and
   speech metadata. Do not rewrite secrets or downgrade the owner's admin.
6. Run meaningful budget/grounding/access/cancel/provider tests, Node chat guards,
   build and full app suite after focused checks pass. Record failures/skips;
   repeated isolated passes cannot replace the failed suite. Review the diff.
7. Update bug evidence, evaluation/envelope/rehearsal reports, repair handoff and
   cumulative manifest. Include this new guidance and actual source/test changes;
   use the preserved reviewed155-entry snapshot as predecessor.

## Acceptance and Vietnamese handoff

F01 must have executable retry/unknown/aggregate-stop evidence; F02 must identify
the actual data/actor/date/source context and every denominator; F03 must pass in
the relevant/full suite without weakened assertions. State any Docker/browser
blocker separately and distinguish fake from live evidence. No paid calls.

Return the nonpaid repair for review and present the corrected envelope. Keep
required live acceptance BLOCKED until the owner grants the exact request/token/
duration scope and real checks are run. Do not ask the owner to approve the old
A/B numbers before correcting them, silently enlarge a grant or claim Phase7 done.
