# Phase 0 — discovery, contracts and evaluation baseline

Prepared 2026-11-06 for Cursor Auto. Initial state READY; owner launch is required.
Mode on launch: IMPLEMENT_LOCAL for documentation, synthetic fixtures and a baseline
test harness only. No application behavior, model, DI, schema or real content edits.
Read [review-gates](review-gates.md), [state](review-state.md) and AGENTS.md.

## Outcome and entry evidence

Make later phases executable from current source, with a coverage/access matrix,
contracts and reproducible baseline. Source evidence from 05/11 is a starting point,
not a substitute for checking the actual branch/source on execution day.

Inspect `AiChatService`, `IAiProviderClient`, `OpenAiProviderClient`, internal
retrievers/composer, current controller/hub callers and non-chat provider consumers
through CodeGraph. Inspect only needed gaps and current bug memory. Verify framework/
provider versions from project files without editing them. Inventory 41 existing
guides; do not assume legacy `/Home/...` routes are broken or still correct.

## Deliverables (new paths are proposed until created)

- `docs/feature-audits/hybrid-rag/phase-0/coverage-matrix.md`: domain, source key/type,
  feature/service/route, public/owner/class/role access, freshness, reviewed claims,
  answerable examples, missing source and browser verification status.
- `docs/feature-audits/hybrid-rag/phase-0/contracts.md`: accepted source/evidence,
  outcome, retrieval, answer, usage and lifecycle boundaries for the next phases.
- `TCTEnglish.Tests/Fixtures/AI/hybrid-rag-evaluation.json`: 120 synthetic cases.
- A focused fixture validator/baseline harness in `TCTEnglish.Tests/`, using existing
  testing patterns and no new package/project-file changes.
- Real baseline report, handoff and scope manifest under the phase audit directory.

## Ordered checklist

- [ ] Record branch/HEAD/status and initial dirty files. Preserve all current plans.
- [ ] Trace HTTP and SignalR chat through provider/context/quota/persistence. Record
      which internal retrieval path is active, with current source evidence.
- [ ] Map feature access rules for guides, cards/sets/folders, class membership,
      Speaking, Reading/Writing/Listening, goals, progress, notifications and billing.
      Record membership/role revocation, Premium locks and the correct business date.
- [ ] Validate guide claims and routes against source. Where rendered inspection is
      necessary, use the local launcher and synthetic authorized accounts; do not
      rewrite User Secrets, stop unknown processes or call paid APIs. Mark unrun
      rendered checks as gaps, not verified source correctness.
- [ ] Specify evidence keys, source versions/hash, sentence IDs, authorized route,
      visibility and capture time. Define typed answer outcomes and mixed-question
      policy. Keep history distinct from evidence. Proposed names remain tentative
      until this phase's review approves the contracts.
- [ ] Decide an explicit local index rebuild entrypoint for later implementation.
      Prefer a narrowly secured maintenance action in existing Admin AI management,
      with dry-run/status, antiforgery, bounded operation and no new authoring screen.
      Discover current routes before proposing one. No paid startup action, test
      runner as routine rebuild tool, new package or unapproved Program host change.
- [ ] Document model/index/corpus versioning, deletion/unpublish invalidation and
      the scope of private lexical/live retrieval versus public dense retrieval.
- [ ] Record quota stage risks: current ReserveQuestionAsync recovers Pending ledgers
      with ActualTokens>0. Marking embedding spend pending while the same turn is
      still generating could let another conversation settle it early. Require a
      stage-aware design in Phase 4; do not merely reuse recovery between live stages.
- [ ] Create 120 cases: 25 navigation, 25 lesson/card, 20 personal facts, 15 outside
      scope, 15 injection/access, 10 ambiguous/mixed, 10 stale/error/cancel.
      Suggested fixed split: development counts 17/17/13/10/10/6/7 (80 total),
      held-out 8/8/7/5/5/4/3 (40 total). Freeze split/IDs before tuning.
- [ ] Each case has category/split, synthetic actor/context/fixture source IDs,
      question, expected outcome, required facts/source IDs, forbidden disclosures
      and risk tags. Include accents/no accents/English, safe password-change help,
      >500-card lookup, ambiguous follow-up, revoked access and malicious source text.
      Separate answerable facts from expected exact wording. Never copy real users.
- [ ] Validate case counts, unique IDs, split, referenced synthetic sources and
      deterministic fixture setup. Run only nonpaid current classifier/retriever
      baseline observations; mark final RAG metrics not yet applicable.
- [ ] Write gaps and proposed decisions; no runtime fix in this phase. Review the
      changed documents/fixtures for secrets, invented results and scope expansion.

## Checks and acceptance

Use existing xUnit patterns; verify any new test filter actually discovers tests.
Run focused fixture/baseline tests and the relevant existing retriever/classifier
tests. Build only as needed by those tests, preserving running output locks with
an isolated configuration. For guidance edits run the repository tooling validator
and Python suite. Check UTF-8, local links and `git diff --check`, including new files.

Required: 120 valid cases; frozen 80/40 split; all source domains have access/freshness
entries and explicit coverage gaps; current chat/provider contracts and rebuild
entrypoint decision are reviewable. No fake model outputs, live scores or assertions
that synthetic vectors demonstrate semantic quality. Unavailable required discovery
is BLOCKED; do not create a false complete baseline.

## Handoff and next-phase boundary

Use [handoff-template](handoff-template.md); include exact commands/counts, coverage,
proposed contracts, unknowns and manifests covering untracked fixtures. Mark Phase 0
AWAITING_REVIEW. Phase 1 stays LOCKED until Codex approves. The reviewer may require
contract or fixture corrections before any application edit begins.
