# Execution Prompt: Hybrid RAG Phase 1 repair round 5

## Outcome and evidence

Repair only HR-P1-R1-F01, the remaining unsupported-full-answer finding from Codex
review round 5. F02/F03/F04/HR-P1-R3-F05 and stable card identity are verified closed;
preserve their regressions.

Read AGENTS.md, `.ai/prompts/README.md`, current review-state/gates, accepted
Phase 0 contracts, symptom-relevant bug memory and:

- `docs/feature-audits/hybrid-rag/phase-1/review-round-5.md`.
- `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-5.md` (active checklist).

Reviewed branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 44-entry fingerprint
`26a1d1a138ba648137ea3c85b9d1fdfe8e221efe65ccc615f9b04bcf41ebf73e`.
Inspect actual scope/drift; never reset dirty work to match a fingerprint.

## Mode, authority and scope

On the owner's launch: IMPLEMENT_LOCAL for this finding only. One Cursor Auto
agent, no AI delegates/processes. Apply tct-superpowers, tct-engineering and
tct-security-audit inline. CodeGraph first, bounded reads for concrete gaps;
no index init/rebuild.

Require Phase 1 CHANGES_REQUESTED and Phase 2–7 LOCKED. Carry the existing narrow
Program.cs chat-DI grant without re-asking; no DI edit is expected. No new
appsettings/.csproj, persisted schema/content, secret write, paid API, remote
Git/CI or release authority. Preserve data/workloads. No BM25/vector/generation,
durable citations or UI phase work.

Expected scope under `TCTEnglish/Services/AI/`: Rag/AiChatAnswerService.cs,
Rag/ChatExternalFactPolicy.cs and narrowly needed transient request-kind/target
parsing/composer contracts, focused tests and required evidence.

## Ordered execution

1. Verify current manifest/dirty boundary. Add permanent failing SQLite tests on
   production answer service/retrievers for both residual paths:
   - France definition `a country; its capital is not given in Unit12` must not
     answer `What is the capital of France?` as Answered/full. A reference ID is
     not a capital value. Stored-meaning lookup remains valid.
   - With only `a European country`, both the direct question and
     `Can you tell me the capital of France?` lack support and must be non-full.
   Add controls showing that changing a reference ID or adding a polite wrapper
   cannot grant support; include another entity/relation. Preserve earlier English/
   Vietnamese absence-note, Kenya and actual lookup controls.
2. Follow the plan's conservative Phase 1 answer-kind decision. Full template
   answers need positive support for recognized platform help, typed own-user
   facts or genuine card-content lookup of the requested target. An authorized
   lexical hit is a candidate, not full-answer permission. Unknown demand must
   clarify/refuse, including paths the previous fact parser did not recognize.
3. Retire speculative free-text attribute answering from this baseline. Do not
   treat nearby letter/digit tokens or absence of a fact frame as support. Do not
   add Unit/Lesson blacklists, more polite prefixes or new negation strings as the
   complete fix. Richer fact grounding belongs to separately gated Phase 4.
   Reconcile the synthetic LIMA9 full-answer test with this safer baseline; it is
   not a business requirement to retain that heuristic. Keep negative tests and
   replace any removed positive fact expectation with an actual positive stored-
   definition lookup control. Record the capability limit in the handoff.
4. Align kind/target, selected evidence, composition, outcome and scope through
   the boundary. Preserve genuine low-confidence lookup, bare terms, contextual
   forecast, guides and typed user facts. A refusal label attached to an unrelated
   full template is not a repair. Preserve ACLs, stable card IDs and bounded queries.
5. Run focused red/green regressions and every command in the active plan, including
   new classes explicitly. Keep closed findings, password help/credential refusal,
   HTTP/SignalR, quota/Stop/history and global non-chat provider behavior. No paid
   or general-knowledge fallback is needed. Use current binaries; record actual
   counts/failures/skips and translation versus executed-provider evidence.
6. Review complete modified/untracked diff; keep canonical Phase 0 hash
   `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
   Append verified bug memory. Write Vietnamese
   `docs/feature-audits/hybrid-rag/phase-1/fix-round-5-handoff.md` with F01 red/green
   evidence, the positive answer-kind rule and capability limits. Refresh cumulative
   scope manifest with controlling guidance and predecessor chain, excluding
   recursive result/handoff/state files. Preserve historical reviews.

## Acceptance and stop

Unsupported fact questions cannot become full card-definition answers by adding
a reference ID or polite wording. Valid baseline lookup/platform/user-data flows
must continue to work. Static tooling, test counts and synthetic token matches
alone do not prove support.

Return Phase 1 AWAITING_REVIEW, Phase 2–7 LOCKED. Report in Vietnamese with files,
checks and remaining limits. Do not self-approve, start Phase 2, commit/push/PR or
deploy. Codex must perform review round 6.

## Short owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-1-fix-round-5.md only. Repair HR-P1-R1-F01
with failing regressions first and the plan's conservative positive answer-kind
decision. Retire the free-text attribute heuristic; do not add more token or
prefix exceptions. Preserve closed F02/F03/F04/F05 and lifecycle/access/provider
contracts. Run checks, refresh scope/handoff and return AWAITING_REVIEW.
Keep Phase 2–7 LOCKED; do not self-approve or start Phase 2.
```
