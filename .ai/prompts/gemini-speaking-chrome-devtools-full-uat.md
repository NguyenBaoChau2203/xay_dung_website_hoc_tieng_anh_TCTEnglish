# Gemini 3.8 Flash High — full Speaking UAT with Chrome DevTools MCP

## Outcome

Test all existing Speaking features in the real local application using **Chrome
DevTools MCP**: paste YouTube URLs for personal practice, SRT/VTT import, private
library, catalog/filter/pagination, player/transcript selection, recording,
automatic scoring, self practice, dictation, saved progress/completion, ownership
and Speaking Admin controls. Deliver verified findings and repair instructions;
do not change application code to make the test pass.

Use the owner-selected **Gemini 3.8 Flash High** and one agent. The IDE model is
separate from the application's configured speech model. Do not substitute models
or delegate to additional agents.

## Read and follow

- AGENTS.md and `.ai/prompts/README.md`.
- `docs/superpowers/plans/2026-11-13-speaking-chrome-devtools-full-uat.md`:
  executable order, complete minimum80-case matrix, fixtures, evidence and cleanup.
- tct-superpowers, tct-engineering, tct-speaking-feature, tct-ui-review and the
  relevant access checks from tct-security-audit; DESIGN.md for visual assessment.
- Current symptom-relevant bug memory and the Speaking transfer ledger, loading
  only the active source/contract gaps. Use CodeGraph first where indexed.

## Mode and authority

`TEST_AND_REPORT_LOCAL`: browser testing, existing supplemental tests, read-only
DB validation, reversible test-owned fixtures and report/repair-plan authoring.
Preserve the actual checkout and existing work. Expected current branch:
`fix/speaking-catalog-transcript-polish`; inspect HEAD instead of assuming it.

No app source/config/DI/schema/dependency changes, global user-role/password resets,
commit/push/PR/merge, remote CI or staging/production deployment. Do not test Auth,
payments, emails or Listening as separate modules. A login/role check and verifying
that the old Listening batch stays retired are relevant Speaking prerequisites.

**Owner explicitly authorizes up to10 real assessment requests, at most10 seconds
of audio each, maximum100 submitted audio seconds total, using saved local keys.**
The owner will assist with real-microphone speech. Do not ask again for this grant.
Count retries and timeouts after dispatch toward the cap. Ask for readiness to
speak/OS microphone permission only when actually required. Never print keys.
This does not authorize paid long-video ASR, Admin audio-fallback imports, paid TTS,
AI suggestions or other provider calls. No automatic paid retry storm.

## Important current contracts to recheck

- Local primary URL `http://localhost:5100/Speaking`; readiness is
  `/health/ready`. Verify this process belongs to this checkout/current build.
- Public baseline95 videos (A1=18,A2=15,B1=17,B2=15,C1=15,C2=15), including90 new
  source-selected items. The6 original public Listening lessons remain active.
- The transfer ledger says all90 videos have cues and are available for local
  testing, but full acoustic/whole-speech quality approval remains pending.
  Nonempty transcripts, ImportStatus=ready or moving DOM highlights are not
  evidence that every sentence timing/word is correct.
- Public/private catalog and practice: `/Speaking`, `/Speaking/Index`,
  `/Speaking/Videos`, `/api/speaking/videos`, `/Speaking/Practice/{id}`.
- Private import UI uses POST `/api/speaking/imports` or
  `/api/speaking/imports/subtitles`; poll `/api/speaking/imports/{jobId}`.
  The legacy `/Speaking/My/Create` and `/Speaking/My/Delete` contracts also exist.
  Premium/Admin can import; Standard cannot bypass the lock. Use actual queued,
  running, succeeded, failed/cancelled states and the current watch deadline.
- Assessment: POST `/api/speaking/{sentenceId}/assess`, followed by POST
  `/api/speaking/{sentenceId}/progress`; antiforgery required. Private ownership
  applies, assessment receipt belongs to user+cue, expires and can be consumed.
- Current score is server **text match**, not measured phoneme pronunciation,
  prosody or fluency. Fluency/completeness availability is false. Check truthful
  labels, word comparison, error states and server-backed persistence.
- Current pass>=70; completion requires ceil(70% of distinct video cues), minimum1.
  Client numeric scores do not authorize progress. Verify rewards only once.
- Practice includes automatic/self choices, local playback, dictation/hints/slow
  playback/video hiding, transcript controls and keyboard shortcuts. Inventory
  every visible action, including the advertised AI suggestion button; a cosmetic
  toggle without useful behavior is not a passed feature.

## Start with browser evidence

1. Discover the Chrome DevTools MCP tools actually available in this chat. Call
   list_pages, open/select a dedicated localhost tab, take a fresh page snapshot
   and prove the visible page. `page_id:null` Companion context is not attachment.
2. Use snapshot→click/fill/press_key/upload_file for real success flows. Use
   evaluate_script to inspect DOM/player timings or make browser-origin negative
   requests, not to manufacture score text or bypass the UI. Direct fetch evidence
   is a request test, not a successful button interaction.
3. Use isolated role contexts when supported; otherwise switch real sessions
   carefully. Two tabs in one context share authentication. Keep credentials in
   supported ignored local access files, out of screenshots/reports.
4. Inspect relevant network requests/responses, console messages, screenshots
   and read-only persisted state. Redact tokens/cookies/Authorization/signed URLs.
   Browser tools may lack OS mic permission/audio hearing/request interception;
   inspect capabilities and report blockers, do not invent a tool.
5. If web startup is needed use `pwsh -NoProfile -File scripts/start-local.ps1
   -NoBrowser`, confirm readiness and `scripts/speech-status.ps1`. Keep5100; do not
   stop a foreign process or silently use another port. No paid calls on startup.
   Record working-tree hashes/build identity too. If another editor changes the
   tested files, preserve those edits and rerun affected cases on an identified
   current build; do not mix old/new page evidence into one pass claim.

## Execute the complete plan

Execute groups A–H from the plan and extend its matrix for newly discovered
features. Work through:

1. Catalog/roles/search/topic+CEFR intersection/pagination/My Videos.
2. Genuine paste-only YouTube import, async job/resume/duplicates/errors and
   English SRT/VTT upload, ownership and test-owned deletion.
3. Player/cue/reference/highlight/loop/early-middle-final navigation and long-video
   tail; distinguish audible alignment from DOM selection.
4. Self practice and mic allow/deny/device/lifecycle/rapid interaction cases.
5. Bounded real assessment through the normal recording flow; verify request,
   transcript/comparison/score, progress save and reload. Coordinate real speech
   with the owner. Include correct, mismatched and silent audio without exceeding
   the cap. Reserve requests; do not spend all10 unnecessarily.
6. Persisted best score, invalid/reused/wrong-user receipt, forged client score,
   distinct-cue completion and no duplicate XP/streak/notification. Use existing
   synthetic/fake tests for expensive boundary/TTL/concurrency branches; no
   clock edits or direct production receipt insertion to fake a UI pass.
7. Dictation/hint/reveal/slow replay/video visibility, native keyboard controls,
   unsupported features and all advertised AI actions.
8. Speaking Admin filters and fixture-only create/edit/delete/permission checks.
   Admin creation can trigger paid audio fallback; use a guaranteed-caption fake
   environment for that branch rather than spending the assessment grant.
9. Responsive1440/1366/768/390, limited laptop height, keyboard/focus, reduced
   motion and actual Chrome200% zoom. CSS zoom/reflow does not prove native zoom.

Use clearly labeled synthetic private fixtures for write/destructive cases.
Never delete/edit the95 public videos or someone else's private clips. Reuse a
small3-cue fixture for completion tests. A successful subtitle-upload alternative
does not make a failing paste-only YouTube import pass. Do not rewrite source
or clear the database to hide a defect.

Observe live responses for ordinary cases. Faults such as503/429/timeout/malformed
data may use supported session-scoped interception or existing isolated fake tests;
label them SIMULATED_BROWSER/AUTOMATED_OFFLINE and restore the setup afterwards.
Do not revoke real credentials, exhaust quota or disable the owner's DB.

Report evidence levels separately: REAL_UI, LIVE_PROVIDER, REAL_MIC, SAMPLE_AUDIO,
SIMULATED_BROWSER, AUTOMATED_OFFLINE, READ_ONLY_DB, SOURCE_ONLY. No real-mic quality
claim from a synthetic fixture, and no persistence pass from a success toast.

Keep a progress file with completed/failed/blocked cases, import jobs and real
request/audio-second counts. Update after every group and costly action. Do not
run a second paid request when a progress-save retry can reuse the first receipt.
Bound waiting/polling and move to independent cases if an external dependency is
blocked. Ask concise owner questions only for actual missing inputs/manual action.

## Deliverables and completion

- `docs/feature-audits/2026-11-13-speaking-full-uat-ledger.md`.
- `docs/feature-audits/2026-11-13-speaking-full-uat-cases.csv` with actual case IDs,
  role, expected/actual, status, evidence level/reference and finding ID.
- Ignored detailed screenshots/network/fixture evidence under
  `.appdata/local/speaking-full-uat/<run-id>/`; never commit audio or private state.
- Confirmed findings by P0–P3 with exact reproducer, consequence, source candidate
  and proposed fix. Identify SOURCE_ONLY suspicions honestly.
- If actionable failures exist, author
  `docs/superpowers/plans/2026-11-13-speaking-uat-repair.md` and
  `.ai/prompts/codex-speaking-uat-repair.md`, plus a short Codex launch message.
  Follow the repository prompt template; fixes await separate owner instruction.
  No findings means no empty repair plan.

Clean up only this run's verified fixture records where safe; stop mic tracks,
restore emulation/interception and preserve other tabs and public/private history.
Leave5100 ready. Report retained test records and blocked cleanup explicitly.

Final answer in Vietnamese: feature coverage denominator, PASS/FAIL/BLOCKED/N_A,
most important reproducible defects, manual/unobserved gates, real assessment
request/audio-second count, cleanup, artifact links and local Speaking URL.
Do not claim every feature works, full-catalog acoustic approval or staging/CI
success when the evidence does not establish it.

## Short launch message

```text
Use Gemini 3.8 Flash High. Read and execute .ai/prompts/gemini-speaking-chrome-devtools-full-uat.md and its linked plan. Use Chrome DevTools MCP to test all current Speaking workflows, including private YouTube/SRT import, practice, real recording/scoring, self practice, dictation, persistence, ownership, failure states and Admin fixture operations. The owner already permits up to10 real assessments, each<=10 seconds, using the saved local key, and will assist with real-mic speech. Preserve data, keep port5100, use one agent and report in Vietnamese with evidence and repair artifacts. This is testing/reporting only: no application fixes, commit, push or deploy.
```
