# Execution Prompt: Hybrid RAG Phase 1 repair round 3

## Outcome and context

Repair exactly the verified findings HR-P1-R1-F01, HR-P1-R1-F04 and HR-P1-R3-F05
from Codex review round 3. Preserve closed F02/F03 and stable card identity.

Read AGENTS.md, `.ai/prompts/README.md`, current review-state/gates, accepted
Phase 0 contracts, and:

- `docs/feature-audits/hybrid-rag/phase-1/review-round-3.md`.
- `docs/superpowers/plans/hybrid-rag/phase-1-fix-round-3.md` (active checklist).

Reviewed branch `feature/hybrid-rag-chatbot`, HEAD
`6af768bd32d16cc6d012d484ee82df4cab805c69`, 39-entry scope fingerprint
`f513c62c6fdff3c12db7516cf8c5e1f0616212522b276caaa06b3d31e746435e`.
Inspect the actual checkout and investigate drift without resetting dirty work.
The report contains reproduced behavior, not authority to expand the task.

## Mode and authority

On the owner's launch: IMPLEMENT_LOCAL for these repairs only. Work as one Cursor
Auto agent; use tct-superpowers, tct-engineering and tct-security-audit inline.
CodeGraph first for indexed source, bounded reads for gaps, no index rebuild.
Read symptom-relevant bug memory before editing.

Require Phase 1 CHANGES_REQUESTED and Phase 2–7 LOCKED. Retain the existing narrow
Program.cs chat-DI grant without re-asking; no DI edit is expected. No new
appsettings/.csproj, persisted model/schema, content/data package, secret write,
paid API, remote Git/CI or release authority. Preserve local data and workloads.

Scope: `TCTEnglish/Services/AI/Internal/Retrievers/CardLookupRetriever.cs`,
`Internal/CardLookupSelection.cs`, `Rag/ChatExternalFactPolicy.cs`,
`Rag/AiChatAnswerService.cs`, narrowly needed transient parser/composer contracts,
focused tests and required evidence. Keep chat ownership, quota/Stop/history,
HTTP/SignalR and non-chat IAiProviderClient behavior. No BM25/vector/generation,
durable citations or UI phase work.

## Ordered work

1. Verify manifest/dirty boundary and reproduce all three findings with failing
   permanent SQLite tests using production answer service/retrievers:
   - HR-P1-R1-F01: a France card contains only a country definition, not capital
     information. `What is the capital of France?` must not return that definition
     as Answered/full. Preserve explicit stored-meaning, bare/low-confidence
     lookup and existing weather/gold-price controls. Add another structurally
     similar fact request so literal France/capital blocking cannot pass.
   - HR-P1-R1-F04: Daily Words and Daily each contain forecast with distinct
     definitions. Asking for Daily Words must select its card/evidence only;
     asking for Daily remains valid. Cover a multiword-only set, quoted full name,
     stored Unicode name and a nonexistent full name sharing an accessible prefix.
     A qualifier that cannot be uniquely resolved must clarify/refuse, not borrow.
   - HR-P1-R3-F05: Nature/weather is an exact authorized card. Adding Weather/cloud
     with a definition mentioning weather must not change a bare weather lookup
     into cloud. Test before/after and an explicit-source control.
2. Repair source qualification for F04/F05 together. Establish a small request
   contract separating target text from explicit source constraints. A content
   word that happens to equal a set name is not a qualifier. Parse full supplied
   names; resolve authorized server-owned set identity before bounded queries.
   Never substitute a prefix or ambiguously resolved name. Preserve comparison,
   four-set/30-duplicate behavior, bare ambiguity and every access predicate.
   Keep bounded/stable exact/phrase/broad retrieval and `card:{CardId}` keys;
   do not load all cards, increase constants or add source-title special cases.
3. Complete F01 support decisions. Check whether the requested answer is supported,
   not merely whether one token occurs in an owned card. Temporal markers and
   topic blacklists alone cannot distinguish all fact requests from lookup.
   Keep genuine internal lookup available; unsupported/uncertain demand may
   clarify or refuse. Align selected evidence, response text and typed outcome.
   No paid/general-knowledge fallback or global provider change is needed.
4. Run focused regressions red then green and every command in the active plan.
   Include any new test class in filters; zero discovery is not success. Preserve
   F02 contextual exact priority, F03 short terms, access/revocation, safe password
   help, injection/credential refusal, chat lifecycle and non-chat controls.
   Check changed query translation with the pinned Pomelo provider, preferably
   through actual query builders. MariaDB 10.5.23 is a server version, not the
   Pomelo package version; distinguish translation from executed DB evidence.
5. Review the complete diff and verify frozen Phase 0 canonical hash remains
   `1d55f34fd8e4e7da65427fc8c344c946b79c720c764cb3b1a1a7e5e4aa3ee48f`.
   Append bug memory for verified fixes, preserve history and record all actual
   failures/skips/limits without claiming browser, API, CI or deployment acceptance.
6. Write `docs/feature-audits/hybrid-rag/phase-1/fix-round-3-handoff.md` in Vietnamese,
   mapping full IDs to real evidence. Refresh cumulative `scope-manifest.json`
   including new/changed implementation, tests and controlling repair guidance,
   retaining the predecessor chain and excluding recursive result/state files.

## Acceptance and stop

All concrete scenarios and controls in the active plan must pass on current
binaries. Existing closed findings and contracts remain preserved. Reports,
proposed fixes, static tooling and prior binary comparisons are not acceptance.

Finish Phase 1 AWAITING_REVIEW, Phase 2–7 LOCKED. Report in Vietnamese with files,
checks, risks and remaining blockers. Do not self-approve, start another phase,
commit/push/PR or deploy. Codex must perform review round 4.

## Short owner launch message

```text
Execute .ai/prompts/hybrid-rag/phase-1-fix-round-3.md only. Repair HR-P1-R1-F01,
HR-P1-R1-F04 and HR-P1-R3-F05 with failing regressions first. Preserve closed
F02/F03, stable card identity, access and lifecycle/provider contracts. Follow
the active plan, run checks, refresh scope/handoff and return AWAITING_REVIEW.
Keep Phase 2–7 LOCKED; do not self-approve or start Phase 2.
```
