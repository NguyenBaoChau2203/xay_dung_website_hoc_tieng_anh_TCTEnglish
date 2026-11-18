# Private Speaking YouTube import repair

Mode: IMPLEMENT_LOCAL. The owner authorized investigation, fixes and a failure
popup. One agent performs all work. No commit/push/release or paid ASR authority.

## Evidence and scope

- Branch: `fix/speaking-catalog-transcript-polish`.
- Existing uncommitted Speaking Index/CSS/JS and UAT documents belong to the
  owner; preserve them and make focused additions.
- The running import worker reports YoutubeExplode VideoUnavailableException
  for `gOMypAhVaXE`. The public transcript panel returns 27 real timed English
  cues from 0 to 212.6 seconds. The playlist query is normalized correctly.
- Current caption service swallows provider failure as missing captions. The
  browser remembers a job but never reads it back after reload.
- Keep ownership, Premium/Admin checks, antiforgery, job reuse, atomic private
  publication and captions-only behavior. No persisted mapping/schema changes,
  protected configuration edits, or public/shared-content mutations are needed.

## Ordered checklist

- [x] Reproduce with meaningful offline regressions for blocked player/public
  panel captions, invalid timing/cancellation, job restoration and failure popup.
- [x] Add a bounded public YouTube transcript fallback using the existing
  acquisition contract, genuine text/timing and fixed upstream endpoints.
- [x] Restore in-progress browser jobs on reload, retain retry input and show an
  accessible Bootstrap failure modal with an actionable explanation.
- [x] Run focused .NET/Node checks and build; review the incremental diff inline.
- [x] Start current code locally, verify the owner's exact URL through a real
  authenticated job and MariaDB, check popup/keyboard/responsive behavior.
- [x] Update bug memory and record actual evidence/limits here; report Vietnamese.

## Acceptance

The exact URL produces an owned private video with genuine timed captions; no
audio/paid fallback runs. Missing/unusable captions produce a visible, actionable
popup and no partial video. Reload resumes the existing job without another
enqueue. Existing private access and publication regressions remain green.

## Verification ledger

- Red:2 provider fixture failures and3 Node UI failures before implementation.
- Green:`dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
  --filter 'FullyQualifiedName~YoutubeClientFixtureCompatibilityTests|FullyQualifiedName~SpeakingImportJobLifecycleTests|FullyQualifiedName~PremiumSpeakingLifecycleIntegrationTests|FullyQualifiedName~SpeakingImportReliabilityTests|FullyQualifiedName~AudioTranscriptImporterTests'
  --no-restore --verbosity quiet -p:WarningLevel=0` passes42/42, including a
  fresh rerun after the final backend message change.
- Node import/behavior/Clay/transcript-selection checks pass28/28. After adding
  a visible close icon, the affected import/Clay checks pass19/19.
- Real loopback MariaDB job56451e51-077f-41ee-b0c7-2a466ab22d6f succeeds as
  owned video171, “A2 English Listening Practice - Travel”, duration3:32,27 cues
  with genuine0–212.6s bounds. Browser reload displays the private card/count1.
  Practice171 contains all27 source cues.
- Real unavailable-video jobbaab70c1-a62c-4f5c-aa6b-228784ea850c fails without a
  result video; the popup opens. Reload while pending restores this job and only
  issues GET. Private video count stays1. Invalid URL POST also opens the popup.
- Chromium1440x900/768x1024/390x844: popup stays inside viewport. Tab focus trap,
  Escape dismissal and focus return to the URL input pass. Screenshot in ignored
  output/playwright/speaking-import-failure-390.png. Native200% zoom is unverified.
- `git diff --check` passes. The whole diff also contains pre-existing owner
  visual/UAT work; no unrelated changes were reverted. No schema migration or
  shared package is needed: only runtime acquisition/presentation and personal
  import rows changed. No paid speech requests, public content or release action.
- One final rebuild initially hit the verification process's apphost file lock.
  After checking executable path and `localhost:5110` arguments, stopped only
  that chat-owned process and reran successfully. Original5100 process was also
  owned by this chat; no unrelated web process or MariaDB volume was stopped.
- Final launcher rebuild succeeds on5100, `/health/ready` returns ready/HTTP200.
  Shared sync checks271 releases with0 record operations; last-sync metadata is
  current. Speech status is configured, not live account/quota verified. Browser
  on5100 still shows private video171/count1 and the failure modal's close icon.
- Final5100 popup also fits the viewport at CSS-simulated200% zoom on1440x900
  (dialog bounds1000x652). Native browser zoom remains unverified. An initial
  zoom check used an11-character string accepted as a raw YouTube ID; corrected
  the test input to an external URL and confirmed the real400-error popup.
