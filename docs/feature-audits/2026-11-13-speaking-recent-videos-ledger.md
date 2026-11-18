# Speaking recent videos: implementation and review ledger

Plan date: 2026-11-13. Implementation/review completed locally: 2026-11-14,
Asia/Bangkok. Branch: `fix/speaking-catalog-transcript-polish`.

Scope authority: the owner asked to inspect Gemini's progress and finish the
remaining feature. One agent completed implementation and verification. No commit,
push, PR, remote CI, staging or production actions were performed.

## Baseline found

Gemini had added the visit entity, navigations, DbSet/mapping, migration/Designer/
snapshot, two ViewModels/interface signatures and an initial test file. Both
service methods still threw NotImplementedException. There were no endpoints,
recent UI, lifecycle script, account cleanup or implementation ledger. The Index
service's includeRecent option did not populate anything.

Initial test command failed 11/11: ten fixtures omitted required PasswordHash and
the remaining path reached an unimplemented service. The tests also used
InMemory for relational/cascade behavior and simulated personal-row deletion
instead of calling the account deletion service. Converted the service tests to
SQLite with the existing model customizer, supplied synthetic required fields and
replaced the simulated deletion test with actual deletion/anonymization coverage.
The corrected baseline reproduced ten NotImplementedException failures.

The existing dirty tree was preserved. Prior Speaking catalog, transcript, import,
score restoration and coaching-metric changes were retained. No content packages,
provider configuration, dependencies, shared layouts or design tokens changed.

## Implemented behavior

- Personal server-side history stores one row per account/video, with UTC time.
  A → B → A gives A, B. At most six accessible, usable videos are displayed.
- History includes public videos and eligible own private videos. Current role,
  ownership, account state and cue availability are checked; private history is
  hidden on downgrade before Take(6), retained for a later upgrade, and never
  projected to another account.
- Authenticated antiforgery POST `/api/speaking/{videoId:int}/recent`; identity and
  time come from the server. Read-only GET `/api/speaking/recent` and Index/Practice
  personal responses use no-store/no-cache. Existing routes/contracts remain.
- The practice page records its initial visible visit, waits when opened hidden,
  supports restored pages and small keepalive requests on navigation, and does
  not keep writing on ordinary focus. History requests have an 8-second deadline.
- Index initially renders typed Razor data, refreshes on return and performs one
  bounded follow-up on browser Back. Refresh failures retain rendered cards and
  expose an accessible retry. Out-of-order responses cannot replace newer data.
- The new compact Clay section appears below the hero, has distinct JS hooks,
  three/two/one responsive columns and encoded text. Times are localized in the
  browser from explicit UTC values. It does not promise exact-sentence resume.
- Tracking never consumes an assessment or writes score/completion/XP/streak/time
  records. The existing independent page time tracker remains unchanged.
- Account deletion explicitly removes visits on both physical removal and
  anonymization. Video deletion cascades to history. No backfill fabricates visits.

## Additional race found and fixed during review

An update-then-existence-check path could lose the newer timestamp when an older
concurrent first visit inserted between the initial UPDATE and existence check.
It did not create duplicates, so a row-count-only concurrency test missed it.

Added a relational command interceptor reproducing that exact ordering. The new
test failed against the first implementation. The final implementation performs
another conditional monotonic UPDATE after the first-visit insertion/existence
path. It handles both a duplicate-key insert race and an insert that becomes
visible before the existence check; a stored newer time never moves backward.
The new regression passes in the final broad run. Only specific unique-key
failures are recovered, rather than swallowing FK/database errors.

## Schema and data evidence

Migration: `20261113125249_AddSpeakingVideoVisits`.

- Additive new table only: UserId, VideoId, LastOpenedAtUtc datetime(6), composite
  PK, recent-order index and VideoId FK index. Existing tables are not rewritten.
- Both FK cascades were exercised on real MariaDB 10.5.23. Account anonymization
  also exercises the explicit cleanup path, where a user FK cascade is insufficient.
- A disposable, uniquely named MariaDB container with a fresh guarded acceptance
  database/sentinel upgraded from `20261113112346_AddSpeakingAssessmentComponentScores`,
  preserved original user credentials, video/cue and progress data, repeated the
  upgrade without pending migrations, exercised 12 concurrent first visits and
  retained a deliberately newer stored timestamp. It also exercised video deletion
  and both actual account cleanup paths. The owned disposable container was removed.
- Local application: full ignored SQL backup taken before migration. Deterministic
  dumps of **all existing row data**, excluding EF migration metadata and the new
  visit table, matched before and after applying the migration:
  `DF840E533D7BD7001E56BE295D699B59D6A8C4AF10875FD4B66D6C60109BA880`.
- `has-pending-model-changes`: no changes since the last migration. Launcher
  repeat reports the database already up to date. Shared-data startup checks
  271 catalog releases with **0 record operations**.
- Browser UAT subsequently created seven intentional visit rows for an existing
  synthetic Premium UAT account; no scoring/assessment rows were created for it.
  The normal independent page timer may record time while viewing practice.
  Public Speaking remains 95 videos. Standard UAT history remained empty.

Backups/logs and preservation JSON are ignored under
`.appdata/local/speaking-recent-videos/`; they are not shared content or Git assets.
No users, credentials, progress or histories were exported into data/shared.

## Commands and actual automated results

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --filter "FullyQualifiedName~Speaking|FullyQualifiedName~AccountFunctionalCompletionTests|FullyQualifiedName~AccountCorrectnessIntegrationTests|FullyQualifiedName~AccountReviewRepairTests" --logger "console;verbosity=minimal"
```

Final result: **162 passed, 0 failed, 6 environment-gated skips**, total 168.
This build includes the new exact-order race regression. The broader skips relate
to opted-in MariaDB catalog/metrics/completion/schema fixtures; the new recent
MariaDB test is separately executed below, not counted as provider proof by skip.
Pre-existing compiler warnings remain in unrelated test infrastructure/views.

Documentation/tooling closeout: `uv run --no-project python scripts/agent_tooling.py`
passes. `uv run --no-project python -m unittest discover -s scripts/tests -v`
runs 142 cases: **139 passed, 3 conditionally skipped**, zero failures (150.408s).
These checks validate guidance/tooling only. The staging verification text emitted
by this suite belongs to its simulated fixtures; it is not a real deployment.

```powershell
pwsh -NoProfile -File .appdata/local/run-speaking-recent-provider.ps1
```

Ignored owned harness, final execution: **1 passed, 0 failed, 0 skipped** on real
MariaDB 10.5.23, after the final timestamp fix. It supplies ephemeral credentials
through child environment and restores them; no credentials appear in tracked
files or command arguments.

```powershell
$speakingNodeTests = @(Get-ChildItem scripts/tests/test_speaking*.cjs | ForEach-Object FullName) + @((Resolve-Path scripts/tests/test_speech_recorder.cjs).Path,(Resolve-Path scripts/tests/test_ai_outage_ui.cjs).Path)
node --test @speakingNodeTests
```

**94 passed, 0 failed, 0 skipped**, including nine new recent-history lifecycle,
failure, timeout, safe rendering and stale-response cases. Existing import,
transcript, dictation, score restoration, filters, Clay interactions, recorder
and outage UI checks remain passing.

```powershell
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --configuration Release --no-build
pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration Release
git diff --check
```

Pending-model check clean; launcher Release build succeeds; diff check passes;
`http://localhost:5100/health/ready` returns `{"status":"ready"}`. Saved local
speech configuration remains configured, live verification false. No paid audio
or other provider acceptance request was made for this feature.

## Browser evidence and acceptance matrix

Chrome DevTools MCP was used against the actual newly built local application.
Normal UI card clicks/navigation were used for the main A → B → A flow; API reads
and read-only DB queries cross-checked actual persistence. No JavaScript-generated
score or mocked server history was used as end-to-end success evidence.

| Case | Result | Evidence and limits |
| --- | --- | --- |
| R01 | PASS | Actual Premium empty state before visits; Standard empty state after switching account. |
| R02 | PASS | Actual public card click sends POST 200; HTTP tests cover both route forms and GET non-mutation. |
| R03 | PASS | Actual card clicks: 82 → 84 → 82; Back renders 82,84 and DB has two distinct rows. |
| R04 | PASS | Seven actual public visits leave seven rows and show six: 131,127,101,97,88,82. |
| R05 | PASS | Sign out Premium, sign in Standard: empty; sign back into Premium: same saved six. HTTP tests cover independent authenticated clients. |
| R06 | PASS | Relational service tests cover private owner/other learner; HTTP rejects locked private and missing IDs with 404. |
| R07 | PASS | SQLite role downgrade filters before Take; six public slots fill; re-upgrade restores private entry. |
| R08 | PASS | HTTP tests: anonymous, invalid/missing CSRF, nonexistent/locked private; service tests: blocked/missing user and empty video. |
| R09 | PASS | Real MariaDB 12-context first writes, monotonic future timestamp; deterministic relational insertion-order regression. |
| R10 | PASS | Endpoint tests leave score/receipt/completion/credit/session tables empty; UAT account has zero scores and assessments. |
| R11 | PASS | SQLite and MariaDB delete fixture video and verify cascade cleanup. |
| R12 | PASS | Actual account deletion service on SQLite and MariaDB covers removal, anonymization, other learner retention. |
| R13 | PASS | Actual C2 filter shows 15 public lessons and 8 per slide while all six recents remain; nonsense search shows public zero, recents unchanged; native keyboard clearing restores 15. Existing pagination/filter suites pass. |
| R14 | PASS with evidence split | Actual native Back returns show updated order; Node tests exercise persisted pageshow, initial hidden state and focus deduplication. Native bfcache persisted=true was not independently captured. |
| R15 | PASS with evidence split | Quick click/Back successfully persisted. Browser offline emulation with an explicitly dispatched pageshow shows error and retains six cards; real retry-button click online restores normal state. Node verifies abort deadline/nonblocking visit failure. |
| R16 | PASS | HTTP cache no-store/no-cache and server-derived identity/time; Node literal unsafe-title/URL rendering and HTML redirect rejection. Razor uses encoded typed values. |
| R17 | PARTIAL | Real 1440/1366/768 widths and emulated 390px: zero horizontal overflow, 3/3/2/1 columns, six cards. Native Tab moves between anchors with focus-visible. Reduced-motion emulation keeps content. Native 200% browser zoom was attempted but width/DPR stayed 1440/1, so it is unverified. |
| R18 | PASS | Disposable upgrade/repeat/preservation, local old-row SHA256 match, clean EF snapshot check. |
| R19 | PASS for targeted regressions | Broad .NET and Node suites above; no paid provider or full unrelated-module UAT. |

Native resize bottoms out at 500px in this Chrome window; the mobile result was
therefore verified using actual DevTools 390x844 device emulation and measured
innerWidth=390, not mislabeled from the 500px native window.

Screenshots were visually inspected inline in the Chrome tool response. Export to
a local image was unavailable: Chrome's MCP workspace-root check rejected the
target, and forwarding the original large image exceeded the Node tool review
payload limit. No policy/permission limit was bypassed. Console network errors
from the deliberate offline drill are expected, not feature success evidence.

## Local environment recovery and limits

On resuming the session after midnight, Docker Desktop and the web were stopped.
The first Docker startup failed on its zero-byte Secrets Engine socket. This
matches a reported [Docker startup issue](https://github.com/docker/for-win/issues/15064).
The CLI stop/restart was used; an attempted rename of the socket-only runtime
directory was denied and did not move it. No ACL, credential, User Secrets,
settings or VHD/volume changes were made. Docker's subsequent start recovered the
engine. This is session recovery, not a permanent Docker fix.

The current task's owned web process was restarted after the final backend change.
The final browser has normal online conditions, emulation reset and desktop width
1440. Web is left running on 5100, with the existing Premium UAT account showing
the new section for owner review. Normal practice history rows are retained.

Outstanding manual acceptance: native browser zoom 200%. This feature does not
establish full transcript acoustic QA, full 90-video UAT or provider scoring quality.
Those earlier content/assessment scopes remain separate. There are no known
unfixed defects in this recent-history implementation from this review.
