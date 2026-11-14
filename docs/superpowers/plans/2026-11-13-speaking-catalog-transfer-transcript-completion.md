# Correct the catalog destination and complete 90 Speaking transcripts

Date: 2026-11-13, Asia/Bangkok. Status: all90 Speaking videos/transcripts are
available locally for owner UAT; durable Listening withdrawals and topic browsing
are implemented. Content QA before staging review remains pending. See the current
[execution ledger](../../feature-audits/2026-11-13-speaking-transfer-transcript-ledger.md).
Branch renamed locally to
`fix/speaking-catalog-transcript-polish`; dirty work was preserved.

## Owner correction and outcome

Final owner request: remove YouTube `>>` speaker markers from transcript
presentation, verify the old videos shown in the screenshots and commit all
work in this catalog branch locally for Antigravity handoff. Local commit is
now explicitly authorized; push/PR/staging/production remain unauthorized.
Presentation formatting must preserve persisted sentences, IDs, timing and
learner history. Verify service/public/private projection, browser selection,
build and package bytes, then review the staged tree for credentials/runtime
artifacts before the commit.

Latest owner instruction: make all90 videos with their full acquired transcripts
available in the **local Speaking catalog for UAT now**, before staging review.
Local availability is not a claim of verified transcript QA or a deployment.
This supersedes clauses that required manual token/coverage/timing approval
before any local catalog insertion. Preserve source/hash/numeric-bound checks,
record unresolved QA honestly, export canonical packages for later review, and
do not move anything to staging/production. The owner also requests browsing
by real video subject (food, recreation, conversation etc.), alongside CEFR.

Active checklist:

- [x] Add separate loopback-only local UAT authoring with pending QA preserved.
- [x] Categorize90 selected videos into reviewed broad subjects without changing
  original Listening identities or CEFR evidence.
- [x] Import90 complete video/cue aggregates and export90 Speaking packages.
- [x] Show topic groups by default; retain CEFR view/filter, full-catalog search
  and discovery of topics beyond loaded pages.
- [x] Verify rerun IDs/progress, canonical sync, preserved originals and browser
  topic+CEFR/practice behavior; leave local web on5100.

Subsequent owner amendment during execution on2026-11-13: unavailable media may
be replaced with another transcript-bearing video of the same verified CEFR
level. This supersedes exact target-ID clauses throughout this historical plan.
Keep the original90 as the exact Listening withdrawal set; the canonical
Speaking manifest records every original-to-replacement identity, acquisition
failure and new level/source evidence. No lower-level relabeling, transcript
quality waiver or paid spending authority was granted.

The owner clarified that the selected batch belongs to **Speaking**, while
Listening is another teammate's area. This supersedes the previous Listening
destination and the earlier acceptance of video-only lessons.

Transfer the exact 90 selected YouTube videos to the public Speaking catalog:
15 each at A1, A2, B1, B2, C1 and C2. Every transferred video must have a genuine,
complete English transcript with verified, useful cue timing before it is ready
for Speaking practice. Retire only the mistakenly added Listening batch from the
active Listening catalog. Preserve existing content, ownership and learner data.

Execution prompt:
[codex-speaking-catalog-transfer-transcripts.md](../../../.ai/prompts/codex-speaking-catalog-transfer-transcripts.md).
Use GPT 6.1 Sol with High reasoning, one agent, on the current checkout.

## Verified baseline and implications

| Evidence inspected | Observation |
| --- | --- |
| `data/listening/reach-a1-c2-catalog.json` | 90 unique selected videos, 15/level; 71 REACH and 19 Viet FCE. Reuse these exact IDs and source evidence. |
| Local `ListeningLessons` | 96 published public lessons; the selected 90 all match public rows and have no transcript lines. The 6 original public lessons have existing lines. |
| Listening progress for the selected IDs | 0 rows at inspection. Recheck before transfer; absence now is not permission to cascade-delete future history. |
| Existing public `SpeakingVideos` | 5 videos, all have some sentence rows with valid numeric bounds; none overlap the selected 90 IDs. Existence of lines alone does not prove whole-video completeness. Preserve these 5 videos/sentence IDs. |
| Private Listening | 7 lessons; outside this correction. Other private content and users are outside scope. |
| Media duration from the selected manifest | 117,347 seconds, about 32.60 hours; 21 videos exceed 30 minutes; maximum about 48.43 minutes. |
| `AudioImportOptions`, `FfmpegPcmDecoder`, `SpeakingSubtitleParser` | Existing ordinary import has a 1,800-second/PCM bound, decoder `-t 1801`, and subtitle parser default 30-minute bound. It cannot simply be looped over this full batch. |
| `PcmSpeechWindowPlanner`, `AudioTranscriptImporter` | Current ASR maps text to measured <=10-second audio windows. These are not word- or sentence-aligned provider timestamps. Approximately 11,775 uniform 10-second windows would cover the source duration before VAD/retries; this is not an actual billed request count. |
| `ListeningCatalogImporter.FindUnchangedAsync` | The original writer assumes the old public, published, video-only shape. Rerunning it after correction would conflict; it must not republish retired Listening entries. |
| Shared data | 90 Listening version1 packages already exported, plus team.onboarding/1. Do not delete/retype their stable keys or rewrite immutable bytes. |
| `.gitattributes` | The prior byte-preservation exception covers only `listening-youtube-*/1/package.json`. New versions need a verified cross-platform byte strategy. |

## Authority and protected boundaries

The preparation turn changed the branch name and prepared documents only. The
owner's subsequent execution instruction authorizes the scoped local tools/code,
content authoring/packages and ordinary verification. Keep the web on port5100.
No commit, push, PR, remote CI, merge or
deployment is granted for this task. Do not change release gates.

Preserve application Program.cs, appsettings, .csproj, existing credentials and
schema unless separately authorized. New isolated tool code can reuse existing
models/services and local configuration; do not register a startup import job.
Keep media, raw provider results, private baselines and receipts in ignored
`.appdata/local/speaking-catalog-transfer` on D. Do not restore local Whisper,
WhisperX, its retired launcher, downloads or a background local ASR engine.

The old no-paid-ASR boundary has not yet been replaced by a spending grant.
Implement free/local preparation and caption diagnosis, calculate costs, then
obtain explicit approval before the first paid probe or transcription request.
If the owner later grants a budget, carry it forward without asking again.

## Ordered work and acceptance

### A. Freeze identity, ownership and before-state

1. Inspect actual HEAD/dirty work. Reuse the 90-video manifest and import receipts;
   do not redo source selection or silently replace hard-to-transcribe videos.
2. Capture a fresh private backup and an ignored inventory of the selected
   Listening IDs, package bindings, progress and completion references, existing
   Speaking video/sentence IDs and private content. Verify exact source IDs with
   ordinal case-sensitive comparisons, not MariaDB's default collation.
3. Record the transfer mapping independently of numeric IDs:
   `youtubeId -> oldListeningId -> newSpeakingId -> old/new package keys`.
   Never copy a Listening numeric ID into Speaking or transfer completion credit
   between domains as if the assessment were equivalent.
4. Check shared-data Status. Stop on an unrelated content/binding conflict;
   preserve it, identify the stable key and continue independent preparation.
5. Create the new canonical selection/status manifest at
   `data/speaking/reach-a1-c2-catalog.json`, retaining the exact90 source IDs and
   provenance with a link/hash back to the old Listening selection. Keep old
   manifests/receipts as superseded history, not a second active import source.

Exit: 90 exact source rows accounted for, 5 existing public Speaking rows and all
private rows protected, and an explicit list of any changed-since-baseline IDs.

### B. Diagnose caption failures before paying for transcription

1. Reuse cached track metadata and prior failure receipts. Test a small meaningful
   sample covering REACH, Viet FCE, manual/automatic English tracks and a known
   working caption fixture. Record status, response byte length, track language,
   format and failure class; redact signed URLs, cookies and credentials.
2. Distinguish a listed track from successfully fetched, parsed, English timed
   cues. HTTP200 with an empty body is failure, not a usable transcript.
3. Inspect the pinned YoutubeExplode version and actual request behavior. Prefer
   a narrow verified retrieval/parser fix over upgrading the whole dependency
   tree. Add fake HTTP regressions for empty200, valid payload, missing captions,
   403/429, cancellation and malformed/non-English data.
4. Use legitimate public caption retrieval or creator-provided SRT/VTT. Do not
   bypass account/paywall/access controls, expose browser credentials, or assume
   possession of someone else's YouTube channel ownership.
5. If a transcript is available only as creator text in a description, retain it
   as text evidence and align it to real audio. Do not generate cue times from
   character/word proportions. Stop repeating the same failed90-item caption run.

Exit: per-video source decision: usable timed captions, creator-text-plus-audio,
audio transcription required, or a concrete acquisition blocker.

### C. Full-duration audio, cost and timing design

1. Use `gpt-transcribe` for approved ASR through the current saved server-side
   credential source. Do not substitute the IDE model for ASR or change global
   runtime model settings. Configured credentials are not proof of quota/access.
   Verify current request parameters, language hints and response schema against
   official documentation and fake HTTP tests before an approved live pilot.
2. Build a **catalog-specific offline/batch profile**. Do not globally relax the
   ordinary user-import/assessment limits to make a48-minute lesson fit. Stream
   bounded audio sections covering the entire measured source duration, with
   explicit original-timeline offsets and chunk hashes. Detect source truncation
   before accepting a job. Handle21 videos longer than30 minutes and final tails.
3. Split at real pauses where possible, retain limited context/overlap and merge
   only duplicate text from that overlap. Preserve genuinely repeated sections
   at their real times; REACH lesson repetition is not a duplicate to discard.
   Track quiet/music/instruction intervals separately instead of inventing text.
4. Text recognition and precise alignment are separate obligations. Prefer real
   caption cue times. For raw ASR or untimed creator text, align against measured
   original audio with a verified alignment-only method. The existing10-second
   VAD window timing can locate audio, but cannot be presented as sentence/word
   alignment without evidence.
5. A possible alignment-only tool is Montreal Forced Aligner. Before using it,
   establish the actual supported version, model/dictionary provenance, hashes,
   license, Windows/isolated-runtime behavior and storage on D. Use alignment,
   not a new local transcription service. Another method is acceptable if it
   proves the same timing contract. Do not install retired Whisper tooling.
6. Keep API requests within current official upload/format limits. Use bounded
   concurrency, retries, timeout/cancellation and a global request/cost ledger.
   Cache successful chunks atomically; interruption must not retranscribe them.
   An ambiguous paid POST outcome is not a safe unlimited automatic retry.
7. Produce an estimate from the uncovered audio minutes plus measured pilot
   overhead and verified current pricing. Approval must cover the pilot too.
   Proposed budget, **not authorized**: USD12 maximum for the90-video batch.

Illustrative estimate checked on 2026-11-13: the official model page lists
USD0.0045/minute. All117,347 seconds once would cost approximately **USD8.80**
for transcription; a20% repeated-audio allowance gives **USD10.56**. Captions
may reduce the paid amount. These figures exclude any separately priced optional
services and are not a measured bill. Recheck pricing before execution and stop
before exceeding the owner-approved ceiling.

References: [OpenAI model/pricing](https://developers.openai.com/api/docs/models/gpt-transcribe),
[file transcription and timestamp limitations](https://developers.openai.com/api/docs/guides/speech-to-text),
[MFA audio/text alignment inputs](https://montreal-forced-aligner.readthedocs.io/en/stable/user_guide/corpus_structure.html).
Current OpenAI documentation does not justify requesting whisper-only timestamp
options from gpt-transcribe. Preserve the model and prove alignment separately.

### D. Quality gate: genuine full transcripts for Speaking

Create a resumable ledger with stages such as discovered, acquired, transcribed,
aligned, quality_checked, published and blocked. Record source hash, duration,
language, acquisition method, model/tool version, chunk ranges, cue hash, quality
evidence and estimated/actual cost metadata. Do not expose keys or raw media URLs.

For **each** of the90 videos require:

- English speech transcribed faithfully; no title/description summary, translation
  pretending to be original speech, fabricated names or invented subtitles.
- Whole-video speech coverage, including later sections and tail, with an
  interval map. Review every unassigned speech interval and classify real silence,
  music or non-English instructional content. Do not equate lastEnd/duration or
  "has >0 cues" with completeness; VAD alone is not proof of semantic coverage.
- Finite original-timeline start/end values: 0 <= start < end <= actual duration;
  ordered cues and a documented handling of genuinely overlapping speakers.
  Sentence/short-utterance units must suit shadowing and A–B replay; do not store
  a whole podcast or a large paragraph as a single SpeakingSentence.
- A concrete proposed acceptance threshold: <=0.75-second onset/offset error on
  reviewed cues, no clipped utterance in replay, >=98% token accuracy on manually
  transcribed sample spans, and zero known omitted speech ranges. Correct/re-align
  failures. These are project thresholds, not a provider guarantee.
- Automated bounds/chunk/coverage checks on every video. Observe beginning,
  middle, late and chunk-boundary cue playback for every video; fully review the
  six-level pilot including a >30-minute source before releasing the batch.
  Human spot checks are sampled evidence, not a claim that all32.6 hours were
  manually transcribed. Track unresolved quality flags and do not mark them ready.

`SpeakingSubtitleParser` defaults to30 minutes/2,000 cues/1MiB. Use explicit,
validated catalog bounds or a dedicated catalog validator; preserve ordinary
learner-upload defaults. Long-video acceptance must test the final timestamp,
not only the first30 minutes. Unsupported/blocked items remain visibly unfinished
in the work ledger; they are not substituted or declared video-only complete.

### E. Publish in Speaking and retire the wrong Listening entries

1. Stage work in ignored files until quality passes. Public Speaking lists do
   not universally filter ImportStatus, so do not expose empty public stubs and
   rely on a pending flag to hide them.
2. Use existing `SpeakingVideo`/`SpeakingSentence` models. Create the complete
   public video and cues transactionally, with `OwnerUserId = null`, correct
   level/source metadata, actual TranscriptSource and `ImportStatus = ready`.
   Never invent a SpeakingVideo.IsPublished field or use the private My Videos
   import as a public catalog writer. Reuse a matching existing public row only
   after a complete identity/content comparison; stop on conflicting data.
3. Use new Speaking stable keys, e.g.
   `tct.speaking.youtube.<sha256-of-exact-youtube-id>`; do not change the kind of
   an existing `tct.listening.youtube.*` binding. Playlist organization is optional;
   if needed, preserve existing playlists and use canonical root/dependency exports.
4. Withdraw exactly the90 mistaken Listening rows using `IsPublished = false`,
   preserving IDs/rows/any newly created learner history. This is a logical
   transfer/retirement, not a table rename or cascade delete. The active Listening
   catalog should return to its6 original public lessons if no concurrent owner
   edits occur; the7 private lessons remain unchanged. Store the cross-domain map.
5. Retirement may happen before audio completion to return the teammate's catalog
   promptly, but record the phase as incomplete and export its canonical withdrawal
   versions before running shared-data Sync. Alternatively perform a validated
   per-video cutover once the destination is ready. Do not lose either side if
   a chunk/provider/process fails.
6. Review task-owned Listening UI/tooling changes separately. Stop extending that
   module. Revert only proven batch-only presentation hunks if appropriate; never
   use blanket reset/checkout over dirty work or silently reintroduce the public
   topic/privacy bug fixed during the previous import. Preserve an auditable patch
   for the Listening owner's follow-up.
7. Update the old Listening importer/manifest disposition so running its old
   command cannot silently republish the transferred batch. Preserve its original
   provenance and receipts; require an explicit superseded-batch rejection or
   read-only historical mode. Add a regression for this exact rerun hazard.

Expected final visible counts, subject to fresh baseline: **95 public Speaking
videos = 5 existing +90 transferred**. Each selected level has15 transferred
videos with ready transcripts. Active public Listening returns to6; physical
Listening row count may retain archived entries intentionally.

### F. Shared packages, repeat safety and data preservation

- Preserve the90 existing Listening version1 packages and their bytes. Export a
  new version for each retired root with IsPublished=false. Do not delete roots
  from package history or convert a Listening stable key into a Speaking key.
- Export new SpeakingVideo roots plus SpeakingSentence children using the
  canonical workflow. If playlists are created, export their supported dependencies
  in the verified order. Keep production.json untouched.
- Verify new versions' raw digest **and Git-filtered/checkout bytes**. Prefer
  deterministic UTF-8 LF serialization before hashing for newly exported versions,
  preserving all old package bytes. If an exporter fix is needed, assess shared
  consumers and test old-package reads plus new Windows/Linux byte consistency;
  do not manually patch a digest or copy the old /1 exception blindly to /2.
- Run validate and repeat Sync. A clean destination must end with the Speaking
  batch ready and old Listening batch inactive; a later pull/startup must not
  resurrect wrong-module content. Same-database repeat creates0 roots/cues and
  preserves sentence IDs, assessments and progress.
- Test a seeded disposable MariaDB10.5.23 round trip. Preserve protected original
  rows across domains; allow only the90 specifically authorized Listening flag
  changes and expected binding/package metadata changes in preservation checks.
  The previous all-69-tables-unchanged assertion must not be weakened broadly or
  falsely fail on these explicitly approved flag updates.

### G. Verification and owner handoff

Proposed focused code/test areas: `Infrastructure/SpeakingCatalog/`, isolated
`tools/SpeakingCatalogImport/`, local wrappers under `scripts/`, shared exporter
only if necessary, and `SpeakingService`/views only for demonstrated integration
gaps. Preserve API/antiforgery/role contracts and the existing text-only assessment
meaning; no learner assessment/paid microphone test is needed to check captions.

Meaningful regressions: >30-minute full tail, no truncated success, real offset
mapping, repeated lesson sections versus overlap duplication, blank/no-speech
audio, missing/empty captions, invalid timestamps, partial chunk failure, persisted
cache/restart, cost cap, unknown paid outcome, ordinal IDs on MariaDB, transaction
rollback, old Listening importer rejection, package kind separation and no resurrected
Listening rows. Test new native writer on disposable MariaDB, never reset owner DB.

Browser: `/Speaking`, every CEFR filter and pagination, `/Speaking/Practice/{id}`,
sentence selection/highlighting, A–B repeat and dictation/reference display where
implemented. Play early/middle/late cues and >30-minute tail. Confirm the selected
batch is absent from active Listening, original Listening/private pages still work,
and original Speaking progress remains intact. Follow DESIGN.md for changed UI;
keep port5100 and use the standard local launcher for an owned-process restart.

Create `docs/feature-audits/2026-11-13-speaking-transfer-transcript-ledger.md` with
per-level selected/retired/transcript-ready/published/blocked counts, source-to-target
mapping reference, quality evidence, actual spend/request counts if approved,
package versions, before/after preservation, build/test/native/browser results
and blockers. Keep local numeric IDs and private snapshots in ignored receipts.

Completion is **90/90 transferred, transcript-ready Speaking videos**, a retired
wrong Listening batch, durable package delivery and preserved old content/history.
Nonempty text, empty captions, paid calls merely attempted or a budget awaiting
approval do not satisfy completion. Report in Vietnamese, distinguishing local
work from ungranted CI/staging/production stages.

## Short Codex launch message

```text
Use GPT 6.1 Sol with High reasoning. Read and execute .ai/prompts/codex-speaking-catalog-transfer-transcripts.md and its linked plan. The owner corrected the destination: transfer the exact90 selected videos from the mistaken Listening batch to public Speaking,15 per A1–C2, with complete genuine timed English transcripts for all90. Work on fix/speaking-catalog-transcript-polish. Preserve old Speaking, original/private Listening and learner history; retire the wrong batch through canonical package versions. Continue free/local preparation and obtain approval before paid ASR unless a budget is already explicitly granted. Keep port5100, use one agent, do not commit/push/deploy, and report in Vietnamese.
```
