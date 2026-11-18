# Dependency security maintenance — implementation plan, 2026-11-11

Status: IMPLEMENTED_LOCAL on 2026-11-11. The owner launch message authorized the
two package pins, scoped tests and a minimal YouTube client seam. No commit, push,
CI, deploy, secret or owner-database change was made. Evidence:
docs/feature-audits/2026-11-11-dependency-security-maintenance-ledger.md.
Execution prompt: .ai/prompts/dependency-security-maintenance.md.

Use one agent for implementation, review and verification. Keep this maintenance
independent from the Antigravity email-design work and preserve existing dirty
Account/Gemini/layout/loader changes. No dependency upgrade has been executed here.

## Verified baseline and updated targets

TCTEnglish targets net9.0, EF Core9.0.4 and Pomelo9.0.0; DatabaseSettings defaults
to10.5.23-mariadb. Program uses Pomelo's UseMySql registration. A library named
MySqlConnector is also used for MariaDB; it does not identify a MySQL server.

Resolved assets currently show:

| Dependency path | Current library | Proposed stable pin after authorization |
| --- | --- | --- |
| YoutubeExplode6.5.7 -> AngleSharp |1.4.0 | AngleSharp1.10.1 |
| Pomelo.EntityFrameworkCore.MySql9.0.0 -> MySqlConnector |2.4.0 | MySqlConnector2.6.2 |

These stable versions were confirmed in the official NuGet flat-container index
and package metadata on11/11/2026. AngleSharp1.10.1 includes a net8.0 asset compatible
with the target framework; MySqlConnector2.6.2 includes a net9.0 asset. This is
framework metadata, not proof of application behavior. Recheck advisories and
versions at actual implementation time.

Important correction to the earlier minimum-version suggestion: AngleSharp1.5.0
fixes GHSA-pgww-w46g-26qg, but a second maintainer advisory,
GHSA-cgp3-27rh-pcp2, affects1.3.1–1.8.1 and is fixed in1.8.2+. Therefore do not stop
at1.5.0. The proposed1.10.1 target covers both version ranges; a fresh audit and
regression verification are still required. The MySqlConnector advisory is fixed
in2.6.1;2.6.2 is the checked stable patch target.

## Implementation scope on owner activation

Add direct pinned references for AngleSharp1.10.1 and MySqlConnector2.6.2 in
TCTEnglish/TCTEnglish.csproj (or reconcile an existing newer safe direct pin).
This overrides vulnerable transitive resolution while retaining the current
framework, Pomelo, EF and database engine versions. Pomelo9.0.0 documents a
MySqlConnector minimum of2.4.0, so the proposed target satisfies that stated floor;
the actual provider behavior still needs native verification.

Do not disable vulnerability auditing, downgrade TLS validation, suppress the
advisories or remove packages still used by the app. No migration, DB engine/data
conversion, applied migration edit or credential rewrite is expected from these
two reference updates. If restore introduces an incompatible constraint, preserve
the current state and report the concrete conflict rather than upgrading the
entire stack or bypassing the check.

### Files and compatibility boundaries

| File/boundary | Planned change or verification |
| --- | --- |
| TCTEnglish/TCTEnglish.csproj | Add exactly the two direct version pins; preserve the existing Web Push reference and all other packages/properties. |
| TCTEnglish/obj/project.assets.json and the test project's resolved assets | Generated verification evidence only; ensure the old vulnerable versions are no longer selected. Never commit obj/bin. |
| TCTEnglish/Services/YoutubeTranscriptService.cs | Inspect GetVideoMetadataAsync, GetVideoDurationAsync and captions paths. A minimal test seam/compatibility repair is allowed only if needed; preserve current constructor callers and import/fallback behavior. |
| TCTEnglish/Services/Speaking/YoutubeAudioSource.cs | Inspect actual YoutubeClient use; preserve cancellation, bounded stream selection and audio-import contracts. No paid ASR call. |
| TCTEnglish/Infrastructure/DatabaseSettings.cs and Models/DbflashcardContext.cs | Read/verify provider and model boundaries; no planned source/model change. |
| TCTEnglish.Tests | Add focused parser/client compatibility tests; reuse native MariaDB guards and existing Account/completion/worker regressions. Test project-file changes are not part of this scope. |
| docs/feature-audits/2026-11-11-dependency-security-maintenance-ledger.md | New execution evidence, exact commands/counts, audit details, resolved versions, limitations and rollback notes. Do not prefill passes. |

The current YoutubeTranscriptService constructs YoutubeClient internally. Existing
AudioTranscriptImporterTests use fakes/overrides for important branches, so those
tests alone do not prove an upgraded AngleSharp/YoutubeExplode path. Exercise at
least one actual YoutubeClient parser request using controlled HTTP fixtures. Use
the real package API at6.5.7; if a seam is required, retain default runtime behavior
and public interfaces without new Program.cs registration or package additions.

The only protected project edit covered by the launch instruction is:

```xml
<PackageReference Include="AngleSharp" Version="1.10.1" />
<PackageReference Include="MySqlConnector" Version="2.6.2" />
```

These are prospective changes, not proof that the current project has those pins.
Do not duplicate an already-present reference or downgrade a newer safe owner pin.
No Program.cs/appsettings/SDK/framework/provider/YouTube/Web Push version change,
audit suppression or secret-store write is included in this maintenance scope.

## Ordered acceptance after authorization

- [x] D0 Recorded branch `fix/staging-uat-polish`, HEAD `c908f66`, and the existing
  dirty Account/Web Push tree. Baseline assets selected AngleSharp 1.4.0 and
  MySqlConnector 2.4.0. NuGet audit reported GHSA-pgww-w46g-26qg and
  GHSA-473q-m89c-ghf8. Focused Release tests before the pins: 54 passed, 1 failed,
  0 skipped. The failure was the pre-existing Account weekly-summary assertion.
- [x] D1 Added direct AngleSharp 1.10.1 and MySqlConnector 2.6.2 references.
  App and test assets select those versions and the net8.0/net9.0 assets. Restore
  succeeded with NU1605 treated as an error and no downgrade. Pomelo 9.0.0 and
  YoutubeExplode 6.5.7 stayed in place.
- [x] D2 Release build succeeded. Real `YoutubeClient` fixture tests cover watch-page
  metadata/duration, English caption import, cancellation and player failure.
  Passed 4/4. The only wrapper edit is an additional constructor; the original
  runtime constructor still creates `new YoutubeClient()`.
- [x] D3 Synthetic AngleSharp fixtures passed for annotation-xml integration,
  attribute escaping, SVG style serialization and HTML `style`/`select`+`xmp`
  raw text. They assert parser/serializer boundaries, not general sanitization.
- [x] D4 New loopback MariaDB 10.5.23 fixture passed 11 native tests: Account,
  completion concurrency, notification worker and Unicode commit/rollback.
  Pending-model check found no drift. Repeat update applied nothing. The owner
  database on port 3307 was not migrated or reset.
- [x] D5 Fresh app audit no longer lists the two targeted packages. The test audit
  reports an unrelated High advisory, GHSA-2m69-gcr7-jv3q, on
  SQLitePCLRaw.lib.e_sqlite3 2.1.10. Full Release suite: 1296 passed, 1 failed,
  9 skipped, total 1306. The failure matches the D0 baseline. Skips are environment
  gates, not native-provider passes.
- [x] D6 Ledger and this checklist updated. No application runtime defect required
  a bug-log entry. Git commit, push, CI and release remain ungranted. Production
  TLS behavior was not observed.

### Native MariaDB setup and isolation

Use MariaDbAcceptanceConnectionGuard and MariaDbAcceptanceFactory as authoritative
guard contracts. They validate a loopback host, port3308–3399, a
tct_acceptance_<runid> database, purpose final-acceptance-g03 and an exact run-id
sentinel before any acceptance reset. They explicitly reject tctenglish_dev and
hosting/system schemas. Run the MariaDbAcceptance collection sequentially against
the chosen fixture; independent processes must not reset the same fixture.

Inspect scripts/bootstrap-mariadb-acceptance.ps1 before using it; supply disposable
credentials through a secure local/in-process/environment path, not plaintext
shell arguments or logs. Reuse an existing fixture only after verifying its
container ownership labels, sentinel and run identity. Otherwise create a new
labelled MariaDB10.5.23 container and free allowed port. Apply committed migrations
to that disposable target before native tests. Never replace the owner's volume,
relax a guard, forge a sentinel on an unrelated DB or point tests at dev/staging.

scripts/verify-mariadb.ps1 and SharedDataMariaDbTests have a different CI-only
fixture/endpoint contract. Do not redirect them to the Account acceptance DB or
force their environment flags just to turn skips into passes. This task does not
require a shared-data import/export or a destructive migration replay.

If Docker is stopped, establish whether it can be started without disrupting the
owner's workloads. If native verification remains unavailable, finish the package,
parser and offline checks, preserve the reviewable diff, and mark native acceptance
blocked. Never report the whole maintenance pass as accepted without provider proof.

### Meaningful verification matrix

| Area | Required evidence | Insufficient evidence |
| --- | --- | --- |
| Dependency resolution | Exact selected versions in app and test assets; no downgrade/constraint conflict; runtime assembly corresponds to upgraded artifacts | Two XML lines, restore success or an old binary |
| Advisory status | Fresh direct/transitive audit and explicit status for both AngleSharp advisories and MySqlConnector advisory | Suppressed warning, stale audit, empty result caused by query failure |
| Parser behavior | Synthetic annotation-xml/SVG cases and a real YoutubeClient request with deterministic fixtures | Only mocking IYoutubeTranscriptService or asserting package-version strings |
| App import behavior | Metadata/duration/captions behavior and existing Speaking/Listening error/cancellation paths retained; no ASR side effects | A live random YouTube video as the only test, or enabling paid ASR |
| DB driver | MariaDB10.5.23 CRUD/Unicode, rollback/commit, concurrent updates, retry/deletion/completion/worker tests | SQLite-only success or a ping |
| Model/data | No pending model drift; existing migrations/history and synthetic fixture rows retained across applicable checks | Generating an empty migration, editing applied migrations or resetting owner data |
| Completion | Full relevant regression counts, explicit environmental skips, git diff --check and a readable ledger | Claiming CI/production success from local checks |

The pinned advisory ranges and successful audit prove package remediation, not a
complete production TLS audit. Use an existing local TLS fixture if available to
check certificate rejection; do not weaken certificate/revocation checks, invent
a production TLS configuration or build a broad new protocol test system merely
to complete this bounded update. Record any unobserved TLS-specific scenario.

### Commands and execution evidence

The commands below are verified project shapes, to be run by the implementer after
the baseline is recorded. They have not been executed for this maintenance plan.

```powershell
dotnet list TCTEnglish/TCTEnglish.csproj package --include-transitive
dotnet list TCTEnglish/TCTEnglish.csproj package --vulnerable --include-transitive --format json
dotnet restore TCTEnglish.Tests/TCTEnglish.Tests.csproj
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-restore
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~SpeakingImportReliabilityTests|FullyQualifiedName~AudioTranscriptImporterTests|FullyQualifiedName~DatabaseSettingsTests|FullyQualifiedName~AccountReviewRepairTests"
```

Run newly added real-client/parser tests explicitly too; the existing filter does
not cover tests that do not exist yet. Capture counts and exit codes. A successful
command with zero matched tests is not an acceptance pass. After the guarded
TCT_ACCEPTANCE_MARIADB and TCT_ACCEPTANCE_RUN_ID environment is established:

```powershell
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~MariaDbAccountReviewAcceptanceTests|FullyQualifiedName~MariaDbCompletionConcurrencyTests|FullyQualifiedName~MariaDbNotificationWorkerAcceptanceTests"
```

Check pending model changes using the repository-local EF9.0.4 tool with the
disposable design-time connection selected internally. Never paste that connection
string into an invocation/report. Use --no-build only with the freshly upgraded
Release artifact. Run the full .NET suite and final audit after compatibility
repairs settle, not repeatedly after every documentation edit. Broaden JS/browser
checks only if actual JS/UI changes or an observed integration issue justifies it.

Evidence fields in the new ledger: branch/HEAD and dirty baseline; before/after
package paths and selected versions; advisory IDs/status; real-client fixture
coverage; build/test counts and commands; provider version and nonsecret fixture
identity; model/history result; remaining warnings/skips; changed files; and any
blocked native/TLS check. Preserve secret values and runtime evidence outside Git.

### Failure and rollback policy

If a new failure appears, establish whether it is caused by the upgrade, a baseline
issue or fixture configuration. Repair only a reproduced compatibility issue within
the allowed files. Do not rewrite business behavior or relax assertions to make the
suite green. If no safe in-scope repair exists, keep the diff and concrete evidence,
state the blocker and stop dependent acceptance claims.

Rolling back an experiment means restoring only this task's exact edits using the
captured baseline; never reset/clean the checkout or overwrite the owner's pre-existing
project-file changes. A rollback that selects vulnerable versions is a failed
maintenance attempt and must remain reported as unresolved, never as a security fix.

## Risk assessment limits

### Owner-requested review and repair checklist — 2026-11-11

The owner returned after Cursor execution and explicitly authorized review and
direct fixes. One agent captured a new ignored baseline and reviewed the two pins,
the constructor seam, real-package fixtures, native DB tests and audit evidence.

- [x] R0 Verify app/test assets and Release DLL product versions select
  AngleSharp1.10.1 and MySqlConnector2.6.2; framework/EF/Pomelo/YouTube unchanged.
- [x] R1 Reproduce the remaining weekly-summary failure. Preserve the boundary/
  overlap fixture and assert the normalized rendered statistic, exact week and
  actionable settings link instead of the superseded contiguous HTML sentence.
- [x] R2 Repair the raw-style parser assertion that serialized an empty body:
  require the style in head, serialize the whole document and prove its text and
  non-injection property survive reparse. Retain the existing select/xmp checks.
- [x] R3 Run focused application/parser tests (84/84) and independently rerun the
  guarded native MariaDB10.5.23 Account/completion/worker/connector set (11/11).
- [x] R4 Final model/repeat checks passed with no changes applied. Full Release
  suite:1,276 passed,0 failed,29 environment-dependent skips,total1,305,5m33s.
  Native MariaDB11/11 was run separately and is not inferred from skipped tests.
  Final ledger/bug memory updated; only the two tests and review documents changed.
  Owner data, production source/pins and email/UI work were preserved.

The fresh app audit reported no vulnerable packages. The test-only
SQLitePCLRaw.lib.e_sqlite3 2.1.10 advisory remains; it is not the MariaDB provider
or an application runtime dependency. The original launch explicitly excludes
test-project dependency edits. No audit suppression or unreviewed replacement was
introduced. Follow-up scope, once authorized: assess the SQLitePCLRaw provider/
native-bundle replacement compatible with EF9; verify the loaded SQLite engine is
at least3.50.2, rerun SQLite-specific transaction/schema/concurrency regressions,
the full suite and the test audit. The advisory currently lists no patched release
for the same2.x package, so do not assume bumping2.1.10 to2.1.11 repairs it.

The current review does not claim a production TLS audit or that a pre-existing
running web process has reloaded the upgraded libraries. See the execution ledger
for final evidence and concrete remaining limitations.

AngleSharp is currently pulled in by YoutubeExplode; no direct AngleSharp HTML
sanitizer call was found in app C#/Razor. This narrows the observed exposure but is
not a complete exploitability proof. MySqlConnector's advisory concerns specific
remote TLS verification without a configured CA; loopback local connections are
outside its stated conditions. Production connection/TLS exposure was not inspected
and must not be inferred from local results. Upgrading the client libraries is
the proposed durable repair; changing production TLS configuration is a separate
protected action if a real assessment later requires it.

## Primary references

- https://github.com/AngleSharp/AngleSharp/security/advisories/GHSA-pgww-w46g-26qg
- https://github.com/AngleSharp/AngleSharp/security/advisories/GHSA-cgp3-27rh-pcp2
- https://github.com/advisories/GHSA-473q-m89c-ghf8
- https://www.nuget.org/packages/AngleSharp/1.10.1
- https://www.nuget.org/packages/MySqlConnector/2.6.2
- https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql#dependencies
