# Listening REACH A1–C2 catalog execution ledger

Execution started 2026-11-13 (Asia/Bangkok), one agent, local implementation only.
The owner's GPT 6.1 Sol / High instruction supersedes the historical Gemini model
requirements. Application runtime AI settings are unchanged.

Branch: `fix/listening-ui-functional-polish`.
Initial HEAD: `f86a34dfbf66c065527fe2ca0bb7b48fd1493936`.
No commit, push, PR, CI, staging or production action is authorized/performed.

## Baseline and discovery checkpoint

- Reused the prior ignored SQL backup and baseline. Rechecked Docker MariaDB
  10.5.23 at `127.0.0.1:3307/tctenglish_dev`: 13 lessons, 6 published public,
  7 private. Original baseline remains intact.
- Replaced the ignored baseline helper's literal password with credentials read
  only inside the existing Docker container. No credential was printed or added
  to tracked source. Saved current comparison as `baseline-rechecked.json`.
- Captured hashes of every original row across 69 non-shared-ledger tables before
  content writes. Hash-only snapshots and local row-ID receipts remain ignored.
- Discovery cache persists after each playlist and every validated video under
  `.appdata/local/reach-listening-import`. Source data is untrusted reference data.
- Five REACH playlist observations: A1 first100, A2 63, B1 first100, B2 32,
  C1 10 available items. The first pages already supply >15 eligible A1–B2 items;
  no claim to have crawled all 212 A1/136 B1 videos. Single-level title evidence
  is required; A1–A2 mixed labels and misplaced A1 items in A2 were excluded.
- Channel search found an additional explicitly C1-labelled REACH video about
  China, absent from its C1 playlist. Search for C2 produced lower-level fuzzy
  matches, none selected as C2. Remaining C1/C2 slots use Viet FCE, whose video
  titles and descriptions explicitly identify C1 Advanced (CAE) / C2 Proficiency
  (CPE) listening tasks. This is creator classification, not CEFR certification
  or a claim of Cambridge endorsement.
- Every selected watch-page player response reports `OK`, public and embeddable;
  ID/channel/title/duration were checked. Actual browser playback is recorded
  separately below. No paid AI/ASR or media downloads are part of selection.

| Level | Public before | Selected new | REACH | Viet FCE | Imported | Remaining |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A1 | 2 | 15 | 15 | 0 | 15 | 0 |
| A2 | 2 | 15 | 15 | 0 | 15 | 0 |
| B1 | 2 | 15 | 15 | 0 | 15 | 0 |
| B2 | 0 | 15 | 15 | 0 | 15 | 0 |
| C1 | 0 | 15 | 11 | 4 | 15 | 0 |
| C2 | 0 | 15 | 0 | 15 | 15 | 0 |

The [90-item source manifest](../../data/listening/reach-a1-c2-catalog.json)
contains exact case-sensitive IDs, source/display titles, durations, channels,
playlist/evidence URLs, classification rationale, caption-track metadata and
observed UTC times. Metadata availability does not mean captions were retrieved.

## Implementation and verification

- Added C2 ordering, metadata, learner description/icon/token style and Admin
  filter. Practice badges already display the model's level without a map.
- Catalog topic enumeration previously included published private topics;
  scoped it to public roots, matching the existing card query.
- No persisted entity/mapping/schema change. No migration expected; pending-model
  check remains required. No application Program.cs/appsettings/.csproj change.
- Local runner stages network results outside transactions, requires the exact
  local DB target, uses file + MariaDB advisory locks and full preflight, compares
  IDs ordinally after materialization, and writes each public lesson atomically.
  Same-ID conflicts stop without overwrite; unchanged rows preserve IDs/children.
- Initial level regression failed on missing C2. Focused catalog tests then
  passed 9/9, zero skips (including rendered C2, privacy, rerun/progress retention,
  case-distinct IDs, conflict/draft preservation, cancellation, invalid captions
  and strict captions-only method invocation).
- Pilot captions-only attempts returned no usable transcript for all six levels.
  Independent direct caption URL check returned HTTP200 with an empty body,
  including in the real YouTube browser page. No fallback ASR was attempted.
- Isolated console runner needed an explicit EF Relational9.0.4 reference to match
  the application's private EF Tools runtime asset; its first Snapshot command
  failed before any DB write, then succeeded after correcting runner references.

Final import, provider, browser and package evidence follows below.

## Verified import and browser checkpoint

- Imported a six-level pilot (6 new), then remaining84. There were no previously
  imported batch rows to reuse from Gemini. Total new=90; conflicting/unavailable
  selected entries=0. Repeat import reports create0/unchanged15 at every level.
- Final public totals: A1=17, A2=17, B1=17, B2=15, C1=15, C2=15 (96).
  Total lessons103; private7. Hash comparison after import verified every original
  row unchanged across69 tables, including accounts, private content and progress.
- All90 captions-only attempts returned no usable lines. All90 are honestly
  video-only; new transcript/quiz/vocabulary/translation coverage=0. Track listing
  is retained separately from retrieval outcome. No synthetic text/timecodes,
  audio fallback, application AI or paid speech probes were used.
- Release focused suite:43 passed,0 failed,2 environment skips. The new native
  acceptance test subsequently passed on separate Docker MariaDB10.5.23/3339.
  The existing SharedDataMariaDbTests fixture hardcodes its CI-owned33307 endpoint
  and was not run against the owner's local DB. It remains a reported skip.
- The new native test initially passed; repeating its fixed video IDs then hit a
  fixture-only duplicate-progress key because the shared acceptance factory
  retains Listening content. Changed fixture IDs to fresh case-distinct pairs;
  native rerun passed. This did not affect the owner's database.
- Added failure-injection coverage: a child INSERT failure rolls back the root
  and permits a clean retry. Node filter regressions passed2/2 after reproducing
  level-count intersection and hidden-empty-state defects.
- EF9.0.4 pending-model check, with the existing local connection passed through
  process environment: no changes since last migration. No migration generated.
- Local launcher: `scripts/start-local.ps1 -NoBrowser -Configuration Release` on
  port5100; readiness200. Speech status configured from saved User Secrets,
  `VerifiedLive=false`; configuration/quota not probed. Secret store untouched.

Browser evidence (real application, saved local synthetic UAT accounts):

| Embedded video | Level / channel | Observed playback |
| --- | --- | --- |
| `F5QyH36MnVo` | A1 / REACH | currentTime0→42.45s, playing1, no player error |
| `8pzPtQwsUeg` | A2 / REACH | 0→29.57s, playing1, no player error |
| `ZlQbDRE7jBU` | B1 / REACH | 0→45.17s, playing1, no player error |
| `YA-Rpu6X3yI` | B2 / REACH | 0→51.16s, playing1, no player error |
| `-IWxjaIM3iU` | C1 / REACH | 0→60.05s, playing1, no player error |
| `H_xAzee2ogs` | C2 / Viet FCE | iframe time increased10.90→44.35s |

These are six playback spot checks across all six levels and both source
channels; not a claim that all90 were watched. All90 metadata responses were
validated separately. Browser checks also established:

- Each level's three pagination pages exposed all17/17/17/15/15/15 cards.
- All73 topic controls (72 topics plus All) matched their expected count;
  a precise C2 search returned exactly one matching card. C2 plus an A1-only
  search returned0 and the visible empty state after the scoped JS correction.
- Admin C2 filter returned15 C2 rows; Edit loaded the exact C2/video ID without
  validation errors. No Admin content update was submitted.
- Standard and Premium sessions can GET the new public C2 practice page200;
  their Admin request is redirected to access control. Existing private7 are
  preserved. These UAT accounts have no private cards, so visual private-lock
  behavior was not exercised with existing owner content.
- Transcript and Quiz tabs show their genuine empty states. The Premium AI quiz
  button was observed, never invoked. No vocabulary tab appears for empty data.
- Screenshots/DOM checked at1440/768/390 CSSpx, no document horizontal overflow;
  C2 long titles stay inside cards. Keyboard Enter activates C2, Tab advances
  to topic controls, focus outline visible. CSS zoom200% has no horizontal
  overflow. Native browser200% was not confirmed: Ctrl+plus did not change the
  reported viewport/DPR, so it is not claimed as native zoom acceptance.
- Guest catalog is visible. Guest practice currently returns404 under existing
  service behavior; this task does not claim or change Guest practice access.

## Package byte portability

The canonical Windows exporter emits CRLF JSON and hashes the raw bytes. The
general Git LF normalization rule would change those bytes after checkout.
The new batch has a narrowly scoped `.gitattributes` `-text` exception for
`data/shared/packages/listening-youtube-*/1/package.json`: preserve exact bytes
and digests, retain ordinary JSON diffs, do not rewrite any published package.
The general LF policy, old packages and production manifest remain unchanged.
The package regression verifies raw SHA256 and compares Git-filtered versus raw
blob hashes for each selected package. Export/sync closeout follows below.

## Final package and preservation closeout

- Exported90 new version1 lesson packages using the canonical
  `scripts/shared-data.ps1 -Action Export -Kind ListeningLesson`, with real receipt
  IDs, lowercase SHA256-derived stable keys/packages, `-SkipBuild -DatabaseReady`.
  Existing team.onboarding/1 remained unchanged. Catalog now contains91 releases
  and94 record revisions. No private/user/progress/AI/credential/media data added.
- `scripts/shared-data.ps1 -Action Validate -SkipBuild`: exit0,91 immutable
  releases valid. Canonical Sync twice: both exit0; second checks91 releases
  with0 record operations. Last-sync at2026-11-12T19:49:18.131145Z
  (13/11/2026 02:49:18 Asia/Bangkok) matches the current catalog SHA256
  `0ca4a91df50148a10eb23200b8d5a426a0dffd9b83edf70156d6f372c98620b9`.
- Node final:3 passed/0 failed/0 skipped, including every manifest/package mapping,
  raw digest, public-only root/field boundary, six-level15-item distribution,
  pagination/filter/empty behavior and Git-byte portability for all90 packages.
- `uv run --no-project python scripts/check-shared-data.py --base origin/master`:
  PASS, immutable history/public-data boundary. `git diff --check`: exit0;
  only normal CRLF→LF notices for non-hashed documentation/catalog files.
- Final Snapshot after exports and both syncs: all original row hashes across69
  tables still present unchanged. Final DryRun: selected15/create0/unchanged15,
  conflict0/unavailable0/videoOnly15 at every level. No duplicate roots/children.
- Protected application Program.cs/appsettings/.csproj, Models/Migrations and
  production approval manifest have no diff. No schema changes or secret writes.
- Local web remains ready at [Listening](http://localhost:5100/Home/Listening).
  CI/staging/production were not run. Core90-video delivery is complete locally;
  caption enrichment and unobserved native200% zoom remain explicit limitations.

Reviewable file groups: C2/privacy service and Razor/CSS/JS changes; typed importer
`Infrastructure/ListeningCatalog/ListeningCatalogImporter.cs`; local wrapper
`scripts/import-listening-catalog.ps1`; isolated runner/discovery/readme under
`tools/ListeningCatalogImport`; three .NET test files and two Node regressions;
source manifest;90 new packages/catalog; narrow `.gitattributes` byte rule; current
plan/prompt override and bug memory. Local backups/caches/receipts remain ignored.
