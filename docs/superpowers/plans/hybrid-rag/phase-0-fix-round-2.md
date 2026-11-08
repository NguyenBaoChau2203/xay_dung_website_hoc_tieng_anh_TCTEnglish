# Phase 0 repair plan — round 2, scenario consistency only

Prepared 2026-11-06. PLAN_ONLY now; IMPLEMENT_LOCAL activates on the owner's
explicit Cursor repair launch. One agent. Phase 0 CHANGES_REQUESTED; later phases
remain LOCKED. Do not reopen the four resolved findings without a real regression.

## Evidence and bounded scope

Review: [round 2](../../../feature-audits/hybrid-rag/phase-0/review-round-2.md).
Finding **HR-P0-R2-F01, P2**, completes the remaining R1-F02 consistency work.
Reviewed HEAD 6af768bd32d16cc6d012d484ee82df4cab805c69 on feature/hybrid-rag-chatbot.
Returned fingerprint b4205acda7cc68a5e0ec94a1c68410b6d7ee3181381bdebfec09f69d8ea65ce2.

Allowed implementation: `TCTEnglish.Tests/HybridRagFixtureValidator.cs`,
`TCTEnglish.Tests/HybridRagEvaluationBaselineTests.cs` and narrowly necessary test
helpers; updated handoff/baseline/manifest and verified bug memory. The existing
120-case revision 2 fixture is valid and should remain unchanged, with canonical
hash 1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f.

No runtime, Program.cs, appsettings, project-file, schema, real content, credentials,
paid API or remote Git/CI/release change. Read the existing protocol and preserve
dirty work. This is a local test-validator repair, not a new RAG capability.

## Ordered checklist

- [ ] Verify the actual checkout and source via CodeGraph. Reproduce each of the
      four inconsistent clones from the real round-2 review against the public
      validator, independently of freeze hashing. Record failing-before evidence.
- [ ] Add regression cases proving that an unexhausted quota cannot support an
      expected quota_exceeded outcome, a provider-unavailable case cannot omit the
      fault, a cancelled case cannot omit the cancel action, and a tombstone cannot
      target an undeclared source for its case. Keep the original fixture accepted.
- [ ] Pass the necessary declared case context/outcome/source IDs into validation,
      or add an equivalent generic cross-field validation step. Do not special-case
      known case IDs, question strings or risk tags to make the tests pass.
- [ ] Check supplied setup against expected results; do not infer, fill or mutate
      environment from an expected outcome. Explicitly keep these directions separate.
- [ ] For quota_exceeded require at least one applicable documented limit actually
      reached by the supplied usage. Match actor/limits and preserve the current
      valid 15/15 scenario. A negative number is not the only invalid quota setup.
- [ ] For outcomes requiring an operational failure/action, require compatible
      explicit scenario evidence. Do not force every provider-fault case to produce
      an error if a future explicitly modeled fallback can legitimately answer;
      validate the direction needed by the current outcome contract.
- [ ] Require a scenario target source to belong to the case's declared source
      scope, in addition to global existence/hash/state checks. Preserve tombstone
      source-not-required constraints and existing access/stale scenarios.
- [ ] Run focused tests and related baseline regressions, review the diff and
      confirm the canonical fixture and all resolved behaviors remain unchanged.
- [ ] Update only actual new measurements and verified bug memory. Create
      `docs/feature-audits/hybrid-rag/phase-0/fix-round-2-handoff.md` and refresh
      cumulative scope evidence including the controlling round-2 repair guidance.

## Acceptance matrix

| Input | Required result |
| --- | --- |
| Unchanged frozen revision 2 | No validator errors; same canonical fixture hash |
| Quota 0/15 and 0/60000 while expecting exhaustion | Case-specific validation error |
| Expected provider_unavailable with scenario none | Case-specific missing/incompatible fault error |
| Expected cancelled with scenario none | Case-specific missing/incompatible action error |
| Tombstone for another source not declared by the case | Case-specific source-scope error |
| Prior malformed clones, LF/CRLF and mutations | Existing regressions remain green |

Do not add a second freeze constant, relax the assertions or edit expected outcomes
to disguise invalid setup. Mechanical consistency checks still do not claim general
semantic/model quality.

## Verification and stop

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixRound2 --filter 'FullyQualifiedName~HybridRagEvaluationBaselineTests' --logger 'console;verbosity=detailed'
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Phase0FixRound2 --no-build --filter 'FullyQualifiedName~WebsiteGuideRetrieverTests|FullyQualifiedName~DeterministicIntentClassifierTests|FullyQualifiedName~StudyRecommendationRetrieverTests|FullyQualifiedName~UserVocabularyRetrieverTests|FullyQualifiedName~LearningProgressRetrieverTests|FullyQualifiedName~InternalKnowledgeProviderTests|FullyQualifiedName~TemplateAnswerComposerTests|FullyQualifiedName~AiBaselineRegressionTests'
uv run --no-project python scripts/agent_tooling.py
uv run --no-project python -m unittest discover -s scripts/tests -v
git diff --check
```

Verify actual test discovery/counts and new-file links/whitespace. Preserve running
output locks using an isolated configuration if necessary. No paid API/browser/DB
check is needed for this scope. Keep historical reviews/handoffs intact.

Report in Vietnamese with each acceptance item, real commands/results and remaining
limits. Set Phase 0 AWAITING_REVIEW, leave Phase 1–7 LOCKED and stop for Codex round 3.
