# Dependency security maintenance ledger — 2026-11-11

Local implementation only. No commit, push, pull request, remote CI, staging
or production deploy. The owner's development database was not migrated, reset
or exported.

## Checkout

| Item | Value |
| --- | --- |
| Branch | `fix/staging-uat-polish` |
| HEAD | `c908f665cff1a5b9b65a05e9009de24e571cde7a` |
| Pre-existing dirty work | Account, Web Push, Gemini, layout and email files were already dirty. The Web Push package reference stayed in `TCTEnglish.csproj`. |
| Working-tree csproj hash before the pins | `e884e331c0f0d87719a106ac058f4dab894fbc67` |

## Versions

| Package | Before | After | Asset selected for net9.0 |
| --- | --- | --- | --- |
| AngleSharp | 1.4.0 transitive from YoutubeExplode 6.5.7 | 1.10.1 direct | `lib/net8.0/AngleSharp.dll` |
| MySqlConnector | 2.4.0 transitive from Pomelo 9.0.0 | 2.6.2 direct | `lib/net9.0/MySqlConnector.dll` |
| YoutubeExplode | 6.5.7 | 6.5.7 | unchanged |
| Pomelo.EntityFrameworkCore.MySql | 9.0.0 | 9.0.0 | unchanged |
| Microsoft.EntityFrameworkCore | 9.0.4 | 9.0.4 | unchanged |
| Target framework | net9.0 | net9.0 | unchanged |
| MariaDB engine | 10.5.23 | 10.5.23 | unchanged |

Release output `TCTEnglish/bin/Release/net9.0` and the test output copy the same
binaries. AngleSharp product version is `1.10.1+d3cd1a023256b06497bc0cf803702fe94661f358`.
MySqlConnector's assembly version remains `2.0.0.0`; its product/file version is
`2.6.2+775689bb3e1ab55d16171e0795264fa746e364c4`. Pomelo's product version remains
`9.0.0`. `TCTEnglish.deps.json` records AngleSharp `1.10.1` and MySqlConnector `2.6.2`.

NuGet dependency floors remain YoutubeExplode -> AngleSharp `1.4.0` and Pomelo ->
MySqlConnector `2.4.0`. The direct pins resolve above those floors. `NU1605` is
`warnAsError` in the assets file, and both restores exited 0 with no downgrade.

## Advisories checked on 2026-11-11

| Advisory | Affected range checked | Patched floor | Status at 1.10.1 / 2.6.2 |
| --- | --- | --- | --- |
| GHSA-pgww-w46g-26qg / CVE-2026-54570 | AngleSharp through 1.4.1 | 1.5.0 | Addressed. NuGet listed it before the pin and does not list it after. |
| GHSA-cgp3-27rh-pcp2 | AngleSharp 1.3.1–1.8.1 | 1.8.2 | Addressed by the maintainer range. OSV and `github.com/advisories` returned 404; the repository advisory remains published. |
| GHSA-h5fx-ppwm-hhh5 | AngleSharp 1.0.0–1.8.1 | 1.8.2 | Addressed by the maintainer range. High CPU advisory, published 2026-11-05. Not indexed by the NuGet audit that ran here. |
| GHSA-9jfh-hwvc-37wj | AngleSharp through 1.7.1 | 1.7.2 | Addressed by the maintainer range. Published 2026-11-05. Not indexed by the NuGet audit that ran here. |
| GHSA-473q-m89c-ghf8 | MySqlConnector through 2.6.0 | 2.6.1 | Addressed. NuGet listed 2.4.0 before the pin. OSV reports no vulnerabilities for 2.6.2. |

AngleSharp 1.10.1 was published on 2026-11-09 and is the latest stable release.
`1.10.2-beta.945` exists and was not selected. The public AngleSharp repository
advisory list reviewed for this pass contained the four AngleSharp rows above.

NuGet's fresh app audit (`dotnet list TCTEnglish/TCTEnglish.csproj package --vulnerable --include-transitive --format json`, exit 0) returned no vulnerable packages.
The same command for `TCTEnglish.Tests` returned one unrelated High finding:

- `SQLitePCLRaw.lib.e_sqlite3` 2.1.10, GHSA-2m69-gcr7-jv3q, "vulnerable dependency on SQLite".
- It is in the test graph only. The app audit does not list it. The test project file was not changed. No patched release was identified in the OSV response captured here, so this task did not attempt to move it.

## Parser and client fixtures

`AngleSharpAdvisoryCompatibilityTests` uses AngleSharp 1.10.1 directly:

- annotation-xml `encoding="text/html"` exposes the injected `img` to a DOM walk;
- an attribute containing markup serializes with `&lt;`/`&gt;` and does not become an `img` on reparse;
- the SVG `style` character-reference payload does not create an HTML `img` or an `onerror` attribute on reparse;
- HTML `style` and `select`/`xmp` keep raw text instead of creating `img` or `script`.

These assertions follow the maintainer boundaries. They do not claim AngleSharp removes arbitrary unsafe HTML.

`YoutubeClientFixtureCompatibilityTests` uses YoutubeExplode 6.5.7 with an injected `HttpClient` and an in-memory handler. All HTTP is intercepted by that handler; no YouTube network request is made, and the audio importer was not called on the success path:

- watch-page HTML is parsed for title `Fixture lesson` and duration `1:35`;
- English manual captions import three sentences with source `youtube-captions`;
- cancellation during the caption request propagates and does not call audio import;
- an unavailable player returns empty captions, then the fake importer returns `fixture_blocked`.

The production wrapper change is one additional constructor. `Program.cs` still registers `YoutubeTranscriptService` through the original constructor, which still executes `new YoutubeClient()`.

## Commands and counts

| Command | Result |
| --- | --- |
| Focused Release tests before the pins | 54 passed, 1 failed, 0 skipped, total 55, exit 1 |
| `dotnet restore` app and tests after the pins | exit 0 |
| AngleSharp + YoutubeClient fixture tests | 9 passed, 0 failed, exit 0 |
| Focused filter again, including the new tests, `--no-build` | 63 passed, 1 failed, total 64, exit 1 |
| Native MariaDB filter | 11 passed, 0 failed, 0 skipped, about 1 minute, exit 0 |
| Full `dotnet test --configuration Release --no-build` | 1296 passed, 1 failed, 9 skipped, total 1306, duration 6m54s, exit 1 |

The single failure in every post-pin run is the same baseline failure:
`AccountReviewRepairTests.WeeklySummary_ClipsAndUnionsAcceptedIntervalsAtBusinessWeekBoundaries`
does not find `khoảng 2 phút` in the rendered weekly email. It was not changed here.

The nine full-suite skips are OpenAI live/speech acceptance, shared-data MariaDB,
catalog scale, quota-migration upgrade and writing-answer MariaDB upgrade/guard.
They are environment skips. The 11 native acceptance tests ran and passed; they
are not part of those skips.

`git diff --check` on the two tracked source files exited 0.

## MariaDB fixture

| Item | Value |
| --- | --- |
| Container | `tctenglish-mariadb-acceptance-depsec20261111` |
| Image | `mariadb:10.5.23@sha256:6122f729893e286a3cfc789fb546dd041cf244e4de84272ab5727028ca107730` |
| Engine | Docker 29.9.2 |
| Bind | `127.0.0.1:3361` |
| Database / run | `tct_acceptance_depsec20261111` / `depsec20261111` |
| Labels | `tct.acceptance=1`, `tct.purpose=final-acceptance-g03`, `tct.run=depsec20261111` |
| Server version | `10.5.23-MariaDB-1:10.5.23+maria~ubu2004` |
| Character set | `utf8mb4` / `utf8mb4_unicode_ci`, `lower_case_table_names=1` |
| Connection mode | `SslMode=None` |
| Migration history | 16 rows, including the checkout's existing `20261111114120_AddAccountFunctionalCompletion` |

`dotnet ef database update --project TCTEnglish/TCTEnglish.csproj --configuration Release --no-build`
applied those migrations to the disposable database and exited 0. A second update
reported that the database was already up to date. `dotnet ef migrations has-pending-model-changes`
reported no model changes and exited 0. The EF tool was the repo-local 9.0.4 tool.
No new migration was generated.

The Unicode test stored `Nguyễn Văn Á — 日本語 — café`, rolled back a later name
change, and read the committed value back. Account acceptance covered retrying
deletion, completion concurrency covered one credit under two workers, and the
notification worker covered claim/recovery.

`tctenglish-mariadb-local` remained `Up (healthy)` on `127.0.0.1:3307`. Its volume
was not replaced. Starting Docker Desktop also resumed other existing containers
because of their restart policies. Those containers were not reset.

## Limits

- Loopback `SslMode=None` does not exercise the MySqlConnector zero-configuration
  TLS password disclosure. No certificate-rejection probe was run in this pass,
  and this ledger is not a production TLS audit.
- NuGet audit does not currently index every AngleSharp repository advisory.
  The maintainer ranges and the parser fixtures are the evidence for those rows.
- GHSA-2m69-gcr7-jv3q remains on the test SQLite native package.
- The pre-existing weekly-email assertion still fails.
- A process that was already running before this build still has its old loaded
  libraries. This pass did not stop or restart an owner web host.
- CI, staging and production were not run.

## Independent owner-authorized review and repair — 2026-11-11

The owner explicitly requested review and fixes after Cursor finished. One agent
reviewed source/diff, captured an ignored file-hash baseline and independently
reran actual checks. No extra AI worker/reviewer was used. Initial findings:

| Priority | Finding | Repair and evidence |
| --- | --- | --- |
| P2 | WeeklySummary_ClipsAndUnionsAcceptedIntervalsAtBusinessWeekBoundaries still expected the old contiguous HTML sentence. Antigravity's strong-tag/card layout split the text and the zero-day state omitted that sentence. | Reproduced the failure. Preserve the same overlapping/cross-week intervals; parse rendered HTML and assert normalized "Thời gian tương tác 2 phút", the exact stored week and the actual settings anchor destination. No time calculation or email design changed. |
| P2 | HtmlStyleAndSelectXmp_KeepRawTextInsteadOfCreatingScriptOrImage reparsed Body.InnerHtml even though a leading style is stored in Head. That half of the test was passing on empty content. | Require the style and its original raw text in Head, serialize DocumentElement, then assert retained style text and absence of an injected img after reparse. No production parser workaround or weakened assertion. |

Only AccountReviewRepairTests.cs and AngleSharpAdvisoryCompatibilityTests.cs were
changed for these repairs. The production pins and constructor seam passed review
without additional runtime changes. Existing Account/email/UI work remains intact.

Verification so far:

- Reproduced pre-repair targeted result:9 passed,1 failed,1 native-environment skip.
- Fresh Release build and focused parser/real-YoutubeClient/Account/email/import/
  database-settings suite after repair:84 passed,0 failed,0 skipped.
- Independently inspected both app/test project assets and Release DLL product
  versions: AngleSharp1.10.1 and MySqlConnector2.6.2. The assembly-version quirk is
  accounted for; Pomelo9.0.0, EF9.0.4 and YoutubeExplode6.5.7 remain unchanged.
- Reused only the verified depsec20261111 fixture after checking its ownership,
  loopback3361 binding and exact sentinel. Native MariaDB set:11 passed,0 failed,
  0 skipped,47s. No owner's dev DB operation was performed.
- Fresh app vulnerability audit: no vulnerable package entries. Fresh test audit:
  the same SQLitePCLRaw.lib.e_sqlite3 2.1.10 High advisory remains, not suppressed.
- Final model/repeat checks: no pending model changes; no migrations applied;
  the disposable database is already up to date. No migration was created/edited.
- Final full Release suite, independent run without native/live environment flags:
  **1,276 passed,0 failed,29 skipped,total1,305,5m33s**, exit0. The full-suite skips
  are environment gates, not provider passes; the applicable native set passed11/11
  separately. Final TRX: ignored results/dependency-review-final.trx under the review
  evidence directory. Git diff --check exited0 (pre-existing line-ending advisory
  on the untouched shared loader only).

Primary-source checks confirmed the additional AngleSharp maintainer advisory
ranges remain below the selected1.10.1 version. The test-only SQLite advisory
[GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) describes a
SQLite engine issue fixed from3.50.2, with no patched version currently listed for
the same native2.x package. It is absent from the app dependency graph and does not
mean the MariaDB engine/provider is affected. A safe test-provider/native-library
replacement needs separate compatibility work and test-project authority; the
original package launch explicitly excluded that file/scope. See the plan follow-up.

No new mail, paid AI request, config/credential write, package-pin change, schema
change, Git commit or release occurred during this review. Local reproduction logs,
audit JSON and final TRX are retained under ignored
.appdata/local/dependency-review-20261111. The disposable credentials were read only
in memory and passed to child environment; they were never printed or put in argv.

Review conclusion: the two-pin application maintenance and repaired regressions
are accepted locally. No additional production defect was found in the bounded
upgrade/fixture scope. The test-only SQLite advisory, unobserved production TLS,
live YouTube behavior and release stages remain explicitly outside this conclusion.
The owner did not request starting/restarting a web host for this review; the
verified runtime artifacts are the fresh Release app/test outputs.
