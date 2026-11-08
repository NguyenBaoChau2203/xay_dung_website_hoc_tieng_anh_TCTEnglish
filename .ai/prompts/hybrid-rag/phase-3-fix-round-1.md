# Execute Hybrid RAG Phase 3 repair — round 1

## Outcome and source context

Repair the four verified P1 findings HR-P3-R1-F01–F04, complete the outstanding
vector-form keyboard evidence, and return Phase 3 for Codex review. Read:

- `AGENTS.md` and `.ai/prompts/README.md`.
- `docs/feature-audits/hybrid-rag/phase-3/review-round-1.md`.
- `docs/superpowers/plans/hybrid-rag/phase-3-fix-round-1.md` (the ordered repair plan).
- The phase review gates/state and only the source/contracts needed for that step.

Verified entry: branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, Phase 3 CHANGES_REQUESTED, Phases 4–7
LOCKED. The returned cumulative 81-entry scope fingerprint is
`dbee216f70cb1a9b2475e3e5b9a0e4478b3996165247853ad8b19083f971339b`.
Recompute it before editing and classify any drift; preserve existing dirty work.

## Mode and authority

On the owner's repair launch, mode is IMPLEMENT_LOCAL for these findings only.
Work as one agent using the selected Cursor model; no AI delegates, alternate
model processes or separate reviewer. Apply tct-superpowers/tct-engineering and
relevant security/admin/UI procedures. CodeGraph first for indexed source.

Primary scope: embedding client/options/interface, vector index service/store,
corpus recheck helpers and focused AiRag tests. A small shared admission/budget
helper is allowed when needed. Carry the existing narrow Program.cs RAG DI grant
for coordinator registration; preserve scoped DbContext/service lifetime and
global IAiProviderClient/text consumer behavior. No appsettings/.csproj/model
setting/secret change, schema/content edit, DB reset, paid API, remote Git/CI,
merge or deployment is granted by this prompt. Complete routine authorized work
without repeated approval requests. State any concrete new dependency separately.

## Ordered implementation

1. Follow the plan's reproduction checklist. Add portable meaningful failing
   regressions before implementation for every ID, using recording HTTP and
   synthetic SQLite fixtures. The ignored review probe is optional local evidence,
   not a substitute for repository regressions.
2. F01/F02: implement one per-job budget ledger across every real HTTP attempt,
   retry and batch. Enforce request allowance before sending; enforce a safe
   pre-send token bound independently of the labelled chars/4 estimate. Retain
   every known usage/attempt on success, failure, cancellation and overage. Do not
   turn a known charge into zero or an unknown outcome into an unused request.
3. F03: share admission by the server-owned index root across separate request
   scopes. Cover the whole rebuild, bounded wait/cancel/release and fresh cache
   planning. Do not make the EF-dependent service singleton or rely only on the
   commit lock/frontend guard. Test independent scopes behind a provider barrier.
4. F04: recheck authoritative common-corpus eligibility/publication/hash and
   snapshot before every outgoing batch/retry and immediately before promotion.
   Stop on drift before sending stale/revoked text. Preserve the last valid index
   and known cost; do not rebuild/overwrite sources automatically.
5. Run the plan's focused and compatibility commands with a fresh configuration,
   then the broader app suite once. Preserve exact cosine/RRF/cache/error and
   no-paid ordinary-chat contracts. Keep auth/CSRF/budget-rejection checks at zero
   provider calls. Report failures/skips honestly; no held-out tuning or fake
   semantic-quality benchmark.
6. Complete real admin Dry-run-vector Tab/Shift+Tab/Enter, duplicate-submit,
   redirect and responsive checks through scripts/start-local.ps1 -NoBrowser.
   Preserve existing listeners, use a free port/configuration, confirm readiness
   and speech metadata only. Test embed with fakes; do not press live Tạo vector
   or make any paid embedding/text/speech request without a separate bounded grant.
7. Review the complete diff. Append accurate bug memory for verified fixes,
   refresh the cumulative scope manifest including executed plan/prompt, and write
   docs/feature-audits/hybrid-rag/phase-3/fix-round-1-handoff.md in Vietnamese with
   per-finding red/green evidence, files/checks, measured usage/limits and blockers.

## Acceptance and stop

One-request budget must never send a second HTTP attempt; usage already known
must survive a failed/over-budget build. Two scopes on one index cannot overlap
paid work. Unpublish/hash/snapshot changes between batches/retries must stop the
next stale payload and prevent stale promotion. Existing valid index/readers,
source access checks, non-chat clients and still-lexical normal chat remain intact.

Finish only Phase 3 as AWAITING_REVIEW when the repair/evidence is ready, or
BLOCKED with the exact unmet prerequisite. Keep Phases 4–7 LOCKED and stop.
Do not mark APPROVED or claim the repairs have already been reviewed by Codex.
