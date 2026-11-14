# Speaking component scores and compact summary

Authority: owner requests local implementation and verification; one agent. Preserve
the current checkout and uncommitted Speaking work. No commit, push or release.

## Evidence and contract

The assessment service computes transcript overlap only. Commit hardcodes both
other metrics to zero and marks them unavailable. The score section is a column,
so the ring and bars cannot share a row. No recording audio is retained for old
receipts; do not invent historical acoustic scores.

Migration impact: AddSpeakingAssessmentComponentScores. Additive nullable
FluencyScore, CompletenessScore and MetricVersion columns on SpeakingAssessments.
Backward compatible; no existing-value changes or data loss. Existing progress
already has numeric component columns. Keep total/accuracy and completion/XP
threshold semantics unchanged.

Completeness measures attempted reference positions in the ordered alignment:
matched and substituted positions count; missing positions do not. Extra words
cannot increase coverage. Accuracy/total continue measuring correctly matched
words. Fluency is explicitly an estimate of temporal flow:
reuse the PCM energy window planner, trim leading/trailing silence, penalize
internal gaps and extreme speech rates. A broad 60–240 words/minute coaching band
is an application heuristic, not a validated proficiency standard. Weight
continuity 65%, pace 35%. Require >=2 recognized words and >=0.5s usable signal;
otherwise leave fluency unavailable. Do not infer phoneme accuracy or prosody.
Reuse one ASR result; no new provider, configuration or paid test calls.

## Checklist

- [x] Add failing assessment/commit regressions, including authoritative values.
- [x] Implement deterministic component scorer and nullable receipt persistence;
      forward values through API, retained progress, reload and navigation.
- [x] Preserve best-total progress; allow an equal-score new take to refresh
      component metrics and its matching receipt. Return retained best values and
      matching comparison when a lower-scoring take is not retained.
- [x] Place ring and bars side by side; explain estimated fluency and coverage in
      an accessible details disclosure. Preserve mobile controls/transcript.
- [x] Generate/review EF migration, Designer and snapshot; verify no pending model
      changes. Test previous-schema upgrade/repeat/data preservation on disposable
      MariaDB 10.5.23 before updating the local development DB.
- [x] Run focused .NET/JS regressions and Release build; inspect actual Chrome at
      1440/768/390, navigation/reload and keyboard disclosure; report native zoom
      or real-microphone checks separately when not observed.
- [x] Review complete task diff; update bug memory and record exact evidence.

## Verification ledger

- Baseline integration regression failed on the unavailable fluency flag before
  implementation. Final core assessment/scorer/restoration suite: 37 passed.
  Broader focused check: 46 passed, 1 skipped (existing MariaDB dual-completion
  concurrency acceptance requires its own environment). No claim of a full-suite
  or concurrency acceptance pass.
- Word comparison, outage and catalog navigation Node checks: 35 passed. New
  cases cover measured zero and retained-best comparison/metric consistency.
- Disposable MariaDB 10.5.23 upgrade test: 1 passed. Applies the prior schema,
  creates synthetic private ownership/progress/receipt, upgrades and repeats,
  checks old values/nullable fields, writes new metric values including zero,
  repeats and verifies no pending model/migrations. Unique test container removed.
- Generated migration 20261113112346_AddSpeakingAssessmentComponentScores adds
  only three nullable columns. Down reverses those additions; no downgrade was
  executed. Snapshot diff is limited to those three fields. EF pending-model check
  passes. Release build: 0 warnings/errors on the final incremental run; earlier
  full compilation surfaced existing warnings outside the changed files.
- Before local upgrade, affected tables were backed up into ignored
  .appdata/local/speaking-metrics-before-20261113-182926.sql. Restarted only the
  known checkout-owned Debug web using scripts/start-local.ps1 -NoBrowser.
  Existing DB/users/content preserved; shared-data sync reports 0 operations.
  Readiness200 on5100; speech metadata configured, VerifiedLive=false.
- Chrome MCP: private Sports172, 43 cues, restored first-cue scores90/90/—/95
  before/after1→2→1 and reload. Historical completeness is reconstructed from the
  retained transcript; historical fluency remains unavailable without PCM data.
  Ring/bars share their vertical center and bars are to the right at1440/768/390;
  each viewport has0 horizontal overflow. Keyboard Enter opens the explanation.
  Public82 (230 cues) also has correct summary layout and working full transcript
  hide/show. No Chrome warning/error messages during those checks.
- No real-microphone/provider request or native200% zoom acceptance was run.
  Fluency is an explicitly labeled energy/pause/pace heuristic, not a calibrated
  pronunciation/prosody grade. No credentials, protected config, shared content,
  commit, push, CI, staging or production actions.

## Local commit authorization follow-up

The owner subsequently authorized committing the completed work locally. The
consolidated commit includes the implemented Speaking import/catalog/practice/
scoring changes, regression tests, migration and completed execution ledgers.
Pre-commit checks:64 focused .NET tests passed without skips;146 shared DOM fixture
consumer tests passed. The earlier disposable MariaDB upgrade/preservation and
browser evidence above remain applicable. Four separate Gemini future-feature/UAT
plan/handoff documents remain untracked and outside this commit. No push, PR or
deployment is authorized by this local commit request.
