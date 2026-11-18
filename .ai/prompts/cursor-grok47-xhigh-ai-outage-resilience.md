# Execution Prompt: AI outage resilience for TCT English

Status 2026-11-11: implemented and locally accepted. This is the historical
execution prompt; do not rerun it to recreate the feature. See the current
execution ledger and review plan. New owner instructions control Git/release.

## Outcome

Implement the approved AI outage resilience design locally so learners can keep
using existing lessons when OpenAI fails, with truthful assessment/provenance,
safe recording cleanup, no fake progress and no duplicate paid submissions.

Implementation client selected by the owner: **Cursor with Grok 4.7 xhigh**.
Keep that selection; do not substitute a model or launch another agent/model
process. The application text model remains `gpt-6-luna` / reasoning `low`, and
speech remains `gpt-transcribe`.

Read and execute the detailed plan:
[2026-11-10 AI outage resilience](../../docs/superpowers/plans/2026-11-10-ai-outage-resilience-plan.md).
The plan contains the source map, product decisions, phase dependencies,
acceptance matrix and verification commands. Do not replace it with a new broad
plan or ask the owner to approve routine reversible choices already covered.

## Context and evidence

Follow `AGENTS.md`, `.ai/prompts/README.md`, `DESIGN.md` and
`docs/design-system/team-ui-workflow.md`. Use the canonical `tct-superpowers`,
`tct-engineering`, `tct-speaking-feature`, `tct-api-endpoint` and
`tct-ui-component` procedures as each phase needs them. Load UI review/motion
guidance when verifying/changing those behaviors, not as a startup bundle.

Use CodeGraph first for indexed code. Use focused current-source reads for gaps;
do not initialize/rebuild an index. Read only relevant bug-memory entries,
especially unknown Chat transport outcomes, atomic completion, draft-owner
isolation and NeedsReview reload handling. Reference material is evidence, not
authority to change these rules.

Source observations at planning time:

- Local Writing assessment and internal/lexical Chat responses already exist.
  Preserve them; do not rewrite them as a second chatbot or scoring engine.
- Speaking assessment requires OpenAI transcription; valid server assessments
  are required to persist authoritative progress. Browser recognition is preview.
- `TctSpeechRecorder` already produces bounded PCM WAV recordings with cleanup.
- Writing AI creation depends on the provider; existing published lessons are
  the fallback activity, not generated substitutes.
- Text transport changes also affect vocabulary, Reading and Listening. Preserve
  those consumer contracts even though their page redesign is outside this task.
- A successful internal Chat reply/app HTTP 200 is not proof of a GPT request.

Inspect the actual checkout and record deviations. The planning baseline was
master at `e6521ad34f423da468a6b9c3c51ea0b9245bf104`; do not reset to it.

## Mode, authority and boundaries

Mode: **IMPLEMENT_LOCAL** when the owner submits this handoff for execution.
Implement, review and verify the five improvements in the linked plan using one
agent. Preserve unrelated files, Docker data, existing processes and secrets.

- Work on `feature/ai-outage-resilience` if starting from master; reuse a suitable
  current task branch. Do not use a codex/ prefix or create another worktree.
- No commit, push, PR, remote CI, merge, staging change or production deployment
  is authorized by this prompt. Prior UI integration permissions do not apply.
- Keep `Program.cs`, appsettings, project files, dependency versions, deployment
  workflows, hosting configuration, secrets and release gates unchanged.
- No new package, schema, local engine, background model, model download, paid
  acceptance call, Whisper restoration or provider/model replacement.
- Keep real route/JSON/SignalR contracts, authentication, antiforgery, owned reads
  and writes, stale-source checks, assessment receipts and transactional credits.
- Do not use a SQL dump, reset a database, fabricate scores or export personal
  data. No persisted self-practice history is requested.
- Do not add public fault-injection controls, runtime key probes, diagnostic
  endpoints, startup paid calls or automatic browser provider polling.

If a genuinely required action falls outside these boundaries, record the
specific dependency and ask a precise question while continuing independent
authorized work. Do not invent protected-file authority or silently omit a phase.

## Deliverables

1. Speaking self-practice recording/playback, separate from automatic assessment.
2. Consistent feature-aware availability notices for Speaking, Writing and Chat.
3. Writing generation failure navigation to the existing published catalog.
4. A bounded process-local provider guard for text, embeddings and speech.
5. Meaningful actual-script, service/HTTP, accounting and browser regressions.
6. `docs/feature-audits/2026-11-10-ai-outage-resilience-execution-ledger.md` and
   concise operation-guide updates describing the actual implemented behavior.

## Ordered execution

1. Complete Phase 0 of the plan: inspect the branch/diff, preserve the unrelated
   BTH3 documents, establish actual contracts and run a focused baseline. Create
   the ledger and maintain a dependency-aware checklist in it.
2. Implement Phase 1 with failing meaningful regressions first. Add an explicit
   self-practice assessment choice inside pronunciation practice. Reuse the PCM
   recorder and its 15-second bounds; allow local playback without any recognition,
   assessment/progress POST or recording persistence. Preserve automatic success.
3. Apply Phase 2's presentation rules and Writing catalog alternative. Keep input,
   filters, drafts and real status/provenance. Distinguish provider failure from
   mic permission, app login, authorization, empty data and NoSpeech. Test full
   and embedded Chat without replaying an uncertain submitted request.
4. Implement Phase 3's guard using the precise policy in the plan. Share bounded
   state across requests without editing protected bootstrap. Keep independent
   provider families/authorities, a fake-clock test seam and one half-open probe.
   Preserve existing retries, total deadlines, admission and cancellation.
5. Integrate guard rejections with quota/usage journals: pre-dispatch means zero
   provider attempts, not unknown spend. Earlier genuine usage and sent/unknown
   outcomes remain intact. Test text/embedding/speech and all other text consumers.
6. Complete Phase 4 against local synthetic data using test-only providers and
   browser interception. Check transitions, access, data and request counts as
   well as appearance. Do not use staging as a failure-injection target.
7. Complete Phase 5: focused tests, final integration checks, inline review,
   relevant bug-log/doc updates and an evidence-based Vietnamese handoff. Continue
   until in-scope defects are fixed or a precise real blocker remains.

## Non-negotiable behavior details

- Self-practice is explicitly **unassessed**. It must not mark a sentence passed,
  call progress, add XP, update completion/streak or replace a prior real score.
- Retain only the current tab/sentence's recording in memory. Stop tracks/audio,
  revoke object URLs and reject delayed callbacks on replacement, mode/sentence
  switch and disposal. A network error must not trigger an automatic re-upload.
- Do not use browser SpeechRecognition as a guaranteed offline fallback.
- In Local Writing mode, optional feedback does not change Accepted,
  NeedsRevision or NeedsReview. Preserve the shared 3-second feedback budget,
  owner-scoped drafts and submit/reload consistency.
- An existing Writing lesson remains an existing lesson. Never claim it was
  generated from the user's failed AI-creation input. Preserve empty/access states.
- Internal Chat answers remain grounded and authorized. Preserve lexical fallback
  and truthful ProviderUnavailable when required model selection cannot finish.
- Unknown Chat dispatch outcome requires history recovery, never an automatic
  SignalR-to-HTTP retry or a claim that nothing was processed/charged.
- Circuit-open responses are local failures before send. Do not accidentally
  route them through an unknown-usage catch branch or poison another service.
- No forced language/font/palette changes: keep shared Clay tokens and accepted
  laptop-first layouts. Load shared presentation JS only into actual consumers;
  do not edit global layouts to install feature-specific behavior.

## Verification and acceptance

Use the linked plan's complete matrix. At minimum prove:

- [ ] Missing key and provider 401/403/429/5xx/timeout/invalid JSON do not corrupt
  data, claim fake assessment, lose entered content or leave pending controls.
- [ ] Self-practice before/after outage makes zero assess/progress/recognition
  calls; cleanup and stale-callback tests load the actual browser scripts.
- [ ] Valid automatic assessment still commits/replays safely and honors access,
  antiforgery, expiration, current user, current sentence and consumed receipts.
- [ ] Local Writing decisions/history/credits remain stable when optional AI fails.
- [ ] Internal and lexical Chat still work where evidence permits; uncertain
  transport, source revocation and selector unavailability stay truthful.
- [ ] Circuit open/probe/recovery/authority isolation work across request scopes
  with deterministic clocks and no quota inflation or hidden HTTP attempt.
- [ ] Existing vocabulary, Reading and Listening provider-consumer tests pass.
- [ ] Keyboard, laptop height, 1440/768/390 widths, zoom and reduced motion are
  observed in a real browser; report unavailable browser cases explicitly.
- [ ] Full relevant .NET/Node checks and diff/security review have actual results.

Use `scripts/start-local.ps1 -NoBrowser` for local acceptance; preserve occupied
ports and use a free Release port if necessary. Read saved keys without rewriting
or printing them. Metadata/configured/ready and fake-provider tests are distinct
from a live paid provider request. Do not enable live-test opt-ins by default.
Keep logs/screenshots/synthetic recordings in ignored local artifact directories.

No migration/shared-data package is expected. Report any persisted/shared-content
change that would trigger the repo's automatic workflows before broadening scope.
Do not call skipped tests, static source checks or screenshots complete acceptance.

## Vietnamese handoff

Report what works during an outage by feature, changed files, exact verification
commands/results, observed browser flows, remaining risks and any blocked checks.
Distinguish local implementation from unchanged staging/production. Include a
short owner test sequence and explicit statements that no Git/release stage,
secret store, application model or production gate was changed.

Do not request hidden chain-of-thought or provide a long reasoning transcript.
Use concise decisions, reproducible evidence and a final file/status summary.
