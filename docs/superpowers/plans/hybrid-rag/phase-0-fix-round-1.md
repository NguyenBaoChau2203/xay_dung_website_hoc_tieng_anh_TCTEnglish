# Phase 0 repair plan — review round 1

Prepared 2026-11-06. Current mode PLAN_ONLY; the owner's explicit Cursor repair
launch activates IMPLEMENT_LOCAL for the scope below. One agent throughout.
Phase 0 is CHANGES_REQUESTED; Phase 1–7 remain LOCKED.

## Identity, evidence and authority

- Review: [round 1](../../../feature-audits/hybrid-rag/phase-0/review-round-1.md).
- Branch feature/hybrid-rag-chatbot; reviewed HEAD
  6af768bd32d16cc6d012d484ee82df4cab805c69.
- Returned scope fingerprint:
  f671a9f1a49282d5cab76474efe132e557dcaa07d482ba35c8decc6bb81b6cdd.
- All five returned file hashes matched; 131 existing focused tests passed. The
  defects are not fixed and passing structural tests do not approve the oracle.
- Follow AGENTS.md, prompt authoring rules and the phase review protocol. Repair
  only synthetic fixture/validator and Phase 0 discovery/contracts/report artifacts.
  No runtime, Program.cs, appsettings, project-file, schema, real-content or paid API
  change; no Git remote/CI/release action. Preserve prior uncommitted work.

## Findings and dependency order

| ID | Priority | Repair result |
| --- | --- | --- |
| HR-P0-R1-F01 | P1 | Expected facts actually answer every labeled question |
| HR-P0-R1-F02 | P1 | Operational scenarios have explicit, reproducible typed setup/actions |
| HR-P0-R1-F03 | P2 | Freeze validation survives LF/CRLF while detecting real edits |
| HR-P0-R1-F04 | P2 | Quota counter documentation matches source |
| HR-P0-R1-F05 | P2 | CodeGraph discovery provenance is accurate |

F01/F02 establish the corrected fixture before its new freeze hash/baseline in F03.
F04/F05 are independent document corrections. Final evidence/manifest follows all
repairs. Do not combine unrelated application changes with these documentation fixes.

## A. Reproduce and repair oracle facts — F01

- [ ] Read the active review/source with CodeGraph first and preserve the reviewed
      fixture revision/hash in the historical record before making corrections.
- [ ] Reproduce Quiz/Matching's unrelated requiredFacts at HR-NAV-D14/D15. Audit
      all 120 cases against each question's requested facets, source text, actor's
      authority and expected full/partial/refused result; document actual corrections.
- [ ] Supply appropriate reviewed synthetic Quiz/Matching sources/facts or mark
      insufficient evidence when the fixture genuinely lacks the answer. Do not
      call the folder-placement sentence a full answer to either existing question.
- [ ] For arithmetic personal-fact questions, specify typed expected values/units
      and trusted operands/derivation where needed (for example 10 target minus 4
      reviewed = 6 remaining). Distinguish derived facts from verbatim source spans;
      do not force the text-containment rule to reject legitimate server arithmetic.
- [ ] Preserve 120 cases, stable case IDs and the 80/40 split/category distribution.
      Record fixture revision lineage and reasons; changing bad labels is not a
      measured quality improvement. Do not tune a classifier/model on held-out cases.
- [ ] Add `docs/feature-audits/hybrid-rag/phase-0/oracle-validation.md` with a concise
      case/facet/source/expected result audit, listing gaps and corrected cases.

Acceptance: no answered/full case relies on an unrelated source fact; supported
partial answers identify what remains unanswered; derived expected results are
explicit and mathematically consistent. Human semantic review remains distinct
from mechanical validation; do not claim an LLM or string containment proves it.

## B. Make scenario setup executable and independently valid — F02

- [ ] Add typed scenario/action metadata for quota exhaustion, provider timeout/
      network/503, cancel, source deletion/unpublish, stale index/content hash and
      any simulated role/membership transition. Use deterministic synthetic values.
- [ ] Separate a chat message from Stop/cancel operations. Model source tombstones,
      previous/current source/index versions and actual quota usage as relevant.
      Do not infer environment from natural-language text, expectedOutcome or case ID.
- [ ] Add validators for actor/source references, scenario type/required fields,
      incompatible state, positive quota values/limits, fault/action triggers and
      expected-source/fact consistency. Keep this in fixtures/tests; no actual
      application provider or Phase 4 quota state machine implementation.
- [ ] Factor validation enough to test deliberately invalid in-memory clones without
      rewriting the frozen JSON or disabling its freeze check. Missing setup,
      dangling source/actor, invalid operation or inconsistent stale/delete state
      must fail independently of the fixture's known hash.

Acceptance: D04/D05/D06/H03 and all analogous cases describe real deterministic
world state/actions; valid setup is mechanically accepted and malformed clones
rejected. Case labels do not drive the environment they purport to verify.

## C. Repair freeze portability — F03

- [ ] Read the relevant 01/05 line-ending bug memory. Define a canonical UTF-8/LF
      fixture fingerprint and use it consistently for freeze validation/documents.
      Retain raw-byte hashing only where identifying an exact local scope snapshot.
- [ ] Add meaningful LF/CRLF equivalence and real-content-mutation regressions.
      Do not fix only by updating the raw CRLF constant to the new file's bytes.
- [ ] If pinning `.gitattributes`, scope it to the fixture; this is an optional
      text-normalization edit, not authority to alter unrelated tooling/config.
- [ ] Refresh the freeze value after A/B are complete, then rerun validator/baseline.
      Preserve historical measurements as historical rather than rewriting them as
      observations of the new fixture.

Acceptance: identical JSON text under LF/CRLF passes the same freeze definition;
a changed fact, ID, expected outcome or split is rejected until a deliberate new
revision is recorded. Record both canonical fixture and raw scope hash semantics.

## D. Correct source-based documentation — F04 and F05

- [ ] F04: inspect reserve/finalize counter code and document Pending→Settled versus
      Pending→Released accurately. Show PendingQuestions-- and SettledQuestions++
      preserve the admission question sum on settlement. Retain the genuine
      generation-token undercount race; do not claim a returned Standard question.
- [ ] F05: inspect `.codegraph` presence and actual tool availability separately.
      Use available CodeGraph/pinned CLI for focused source discovery, or state the
      exact unavailable capability/fallback. Correct the false directory statement
      and reconcile handoff/source provenance without inventing past tool calls.
- [ ] Do not init, rebuild or sync the index, modify production code or claim fixes
      to the application's underlying quota/access issues from this Phase 0 repair.

## Verification and handoff

Run new meaningful validator/probe regressions and the existing focused baseline
suite. Verify test discovery includes the new assertions, not a zero-test filter.

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixReview --filter 'FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=detailed'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixReview --no-build --filter 'FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

Use an isolated output/configuration if this one is locked. Check UTF-8/local links/
whitespace for new untracked files separately. No app/browser/live API/DB migration
is needed for these repairs. Update bug memory only for fixes actually verified.

Create `docs/feature-audits/hybrid-rag/phase-0/fix-round-1-handoff.md`, revised
baseline/oracle evidence and refreshed cumulative scope manifest covering synthetic
code/fixtures/docs and controlling repair guidance. Preserve review-round-1 as the
historical snapshot; do not mark its findings resolved on the reviewer's behalf.
Record each ID addressed/pending with actual checks, set only Phase 0 AWAITING_REVIEW
and stop. Codex must re-review before Phase 1 can be READY.
