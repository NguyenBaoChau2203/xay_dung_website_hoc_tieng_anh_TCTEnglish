# Execution Prompt: Hybrid RAG Phase 1 repair round 4

## Outcome and evidence

Repair the remaining HR-P1-R1-F01 and HR-P1-R1-F04 from Codex review round 4.
Preserve closed F02/F03/HR-P1-R3-F05 and stable server card identity.

Read AGENTS.md, `.ai/prompts/README.md`, current review-state/gates, accepted
Phase 0 contracts, relevant bug memory and:

- `docs/feature-audits/hybrid-rag/phase-1/review-round-4.md`.
- `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-4.md` (active checklist).

Reviewed branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 42-entry fingerprint
`ba644991f0284ee7a0b43eac3e1a5670b492f2b5941da0506be9b1b024bb502d`.
Inspect actual scope/drift; preserve dirty work instead of resetting it to hashes.

## Mode and boundaries

On owner launch: IMPLEMENT_LOCAL for these two findings only. One Cursor Auto
agent; no delegates or other AI processes. Use tct-superpowers, tct-engineering
and tct-security-audit inline. CodeGraph first, bounded reads for gaps; no rebuild.

Require Phase 1 CHANGES_REQUESTED and Phase 2–7 LOCKED. Carry the recorded narrow
Program.cs chat-DI grant without re-asking; no DI edit is expected. No new
appsettings/.csproj, model/schema/content, paid API, secret write, remote Git/CI
or release authority. Preserve data/workloads and existing access/lifecycle rules.

Expected scope under `TCTEnglish/Services/AI/`: Rag/ChatExternalFactPolicy.cs,
Rag/AiChatAnswerService.cs, Internal/Retrievers/CardLookupRetriever.cs and narrowly
needed transient parsing/selection/composition. Add meaningful tests and the
required bug-log/handoff/manifest evidence. No BM25/vector/generation/citation UI.

## Ordered execution

1. Verify scope/predecessor and reproduce both findings with red SQLite tests on
   production boundary/retrievers:
   - F01: France definition `a country; its capital is not provided in this card`.
     Neither `What is the capital of France?` nor `Thủ đô của France là gì?` may
     return Answered/full from it. Keep stored-meaning and bare lookup controls.
     Add a non-negated mere attribute mention and another phrasing/entity.
     Correct the Kenya test oracle: “the capital city is recorded here” has no
     capital value, so it cannot prove a supported full answer. A retained positive
     fact control must supply and assert an actual explicit synthetic value.
   - F04: Daily Study and Daily each contain forecast with distinct meanings.
     Quoted/unquoted Daily Study must select its full-name source, not Daily.
     Preserve explicit Daily. Cover a label beginning with a framing token and
     a single-word label such as Study, plus existing full-name/Unicode controls.
2. Repair F04. Keep target framing separate from literal source names. Never trim
   Study/Class/My/Notes/Reading or other stopwords from a supplied SetName. Preserve
   source spans/quotes as needed before normalization; resolve full accessible
   SetIds before caps. Handle generic “trong bộ thẻ của tôi” separately so F02
   remains valid. Unknown/ambiguous labels must not silently borrow a prefix.
   Preserve comparison, duplicate-name clarification, all ACLs and bounded queries.
3. Repair F01 with a conservative supported-answer contract. Keyword/entity overlap
   is not proof that the source supplies the requested value. Missing/unknown
   support must refuse or clarify with matching response/outcome/scope. Do not add
   negation strings or more language regexes as the complete fix. Apply the support
   decision outside the former narrow English attribute frame as well. Keep true
   definition/learning-content lookup, low-confidence fallback, guides and typed
   user facts. A local baseline can abstain from fact kinds it cannot verify;
   it does not need a paid or unrestricted fact-answering feature in Phase 1.
4. Review nearby positive controls for the same semantic error. Returning another
   card just because its definition mentions the requested term does not establish
   a full definition answer. Correct oracle/setup with real semantic assertions;
   do not weaken access, lifecycle or valid lookup regressions to get a green run.
5. Run focused red/green tests and every command in the active plan. Explicitly
   include any new test class. Preserve F02/F03/F05, `card:{id}`, access/revocation,
   password help/credential refusal, HTTP/SignalR, quota/Stop/history and non-chat
   provider behavior. Recheck Pomelo builders if query construction changes.
   Use current binaries; record translation versus executed-provider evidence,
   all failures/skips and actual local checks. Do not invent browser/API/CI results.
6. Review the complete modified/untracked diff; keep canonical Phase 0 hash
   `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
   Append bug memory for verified repairs. Write Vietnamese
   `docs/feature-audits/hybrid-rag/phase-1/fix-round-4-handoff.md`, mapping full IDs
   to red/green evidence and oracle corrections. Refresh cumulative scope manifest
   including controlling guidance, retaining the predecessor chain and excluding
   recursive review/handoff/state results. Preserve historical review reports.

## Acceptance and stop

The concrete scenarios and preserved controls in the active plan must pass.
An unsupported full answer is a failure even when its text quotes a real card.
Test counts, reports, word-overlap controls and tooling alone do not prove support.

Return Phase 1 AWAITING_REVIEW, Phase 2–7 LOCKED. Report in Vietnamese with files,
checks and remaining limits. Do not self-approve, start Phase 2, commit/push/PR or
deploy. Codex must perform review round 5.

## Short owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-1-fix-round-4.md only. Repair HR-P1-R1-F01
and HR-P1-R1-F04 with meaningful failing regressions first; correct the unsupported
Kenya positive oracle. Preserve closed F02/F03/F05, source identity, access and
lifecycle/provider contracts. Follow the plan, run checks, refresh scope/handoff
and return AWAITING_REVIEW. Keep Phase 2–7 LOCKED; do not self-approve.
```
