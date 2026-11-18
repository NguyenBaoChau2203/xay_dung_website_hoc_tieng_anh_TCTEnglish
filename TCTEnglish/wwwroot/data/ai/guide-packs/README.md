# Reviewed learner guidance packs

These files are source-managed, general application guidance. They contain no
learner records, credentials, conversation exports or paid provider responses.
Questions are authored examples, not harvested user logs. The root
`../website-guides.json` explicitly declares the six files; arbitrary JSON files
in this directory are never discovered automatically.

| File | Guides | Question variants | Focus |
| --- | ---: | ---: | --- |
| learning-roadmaps.json | 12 | 96 | CEFR stages, TOEIC 450/650, IELTS 4.5/6.5, placement and time expectations |
| study-routines.json | 10 | 80 | 15/30/60-minute sessions, busy schedules, returning learners and skill difficulties |
| practice-methods.json | 10 | 80 | Choosing vocabulary activities, eligible sets, review and saved progress |
| media-troubleshooting.json | 10 | 80 | Microphone/audio, transcript/imports and pending scoring/creation/chat results |
| account-and-access.json | 10 | 80 | Safe account help, verification, Premium/payment status, permissions and score limits |
| demo-onboarding.json | 8 | 64 | First visit, feature distinctions, empty lists, chat sources and clarification |

Keep each guide's stable `id`, actual learner GET `route`, useful `title`,
original `body` and distinct `questions`. `matchPhrases` are explicit topic
phrases. Each `matchRules` array is an AND of normalized phrases; alternative
arrays are OR choices. Use at least two concrete concepts per rule, avoid generic
pronouns, and require feature context. Accents and punctuation are normalized;
word boundaries matter. A unique strongest match is required for early routing.
Do not use regex, executable instructions, arbitrary external routes or personal
facts. Security refusals and source-access checks retain precedence.

To add a pack, declare it in the root manifest and explicitly admit reviewed IDs
in `GuideCorpusAdapter.ReviewedIds`. Public/authenticated visibility remains an
independent policy decision. Includes are limited to direct files named
`guide-packs/<lowercase-hyphenated-name>.json`; traversal, filesystem links,
nested includes, missing/corrupt packs and conflicting identities are rejected.
The source version combines the individual pack version, identity and content
hash, so history verification still detects changed/removed sources.

Keep novel paraphrases in `TCTEnglish.Tests/Fixtures/AiDemo/held-out-questions.json`
outside the runtime catalog. Never copy these full questions into runtime data
to make a regression pass. Safety cases have a separate fixture. Review expected
meaning as well as the selected guide: an XP-to-certificate question belongs to
score limitations even when it names a CEFR stage. Run `ChatDemoKnowledgeTests`,
the existing chatbot/corpus/citation suites and browser checks. Restart the local
web after changing startup-cached guidance.

Plans are general practice suggestions using existing site sections. They do not
assess the learner, create a certified placement/mock exam, equate CEFR with
TOEIC/IELTS or promise an exam score/time. Assessment comparison references:
[IELTS and CEFR](https://ielts.org/organisations/ielts-for-organisations/compare-ielts/ielts-and-the-cefr),
[ETS Global TOEIC and CEFR](https://www.etsglobal.org/gp/en/help-center/score-usage/are-toeic-listening-and-reading-test-scores-mapped-cefr).
These explain comparison limits; session plans are authored suggestions, not
official test preparation schedules.
