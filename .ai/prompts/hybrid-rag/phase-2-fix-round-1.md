# Cursor Auto — Hybrid RAG Phase 2 repair round 1

## Outcome and source context

Repair HR-P2-R1-F01–F04 only. Preserve the approved Phase 1 boundary while making
admin Dry-run read-only, lexical evidence relevant, unsupported questions non-full
and chunk/evidence lengths bounded. This is lexical retrieval, not Hybrid RAG yet.

Read these current files before execution:

- AGENTS.md, `.ai/prompts/README.md` and the applicable execution-template contract.
- `docs/feature-audits/hybrid-rag/phase-2/review-round-1.md`.
- `docs/superpowers/plans/hybrid-rag/phase-2-fix-round-1.md` (ordered repair and checks).
- `docs/superpowers/plans/hybrid-rag/review-gates.md` and `review-state.md`.
- Phase 2 `handoff.md`, `scope-manifest.json`, and Phase 1 review-round-6 approval.

Verified review identity: branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 60-entry fingerprint
`ef1418c674f5e86aeb675cf32ae0b695149293101c9707551c6f4b49c4f4442a`.
Recompute final-byte hashes and investigate drift before editing. Preserve dirty work.
Use CodeGraph first if available; bounded direct reads cover missing/stale gaps.
Read symptom-relevant bug memory. Work as one agent with tct-superpowers,
engineering, security, admin and UI-review procedures; no separate AI reviewer.

## Mode, authority and non-goals

Mode becomes IMPLEMENT_LOCAL only when the owner launches this repair. Require
Phase 2 CHANGES_REQUESTED and Phase 3–7 LOCKED. The review itself did not authorize
application fixes. Carry existing narrow chat-RAG DI authority if needed; do not
alter the global IAiProviderClient/OpenAiProviderClient contract.

Scope: the new admin rebuild form/action, corpus chunking/evidence selection,
lexical answer admission and meaningful focused regression tests. No embedding,
dense branch, free model generation, Phase 4 answer pipeline or later-phase work.
No appsettings/project/dependency/schema/shared-content changes are needed.
No paid API, secret write, data reset, commit/push/PR/CI/merge/deployment authority
is granted. If a real new dependency requires additional authority, report it and
continue independent authorized work. Never replace source or fixtures to force pass.

## Ordered execution

1. Verify entry/scope and mark only Phase 2 IN_PROGRESS. Reproduce each finding
   against current code with a meaningful failing regression before its fix.
   Existing ignored review probes under `.tmp/hybrid-rag-review/phase-2/round-1/`
   are optional supporting evidence; source/tests and the report are authoritative.
2. Fix F01 in `Areas/Admin/Views/AiManagement/Index.cshtml`, with minimal action
   hardening in `AiManagementController` if needed. Disabling the clicked button
   currently removes `dryRun=true` from native form data and defaults the action
   to build. Preserve the operation explicitly; missing/invalid operation must
   not choose mutation. Retain duplicate-submit protection, Admin and antiforgery.
   Prove Dry-run cannot create/promote snapshots and explicit Build still works.
3. Fix F04 before settling evidence identity. `CorpusChunker.Pack` currently keeps
   a 29,700-character, 7,425-approx-token single sentence in one chunk although
   MaxChunkTokens is 600. Enforce the cap for real persisted/reloaded chunks and
   evidence using deterministic exact source spans or an explicit accounted
   exclusion for unrepresentable content. Do not silently truncate, forge sentence
   IDs/hashes or raise the cap. Version any incompatible chunk semantics and
   document a rebuild. Keep display text, paragraph/heading structure and overlap.
4. Fix F02 in `Corpus/RetrievalCorpusService` ToHit/RetrieveAsync. A body containing
   `Welcome to the lesson. The tidal harbor marker is visible at dawn.` must not
   return only s1 for `zzzz tidal harbor marker qqqq`. Select the relevant exact
   sentence/span from the matched chunk, preserve provenance and recheck live
   publication/access/hash before returning it. Test later matches and chunks.
5. Fix F03 in `Rag/AiChatAnswerService.TryLexicalAnswerAsync` and minimal supporting
   contracts. A positive lexical score is a candidate, not proof of full support.
   With only `The tidal harbor marker is visible at dawn.`, questions about why
   it glows at midnight or how many people live nearby must not return full or
   unrelated Add-card/Flashcard text as an answer. Keep conservative deterministic
   extract/reference behavior and safe clarification/insufficient/out-of-scope
   outcomes. Do not just relabel a wrong answer or blacklist those exact phrases.
   Use all production retrievers plus real lexical service in boundary regressions.
6. Preserve positive eligible lesson/body retrieval, live guide help, authorized
   exact card targets/fields, current-user facts and all Phase 1 F01–F05 regressions.
   Keep missing/corrupt/stale-index states, candidate/result limits, immutable
   snapshot promotion, cancellation/failure preservation and foreign-path refusal.
7. Execute the exact focused commands/checklist in the repair plan with fresh
   `Phase2FixRound1` binaries. Add new test classes to the filter if separate.
   Record red/green regressions, discovered counts/failures/skips and build status.
   Run tooling validation/Python checks for guidance/bug-memory changes.
8. Exercise the actual rendered admin page in a browser, including Dry-run with
   missing/existing index, explicit Build, duplicate submission, authorization
   and antiforgery failures. Record selected operation and before/after manifest/
   snapshot state. Use the canonical local launcher if starting the web app,
   preserve existing workloads and never trigger paid AI requests. If blocked,
   report the exact missing evidence instead of substituting an isolated DOM pass.
9. Re-run the development lexical baseline with exact versions/denominator,
   observed misses and resource measurements. Do not tune held-out cases; leave
   held-out scored at zero and preserve the frozen fixture hash. Separate any
   MariaDB/provider evidence from SQLite; unchanged schema needs no empty migration.
10. Review the full tracked/untracked diff. Append bug memory for verified fixes.
    Write `docs/feature-audits/hybrid-rag/phase-2/fix-round-1-handoff.md` in Vietnamese
    and refresh cumulative `scope-manifest.json`, including this prompt/repair
    plan and changed execution guidance. Exclude result/state/manifest files from
    their own digest. Report each ID as addressed/pending with actual evidence.

## Acceptance and stop

All four findings need observable red/green evidence; the plan defines edge cases
and regression scope. An isolated browser fixture proves serialization only and
does not certify the logged-in admin route. A positive source-key ranking alone
does not prove the selected evidence or answer supports the question.

Finish only Phase 2 AWAITING_REVIEW, leave Phase 3–7 LOCKED and stop. Do not approve
your own fixes or start Phase 3. Report outcome, files, commands/results, browser
evidence and remaining risks in Vietnamese; distinguish local from CI/hosting.

## Short owner launch message

Execute `.ai/prompts/hybrid-rag/phase-2-fix-round-1.md` in Cursor Auto. Repair only
HR-P2-R1-F01–F04, return AWAITING_REVIEW, and keep Phases 3–7 LOCKED.
