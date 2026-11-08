# Phase 6 local completion — browser matrix and verified fixes

## Outcome and owner authority

Complete the missing browser acceptance and repair concrete Phase 6 issues inline
as one agent. The owner explicitly authorized local completion and fixes, while
explicitly forbidding APPROVED and opening Phase 7. Keep Phase 7 LOCKED; return
Phase 6 AWAITING_REVIEW only when every required local check is evidenced.
No commit, push, PR, remote CI, deployment or paid API call is authorized.

Input: 151-entry Phase 6 fingerprint
`0fecccd283b3152bad2c1a21c4fb37c6d09d31a0a5c31dd3cc0e1a86ace1f3ae`, branch
`feature/hybrid-rag-chatbot`, HEAD `6af768bd32d16cc6d012d484ee82df4cab805c69`.
The approved Phase 5 predecessor is 143-entry
`33c1a0208f233f8f949d224d21c3cb0244b50e6c98b3eb354cf40dd5c23a30d4`.
The current 151 entries have been verified before edits. Preserve existing work,
runtime data, credentials and native trust records. No model/migration/config/
project changes are expected. Use current AGENTS.md and the active Phase 6 plan.

## Ordered checklist

- [x] Verify actual checkout and the 151-entry input identity; review the handoff,
      browser gaps, current source and relevant engineering/UI/security guidance.
- [x] Preserve the input manifest. Start the required local launcher with a free
      port, fake loopback AI settings scoped to child processes and no saved-key
      writes. Verify readiness and speech metadata without paid requests.
- [x] Prepare isolated synthetic accounts/classes/private sources on disposable
      storage. Never downgrade the owner's sole admin or reset the dev database.
      Use actual cookie login and real HTTP/Razor/JS in a browser.
- [x] Review Phase 6 UI/cache/diagnostics code; reproduce actionable defects before
      fixes and add meaningful maintained regressions where appropriate.
- [x] Prove warm-cache account A → logout → B isolation, same-user premium downgrade,
      and class membership removal. Capture before/after DOM, response status,
      cache-hit evidence, timing within TTL, missing private body/title/source/link
      and backend provider mode. SQLite unit coverage is supplementary.
- [x] Finish every remaining Phase 6 browser row: real older pages/legacy/revocation,
      full/embed consistency, safe malicious/long source rendering, submit/Stop/retry,
      timeout/429/network and missing/corrupt-index states, desktop/narrow/zoom and
      visible keyboard focus. State injected versus real transport evidence precisely.
- [x] Fix verified in-scope defects, retest the actual browser flow and preserve
      HTTP/SignalR/history/antiforgery/access contracts. Keep diagnostics sanitized.
- [x] Run final build, focused access/history/quota/cancel/provider and Node tests,
      full app suite and pending-model check as applicable. Provider evidence
      cannot be substituted with tooling validation or static DOM inspection.
- [x] Write a Vietnamese closeout/browser report with exact actions, assertions,
      screenshots and zero-paid-call evidence. Refresh the cumulative source
      manifest, predecessor identity and exclusions from final raw file bytes.
- [x] Preserve/stop only task-owned fixture resources, check diff/links/whitespace
      and record all limits. Phase 6 AWAITING_REVIEW; Phase 7 LOCKED; no APPROVED.

Use CodeGraph first for indexed source and bounded reads for concrete gaps. Use
the available browser tools directly; do not create another AI process or agent.
Operational instructions/prompts remain English; owner-facing reports Vietnamese.

## Verified repairs and dependencies

1. **HR-P6-C-F01 (P1): Stop before admission** — retain cancellation on the attempt,
   cancel the late server id, discard unaccepted text/sources and preserve the draft.
   Production boundary: `ai-chat.js` Stop handler / `registerHubEvents`.
2. **HR-P6-C-F02 (P1): Stop/retry event ownership** — after F01, prevent a second
   transport until the old invocation settles; bind failure to the owning attempt
   and reject retired/replaced stream ids. Keep existing wire contracts. A delayed
   old admission/completion must never become a new accepted answer or source.
3. **HR-P6-C-F03 (P2): Reloaded source whitespace** — collapse Razor template
   whitespace at the assistant bubble/source boundary; retain body whitespace.
   Verify with native screenshots, keyboard focus, narrow viewport and text zoom.

F01/F02 were red first: 2 failed / 5 passed, then 7/7 and combined Node 14/14.
Full application: 1136 passed, 22 opt-in skipped, 0 failed. Focused: 224/224.
No persisted-model changes; final build and disposable MariaDB repeat are clean.
Exact browser modes, fixtures, timestamps and remaining limits are in
`docs/feature-audits/hybrid-rag/phase-6/browser-closeout.md`.
