# Listening catalog: 15 additional videos at each CEFR level

> Superseded by the owner's 2026-11-13 correction: the selected90 videos belong
> to Speaking and require full timed transcripts. Follow
> [the transfer/transcript plan](2026-11-13-speaking-catalog-transfer-transcript-completion.md).
> Preserve this document and its execution ledger as historical provenance;
> the earlier video-only completion does not meet the revised acceptance criteria.

Date: 2026-11-12, Asia/Bangkok. Execution resumed 2026-11-13; see the
[execution ledger](../../feature-audits/2026-11-12-listening-reach-catalog-ledger.md).

Final local checkpoint (2026-11-13):90/90 new videos imported,90 canonical packages
validated, repeat import creates0 and second shared sync performs0 record operations.
C2 is visible through learner/Admin surfaces. Original69-table row hashes preserved.
Release focused43 pass (2 reported environment skips), separate new native MariaDB
acceptance1 pass, Node3 pass. All90 items are video-only after captions-only retrieval
yielded no usable lines. Native200% zoom is not confirmed; see ledger for full limits.

Owner outcome: add **90 distinct new public Listening videos**: 15 each for A1,
A2, B1, B2, C1 and C2. Preserve existing lessons and learner progress. Prioritize
REACH; the owner explicitly allows other channels to fill C1/C2 shortages.

Execution prompt: [.ai/prompts/gemini-listening-reach-a1-c2-import.md](../../../.ai/prompts/gemini-listening-reach-a1-c2-import.md).

## 1. Authority and task boundaries

- Current owner authority: local implementation and content authoring on
  `fix/listening-ui-functional-polish`, superseding the original plan-only turn.
- Use **GPT 6.1 Sol with High reasoning**, per the 2026-11-13 owner override.
  Historical Gemini requirements are superseded. Do not change application AI.
- One agent performs discovery, implementation and review. Follow AGENTS.md.
- Prepare code, reviewed public-content packages and verification evidence.
  Commit/push/PR/merge/CI/deployment for this new task are not granted by the
  earlier, completed Account release. Keep production configuration untouched.
- Do not reset the Docker database, replace accounts, remove existing content,
  rewrite User Secrets or change application Program.cs/appsettings/.csproj.
- Add C2 support where necessary for the new content. A Listening redesign,
  changes to other study modes, payments/auth, and mass AI quiz/translation
  generation are outside this task.

## 2. Verified source and external observations

Recheck dynamic observations at implementation time. Counts below are browser
observations, not a promise that every item is distinct, suitable or playable.
The web text fetch was throttled; the channel and playlist were opened in Chrome.

Channel: [REACH](https://www.youtube.com/@REACH-l5p/playlists), canonical channel
ID `UCkGHdvmCAvaJ033DpfXtCog`.

| Level | Public playlist observed | Displayed count | Planning implication |
| --- | --- | ---: | --- |
| A1 | [A1 Listening Podcast](https://www.youtube.com/playlist?list=PLqYGyORvIQiz2lBQGAGxKcNoW6UAmmD7S) | 212 | Select 15 eligible new videos, not the first 15 blindly. |
| A2 | [A2 English podcast](https://www.youtube.com/playlist?list=PLqYGyORvIQizDwGeH6ImlP_I5PIgC6Gr8) | 64 | Verify each video's level; some titles describe an A1–A2 band. |
| B1 | [B1 Listening podcast](https://www.youtube.com/playlist?list=PLqYGyORvIQiznS2WzCWZVlXW6Bb75hUfb) | 136 | Select 15 after deduplication/availability checks. |
| B2 | [B2 Listening Podcast](https://www.youtube.com/playlist?list=PLqYGyORvIQiz0717speyC2jyKn_lprEPT) | 33 | Select 15 eligible videos. |
| C1 | [C1 Listening podcast](https://www.youtube.com/playlist?list=PLqYGyORvIQiwucjWd4dYCTAiozl8qqSg0) | 10 | Recheck the rest of the channel; supplement the actual shortage. |
| C2 | No dedicated C2 playlist was observed on the channel's Playlists tab. | Unverified | Discover suitable external C2 sources; do not relabel C1. |

The C1 playlist itself also displayed 10 episodes when opened. This does not
prove the entire channel contains only 10 C1 videos. Do not treat the channel's
fuzzy search results as proof of C2 classification.

Current application findings:

| Source | Verified contract / consequence |
| --- | --- |
| `Services/ListeningService.cs` | `LevelOrder` and `LevelMeta` contain A1–C1 only. The catalog projects published rows with `OwnerUserId == null`. |
| `Views/Study/Listening.cshtml` | Level descriptions stop at C1; sections/tabs depend on service level metadata. An imported C2 row can be hidden from rendered sections. |
| `wwwroot/js/listening-index.js` | Existing filtering and per-section pagination need browser checks with 15+ cards per level. |
| `Areas/Admin/Views/ListeningManagement/Index.cshtml` | The level filter stops at C1. |
| `Areas/Admin/ViewModels/ListeningManagementViewModel.cs`, `CreateEdit.cshtml` | Creation/validation already accept C2. |
| `Models/ListeningLesson.cs` | Level is an existing string with maximum length 10. C2 does not require a new persisted field. |
| `ListeningService.CreateOwnedLessonAsync` | Creates private learner lessons, defaults to B1, and auto-generates an AI quiz. It is not the catalog ingestion path. |
| `Services/YoutubeTranscriptService.cs` | `GetEnglishCaptionsOnlyAsync` has no ASR fallback. `GetTranscriptAsync` calls the broader import path, which can fall back to OpenAI audio transcription. |
| `Areas/Admin/Controllers/ListeningManagementController.cs` | Existing Admin create/edit/transcript forms use antiforgery. `ImportTranscript` calls the broader transcript method; do not bulk-call it under captions-only authority. |
| `Infrastructure/SharedData/SharedDataRegistry.cs` | Public `ListeningLesson` and transcript/quiz/vocabulary children are supported typed package kinds. Private learner content is rejected. |

Local read-only baseline: 6 published public lessons, two each at A1, A2 and B1.
If unchanged when execution starts, success means 96 public lessons overall:
A1=17, A2=17, B1=17, B2=15, C1=15, C2=15. The binding requirement is **+15 per
level relative to the execution baseline**, not a hardcoded database total.

## 3. Scope of a delivered video

Required for each selected video:

- Real canonical YouTube ID/URL, verified uploader/channel, CEFR evidence,
  source title and a concise display title, suitable topic, real duration,
  thumbnail and an available embeddable video.
- A public catalog row with the correct level, `OwnerUserId = null` and
  `IsPublished = true` only after required checks pass.
- Source/provenance and validation status in a reviewable manifest.

Import usable English captions when available through the captions-only method.
Record manual/automatic availability and actual source; never invent transcript
text, translations, speaker nationality or timestamps. Missing captions do not
silently invoke paid ASR. Prefer an equivalent captioned candidate when practical;
a playable video-only lesson may still fulfill this video-addition request if
the existing UI clearly exposes the unavailable transcript/quiz/vocabulary states.
Record those counts separately. Do not call a video-only item a fully populated
transcript/quiz/vocabulary lesson. If the current empty state is misleading, make
the smallest necessary scoped correction.

Quiz, vocabulary and Vietnamese translations are optional enrichments, not a
requirement to fabricate data or execute 90 application AI requests. Keep the
app's runtime model/key configuration unchanged. Do not download/mirror video or
audio; use the YouTube embed and available caption data through existing paths.

## 4. Ordered implementation work

### Phase A — Establish a safe baseline

1. Inspect branch/HEAD/dirty work; preserve these plan/prompt files and unrelated
   edits. Load only active AGENTS.md/skills and relevant bug memory.
2. Confirm this checkout's Docker MariaDB at `127.0.0.1:3307`, database
   `tctenglish_dev`. Keep connection credentials in supported local stores.
3. Save an ignored baseline of public YouTube IDs, levels, lesson IDs, publication
   flags and relevant progress counts. Preserve a private local DB backup before
   content writes. Do not put users, credentials or backups in Git.
4. Run shared-data Status; identify existing package ownership and local edits.
   Do not pull, change branches or run a sync over unresolved local edits.

Exit: a reproducible before-state, scoped source map, and confirmed local target.

### Phase B — Discover and curate 90 new candidates

1. Open the actual channel and all relevant playlist continuations. Record
   channel ID, playlist IDs and observed date; verify video uploader membership.
2. Prefer REACH at A1–B2. Use as many eligible, distinct REACH C1/C2 videos as
   available; fill C1/C2 shortages from other public channels as authorized.
3. Identify more than 15 candidates per level to allow replacements. Use a mix
   of everyday, work, social and academic topics; do not impose an arbitrary
   duration cutoff that excludes this channel's normal podcast format.
4. Select substantive English listening material. Level evidence should come
   from the creator's course/playlist/video description. Mixed C1/C2 labels or
   search tags alone require stronger evidence and a recorded selection rationale.
   Mark creator/editorial classification honestly; it is not official CEFR certification.
5. Normalize IDs with the existing helper. Deduplicate across all six selected
   groups, source playlists and the existing **public** catalog. A user's private
   copy is not permission to edit/promote that row and is not a global unique-ID
   constraint for the public catalog.
6. Reject unavailable/private/member-only/non-embeddable videos and replacement
   candidates with unclear level evidence. Use bounded retries/backoff; do not
   bypass access controls or fabricate data after a YouTube restriction.

Deliverable, proposed: `data/listening/reach-a1-c2-catalog.json`. This is source
selection/provenance, not an alternative unrestricted shared-data importer.
Keep rejected candidates/errors in the execution ledger or ignored discovery
cache; do not insert rejected rows to reach the quota.

Each selected entry should retain: exact case-sensitive `youtubeId`, watch URL,
`level`, source/display titles, topic, duration seconds and formatted duration,
thumbnail URL, channel ID/name/URL, playlist URL where applicable, level-evidence
URL/rationale, `isReach`, availability/embeddability evidence, caption status,
observed UTC timestamp and selection state. Distinguish metadata availability
from actually observed player playback.

Exit: exactly 15 new selected IDs per level, 90 globally unique IDs, provenance
for every external C1/C2 selection, and no guessed C2 assignments. If a level is
still short, continue legitimate discovery or report the exact shortfall as a
blocker; do not weaken the target or silently change source policy.

### Phase C — Make C2 visible end to end

1. Add C2 to Listening service level order/metadata and the learner description
   map. Preserve A1, A2, B1, B2, C1, C2 ordering and existing filters.
2. Include C2 in the Admin list filter; preserve the already valid Admin create
   contract. Audit other Listening-only level mappings, including practice badges.
3. Use existing icons/design tokens for any C2 scoped styling in
   `listening-index.css`; no new brand palette or shared-layout redesign.
4. Add meaningful regressions: a public published C2 fixture appears in levels,
   rendered cards and filters; private/unpublished rows stay out of the catalog.

Likely files: `Services/ListeningService.cs`, `Views/Study/Listening.cshtml`,
`Areas/Admin/Views/ListeningManagement/Index.cshtml`,
`wwwroot/css/listening-index.css`; JS/practice views only if actual gaps are found.
Proposed tests: `TCTEnglish.Tests/ListeningCatalogLevelTests.cs`.

No migration is expected for a new allowed/displayed string value. Confirm that
no persisted mapping changes were introduced and check pending model changes.
If a real schema change becomes necessary, follow tct-ef-migration instead of
adding an unreviewed global YoutubeId uniqueness constraint.

### Phase D — Stage captions and import locally with repeat safety

1. Prefer a small task-specific local script/runner using existing EF entities,
   URL helpers and the captions-only service. An isolated runner/build cache can
   live under ignored `.appdata/local/reach-listening-import`. A reusable wrapper
   under `scripts/` is optional; do not add startup seeding or a new public API.
2. Existing Admin forms are an alternative for authoring if they can preserve
   validation/antiforgery and batch repeat safety. Fetch captions separately and
   submit genuine lines to the transcript editor; do not call generic
   ImportTranscript or CreateOwnedLessonAsync as a shortcut.
3. Provide a dry-run that validates the entire manifest and reports per-level
   new/duplicate/conflicting/unavailable counts without DB writes. Lock/check
   the expected local DB target before any mutation.
4. Stage network results before write transactions. Keep real caption order,
   finite nonnegative times, start < end and sensible duration bounds. Avoid
   racing the live import with multiple workers; concurrency 1 is sufficient.
5. Import a small cross-level pilot, verify the real page/player, then complete
   the remaining items. Use a transaction per lesson and a resumable ignored
   receipt keyed by YouTube ID. Interrupted runs must not duplicate rows/children.
6. Skip an unchanged item from this batch. Stop on a same-public-ID conflict with
   different level/content; never overwrite another curated lesson or progress.
   Draft/partial rows do not count as delivered published additions.

Proposed regression coverage if a reusable importer is added:
`ListeningCatalogImportTests.cs`: duplicate manifest, existing public ID conflict,
private-row preservation, failure/cancellation, rerun/resume, missing captions,
and proof that neither ASR nor application AI is called by this import path.
Verify case-sensitive ID comparison on MariaDB rather than trusting its default
case-insensitive text collation; case-distinct source IDs must not be conflated.
Use synthetic fixtures for tests; normal tests must not call YouTube/OpenAI.

### Phase E — Export selected public content through the existing workflow

1. Use `scripts/shared-data.ps1 -Action Export -Kind ListeningLesson` for each
   newly authored root, including supported caption children. Export only the
   selected batch; do not touch private lessons/users/progress.
2. Package/key identifiers must be lowercase and stable, while YouTube IDs are
   case-sensitive. Derive identifiers from SHA256 of the exact UTF-8 YouTube ID,
   not from a lowercased ID or an auto-increment Docker key. Suggested conventions:
   key `tct.listening.youtube.<full-lowercase-sha256>`; package
   `listening-youtube-<full-lowercase-sha256>`.
3. A package per lesson fits the existing single-root exporter. Reuse existing
   bindings/package versions where present; inspect Status before exporting so
   unchanged reruns do not publish unnecessary new versions. Build the shared-data
   CLI once and use its supported `-SkipBuild` option for the export loop.
4. Do not handcraft package digests or modify an existing published version.
   Keep `data/shared/production.json` untouched. Inspect all newly exported paths,
   references/hashes and public-content-only boundaries.
5. Validate, sync twice using the normal guarded tools, and verify receipt
   metadata plus stable lesson IDs and learner history. Test a package round trip
   on disposable MariaDB 10.5.23 when importer/package changes warrant it; never
   point destructive fixture initialization at the owner's Docker database.

Exit: reviewed packages under `data/shared/packages/`, additive catalog entries,
successful validation/sync and unchanged existing content/progress.

### Phase F — Verify and leave the web ready for owner UAT

- Run Release build and focused level/importer/shared-data/Youtube fixture tests.
  Run related existing Listening/AI/Goals tests where touched. Only broaden the
  suite when scope or failures justify it; do not label gated skips as passed.
- Check manifest distribution 15/15/15/15/15/15 and global uniqueness; prove actual
  published public additions against the captured before-state. Report new,
  resumed, reused, rejected and pending counts separately.
- Rerun import: zero additional rows, unchanged IDs/children/progress. Rerun shared
  data sync: exit zero, current `.appdata/local/shared-data/last-sync.json` catalog
  hash and no unintended data changes.
- Browser: `/Home/Listening`, every level/topic/search filter, C2 section/card,
  pagination to all 15 selected entries (not merely the first visible page),
  Admin C2 filter/edit, long titles, missing captions and empty optional tabs.
- Check metadata/availability for all selected videos; observe actual embedded
  playback for at least one item per level and every supplemental source channel.
  Record which items were played; do not claim all 90 were manually watched.
- Browser spot checks at 1440, 768 and 390 CSS pixels, keyboard and 200% zoom for
  changed controls. Use current design standards; do not expand into a redesign.
- Preserve Standard/Premium/Admin access, private lesson ownership and existing
  progress. Do not claim native Guest practice behavior beyond verified contracts.
- Keep the owner's requested port **5100**. If compiled changes need a restart,
  identify this checkout's launcher/process, restart only that owned session via
  `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser`, and require
  `/health/ready`. Do not silently switch the owner to 5101.
- Review diff, UTF-8 text and content hashes. Report local results separately
  from CI/staging/production; none of those release stages run in this task.

Suggested existing commands (adapt filters only to actual new test names):

```powershell
dotnet build TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release
dotnet test TCTEnglish.Tests/TCTEnglish.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ListeningCatalog|FullyQualifiedName~SharedData|FullyQualifiedName~YoutubeClientFixtureCompatibilityTests'
dotnet ef migrations has-pending-model-changes --project TCTEnglish --startup-project TCTEnglish --configuration Release --no-build
pwsh -NoProfile -File scripts/shared-data.ps1 -Action Validate
uv run --no-project python scripts/check-shared-data.py --base origin/master
git diff --check
```

Do not use illustrative lesson IDs or unresolved hashes as command arguments.

## 5. Completion evidence and handoff

Create `docs/feature-audits/2026-11-12-listening-reach-catalog-ledger.md` with:

- Actual branch/HEAD, baseline, changes and schema assessment.
- Per-level baseline/new/final counts and source-channel breakdown.
- A linkable 90-row index or manifest reference, with source/CEFR evidence and
  published state; map local lesson IDs only in ignored receipts.
- Counts with English captions vs video-only, plus real quiz/vocabulary coverage.
- Import/rerun/package/sync/MariaDB/build/test/browser evidence and honest limits.
- Rejected/replaced candidates and unresolved blockers without credentials/PII.

Report in Vietnamese with the catalog URL on port5100. Completion requires
90 genuine additions and six visible levels; missing rows, hidden C2, fabricated
timestamps or local-only unexported data are not completion. If blocked, hand off
the validated subset and exact remaining dependency without asserting success.

## 6. Short launch message for the owner

Paste this English message into the implementation chat after selecting the owner model:

```text
Use GPT 6.1 Sol with High reasoning for this task. Read and execute .ai/prompts/gemini-listening-reach-a1-c2-import.md and docs/superpowers/plans/2026-11-12-listening-reach-a1-c2-catalog.md. Add 15 NEW public Listening videos per level A1–C2 (90 total), prioritizing REACH; other channels are authorized for C1/C2 shortages. Implement locally on the current fix/listening-ui-functional-polish branch, include C2 visibility, repeat-safe import, reviewed shared-data exports and meaningful verification. Keep web port5100, preserve existing data, use one agent, and report in Vietnamese. No commit/push/deploy or paid batch AI/ASR calls.
```
