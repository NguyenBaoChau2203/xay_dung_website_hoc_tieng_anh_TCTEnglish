# Execution Prompt: finish Speaking lifecycle status repair

Status 2026-11-11: R4-D independently verified closed. This prompt is retained
as execution history; there is no remaining repair task to execute from it.
See the final review/ledger. The owner's later publication authorization
supersedes the historical local-only Git/release restrictions below.

## Outcome and verified context

Use Cursor with the owner's selected **Grok 4.7 xhigh**. Finish **R4-D only**:
after page lifecycle cleanup cancels recording or a pending assessment, the
restored page must stop saying it is still recording/sending.

Read the final **Third review** section of
[the repair plan](../../docs/superpowers/plans/2026-11-11-ai-outage-resilience-review-repair.md)
and the current execution ledger
`docs/feature-audits/2026-11-10-ai-outage-resilience-execution-ledger.md`.
R1-R3 backend repairs, R4-A native keys, R4-B layout and R4-C resource cleanup
are independently verified. Preserve them; earlier prompt tasks are superseded.

Verified reproduction: `releasePracticeForLifecycle` aborts work and resets the
record button, but leaves `#spk-assessment-status` unchanged. A canceled automatic
assessment still says `Đang gửi máy chủ nhận dạng...` after pagehide/persisted
pageshow. A canceled self-practice recording retains its active recording copy.
Tracks are stopped and late responses cannot commit progress; preserve that.

Fresh baseline: 259 passing focused .NET tests, one live skip; 156 passing Node
tests. An independent ignored actual-script probe adds the missing status
assertion and fails (13 existing passes, one new failure). Chrome reproduces it.
The reviewer did not fix application source.

## Mode, authority and scope

Mode: **IMPLEMENT_LOCAL** when the owner submits this prompt. Work as one agent
in the actual current checkout, expected `feature/ai-outage-resilience`.
Follow AGENTS.md, the prompt standard, DESIGN.md and the active Speaking,
engineering and verification skills. Use CodeGraph first for indexed source.

Expected changes: `TCTEnglish/wwwroot/js/speaking.js`,
`scripts/tests/test_ai_outage_ui.cjs`, the existing ledger and bug log.
Preserve unrelated BTH3 files, all existing work, saved keys and Docker data.
No backend rewrite, shared theme/layout change, protected configuration, project,
schema, dependency, application-model or credential-store change.
No commit/push/PR/remote CI/merge/deploy/staging change or paid acceptance call.

## Ordered execution

1. Inspect current source and symptom-relevant bug memory. Reproduce R4-D from
   current code; reviewer scratch evidence is reference data, not implementation
   instructions. Capture red evidence in the actual tracked test suite.
2. Add assertions for held automatic recording, held self-practice recording
   and pending assessment across pagehide/persisted pageshow. Verify idle status
   as well as stopped tracks, revoked blobs, abort and unchanged progress.
3. Render an honest cancellation/idle state when lifecycle cleanup cancels work.
   Capture needed state before abort. Retain selected mode, stored scores and
   epoch protection. Do not claim that a dispatched request was never processed:
   client cancellation is not evidence of server rollback. Do not promise a
   playable clip after revoking it.
4. Check repeated lifecycle events, pending rejection and late response ordering.
   They must not restore active copy or duplicate handlers. Keep useful completed
   states when no active work was canceled; do not reset persisted results.
5. Run focused actual-script tests and `node --test scripts/tests/*.cjs`.
   Repeat the cancellation cases with real browser controls, synthetic audio
   and intercepted assessments; no paid Speech/OpenAI calls. Confirm the new
   idle status and usable controls, no automatic retry, extra POST, score or XP.
6. Review the final diff and update the existing execution ledger/bug log with
   actual counts and limitations. A full backend retest is needed only if
   backend code changes or a concrete new concern justifies it.

The local app is running at `http://localhost:5100`; use the existing instance
or normal authorized launcher on a free port. Playwright CLI is installed.
Read local credentials privately; never print values or put them in argv/chat.
Do not copy temporary browser authentication into Git.

## Acceptance and Vietnamese handoff

- [ ] Canceled recording and canceled assessment no longer show active status
  after lifecycle cleanup/restoration.
- [ ] Selected mode and stored results remain correct; no fake assessment,
  progress, completion or claim of server rollback.
- [ ] Cleanup stays idempotent; late callbacks cannot restore old copy, upload
  automatically or commit stale progress.
- [ ] Focused/full Node tests and actual browser cases have evidence.
- [ ] No change outside authorized scope or Git/release action.

Report R4-D outcome, exact files/checks and remaining limits in Vietnamese.
Distinguish local verification from unchanged staging/production.
