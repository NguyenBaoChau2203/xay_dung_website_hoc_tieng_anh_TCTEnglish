# Review the completed Phase 6 local snapshot

## Outcome and context

Verify the Phase 6 local completion against its final raw-byte manifest and
browser evidence. Read current AGENTS.md, the active Phase 6 plan,
`docs/superpowers/plans/hybrid-rag/phase-6-local-completion.md`, and
`docs/feature-audits/hybrid-rag/phase-6/local-completion-handoff.md`.
The input was the 151-entry BLOCKED snapshot
`0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`.
Its unchanged archived manifest is `scope-manifest-implementation.json` in the
same phase-6 audit directory. Use the current `scope-manifest.json` for the
returned snapshot. Branch: `feature/hybrid-rag-chatbot`; HEAD:
`6af768bd32d16cc6d012d484ee82df4cab805c69`.

## Mode and authority

REVIEW_ONLY, one agent. Inspect, reproduce and verify locally. The owner explicitly
forbids APPROVED and unlocking/starting Phase 7 in this closeout. Keep Phase 6
AWAITING_REVIEW and Phase 7 LOCKED unless a newer owner instruction changes this.
The implementation agent had local fix authority; this prompt requests review
of that return. Preserve any newer explicit owner fix authority if supplied.
No paid API, commit, push, PR, remote CI, deployment, protected-config change,
owner-account downgrade or dev-database reset is authorized.

## Ordered review

1. Verify branch/HEAD, cumulative file hashes, ordinal fingerprint and predecessor
   identity. Preserve existing uncommitted work. CodeGraph first for indexed source.
2. Review invocation ownership, Stop before id, pending retry, stale events and
   the unchanged SignalR/HTTP contracts in `wwwroot/js/ai-chat.js`. Examine the
   red/green regressions in `scripts/tests/test_ai_chat_phase6_sources.cjs`.
3. Review the CSS whitespace fix and browser screenshots. Verify visible keyboard
   focus, 390px overflow bounds and 200% text size with long hostile source titles.
4. Inspect `browser-closeout.md`: actual cookie login, cache-hit timestamps within
   the two-minute TTL, A→B, Premium→Standard and class-member removal, real history
   pages/legacy rows/source deletion, real HTTP/SignalR full/embed reload. Distinguish
   real application flows from injected UI fault/stream events and provider tests.
5. Recompute meaningful checks as needed: the three recorded Node chat files,
   affected RAG/access/cancel/quota/provider tests, build and pending model check.
   Use disposable fixtures and fake loopback credentials for browser replay;
   never print or copy saved keys. Respect the launcher and existing listeners.
6. Report concrete findings with priority, trigger, source location, impact and
   regression evidence. If further fixes are requested, implement under that
   authority; otherwise prepare a scoped repair plan/prompt only for real findings.

## Acceptance and handoff

No unsupported PASS claims or substitution of SQLite for browser evidence. Record
exact checks/skips and input/output identity. Browser artifacts and fixture helpers
are ignored local evidence, not portable test infrastructure. Review source and
the reproducible procedure if replaying on another checkout.

Respond in Vietnamese with findings, evidence, files, limits and current state.
Do not create an empty repair plan. Do not approve Phase 6, open Phase 7 or infer
Git/release authority from passing checks.
