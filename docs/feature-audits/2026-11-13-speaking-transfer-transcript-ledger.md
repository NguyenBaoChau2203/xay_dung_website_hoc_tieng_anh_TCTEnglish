# Speaking catalog transfer — execution ledger

Latest owner clarification: make all90 complete acquired transcripts usable on
the **local Speaking page for testing now**, before staging/team QA. This
supersedes the earlier manual QA gate before local insertion. Local catalog
availability does not authorize deployment or assert verified content accuracy.
The owner also requested subject grouping alongside CEFR; broad categories are
reviewed from creator-labelled subjects, with mixed exams retained separately.

Status: **90/90 imported into the local Speaking catalog for UAT; content QA
remains pending before staging review**. Local work on
`fix/speaking-catalog-transcript-polish`, one agent. No commit, push, remote CI or
deployment. No paid speech request has been made; no spending budget is approved.

## Identity and preservation

The canonical Speaking selection is `data/speaking/reach-a1-c2-catalog.json`.
After five original videos returned HTTP403 for media, the owner explicitly
authorized same-level replacements. The current90 retain85 original IDs plus
five verified replacements,15 per A1–C2,70 REACH/20 Viet FCE. The original90
remain the exact Listening withdrawal set, with their immutable manifest/raw
SHA256 retained. Original duration was117,347s; current selected duration is
117,171s (32.55h). No lower-level relabeling was used.

| Level | Unavailable original | Verified replacement |
| --- | --- | --- |
| B1 | `J-_Ky-ipy3E` | `7oMeyxbYKjM` |
| B2 | `QHZhyM_KWj4` | `cK_ro3bbXSQ` |
| B2 | `o3K-PqvJMGU` | `Yz1B1DKxi4s` |
| C1 | `-IWxjaIM3iU` | `tdvzBdN4C3M` |
| C2 | `3hAI-sR9QWM` | `kHaTTIv15T0` |

Each replacement has fresh public/embed/creator-level evidence, successfully
parsed English source captions and complete measured media. Re-running
`replace.mjs` reuses the five decisions without reselecting or downloading.

Ignored backup, full original-row hashes, inventory, source-to-target receipts,
caption/media caches and logs are under
`.appdata/local/speaking-catalog-transfer`. The baseline contains 69 tables,
103 Listening rows (96 public/published, 7 private), 14 Speaking videos (5 public,
9 private) and 1,569 Speaking sentences. The preservation comparison permits
only `IsPublished=false` on the exact selected 90 Listening IDs; it does not
exclude an entire domain/table or copy progress between domains.

## Durable Listening withdrawal

All 90 original Listening rows are retired with IDs/history retained. Each has
a canonical version 2 withdrawal with `IsPublished=false`. All 90 version 1
packages/keys/hashes remain unchanged. The package catalog has 181 releases
(onboarding plus 90 originals and 90 withdrawals). Production selection is
unchanged. Both old authoring entry points reject Import before DB/build.

New exporter versions serialize UTF-8 LF before hashing; the historical v1
CRLF exception is retained. A regression proves old CRLF packages load and
apply, new withdrawal bytes use LF, and old bytes remain unchanged. A Node
regression checks raw and Git-filtered hashes for all 180 Listening packages.

## Transcript acquisition and quality

The public YouTube Show transcript panel provides genuine timed English cues
for all 90 videos. The retrieval uses the normal public watch context and
`get_panel` request observed in the browser; no account, cookies, signed media
URLs, token bypass or paid API is used. Text cues are matched to source
millisecond start/end marker entities; absent timing fails closed. HTTP 200
empty is not success. Each video's sanitized payload is cached atomically.

These are **candidate transcripts**, not completed quality proofs. Whole-second
starts, overlapping cues and some caption ends beyond measured media duration
require review/correction. No proportional timing or fabricated silence map is
accepted. Untimed creator scripts (65 descriptions) are reference only.

| Level | Selected | Replaced | Caption/audio acquired | Alignment candidates | Quality passed/published | Listening retired |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A1 | 15 | 0 | 15 / 15 | 1 | 0 / 15 local | 15 |
| A2 | 15 | 0 | 15 / 15 | 1 | 0 / 15 local | 15 |
| B1 | 15 | 1 | 15 / 15 | 1 | 0 / 15 local | 15 |
| B2 | 15 | 2 | 15 / 15 | 1 | 0 / 15 local | 15 |
| C1 | 15 | 1 | 15 / 15 | 1 | 0 / 15 local | 15 |
| C2 | 15 | 1 | 15 / 15 | 1 | 0 / 15 local | 15 |

All90 current videos now have measured complete audio and source captions. The
five HTTP403 originals were not retried/bypassed; replacements resolved those
acquisition failures. C2 pilot media measures2,605.093152s, preserving its tail
beyond30min. All90 have native staging drafts (16,266 source cues; non-speech
markers are separated when complete English alignment is available) outside
public tables. Every draft has source/media/native cue hashes, unresolved flags,
empty review/coverage evidence and `FullSpeechReviewed=false`; these are not
approved files. Stage is repeat-safe and preserves older drafts/reviews.

MFA pilots cover all six levels, including >30min C2. A1 independently padded
utterances produced172 overlapping boundaries; grouping with context reduced
this to16, but remaining overlaps of0.5–2.6s still fail the proposed timing
threshold. Measured PCM correlation across three overlapping group pairs is
approximately1.0 at zero offset, ruling out seek/offset drift in those samples.
CTC alignment-only experiments now test a bounded neural frame/Viterbi method
with explicit wildcard padding; it never decodes new transcript words or treats
confidence as manually measured token accuracy. These experiments remain
quality=false and are recorded in ignored logs/candidates. Final CTC A1 has219
aligned English cues and7 flags (four low-confidence cues plus three mandatory
review categories); A2 has108 aligned English cues and17 flags (ten
low-confidence cues, four source Music markers to classify and three mandatory
review categories). Neither has overlapping cue boundaries. These two pilots
do not certify the other88 videos. All six levels have earlier MFA candidates.
Source captions alone do not establish complete speech coverage.

## Implementation and evidence

The isolated native writer preflights the exact selection and local target,
uses file/database locks, publishes a complete video/cues in one transaction,
rejects incomplete quality evidence and conflicting existing content, and uses
distinct Speaking stable keys. All90 local videos have complete acquired
transcript sequences; no empty stubs were inserted. The owner-authorized
ImportLocalTest path retains pending QA in ignored receipts, keeps genuine
caption overlaps and bounds only source ends beyond measured media. Where an
experimental aligner reversed adjacent utterances, it uses the original ordered
caption sequence. No fabricated times/review evidence or deleted speech is used.
Existing ordinary learner 30-minute limits and
protected application configuration/schema are unchanged.

Local Speaking now has95 public videos (5 original+90 new), with15 transferred
per level. There are90 new canonical Speaking v1 packages; total catalog271
releases (onboarding+180 Listening revisions+90 Speaking). No production
selection was changed. The full local receipt files are under `local-test/`,
and `transfer-map.json` maps all90 old Listening IDs to new Speaking IDs/keys.

Reviewed broad subjects for the90 new videos: food8, recreation11,
conversation/relationships10, family/daily life8, shopping/finance10, health7,
study/work8, society/transport3, science/culture5, mixed exam practice20.
The original5 retain their existing General topic. Speaking defaults to subject
groups, retains the CEFR view and topic+level intersections, and lists global
topic counts even when a topic's video is beyond a loaded page. Topic chips wrap
inside the Clay toolbar on narrow screens.

The first local import encountered a Windows catalog rename denial after the
canonical exporter had committed bindings. The exact generated pending index
was recovered after verifying its one-release extension, immutable package
hashes and committed bindings. No package was overwritten or digest patched.
The continuation compares each existing aggregate/package directly to avoid
repeated whole-catalog scans; reruns preserve roots/cue IDs and stop on edits.

Browser testing found a pre-existing transcript click bridge captured the
practice function before speaking.js loaded: highlight moved but text/player
stayed at the first cue. A failing VM regression reproduced it. The bridge now
resolves the loaded practice API on click; its regression passes.

Verified checks:

- Isolated native tool build succeeds; the app Release launcher build succeeds.
  Protected app configuration/project/schema and production.json remain unchanged.
- Focused .NET20 passed,0 failed; two native tests are environment-skipped in
  that offline run and both pass separately on disposable MariaDB10.5.23. Native
  tests include ordinal IDs, replacement/source key separation, progress/cue IDs,
  clean-recipient application of all180 Listening revisions, repeat and rollback.
- Existing Speaking-related regression run:62 passed,0 failed,4 environment
  skips. This is separate evidence, not a claim that the whole suite ran.
- Node13 passed,0 failed: caption failures/true source times, complete-duration
  chunking, context ownership/repeated sections, budget/unknown outcome/cache,
  immutable Git package bytes and old command rejection.
- Pinned Docker Python CTC synthetic tests:3 passed,0 skipped; includes measured
  frame offsets across neural chunk context and true repeated target tokens.
- Canonical Validate:271 immutable releases/16,536 record revisions. Two successive
  Sync runs exited0, with current ignored shared-data/last-sync.json.
- Retirement rerun creates0 new package versions. Original-row comparison over
  all69 baseline tables passes after withdrawals and replacement mapping.
- EF reports no pending model changes. No migration is needed for tool records.
- Latest local UAT checks:9 focused .NET cases passed (2 environment skips are
  the native cases run separately);2 MariaDB10.5.23 cases passed.14 catalog Node
  cases and30 Speaking interaction Node cases pass. No paid/live microphone
  test was run. Earlier wider62-case Speaking run remains historical evidence.
- Rerun proof: exact public video/cue IDs, CEFR, topics, times, text hashes and
  catalog bytes stay identical;0 roots/cues/package versions created. Both final
  canonical Sync runs check271 releases with0 record operations.
- Live Chrome:95 public cards,11 subject groups (10 new groups plus5 original
  General videos). CEFR totals including originals: A1=18,A2=15,B1=17,B2=15,
  C1=15,C2=15. Subject and CEFR views both retain all95 cards. Food8,
  recreation11,conversation10; food+A1=3; server search AI NẤU ĂN=1. All90 new
  practice pages returnHTTP200 with16,262 transcript rows. Actual row clicks
  update reference text and active highlight at A1 early/middle/final cues and
  C2 final cue after43min. Listening still shows6 original public lessons.
- Browser viewport/document widths match at1440/768/390; category controls are
  reachable by keyboard. Native browser200% zoom and full audible A–B/token
  accuracy review were not observed. DOM selection is not audible QA evidence.
  Current evidence: ignored browser-local-uat-evidence.json and
  uat-rerun-proof.json under the task state folder.
- The strict Publish pilot command still rejects missing quality approval before any
  public mutation; its precise message identifies
  `https://www.youtube.com/watch?v=F5QyH36MnVo`. This is separate from the owner's
  authorized local-UAT import and no longer blocks local availability.

## Remaining gates

Local UAT is available now. Before a separately authorized staging review,
finish verified timing and the six-level quality pilot, align/review actual
audio, account for every speech interval, and
observe beginning/middle/late/boundary playback for every video. Only then
approve the content QA. Speaking packages are already exported for review;
no deployment is authorized. ASR is not currently proven necessary
because captions exist for all 90; paid ASR would still require owner approval
and would not resolve a media-acquisition failure or prove alignment by itself.

Local destination: `http://localhost:5100/Speaking`.

## Final owner handoff: transcript presentation and local commit

The owner requested cleaning literal `>>` speaker markers in existing videos and
a local commit of the entire catalog task before Antigravity interface work.
This authorizes commit only; push, PR and staging/production deployment remain
outside this stage. The branch stays `fix/speaking-catalog-transcript-polish`.

TranscriptDisplayText formats practice ViewModels for both public and owned
private videos. It strips caption speaker markers at starts/inside dialogue,
normalizes extra horizontal spaces and keeps real words, punctuation, line breaks
and ordinary comparisons/shift operators. The source rows, timing, IDs, learner
scores and immutable content packages are unchanged. This is a display change,
not a persisted-model/schema or shared-data edit.

Fresh verification:25 focused .NET tests passed,2 native environment cases
skipped in this run;30 Speaking interaction Node tests passed. New regressions
cover the screenshot examples and integration preservation/private access. Live
Practice44 renders121 cleaned cues with0 `>>` markers in the transcript and
SPK_SENTENCES; selecting cue5 shows the cleaned pasta dialogue and correct active
highlight. Raw DB retains477 marker-bearing source rows and95 public videos.
The standard launcher rebuild succeeded and health is ready at5100.

The staged-tree audit checks all337 task files and271 immutable package hashes
from actual Git index bytes, with0 secret/runtime-media/backup findings. Old
Listening v1 CRLF bytes remain immutable; whitespace review treats CR as EOL
while still checking source whitespace. Ignored backup/media/captions, local
numeric mapping receipts and test access details stay outside Git.
