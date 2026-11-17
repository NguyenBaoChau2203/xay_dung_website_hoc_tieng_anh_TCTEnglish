# Chatbot demo knowledge expansion — observed local results

Date: 2026-11-18. Mode: local implementation, one agent, existing branch
`fix/writing-goals-profile-polish`. This extends the 2026-11-17 learning guidance
and preserves the existing T1/R1–R4 hook/presentation repairs and dirty work.
No branch, subagent, commit, push, remote CI or deployment was created.

## Outcome and cause

The owner's screenshot asks for an A1-to-C1 / zero-to-TOEIC450 /
zero-to-IELTS4.5 roadmap, but received the missing-personal-data/create-set
response. A generic learning roadmap is answerable without owned vocabulary.
The old catalog loaded only one file; recommendation composition only accepted
three starter guides when no live recommendation existed, and an older classifier
could prevent a newly authored guide from reaching the appropriate answer path.

The current answer gives a general framework covering all three goals, explains
which existing website sections to use, distinguishes guidance from an assessed
personal plan and asks for target, self-assessed level, available time and deadline.
It does not claim a certified placement test, complete TOEIC/IELTS course, score
conversion, guaranteed result or fixed completion time.

## Data inventory

Root `TCTEnglish/wwwroot/data/ai/website-guides.json` is version 2.2.0 with explicit
includes. Existing base entries and stable identities are retained.

| New pack under `TCTEnglish/wwwroot/data/ai/guide-packs` | Guides | Questions |
| --- | ---: | ---: |
| learning-roadmaps.json | 12 | 96 |
| study-routines.json | 10 | 80 |
| practice-methods.json | 10 | 80 |
| media-troubleshooting.json | 10 | 80 |
| account-and-access.json | 10 | 80 |
| demo-onboarding.json | 8 | 64 |
| Added | **60** | **480** |

The combined catalog has 126 raw entries, **124 eligible reviewed guides** and
**759 authored question variants** (758 distinct after accent/punctuation
normalization). The one normalized duplicate is the existing accented/accentless
Speaking question within the same identity; there are no cross-identity conflicts.
`study-read` and `ai-chatbox-usage` remain excluded. No new ID was added to public
visibility, and learner navigation routes remain allowlisted.

Two separate fixtures under `TCTEnglish.Tests/Fixtures/AiDemo` contain **120 novel
paraphrases** and **12 negative questions**. None of the 120 full normalized
questions occurs in runtime examples. Each paraphrase is checked with the current
deterministic classifier and a deliberately stale recommendation classifier.
Concept rules were refined against these cases; this is a curated regression
bank, not a statistically independent or exhaustive language-understanding study.

Coverage includes CEFR stages, TOEIC450/650, IELTS4.5/6.5, time expectations,
15/30/60-minute routines, busy/returning learners, vocabulary modes, microphone,
silent recordings, audio playback, transcripts/SRT/imports, pending feedback,
login/verification, Premium/payments, access, notifications and onboarding.

## Implementation and security review

- `Internal/Retrievers/WebsiteGuideCatalog.cs`: one asynchronous reader shared
  by runtime retrieval and the corpus. Direct declared pack paths only; reject
  traversal/absolute paths, filesystem links, nested/missing/corrupt includes,
  oversized documents and duplicate/conflicting identities. Preserve single-file
  fixtures, caller cancellation and the individual pack's version.
- `WebsiteGuideRetriever.cs`: reviewed exact examples, bounded learning-path
  detection and explicit data-authored phrase/concept rules work before stale
  classification. A bare mention of being a beginner does not force a roadmap.
  Personal-fact wording remains on the live retrieval path. Ambiguous top rule
  matches do not become an early authoritative FAQ answer.
- `AiChatAnswerService.cs` and `TemplateAnswerComposer.cs`: available general
  guidance no longer requires personal recommendation data; actual personal
  recommendations still take precedence in their normal flow.
- `Corpus/CorpusAdapters.cs`: explicitly admit all 60 reviewed IDs, keep retired
  and admin exclusions, and use per-file versions/content hashes. Changed bodies
  get a new version/hash; removal cannot reuse the old corpus body.
- `ChatDisclosurePolicy.cs` and `ChatExternalFactPolicy.cs`: negative regressions
  exposed missing handling for OTP extraction and an explicit Vietnamese request
  to complete unrelated homework for submission. These now retain the established
  refusal boundary before FAQ routing. Normal verification and help changing one's
  own password remain allowed. No credential, ownership, antiforgery, mutation,
  delegation or release permission was granted.

No endpoint, persisted model, DbContext, migration, Program.cs, appsettings or
project dependency changed. These authored static guides ship as source, rather
than shared database lesson entities; no SQL dump, learner export or shared-data
package is appropriate. No paid AI/audio/SMTP call was requested. App chat uses
the internal answer path; automated application tests use isolated transports.

Other changed files in this increment: `TCTEnglish.Tests/ChatDemoKnowledgeTests.cs`,
the real-corpus count in `AiRagCorpusBm25Tests.cs`, both fixture JSON files,
the pack maintenance README, the scoped plan and the bug log. Pre-existing
presentation, hook, policy-adapter and generated email-preview edits are retained.

## Regression evidence

The first 15 meaningful regressions ran before the repair: **13 failed, 2 passed**.
Both screenshot cases failed (deterministic: out of scope; stale model: insufficient
evidence). New path/source-loading cases failed. Credential refusal already passed.
Raw log: ignored `.appdata/local/chat-demo-knowledge-red.log`.

The larger paraphrase run then exposed misrouting from the generic beginner cue,
accent-normalized generic words, a missing Cloze topic cue and source-change wording.
These were fixed using bounded concepts, rather than copying full held-out questions
into runtime data. Expected semantics were also reviewed: the B2-to-C1 question
asking whether XP certifies a level correctly belongs to `limits-practice-scores`.
The unrelated France-capital question produces `insufficient_evidence` with no
evidence under the existing deterministic policy. Its fixture now records that
observed non-answer; the other 11 negatives explicitly expect `out_of_scope`.
An intermediate stricter safety run was 11/12, corrected by this fixture distinction.

| Final check | Observed result |
| --- | --- |
| Related .NET suites: new/existing chat, guide retrieval/composition/classifier, baseline, RAG/corpus/access/citations, evaluation, history and SMTP isolation | **690 passed, 0 failed, 0 skipped**, 2m46s |
| New `ChatDemoKnowledgeTests`, rerun after the final fixture/assertion refinement | **277 passed, 0 failed, 0 skipped**, 19s; overlaps the 690, do not add counts |
| All reviewed question variants, checked inside the suite | **759/759**, both deterministic and stale classifier |
| Novel paraphrases | **120/120**, both classifiers (240 theory cases) |
| Negative fixture | **12/12** with exact expected outcome and empty evidence |
| Four existing chat Node suites | **26 passed, 0 failed, 0 skipped** |
| Release build, final incremental check | Success, **0 errors**, 0 emitted warnings; earlier source compilation still emitted the existing project warnings |
| Combined data identity review | 126 raw /124 eligible; no cross-ID question collision; 0 held-out questions copied into runtime |
| Integrated diff / `git diff --check` | Reviewed; no whitespace errors; protected app files have no diff |

The broad test filter is recorded in `.appdata/local/chat-demo-dotnet-final.log`
and `test-results/demo-knowledge-final.trx`. The final new suite is in
`chat-demo-class-final.log` and `demo-knowledge-class-final.trx`. Node and build
logs are `chat-demo-node-final.log` and `chat-demo-build-final.log`. An early
Node invocation named nonexistent extra files and exercised only 13 cases;
the final four-file invocation above is the evidence for 26, not that earlier run.
This is a focused verification, not a new full-solution or remote-CI run.

## Real-browser acceptance on port 5100

The owned Debug process previously started in this chat was stopped and replaced
with `pwsh -NoProfile -File scripts/start-local.ps1 -NoBrowser -Configuration
Release -WebPort 5100`. Existing MariaDB users/data were preserved; bootstrap
was skipped and EF reported no pending migrations. `/health/ready` returned
`{"status":"ready"}`. Saved User Secrets were read without rewriting them.
`speech-status.ps1` reported configured / `gpt-transcribe` / `VerifiedLive:false`;
no audio was submitted. The web is left serving on 5100.

Browser sign-in used the existing synthetic local demo account privately; no
credential/cookie was printed or committed. Four ordinary internal chat turns
were created for acceptance; old conversations were not edited/deleted.

| Surface / action | Observed evidence |
| --- | --- |
| Full chat, exact screenshot text | HTTP200 `/AI/Chat/Send`; answered with `guide:roadmap-overview`, all three targets, no missing-data/create-set fallback; local Home CTA with `_top` |
| Full chat, unseen 15-minute lunch question | Actual SignalR `AiStreamStarted`, **7 chunks (0–6)**, `AiStreamCompleted`; outcome answered, `guide:routine-15-min`; practical 3/5/4/3-minute split |
| Full history reload | Both answers remained answered/current with the same source keys and Home routes |
| Responsive full chat | 1440, 768 and emulated390px: document and chat have no horizontal overflow; 390px CTA widths236px; native images inspected |
| Dashboard floating iframe | Actual 418px iframe; accentless beginner-to-TOEIC450 question selected `guide:roadmap-toeic-450`, Listening CTA; no horizontal overflow |
| Floating iframe, English IELTS4.5 paraphrase | Actual SignalR start, **10 chunks (0–9)** and completion; answered with `guide:roadmap-ielts-4-5`, Writing CTA |
| Embedded history reload | Both TOEIC/IELTS answers answered/current, same source keys; Font Awesome loaded with real headphone/pen glyphs |
| Native iframe CTA click | “Mở luyện viết” navigated the top page to `/Home/Writing/Exercises`; actual h1 “Danh sách bài tập viết” |
| Browser console during checked flows | No warning/error messages observed |

These were actual local responses and events, rather than injected success
fixtures. Native screenshots were inspected inline; no on-disk screenshot-save
claim is made. Temporary event observers only recorded event types, chunk indices
and source keys in the test browser; they did not change production source.

## Limits

This expansion improves reviewed coverage and routing; it does not guarantee an
answer for every possible wording or user intent. The existing answer service
still does not reason across conversation context: ask with the target and time
in the same message rather than relying on a bare follow-up. Personal plans need
real assessment/progress; exam certifications, live model/account quota and paid
speech/provider behavior were not validated. Long answers require scrolling in
the small iframe. Native200% zoom and native Antigravity hook loading were not
retested in this data increment. The earlier T1/R1–R4 ledger remains the evidence
for those host/presentation checks, with its stated limitations.

Official assessment references are linked in the roadmap pack and README:
[IELTS/CEFR](https://ielts.org/organisations/ielts-for-organisations/compare-ielts/ielts-and-the-cefr),
[ETS TOEIC/CEFR](https://www.etsglobal.org/gp/en/help-center/score-usage/are-toeic-listening-and-reading-test-scores-mapped-cefr).
The authored study schedules are suggestions, not schedules endorsed by those bodies.
